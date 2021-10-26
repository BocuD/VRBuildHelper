#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using BestHTTP.Extensions;
using UnityEditor;
using UnityEngine;
using VRCSDK2;

using static BocuD.BuildHelper.AutonomousBuildInformation;
using Debug = UnityEngine.Debug;

namespace BocuD.BuildHelper
{
    public class BuildHelperRuntime : MonoBehaviour
    {
        [SerializeField]private bool vrcSceneReady;
        [SerializeField]private string VRCName;
        [SerializeField]private string VRCDesc;
        [SerializeField]private int vrcPlayerCount;
        [SerializeField]private bool vrcReleaseState;
        [SerializeField]private string vrcTags;
        [SerializeField]private RuntimeWorldCreation runtimeWorldCreation;
        [SerializeField]private Texture vrcImage;

        [SerializeField]private BuildHelperData buildHelperData;

        [SerializeField]private BuildHelperToolsMenu buildHelperToolsMenu;
        private bool shouldAutoUpload;

        private void Start()
        {
            buildHelperData = FindObjectOfType<BuildHelperData>();
        }

        private int timeout = 10;
        private bool appliedChanges = false;
        private bool appliedImageChanges = false;
        
        private void Update()
        {
            if (!vrcSceneReady)
            {
                if (timeout > 0)
                {
                    if (GameObject.Find("VRCSDK").GetComponent<RuntimeWorldCreation>())
                    {
                        runtimeWorldCreation = GameObject.Find("VRCSDK").GetComponent<RuntimeWorldCreation>();
                    
                        Debug.Log("Found RuntimeWorldCreation component, initialising BuildHelperRuntime");

                        //apply saved camera position
                        GameObject.Find("VRCCam").transform.SetPositionAndRotation(buildHelperData.currentBranch.camPos, buildHelperData.currentBranch.camRot);

                        //modify sdk upload panel to add world helper menu
                        Transform worldPanel = runtimeWorldCreation.transform.GetChild(0).GetChild(0).GetChild(1);
                        RectTransform worldPanelRect = worldPanel.GetComponent<RectTransform>();
                        worldPanelRect.offsetMin = new Vector2(-250, 0);

                        GameObject RuntimeTools = (GameObject) Instantiate(Resources.Load("RuntimeTools"),
                            runtimeWorldCreation.transform.GetChild(0).GetChild(0));
                        RuntimeTools.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 335.5f);

                        buildHelperToolsMenu = RuntimeTools.GetComponent<BuildHelperToolsMenu>();

                        buildHelperToolsMenu.saveCamPosition.isOn = buildHelperData.currentBranch.saveCamPos;
                        buildHelperToolsMenu.uniqueCamPosition.isOn = buildHelperData.currentBranch.uniqueCamPos;
                    
                        if (buildHelperData.autonomousBuild.activeBuild)
                        {
                            shouldAutoUpload = true;
                        }

                        vrcSceneReady = true;
                    }

                    timeout--;
                }
                else
                {
                    Debug.Log("okbye");
                    Application.logMessageReceived -= Log;
                    Destroy(gameObject);
                }
            }

