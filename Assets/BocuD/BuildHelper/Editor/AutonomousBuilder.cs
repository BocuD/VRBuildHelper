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

#if UNITY_EDITOR

using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using UnityEditor.Build;
using Object = UnityEngine.Object;
using static BocuD.BuildHelper.AutonomousBuildInformation;

namespace BocuD.BuildHelper
{
    [InitializeOnLoad]
    public static class AutonomousBuilderPlaymodeStateWatcher
    {
        // register an event handler when the class is initialized
        static AutonomousBuilderPlaymodeStateWatcher()
        {
            EditorApplication.playModeStateChanged += PlayModeStateUpdate;
        }

        private static void PlayModeStateUpdate(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (Object.FindObjectOfType<BuildHelperData>() == null) return;

                BuildHelperData buildHelperData = Object.FindObjectOfType<BuildHelperData>();
                buildHelperData.LoadFromJSON();

                if (buildHelperData.autonomousBuild.activeBuild)
                {
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();

                    if (statusWindow.abort)
                    {
                        buildHelperData.autonomousBuild.activeBuild = false;
                        buildHelperData.SaveToJSON();
                        statusWindow.currentState = AutonomousBuildState.aborted;
                        return;
                    }

                    if (buildHelperData.autonomousBuild.singleTarget)
                    {
                        if (buildHelperData.autonomousBuild.progress == Progress.PostInitialBuild)
                        {
                            buildHelperData.autonomousBuild.activeBuild = false;

                            if (statusWindow.currentState != AutonomousBuildState.failed)
                            {
                                buildHelperData.autonomousBuild.progress = Progress.Finished;
                                Logger.Log("Autonomous publish succeeded");
                                statusWindow.currentState = AutonomousBuildState.finished;
                            }

                            buildHelperData.SaveToJSON();
                        }
                    }
                    else
                    {
                        switch (buildHelperData.autonomousBuild.progress)
                        {
                            case Progress.PostInitialBuild:
                                if (buildHelperData.autonomousBuild.secondaryTarget == Platform.mobile)
                                    SetPlatformAndroid();
                                else SetPlatformWindows();
                                break;

                            case Progress.PostSecondaryBuild:
                                if (buildHelperData.autonomousBuild.initialTarget == Platform.mobile)
                                    SetPlatformAndroid();
                                else SetPlatformWindows();
                                break;
                        }
                    }
                }
            }
        }

        private static void SetPlatformWindows()
        {
            Logger.Log($"Switching platform to Windows");

            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                BuildTarget.StandaloneWindows64);
        }

        private static void SetPlatformAndroid()
        {
            Logger.Log($"Switching platform to Windows");

            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }

    public class AutonomousBuilderTargetWatcher : IActiveBuildTargetChanged
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            Logger.Log("Switched build target to " + newTarget);

            BuildHelperData buildHelperData = UnityEngine.Object.FindObjectOfType<BuildHelperData>();
            buildHelperData.LoadFromJSON();

            AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;

            if (autonomousBuild.activeBuild)
            {
                Platform currentPlatform;

                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                switch (target)
                {
                    case BuildTarget.Android:
                        currentPlatform = Platform.mobile;
                        break;
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        currentPlatform = Platform.PC;
                        break;
                    default:
                        return;
                }

                switch (autonomousBuild.progress)
                {
                    case Progress.PostInitialBuild:
                        if (currentPlatform == autonomousBuild.secondaryTarget)
                        {
                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            if (statusWindow.abort)
                            {
                                buildHelperData.autonomousBuild.activeBuild = false;
                                buildHelperData.SaveToJSON();
                                statusWindow.currentState = AutonomousBuildState.aborted;
                            }
                            else
                            {
                                autonomousBuild.progress = Progress.PostPlatformSwitch;
                                buildHelperData.SaveToJSON();
                                
                                if (!APIUser.IsLoggedIn)
                                {
                                    EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
                                    
                                    if (statusWindow.abort)
                                    {
                                        buildHelperData.autonomousBuild.activeBuild = false;
                                        buildHelperData.SaveToJSON();
                                        statusWindow.currentState = AutonomousBuildState.aborted;
                                    }
                                    else if (statusWindow.currentState != AutonomousBuildState.waitingForApi)
                                    {
                                        statusWindow.currentState = AutonomousBuildState.waitingForApi;
                                        LoginStateChecker(buildHelperData);
                                    }
                                } else StartSecondaryBuild(buildHelperData);
                            }
                        }

                        break;

                    case Progress.PostSecondaryBuild:
                        if (currentPlatform == autonomousBuild.initialTarget)
                        {
                            autonomousBuild.activeBuild = false;
                            autonomousBuild.progress = Progress.Finished;
                            Logger.Log("<color=green>Autonomous publish succeeded</color>");

                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            statusWindow.currentState = AutonomousBuildState.finished;

                            buildHelperData.SaveToJSON();
                        }

                        break;
                }
            }
        }
        
        private static async void LoginStateChecker(BuildHelperData data)
        {
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
            
            for (int attempt = 0; attempt < 30; attempt++)
            {
                if (APIUser.IsLoggedIn)
                {
                    if (statusWindow.abort)
                    {
                        data.autonomousBuild.activeBuild = false;
                        data.SaveToJSON();
                        statusWindow.currentState = AutonomousBuildState.aborted;
                    }
                    else
                    {
                        StartSecondaryBuild(data);
                    }
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            //reset build state
            data.autonomousBuild.activeBuild = false;
            data.SaveToJSON();
            
            statusWindow.failReason = "Timed out waiting for VRChat Api login";
            statusWindow.currentState = AutonomousBuildState.failed;

            Logger.LogError("Timed out waiting for login");
        }

        private static void StartSecondaryBuild(BuildHelperData data)
        {
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();

            data.autonomousBuild.progress = Progress.PreSecondaryBuild;
            data.SaveToJSON();
            statusWindow.currentPlatform = data.autonomousBuild.secondaryTarget;
            statusWindow.currentState = AutonomousBuildState.building;
            BuildHelperBuilder.PublishNewBuild();
        }
    }
}

#endif