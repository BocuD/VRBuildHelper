#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BocuD.BuildHelper;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Tools = VRC.Tools;

namespace BocuD.VRChatApiTools
{
    public class VRChatApiUploaderAsync
    {
        private VRChatApiToolsUploadStatus statusWindow;

        public void SetupAvatarImageUpdate(ApiAvatar apiAvatar, Texture2D newImage)
        {
            statusWindow = VRChatApiToolsUploadStatus.ShowStatus();
            
            string imagePath = SaveImageTemp(newImage);
            
            UpdateAvatarImage(apiAvatar, imagePath);
        }

        public async void UpdateAvatarImage(ApiAvatar avatar, string newImagePath)
        {
            avatar.imageUrl = await UploadImage(avatar.imageUrl, VRChatApiTools.GetFriendlyAvatarFileName("Image", avatar.id), newImagePath);

            await ApplyAvatarChanges(avatar);

            statusWindow.SetUploadState(VRChatApiToolsUploadStatus.UploadState.finished);
        }

        public async Task ApplyAvatarChanges(ApiAvatar avatar)
        {
            bool doneUploading = false;

            statusWindow.SetStatus("Applying Avatar Changes", 0);
            
            avatar.Save(
                c => { AnalyticsSDK.AvatarUploaded(avatar, true); doneUploading = true; },
                c => {
                    Logger.LogError(c.Error);
                    doneUploading = true;
                });

            while (!doneUploading)
                await Task.Delay(33);
        }

        private static string SaveImageTemp(Texture2D input)
        {
            byte[] png = input.EncodeToPNG();
            string path = ImageName(input.width, input.height, "image", Application.temporaryCachePath);
            File.WriteAllBytes(path, png);
            return path;
        }

        private static string ImageName(int width, int height, string name, string savePath) =>
            $"{savePath}/{name}_{width}x{height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

        public async Task<string> UploadImage(string existingFileUrl, string friendlyFileName, string newImagePath)
        {
            Logger.Log($"Preparing image upload for {newImagePath}...");
            
            statusWindow = VRChatApiToolsUploadStatus.ShowStatus();
            
            string newUrl = null;

            if (!string.IsNullOrEmpty(newImagePath))
            {
                newUrl = await PrepareFileUpload(newImagePath, existingFileUrl, friendlyFileName, "Image");
            }
            
            return newUrl;
        }

        public async Task UploadLastBuild(Branch targetBranch = null)
        {
            statusWindow = VRChatApiToolsUploadStatus.ShowStatus();
            
            VRChatApiTools.ClearCaches();
            await Task.Delay(100);
            if (!await VRChatApiTools.TryAutoLoginAsync()) return;
            
            PipelineManager pipelineManager = VRChatApiTools.FindPipelineManager();
            if (pipelineManager == null)
            {
                Logger.LogError("Couldn't find Pipeline Manager");
                return;
            }

            pipelineManager.user = APIUser.CurrentUser;

            bool isUpdate = true;
            bool wait = true;
            
            ApiWorld apiWorld = new ApiWorld
            {
                id = pipelineManager.blueprintId
            };
            
            apiWorld.Fetch(null,
                (c) =>
                {
                    Logger.Log("Updating an existing world.");
                    apiWorld = c.Model as ApiWorld;
                    pipelineManager.completedSDKPipeline = !string.IsNullOrEmpty(apiWorld.authorId);
                    isUpdate = true;
                    wait = false;
                },
                (c) =>
                {
                    Logger.Log("World record not found, creating a new world.");
                    apiWorld = new ApiWorld { capacity = 16 };
                    pipelineManager.completedSDKPipeline = false;
                    apiWorld.id = pipelineManager.blueprintId;
                    isUpdate = false;
                    wait = false;
                });

            while (wait) await Task.Delay(100);

            if (apiWorld == null)
            {
                Logger.LogError("Couldn't get world record");
                return;
            }

            //Prepare asset bundle
            string bundlePath = EditorPrefs.GetString("currentBuildingAssetBundlePath");
            string blueprintId = apiWorld.id;
            int version = Mathf.Max(1, apiWorld.version + 1);

            string uploadVrcPath = PrepareVRCPathForS3(bundlePath, blueprintId, version, ApiWorld.VERSION);
            
            //Prepare unity package if it exists
            string unityPackagePath = EditorPrefs.GetString("VRC_exportedUnityPackagePath");
            string uploadUnityPackagePath = "";

            if (!string.IsNullOrEmpty(unityPackagePath) && File.Exists(unityPackagePath))
            {
                Logger.LogWarning("Found UnityPackage. Why are you building with future proof publish enabled?");
                uploadUnityPackagePath = PrepareUnityPackageForS3(unityPackagePath, blueprintId, version, ApiWorld.VERSION);
            }
            
            //Assign a new blueprint ID if this is a new world
            if (string.IsNullOrEmpty(apiWorld.id))
            {
                pipelineManager.AssignId();
                apiWorld.id = pipelineManager.blueprintId;
            }

            await UploadWorldData(apiWorld, uploadUnityPackagePath, uploadVrcPath, isUpdate, targetBranch);
        }