            if (vrcSceneReady)
            {
                VRCName = runtimeWorldCreation.blueprintName.text;
                VRCDesc = runtimeWorldCreation.blueprintDescription.text;
                vrcPlayerCount = runtimeWorldCreation.worldCapacity.text.ToInt32();
                vrcReleaseState = runtimeWorldCreation.releasePublic.isOn;
                vrcTags = runtimeWorldCreation.userTags.text;
                vrcImage = runtimeWorldCreation.shouldUpdateImageToggle.isOn
                    ? runtimeWorldCreation.liveBpImage.mainTexture
                    : runtimeWorldCreation.bpImage.mainTexture;
                
                if (runtimeWorldCreation.titleText.text != "Configure World") return;
                
                if (!appliedChanges && buildHelperData.currentBranch.vrcDataHasChanges)
                {
                    runtimeWorldCreation.blueprintName.text = buildHelperData.currentBranch.VRCNameLocal;
                    runtimeWorldCreation.blueprintDescription.text = buildHelperData.currentBranch.VRCDescLocal;
                    runtimeWorldCreation.worldCapacity.text = buildHelperData.currentBranch.VRCCapLocal.ToString();
                    runtimeWorldCreation.userTags.text = buildHelperData.currentBranch.vrcTagsLocal;
                    appliedChanges = true;
                    return;
                }

                if (!appliedImageChanges && buildHelperData.currentBranch.vrcImageHasChanges)
                {
                    runtimeWorldCreation.shouldUpdateImageToggle.isOn = true;
                    buildHelperToolsMenu.imageSourceDropdown.value = 1;
                    buildHelperToolsMenu.imageSourceDropdown.onValueChanged.Invoke(1);
                    buildHelperToolsMenu.OnFileSelected(Application.dataPath + "/Resources/BuildHelper/" + buildHelperData.currentBranch.name + "-edit.png");
                    appliedImageChanges = true;
                    return;
                }
                    
                if (shouldAutoUpload)
                {
                    Upload();
                    shouldAutoUpload = false;
                }

                if (autoUploading)
                {
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                    
                    if(statusWindow.abort)
                    {
                        buildHelperData.autonomousBuild.activeBuild = false;
                        buildHelperData.SaveToJSON();
                        statusWindow.currentState = AutonomousBuildState.aborted;
                        EditorApplication.isPlaying = false;
                    }
                }
            }
        }

        private bool autoUploading = false;
        
        private void Upload()
        {
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
            
            if (statusWindow.abort)
            {
                buildHelperData.autonomousBuild.activeBuild = false;
                buildHelperData.SaveToJSON();
                statusWindow.currentState = AutonomousBuildState.aborted;
                EditorApplication.isPlaying = false;
            }
            else
            {
                AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;
                switch (autonomousBuild.progress)
                {
                    case Progress.PreInitialBuild:
                        runtimeWorldCreation.uploadButton.onClick.Invoke();
                        break;
                    case Progress.PreSecondaryBuild:
                        runtimeWorldCreation.uploadButton.onClick.Invoke();
                        break;
                }
            }
        }

        private void Log(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Log && logString.Contains("Starting upload"))
            {
                AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;

                if (autonomousBuild.activeBuild)
                {
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                    
                    switch (autonomousBuild.progress)
                    {
                        case Progress.PreInitialBuild:
                            statusWindow.currentPlatform = autonomousBuild.initialTarget;
                            statusWindow.currentState = AutonomousBuildState.uploading;
                            autoUploading = true;
                            break;

                        case Progress.PreSecondaryBuild:
                            statusWindow.currentPlatform = autonomousBuild.secondaryTarget;
                            statusWindow.currentState = AutonomousBuildState.uploading;
                            autoUploading = true;
                            break;
                    }
                }

                buildHelperData.currentBranch.saveCamPos = buildHelperToolsMenu.saveCamPosition.isOn;
                buildHelperData.currentBranch.uniqueCamPos = buildHelperToolsMenu.uniqueCamPosition.isOn;
            
                if (buildHelperData.currentBranch.saveCamPos)
                {
                    GameObject vrcCam = GameObject.Find("VRCCam");
                    if (vrcCam)
                    {
                        if (buildHelperData.currentBranch.uniqueCamPos)
                        {
                            buildHelperData.currentBranch.camPos = vrcCam.transform.position;
                            buildHelperData.currentBranch.camRot = vrcCam.transform.rotation;
                        }
                        else
                        {
                            foreach (Branch b in buildHelperData.branches)
                            {
                                if (b.uniqueCamPos) continue;

                                b.camPos = vrcCam.transform.position;
                                b.camRot = vrcCam.transform.rotation;
                            }
                            
                            buildHelperData.currentBranch.camPos = vrcCam.transform.position;
                            buildHelperData.currentBranch.camRot = vrcCam.transform.rotation;
                        }
                    }
                }
            }
        
