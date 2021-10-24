#if UNITY_EDITOR

using System;
using System.IO;
using BestHTTP.Extensions;
using UnityEditor;
using UnityEngine;
using VRCSDK2;

using static BocuD.BuildHelper.AutonomousBuildInformation;

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
        [SerializeField]private Branch branch;

        private BuildHelperToolsMenu buildHelperToolsMenu;
        private bool autoUpload;

        private void Start()
        {
            buildHelperData = (BuildHelperData)FindObjectOfType(typeof(BuildHelperData));
            branch = buildHelperData.branches[buildHelperData.currentBranch];
        }

        private int timeout = 10;
    
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
                        GameObject.Find("VRCCam").transform.SetPositionAndRotation(branch.camPos, branch.camRot);

                        //modify sdk upload panel to add world helper menu
                        Transform worldPanel = runtimeWorldCreation.transform.GetChild(0).GetChild(0).GetChild(1);
                        RectTransform worldPanelRect = worldPanel.GetComponent<RectTransform>();
                        worldPanelRect.offsetMin = new Vector2(-250, 0);

                        GameObject RuntimeTools = (GameObject) Instantiate(Resources.Load("RuntimeTools"),
                            runtimeWorldCreation.transform.GetChild(0).GetChild(0));
                        RuntimeTools.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 335.5f);

                        buildHelperToolsMenu = RuntimeTools.GetComponent<BuildHelperToolsMenu>();

                        buildHelperToolsMenu.saveCamPosition.isOn = branch.saveCamPos;
                        buildHelperToolsMenu.uniqueCamPosition.isOn = branch.uniqueCamPos;
                    
                        if (buildHelperData.autonomousBuild.activeBuild)
                        {
                            autoUpload = true;
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

                if (autoUpload)
                {
                    if (runtimeWorldCreation.titleText.text == "Configure World")
                    {
                        Upload();
                        autoUpload = false;
                    }
                }
            }
        }

        private void Upload()
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

        private void Log(string logString, string stackTrace, LogType type)
        {
            if (logString.Contains("Starting upload"))
            {
                AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;
                switch (autonomousBuild.progress)
                {
                    case Progress.PreInitialBuild:
                    {
                        AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                        statusWindow.currentPlatform = autonomousBuild.initialTarget;
                        statusWindow.currentState = AutonomousBuildState.uploading;
                    }
                        break;
                    case Progress.PreSecondaryBuild:
                    {
                        AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                        statusWindow.currentPlatform = autonomousBuild.secondaryTarget;
                        statusWindow.currentState = AutonomousBuildState.uploading;
                    }
                        break;
                }

                branch.saveCamPos = buildHelperToolsMenu.saveCamPosition.isOn;
                branch.uniqueCamPos = buildHelperToolsMenu.uniqueCamPosition.isOn;
            
                if (branch.saveCamPos)
                {
                    GameObject vrcCam = GameObject.Find("VRCCam");
                    if (vrcCam)
                    {
                        if (branch.uniqueCamPos)
                        {
                            branch.camPos = vrcCam.transform.position;
                            branch.camRot = vrcCam.transform.rotation;
                        }
                        else
                        {
                            foreach (Branch b in buildHelperData.branches)
                            {
                                if (b.uniqueCamPos) continue;

                                b.camPos = vrcCam.transform.position;
                                b.camRot = vrcCam.transform.rotation;
                            }
                        }
                    }
                }
            }
        
            if (logString.Contains("Asset bundle upload succeeded"))
            {
                if (branch == null)
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

                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            statusWindow.currentPlatform = autonomousBuild.secondaryTarget;
                            statusWindow.currentState = AutonomousBuildState.switchingPlatform;
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
                }
                
                ExtractWorldImage();
                ExtractBuildInfo();
            }
        }

        private void ExtractBuildInfo()
        {
            branch.VRCName = VRCName;
            branch.VRCDesc = VRCDesc;
            branch.VRCPlayerCount = vrcPlayerCount;
            branch.vrcReleaseState = vrcReleaseState;
            branch.vrcTags = vrcTags;
            branch.VRCDataInitialised = true;

#if UNITY_ANDROID
            Debug.Log("Detected succesful upload for Android");
            branch.buildData.androidUploadTime = $"{DateTime.Now}";
            branch.buildData.androidUploadedBuildVersion = branch.buildData.androidBuildVersion;
#else
            Debug.Log("Detected succesful upload for PC");
            branch.buildData.pcUploadTime = $"{DateTime.Now}";
            branch.buildData.pcUploadedBuildVersion = branch.buildData.pcBuildVersion;
#endif
            buildHelperData.branches[buildHelperData.currentBranch] = branch;
            buildHelperData.SaveToJSON();
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

            string filePath = dirPath + branch.name + ".png";
            File.WriteAllBytes(filePath, worldImagePNG);

            filePath = "Assets/Resources/BuildHelper/" + branch.name + ".png";

            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            importer.textureType = TextureImporterType.GUI;
            AssetDatabase.WriteImportSettingsIfDirty(filePath);
            AssetDatabase.ImportAsset(filePath);
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