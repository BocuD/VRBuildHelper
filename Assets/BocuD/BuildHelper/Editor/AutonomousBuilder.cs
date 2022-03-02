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
using BocuD.BuildHelper.Editor;
using BocuD.VRChatApiTools;
using UnityEditor;
using UnityEditor.Build;
using static BocuD.VRChatApiTools.VRChatApiTools;
using static BocuD.BuildHelper.AutonomousBuilder.AutonomousBuildData;

namespace BocuD.BuildHelper
{
    public static class AutonomousBuilder
    {
        [Serializable]
        public class AutonomousBuildData
        {
            public bool activeBuild;
            public Platform initialTarget;
            public Platform secondaryTarget;
            public Progress progress;
            public WorldInfo worldInfo;

            public AutonomousBuildData()
            {
                activeBuild = true;
                worldInfo = new WorldInfo();
            }

            public enum Progress
            {
                PreInitialBuild,
                PostInitialBuild,
                PreSecondaryBuild,
                PostSecondaryBuild
            }
        }

        //public static AutonomousBuildInformation buildInfo;
        public static AutonomousBuilderStatus status;

        public static AutonomousBuildData GetAutonomousBuildData()
        {
            return status == null ? null : status.buildInfo;
        }
        
        private static async void SwitchPlatform(Platform newTarget)
        {
            status.currentPlatform = newTarget;
            status.currentState = AutonomousBuildState.switchingPlatform;
            await Task.Delay(500);

            switch (newTarget)
            {
                case Platform.Windows:
                    Logger.Log("Switching platform to Windows");

                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                    break;

                case Platform.Android:
                    Logger.Log("Switching platform to Android");

                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
                    break;
            }
        }

        public static void BuildTargetUpdate()
        {
            status = AutonomousBuilderStatus.ShowStatus();

            if (status.buildInfo == null || !status.buildInfo.activeBuild)
            {
                status.Close();
                return;
            }
            
            switch (status.buildInfo.progress)
            {
                case Progress.PostInitialBuild when status.buildInfo.secondaryTarget == CurrentPlatform():
                    status.buildInfo.progress = Progress.PreSecondaryBuild;
                    ContinueAutonomousPublish();
                    break;
                
                case Progress.PostSecondaryBuild when status.buildInfo.initialTarget == CurrentPlatform():
                    FinishAutonomousPublish();
                    break;
                default:
                    //todo error out here
                    break;
            }
        }
        
        public static async void StartAutonomousPublish(AutonomousBuildData buildInfo)
        {
            status = AutonomousBuilderStatus.ShowStatus();
            status.BindLogger();
            status.buildInfo = buildInfo;
            Logger.Log("Initiating autonomous builder...");

            await BuildAndPublish(status.buildInfo.initialTarget);

            status.buildInfo.progress = Progress.PostInitialBuild;

            SwitchPlatform(status.buildInfo.secondaryTarget);
        }
        
        private static async void ContinueAutonomousPublish()
        {
            await BuildAndPublish(status.buildInfo.secondaryTarget);

            status.buildInfo.progress = Progress.PostSecondaryBuild;
            
            SwitchPlatform(status.buildInfo.initialTarget);
        }

        private static void FinishAutonomousPublish()
        {
            status.currentState = AutonomousBuildState.finished;
            status.buildInfo.activeBuild = false;
        }

        private static async Task BuildAndPublish(Platform platform)
        {
            try
            {
                if (!await TryAutoLoginAsync())
                {
                    status.currentState = AutonomousBuildState.failed;
                    status.failReason = "Login failed";
                    return;
                }

                status.currentPlatform = platform;
                status.currentState = AutonomousBuildState.building;

                await Task.Delay(100);

                string buildPath = BuildHelperBuilder.ExportAssetBundle();

                if (!await TryAutoLoginAsync())
                {
                    status.currentState = AutonomousBuildState.failed;
                    status.failReason = "Login failed";
                    return;
                }
                
                EditorApplication.LockReloadAssemblies();

                status.currentState = AutonomousBuildState.uploading;

                VRChatApiUploaderAsync uploader = new VRChatApiUploaderAsync();
                uploader.OnProgress = status.UploadProgress;
                uploader.OnError = (header, details) =>
                {
                    status.buildInfo.activeBuild = false;
                    status.UploadError(header, details);
                };
                uploader.Log = contents => status.AddLog($"<b>{contents}</b>");
                uploader.cancelQuery = () => status.abort;

                await uploader.UploadWorld(buildPath, "", status.buildInfo.worldInfo);

                BuildHelperData data = BuildHelperData.GetDataBehaviour();
                if (data != null)
                {
                    await data.OnSuccesfulPublish(status.buildInfo.worldInfo, DateTime.Now);
                    DeploymentManager.TrySaveBuild(data.dataObject.CurrentBranch, buildPath, true);
                }

                status.uploading = false;
            }
            catch (Exception e)
            {
                status.currentState = AutonomousBuildState.failed;
                status.failReason = "Unhandled Exception: " + e.Message;
                VRChatApiTools.Logger.LogError(e.Message);
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }
    }

    public class AutonomousBuilderTargetWatcher : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;
    
        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            AutonomousBuilder.BuildTargetUpdate();
        }
    }
}