            if (type == LogType.Log && logString.Contains("Asset bundle upload succeeded"))
            {
                if (buildHelperData.currentBranch == null)
                {
                    Debug.LogError("Build Helper data object doesn't exist, skipping build data update");
                    return;
                }

                if (buildHelperData.autonomousBuild.activeBuild)
                {
                    AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;
                    switch (autonomousBuild.progress)
                    {
                        case Progress.PreInitialBuild:
                        {
                            Debug.Log($"Succesfully autonomously uploaded {autonomousBuild.initialTarget} build");
                            autonomousBuild.progress = Progress.PostInitialBuild;

                            if (!autonomousBuild.singleTarget)
                            {
                                AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                                statusWindow.currentPlatform = autonomousBuild.secondaryTarget;
                                statusWindow.currentState = AutonomousBuildState.switchingPlatform;
                            }
                        }
                            break;

                        case Progress.PreSecondaryBuild:
                        {
                            Debug.Log($"Succesfully autonomously uploaded {autonomousBuild.secondaryTarget} build");
                            autonomousBuild.progress = Progress.PostSecondaryBuild;

                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            statusWindow.currentPlatform = autonomousBuild.initialTarget;
                            statusWindow.currentState = AutonomousBuildState.switchingPlatform;
                        }
                            break;
                    }

                    buildHelperData.autonomousBuild = autonomousBuild;
                    buildHelperData.SaveToJSON();
                }
                
                ExtractWorldImage();
                ExtractBuildInfo();
                TrySavePublishedWorld();
            }