        public async Task UploadWorldData(ApiWorld apiWorld, string uploadUnityPackagePath, string uploadVrcPath, bool isUpdate, Branch targetBranch = null)
        {
            string cloudFrontUnityPackageUrl = "";
            string cloudFrontAssetUrl = "";

            // upload unity package
            if (!string.IsNullOrEmpty(uploadUnityPackagePath))
            {
                cloudFrontUnityPackageUrl = await PrepareFileUpload(uploadUnityPackagePath,
                    isUpdate ? apiWorld.unityPackageUrl : "",
                    VRChatApiTools.GetFriendlyWorldFileName("Unity package", apiWorld), "Unity package");
            }
            
            // upload asset bundle
            if (!string.IsNullOrEmpty(uploadVrcPath))
            {
                cloudFrontAssetUrl = await PrepareFileUpload(uploadVrcPath, isUpdate ? apiWorld.assetUrl : "",
                    VRChatApiTools.GetFriendlyWorldFileName("Asset bundle", apiWorld), "Asset bundle");
            }
            
            if (cloudFrontAssetUrl.IsNullOrWhitespace()) 
            {
                statusWindow.SetStatus("Failed", 1, "Asset bundle upload failed");
                return;
            }

            if (isUpdate)
                await UpdateWorldBlueprint(apiWorld, cloudFrontAssetUrl, cloudFrontUnityPackageUrl, targetBranch);
            else
                await CreateWorldBlueprint(apiWorld, cloudFrontAssetUrl, cloudFrontUnityPackageUrl, targetBranch);

            statusWindow.SetUploadState(VRChatApiToolsUploadStatus.UploadState.finished);
        }

        public async Task UpdateWorldBlueprint(ApiWorld apiWorld, string newAssetUrl, string newPackageUrl, Branch editBranch = null)
        {
            bool applied = false;

            if (editBranch != null && editBranch.HasVRCDataChanges())
            {
                apiWorld.name = editBranch.editedName;
                apiWorld.description = editBranch.editedDescription;
                apiWorld.tags = editBranch.editedTags.ToList();
                apiWorld.capacity = editBranch.editedCap;

                if (editBranch.vrcImageHasChanges)
                {
                    string newImageUrl = await UploadImage(apiWorld.imageUrl, VRChatApiTools.GetFriendlyWorldFileName("Image", apiWorld), editBranch.overrideImagePath);
                    apiWorld.imageUrl = newImageUrl;
                }
            }
            
            apiWorld.assetUrl = newAssetUrl.IsNullOrWhitespace() ? apiWorld.assetUrl : newAssetUrl;
            apiWorld.unityPackageUrl = newPackageUrl.IsNullOrWhitespace() ? apiWorld.unityPackageUrl : newPackageUrl;

            statusWindow.SetStatus("Applying Blueprint Changes", 0);
            apiWorld.Save(c => applied = true, c => { applied = true; Logger.LogError(c.Error); });

            while (!applied)
                await Task.Delay(33);
        }

