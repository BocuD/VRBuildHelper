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

#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRCSDK2;
using static BocuD.VRChatApiTools.VRChatApiTools;

namespace BocuD.BuildHelper
{
    public class BuildHelperRuntime : MonoBehaviour
    {
        [SerializeField]private bool vrcSceneReady;
        [SerializeField]private RuntimeWorldCreation runtimeWorldCreation;

        [SerializeField]private BuildHelperData buildHelperBehaviour;
        [SerializeField]private BranchStorageObject buildHelperData;
        
        [SerializeField]private BuildHelperToolsMenu buildHelperToolsMenu;

        private void Start()
        {
            buildHelperBehaviour = BuildHelperData.GetDataBehaviour();
            
            if (buildHelperBehaviour)
                buildHelperData = buildHelperBehaviour.dataObject;
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
                    if (GameObject.Find("VRCSDK"))
                    {
                        runtimeWorldCreation = GameObject.Find("VRCSDK").GetComponent<RuntimeWorldCreation>();
                    
                        Logger.Log("Found RuntimeWorldCreation component, initialising BuildHelperRuntime");

                        //apply saved camera position
                        GameObject.Find("VRCCam").transform.SetPositionAndRotation(buildHelperData.CurrentBranch.camPos, buildHelperData.CurrentBranch.camRot);

                        //modify sdk upload panel to add world helper menu
                        Transform worldPanel = runtimeWorldCreation.transform.GetChild(0).GetChild(0).GetChild(1);
                        RectTransform worldPanelRect = worldPanel.GetComponent<RectTransform>();
                        worldPanelRect.offsetMin = new Vector2(-250, 0);

                        GameObject RuntimeTools = (GameObject) Instantiate(Resources.Load("RuntimeTools"),
                            runtimeWorldCreation.transform.GetChild(0).GetChild(0));
                        RuntimeTools.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 335.5f);

                        buildHelperToolsMenu = RuntimeTools.GetComponent<BuildHelperToolsMenu>();

                        buildHelperToolsMenu.saveCamPosition.isOn = buildHelperData.CurrentBranch.saveCamPos;
                        buildHelperToolsMenu.uniqueCamPosition.isOn = buildHelperData.CurrentBranch.uniqueCamPos;

                        vrcSceneReady = true;
                    }