            if (type == LogType.Log && logString.Contains("Image upload succeeded"))
            {
                buildHelperData.currentBranch.vrcImageHasChanges = false;
                buildHelperData.branches[buildHelperData.currentBranchIndex] = buildHelperData.currentBranch;
                buildHelperData.SaveToJSON();
            }
        }
        
        private void ExtractWorldImage()
        {
            Texture2D texture2D = new Texture2D(vrcImage.width, vrcImage.height, TextureFormat.RGBA32, false);

            RenderTexture currentRT = RenderTexture.active;

            RenderTexture renderTexture = new RenderTexture(vrcImage.width, vrcImage.height, 32);
            Graphics.Blit(vrcImage, renderTexture);

            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture2D.Apply();

            RenderTexture.active = currentRT;

            byte[] worldImagePNG = texture2D.EncodeToPNG();

            string dirPath = Application.dataPath + "/Resources/BuildHelper/";
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string filePath = dirPath + buildHelperData.currentBranch.name + ".png";
            File.WriteAllBytes(filePath, worldImagePNG);

            filePath = "Assets/Resources/BuildHelper/" + buildHelperData.currentBranch.name + ".png";

            // TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            // importer.textureType = TextureImporterType.GUI;
            AssetDatabase.WriteImportSettingsIfDirty(filePath);
            AssetDatabase.ImportAsset(filePath);
        }

        private void ExtractBuildInfo()
        {
            buildHelperData.currentBranch.VRCName = VRCName;
            buildHelperData.currentBranch.VRCDesc = VRCDesc;
            buildHelperData.currentBranch.VRCCap = vrcPlayerCount;
            buildHelperData.currentBranch.vrcReleaseState = vrcReleaseState;
            buildHelperData.currentBranch.vrcTags = vrcTags;
            
            buildHelperData.currentBranch.VRCNameLocal = VRCName;
            buildHelperData.currentBranch.VRCDescLocal = VRCDesc;
            buildHelperData.currentBranch.VRCCapLocal = vrcPlayerCount;
            buildHelperData.currentBranch.vrcReleaseStateLocal = vrcReleaseState;
            buildHelperData.currentBranch.vrcTagsLocal = vrcTags;
            
            buildHelperData.currentBranch.VRCDataInitialised = true;

            if (CurrentPlatform() == Platform.mobile)
            {
                Debug.Log("Detected succesful upload for Android");
                buildHelperData.currentBranch.buildData.androidUploadTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                buildHelperData.currentBranch.buildData.androidUploadedBuildVersion =
                    buildHelperData.currentBranch.buildData.androidBuildVersion;
            }
            else
            {
                Debug.Log("Detected succesful upload for PC");
                buildHelperData.currentBranch.buildData.pcUploadTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                buildHelperData.currentBranch.buildData.pcUploadedBuildVersion =
                    buildHelperData.currentBranch.buildData.pcBuildVersion;
            }

            buildHelperData.currentBranch.vrcDataHasChanges = false;

            buildHelperData.SaveToJSON();
        }

        private void TrySavePublishedWorld()
        {
            if (!buildHelperData.currentBranch.hasDeploymentData) return;
            
            if (buildHelperData.currentBranch.deploymentData.deploymentPath == "") {
                Debug.LogWarning($"Deployment folder location for {buildHelperData.currentBranch.name} is not set, no published builds will be saved.");
                return;
            }

            string justPublishedFilePath = EditorPrefs.GetString("lastVRCPath");
            
            if (!File.Exists(justPublishedFilePath)) return; // Defensive check, normally the file should exist there given that a publish was just completed

            string deploymentFolder = Path.GetFullPath(Application.dataPath + buildHelperData.currentBranch.deploymentData.deploymentPath);
            
            if (Path.GetDirectoryName(justPublishedFilePath).StartsWith(deploymentFolder))
            {
                Debug.Log("Not saving build as the published build was already located within the deployments folder. This probably means the published build was an existing (older) build.");
                return;
            }

            string backupFileName = ComposeBackupFileName(buildHelperData.currentBranch, justPublishedFilePath);
            string backupPath = Path.Combine(new []{deploymentFolder, backupFileName});

            File.Copy(justPublishedFilePath, backupPath);
            Debug.Log("Completed a backup: " + backupFileName);
        }
        
        private string ComposeBackupFileName(Branch branch, string justPublishedFilePath)
        {
            string buildDate = File.GetLastWriteTime(justPublishedFilePath).ToString("yyyy'-'MM'-'dd HH'-'mm'-'ss");
            string autoUploader = buildHelperData.autonomousBuild.activeBuild ? "auto_" : "";
            string buildNumber =
                $"build{(CurrentPlatform() == Platform.PC ? branch.buildData.pcBuildVersion.ToString() : branch.buildData.pcBuildVersion.ToString())}";
            string platform = CurrentPlatform().ToString();
            string gitHash = TryGetGitHashDiscardErrorsSilently();
            return $"[{buildDate}]_{autoUploader}{branch.name}_{buildNumber}_{branch.blueprintID}_{platform}_{gitHash}.vrcw";
        }
        
        private static string TryGetGitHashDiscardErrorsSilently()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "git",
                        WorkingDirectory = Application.dataPath,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Arguments = "rev-parse --short HEAD"
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                var trimmedOutput = output.Trim().ToLowerInvariant();
                if (trimmedOutput.Length != 8)
                {
                    UnityEngine.Debug.Log("Could not retrieve git hash: " + trimmedOutput);
                    return "@nohash";
                }

                return trimmedOutput;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log("Could not retrieve git hash: " + e.Message);
                return "@nohash";
            }
        }

        private static Platform CurrentPlatform()
        {
#if UNITY_ANDROID
            return Platform.mobile;
#else
            return Platform.PC;
#endif
        }

        private void OnEnable()
        {
            Application.logMessageReceived += Log;
        }

        void OnDisable()
        {
            if (vrcSceneReady)
                Application.logMessageReceived -= Log;
        }
    }
}

#endif