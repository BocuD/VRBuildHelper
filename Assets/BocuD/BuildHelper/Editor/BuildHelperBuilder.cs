/* MIT License
 Copyright (c) 2021 BocuD (github.com/BocuD)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

using System;
using System.IO;
using System.Threading.Tasks;
using BocuD.BuildHelper.Editor;
using UnityEditor;
using UnityEngine.Networking;
using VRC.Core;
using VRC.SDK3.Editor.Builder;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace BocuD.BuildHelper
{
    using BocuD.VRChatApiTools;
    
    public static class BuildHelperBuilder
    {
        public static string ExportAssetBundle()
        {
            bool buildTestBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            
            if (!buildTestBlocked)
            {
                EnvConfig.ConfigurePlayerSettings();
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                AssetExporter.CleanupUnityPackageExport();
                VRC_SdkBuilder.PreBuildBehaviourPackaging();

                VRC_SdkBuilder.ExportSceneResource();
                return EditorPrefs.GetString("currentBuildingAssetBundlePath");
            }

            return "";
        }
        
        public static void TestLastBuild()
        {
            VRC_SdkBuilder.shouldBuildUnityPackage = false;
            VRC_SdkBuilder.RunLastExportedSceneResource();
        }

        public static void TestNewBuild()
        {
            bool buildTestBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            if (!buildTestBlocked)
            {
                EnvConfig.ConfigurePlayerSettings();
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                AssetExporter.CleanupUnityPackageExport();
                VRC_SdkBuilder.PreBuildBehaviourPackaging();

                VRC_SdkBuilder.ExportSceneResourceAndRun();
            }
        }
    
        public static void ReloadLastBuild()
        {
            // Todo: get this from settings or make key a const
            string path = EditorPrefs.GetString("lastVRCPath");
            if (File.Exists(path))
            {
                File.SetLastWriteTimeUtc(path, DateTime.Now);
            }
            else
            {
                Logger.LogWarning($"Cannot find last built scene, please Rebuild.");
            }
        }

        public static void ReloadNewBuild(Action onSuccess = null)
        {
            bool buildTestBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            if (!buildTestBlocked)
            {
                EnvConfig.ConfigurePlayerSettings();
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                AssetExporter.CleanupUnityPackageExport();
                VRC_SdkBuilder.PreBuildBehaviourPackaging();

                VRC_SdkBuilder.ExportSceneResource();
                onSuccess?.Invoke();
            }
        }
    
        public static void PublishLastBuild()
        {
            if (APIUser.CurrentUser.canPublishWorlds)
            {
                EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders", false);
                
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                VRC_SdkBuilder.UploadLastExportedSceneBlueprint();
            }
            else
            {
                Logger.LogError("You need to be logged in to publish a world");
            }
        }

        public static async Task PublishLastBuildAsync(VRChatApiTools.WorldInfo worldInfo = null, Action<VRChatApiTools.WorldInfo> onSucces = null)
        {
            if (APIUser.CurrentUser.canPublishWorlds)
            {
                VRChatApiUploaderAsync uploaderAsync = new VRChatApiUploaderAsync();
                uploaderAsync.UseStatusWindow();
                
                await uploaderAsync.UploadLastBuild(worldInfo);
                
                onSucces?.Invoke(worldInfo);
            }
            else
            {
                Logger.LogError("You need to be logged in to publish a world");
            }
        }
    
        public static void PublishNewBuild()
        {
            bool buildBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            if (!buildBlocked)
            {
                if (APIUser.CurrentUser.canPublishWorlds)
                {
                    EnvConfig.ConfigurePlayerSettings();
                    EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders", false);

                    VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                    VRC_SdkBuilder.PreBuildBehaviourPackaging();
                    VRC_SdkBuilder.ExportAndUploadSceneBlueprint();
                }
                else
                {
                    Logger.LogError("You need to be logged in to publish a world");
                }
            }
        }
        
        public static async void PublishNewBuildAsync(VRChatApiTools.WorldInfo worldInfo = null, Action<VRChatApiTools.WorldInfo> onSucces = null)
        {
            bool buildTestBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            if (!buildTestBlocked)
            {
                if (APIUser.CurrentUser.canPublishWorlds)
                {
                    EnvConfig.ConfigurePlayerSettings();
                    EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders", false);

                    VRC_SdkBuilder.shouldBuildUnityPackage = VRCSdkControlPanel.FutureProofPublishEnabled;
                    AssetExporter.CleanupUnityPackageExport();
                    VRC_SdkBuilder.PreBuildBehaviourPackaging();

                    VRC_SdkBuilder.ExportSceneResource();
                    
                    await PublishLastBuildAsync(worldInfo);
                    
                    onSucces?.Invoke(worldInfo);
                }
                else
                {
                    Logger.LogError("You need to be logged in to publish a world");
                }
            }
        }
        
        public static void TestExistingBuild(DeploymentUnit deploymentUnit)
        {
            string actualLastBuild = EditorPrefs.GetString("lastVRCPath");
            
            EditorPrefs.SetString("lastVRCPath", deploymentUnit.filePath);
            //EditorPrefs.SetString("currentBuildingAssetBundlePath", UnityWebRequest.UnEscapeURL(deploymentUnit.buildPath));
            VRC_SdkBuilder.shouldBuildUnityPackage = false;
            VRC_SdkBuilder.RunLastExportedSceneResource();
            
            EditorPrefs.SetString("lastVRCPath", actualLastBuild);
        }
        
        public static void PublishExistingBuild(DeploymentUnit deploymentUnit)
        {
            if (VRChatApiTools.FindPipelineManager().blueprintId != deploymentUnit.pipelineID)
            {
                if (EditorUtility.DisplayDialog("Deployment Manager",
                    "The blueprint ID for the selected build doesn't match the one on the scene descriptor. This can happen if the blueprint ID on the selected branch was changed after this build was published. While this build can still be uploaded, you will have to switch the blueprint ID on your scene descriptor to match that of the selected build. Are you sure you want to continue?",
                    "Yes", "No"))
                    BuildHelperWindow.ApplyPipelineID(deploymentUnit.pipelineID);
                else return;
            }

            EditorPrefs.SetString("lastVRCPath", deploymentUnit.filePath);
            EditorPrefs.SetString("currentBuildingAssetBundlePath", UnityWebRequest.UnEscapeURL(deploymentUnit.filePath));
            EditorPrefs.SetString("lastBuiltAssetBundleBlueprintID", deploymentUnit.pipelineID);
            AssetExporter.CleanupUnityPackageExport();
            VRCWorldAssetExporter.LaunchSceneBlueprintUploader();
        }
    }
}