                    timeout--;
                }
                else
                {
                    Application.logMessageReceived -= Log;
                    Destroy(gameObject);
                }
            }

            if (vrcSceneReady)
            {
                if (runtimeWorldCreation.titleText.text != "Configure World") return;
                
                if (!appliedChanges)
                {
                    if (buildHelperData.CurrentBranch.nameChanged)
                        runtimeWorldCreation.blueprintName.text = buildHelperData.CurrentBranch.editedName;
                    if (buildHelperData.CurrentBranch.descriptionChanged)
                        runtimeWorldCreation.blueprintDescription.text =
                            buildHelperData.CurrentBranch.editedDescription;
                    if (buildHelperData.CurrentBranch.capacityChanged)
                        runtimeWorldCreation.worldCapacity.text = buildHelperData.CurrentBranch.editedCap.ToString();
                    if (buildHelperData.CurrentBranch.tagsChanged)
                        runtimeWorldCreation.userTags.text =
                            TagListToTagString(buildHelperData.CurrentBranch.editedTags);
                    
                    appliedChanges = true;
                    return;
                }

                if (!appliedImageChanges && buildHelperData.CurrentBranch.vrcImageHasChanges)
                {
                    runtimeWorldCreation.shouldUpdateImageToggle.isOn = true;
                    buildHelperToolsMenu.imageSourceDropdown.value = 1;
                    buildHelperToolsMenu.imageSourceDropdown.onValueChanged.Invoke(1);
                    buildHelperToolsMenu.OnFileSelected(Application.dataPath + "/Resources/BuildHelper/" + buildHelperBehaviour.sceneID + '_' + buildHelperData.CurrentBranch.branchID + "-edit.png");
                    appliedImageChanges = true;
                    return;
                }
            }
        }
        
        private void Log(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Log && logString.Contains("Starting upload"))
            {
                buildHelperData.CurrentBranch.saveCamPos = buildHelperToolsMenu.saveCamPosition.isOn;
                buildHelperData.CurrentBranch.uniqueCamPos = buildHelperToolsMenu.uniqueCamPosition.isOn;
            
                if (buildHelperData.CurrentBranch.saveCamPos)
                {
                    GameObject vrcCam = GameObject.Find("VRCCam");
                    if (vrcCam)
                    {
                        if (buildHelperData.CurrentBranch.uniqueCamPos)
                        {
                            buildHelperData.CurrentBranch.camPos = vrcCam.transform.position;
                            buildHelperData.CurrentBranch.camRot = vrcCam.transform.rotation;
                        }
                        else
                        {
                            foreach (Branch b in buildHelperData.branches)
                            {
                                if (b.uniqueCamPos) continue;

                                b.camPos = vrcCam.transform.position;
                                b.camRot = vrcCam.transform.rotation;
                            }
                            
                            buildHelperData.CurrentBranch.camPos = vrcCam.transform.position;
                            buildHelperData.CurrentBranch.camRot = vrcCam.transform.rotation;
                        }
                    }
                }
            }
        
            if (type == LogType.Log && logString.Contains("Asset bundle upload succeeded"))
            {
                if (buildHelperData.CurrentBranch == null)
                {
                    Logger.LogError("Build Helper data object doesn't exist, skipping build data update");
                    return;
                }

                //ExtractWorldImage();
                ExtractBuildInfo();
                TrySavePublishedWorld();
            }

            if (type == LogType.Log && logString.Contains("Image upload succeeded"))
            {
                buildHelperData.CurrentBranch.vrcImageHasChanges = false;
                buildHelperData.CurrentBranch.vrcImageWarning = "";
                buildHelperData.branches[buildHelperData.currentBranch] = buildHelperData.CurrentBranch;
                buildHelperBehaviour.SaveToJSON();
            }
        }

        private void ExtractBuildInfo()
        {
            if (CurrentPlatform() == Platform.mobile)
            {
                Logger.Log("Detected succesful upload for Android");
                buildHelperData.CurrentBranch.buildData.androidUploadTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                buildHelperData.CurrentBranch.buildData.androidUploadedBuildVersion =
                    buildHelperData.CurrentBranch.buildData.androidBuildVersion;
            }
            else
            {
                Logger.Log("Detected succesful upload for PC");
                buildHelperData.CurrentBranch.buildData.pcUploadTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                buildHelperData.CurrentBranch.buildData.pcUploadedBuildVersion =
                    buildHelperData.CurrentBranch.buildData.pcBuildVersion;
            }

            buildHelperData.CurrentBranch.nameChanged = false;
            buildHelperData.CurrentBranch.descriptionChanged = false;
            buildHelperData.CurrentBranch.capacityChanged = false;
            buildHelperData.CurrentBranch.tagsChanged = false;

            if (buildHelperData.CurrentBranch.blueprintID == "")
            {
                buildHelperData.CurrentBranch.blueprintID = FindObjectOfType<PipelineManager>().blueprintId;
            }

            buildHelperBehaviour.SaveToJSON();
        }

        private void TrySavePublishedWorld()
        {
            if (!buildHelperData.CurrentBranch.hasDeploymentData) return;
            
            if (buildHelperData.CurrentBranch.deploymentData.deploymentPath == "") {
                Logger.LogWarning($"Deployment folder location for {buildHelperData.CurrentBranch.name} is not set, no published builds will be saved.");
                return;
            }

            string justPublishedFilePath = EditorPrefs.GetString("lastVRCPath");
            
            if (!File.Exists(justPublishedFilePath)) return; // Defensive check, normally the file should exist there given that a publish was just completed

            string deploymentFolder = Path.GetFullPath(Application.dataPath + buildHelperData.CurrentBranch.deploymentData.deploymentPath);
            
            if (Path.GetDirectoryName(justPublishedFilePath).StartsWith(deploymentFolder))
            {
                Logger.Log("Not saving build as the published build was already located within the deployments folder. This probably means the published build was an existing (older) build.");
                return;
            }

            string backupFileName = ComposeBackupFileName(buildHelperData.CurrentBranch, justPublishedFilePath);
            string backupPath = Path.Combine(new []{deploymentFolder, backupFileName});

            File.Copy(justPublishedFilePath, backupPath);
            Logger.Log("Completed a backup: " + backupFileName);
        }
        
        private string ComposeBackupFileName(Branch branch, string justPublishedFilePath)
        {
            string buildDate = File.GetLastWriteTime(justPublishedFilePath).ToString("yyyy'-'MM'-'dd HH'-'mm'-'ss");
            string autoUploader = "";
            string buildNumber =
                $"build{(CurrentPlatform() == Platform.PC ? branch.buildData.pcBuildVersion.ToString() : branch.buildData.androidBuildVersion.ToString())}";
            string platform = CurrentPlatform().ToString();
            string gitHash = TryGetGitHashDiscardErrorsSilently();
            if (branch.branchID == "")
            {
                return $"[{buildDate}]_{autoUploader}{branch.deploymentData.initialBranchName}_{buildNumber}_{branch.blueprintID}_{platform}_{gitHash}.vrcw";
            }
            return $"[{buildDate}]_{autoUploader}{branch.branchID}_{buildNumber}_{branch.blueprintID}_{platform}_{gitHash}.vrcw";
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
                    Logger.Log("Could not retrieve git hash: " + trimmedOutput);
                    return "@nohash";
                }

                return trimmedOutput;
            }
            catch (Exception e)
            {
                Logger.Log("Could not retrieve git hash: " + e.Message);
                return "@nohash";
            }
        }
        
        private static string TagListToTagString(IEnumerable<string> input)
        {
            return input.Aggregate("", (current, s) => current + s.ReplaceFirst("author_tag_", "") + " ");
        }

        public static Platform CurrentPlatform()
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

        private void OnDisable()
        {
            if (vrcSceneReady)
                Application.logMessageReceived -= Log;
        }
    }
}

#endif