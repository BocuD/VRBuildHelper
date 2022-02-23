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
using System.Threading.Tasks;
using BocuD.VRChatApiTools;
using UnityEditor;
using UnityEditor.Build;
using static BocuD.VRChatApiTools.VRChatApiTools;
using static BocuD.BuildHelper.AutonomousBuilder.AutonomousBuildInformation;

namespace BocuD.BuildHelper
{
    public static class AutonomousBuilder
    {
        [Serializable]
        public class AutonomousBuildInformation
        {
            public bool activeBuild;
            public Platform initialTarget;
            public Platform secondaryTarget;
            public Progress progress;
            public WorldInfo worldInfo;

            public AutonomousBuildInformation()
            {
                activeBuild = true;
                worldInfo = new WorldInfo();
            }

            public enum Progress
            {
                PreInitialBuild,
                PostInitialBuild,
                PreSecondaryBuild,
                PostSecondaryBuild,
                Finished
            }
        }

        public static AutonomousBuildInformation buildInfo;
        public static AutonomousBuilderStatus statusWindow;
        
        private static async void SwitchPlatform(Platform newTarget)
        {
            statusWindow.currentPlatform = newTarget;
            statusWindow.currentState = AutonomousBuildState.switchingPlatform;
            await Task.Delay(500);

            switch (newTarget)
            {
                case Platform.PC:
                    Logger.Log("Switching platform to Windows");

                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                    break;

                case Platform.mobile:
                    Logger.Log("Switching platform to Android");

                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
                    break;
            }
        }

        public static void BuildTargetUpdate(BuildTarget newTarget)
        {
            statusWindow = AutonomousBuilderStatus.ShowStatus();
            buildInfo = statusWindow.buildInfo;
            Logger.Log(buildInfo.activeBuild ? $"active build: {buildInfo.progress}" : $"no active build: {buildInfo.progress}");
            
            if (!buildInfo.activeBuild) return;
            
            switch (buildInfo.progress)
            {
                case Progress.PostInitialBuild when buildInfo.secondaryTarget == CurrentPlatform():
                    buildInfo.progress = Progress.PreSecondaryBuild;
                    ContinueAutonomousPublish();
                    break;
                
                case Progress.PostSecondaryBuild when buildInfo.initialTarget == CurrentPlatform():
                    FinishAutonomousPublish();
                    break;
                default:
                    //todo error out here
                    break;
            }
        }
        
        public static async void StartAutonomousPublish()
        {
            statusWindow = AutonomousBuilderStatus.ShowStatus();
            statusWindow.buildInfo = buildInfo;
            Logger.Log("Initiating autonomous builder...");

            await BuildAndPublish(buildInfo.initialTarget);

            buildInfo.progress = Progress.PostInitialBuild;

            SwitchPlatform(buildInfo.secondaryTarget);
        }
        
        private static async void ContinueAutonomousPublish()
        {
            await BuildAndPublish(buildInfo.secondaryTarget);

            buildInfo.progress = Progress.PostSecondaryBuild;
            
            SwitchPlatform(buildInfo.initialTarget);
        }

        private static void FinishAutonomousPublish()
        {
            statusWindow.currentState = AutonomousBuildState.finished;
            buildInfo.activeBuild = false;
        }

        private static async Task BuildAndPublish(Platform platform)
        {
            if (!await TryAutoLoginAsync())
            {
                statusWindow.currentState = AutonomousBuildState.failed;
                statusWindow.failReason = "Login failed";
                return;
            }
            
            statusWindow.currentPlatform = platform;
            statusWindow.currentState = AutonomousBuildState.building;

            await Task.Delay(100);
            
            string buildPath = BuildHelperBuilder.ExportAssetBundle();
            
            if (!await TryAutoLoginAsync())
            {
                statusWindow.currentState = AutonomousBuildState.failed;
                statusWindow.failReason = "Login failed";
                return;
            }
            
            statusWindow.currentState = AutonomousBuildState.uploading;
            
            VRChatApiUploaderAsync uploader = new VRChatApiUploaderAsync();
            uploader.UseStatusWindow();

            await uploader.UploadWorld(buildPath, "", buildInfo.worldInfo);
        }
    }

    public class AutonomousBuilderTargetWatcher : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;
    
        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            AutonomousBuilder.BuildTargetUpdate(newTarget);
        }
    }
}