        private async Task CreateWorldBlueprint(ApiWorld apiWorld, string newAssetUrl, string newPackageUrl, Branch editBranch = null)
        {
            PipelineManager pipelineManager = VRChatApiTools.FindPipelineManager();
            if (pipelineManager == null)
            {
                Logger.LogError("Couldn't find Pipeline Manager");
                return;
            }
            
            ApiWorld newWorld = new ApiWorld
            {
                id = apiWorld.id,
                authorName = pipelineManager.user.displayName,
                authorId = pipelineManager.user.id,
                name = "New VRChat world", //temp
                imageUrl = "",
                assetUrl = newAssetUrl,
                unityPackageUrl = newPackageUrl,
                description = "A description", //temp
                tags = new List<string>(), //temp
                releaseStatus = (false) ? ("public") : ("private"), //temp
                capacity = Convert.ToInt16(16), //temp
                occupants = 0,
                shouldAddToAuthor = true,
                isCurated = false
            };
            
            if (editBranch != null)
            {
                newWorld.name = editBranch.editedName;
                newWorld.description = editBranch.editedDescription;
                newWorld.tags = editBranch.editedTags.ToList();
                newWorld.capacity = editBranch.editedCap;

                if (editBranch.vrcImageHasChanges)
                {
                    newWorld.imageUrl = await UploadImage(newWorld.imageUrl, VRChatApiTools.GetFriendlyWorldFileName("Image", newWorld), editBranch.overrideImagePath);;
                }
            }

            if (newWorld.imageUrl.IsNullOrWhitespace())
            {
                newWorld.imageUrl = await UploadImage("", VRChatApiTools.GetFriendlyWorldFileName("Image", newWorld),
                    SaveImageTemp(new Texture2D(1200, 900)));
            }

            bool doneUploading = false;
            newWorld.Post(
                (c) =>
                {
                    ApiWorld savedBlueprint = (ApiWorld)c.Model;
                    pipelineManager.blueprintId = savedBlueprint.id;
                    EditorUtility.SetDirty(pipelineManager);
                    doneUploading = true;
                    if (editBranch != null) editBranch.blueprintID = savedBlueprint.id;
                },
                (c) => { doneUploading = true; Debug.LogError(c.Error); });

            while (!doneUploading)
                await Task.Delay(100);
        }

        public async Task<string> PrepareFileUpload(string filename, string existingFileUrl, string friendlyFileName, string fileType)
        {
            string newFileUrl = "";
            
            if (string.IsNullOrEmpty(filename))
            {
                Logger.LogError("Null file passed to UploadFileAsync");
                return newFileUrl;
            }

            Logger.Log("Uploading " + fileType + "(" + filename + ") ...");
            
            statusWindow.SetStatus($"Uploading {fileType}...", 0);

            string fileId = ApiFile.ParseFileIdFromFileAPIUrl(existingFileUrl);
            
            ApiFileHelperAsync fileHelperAsync = new ApiFileHelperAsync();

            await fileHelperAsync.UploadFile(filename, fileId, fileType, friendlyFileName,
                (apiFile, message) =>
                {
                    newFileUrl = apiFile.GetFileURL();
                    Logger.Log($"<color=green>{fileType} upload succeeded: {message}</color>");
                },
                (error, details) =>
                {
                    statusWindow.SetErrorState(error, details);
                    Logger.LogError($"{fileType} upload failed: {error} ({filename}): {details}");
                },
                (status, subStatus, progress) => statusWindow.SetStatus(status, progress, subStatus),
                WasCancelRequested
            );

            return newFileUrl;
        }

        private static string PrepareUnityPackageForS3(string packagePath, string blueprintId, int version, AssetVersion assetVersion)
        {
            string uploadUnityPackagePath = Application.temporaryCachePath + "/" + blueprintId + "_" + version + "_" + Application.unityVersion + "_" + assetVersion.ApiVersion + "_" + Tools.Platform +
                                     "_" + API.GetServerEnvironmentForApiUrl() + ".unitypackage";

            if (File.Exists(uploadUnityPackagePath))
                File.Delete(uploadUnityPackagePath);

            File.Copy(packagePath, uploadUnityPackagePath);

            return uploadUnityPackagePath;
        }

        private static string PrepareVRCPathForS3(string assetBundlePath, string blueprintId, int version, AssetVersion assetVersion)
        {
            string uploadVrcPath =
                $"{Application.temporaryCachePath}/{blueprintId}_{version}_{Application.unityVersion}_{assetVersion.ApiVersion}_{Tools.Platform}_{API.GetServerEnvironmentForApiUrl()}{Path.GetExtension(assetBundlePath)}";

            if (File.Exists(uploadVrcPath))
                File.Delete(uploadVrcPath);

            File.Copy(assetBundlePath, uploadVrcPath);

            return uploadVrcPath;
        }

        private bool WasCancelRequested()
        {
            return statusWindow.cancelRequested;
        }
    }
}

#endif
