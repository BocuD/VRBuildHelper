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

using System.IO;
using System.Threading.Tasks;
using BocuD.VRChatApiTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using VRC.Core;
using static BocuD.BuildHelper.AutonomousBuildInformation;

namespace BocuD.BuildHelper
{
    public static class AutonomousBuilder
    {
        private static async void SwitchPlatform(Platform newTarget)
        {
            AutonomousBuilderStatus status = AutonomousBuilderStatus.ShowStatus();
            status.currentPlatform = newTarget;
            status.currentState = AutonomousBuildState.switchingPlatform;
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
        
        public static void FinishedBuild()
        {
            string lastVRCPath = EditorPrefs.GetString("lastVRCPath");
            Logger.Log($"Detected successful build at {lastVRCPath}");

            BuildHelperData data = BuildHelperData.GetDataBehaviour();
            if (!data) return;

            AutonomousBuildInformation buildInfo = data.dataObject.autonomousBuild;
            if (!buildInfo.activeBuild) return;
            
            switch(buildInfo.progress)
            {
                case Progress.PreInitialBuild:
                    buildInfo.progress = Progress.PostInitialBuild;
                    buildInfo.initialBuildPath = lastVRCPath;
                    
                    Logger.Log("Preparing for second build...");

                    SwitchPlatform(buildInfo.secondaryTarget);
                    break;
                
                case Progress.PreSecondaryBuild:
                    buildInfo.progress = Progress.PostSecondaryBuild;
                    buildInfo.secondaryBuildPath = lastVRCPath; 
                    Logger.Log("Preparing for upload...");

                    UploadTask(data.dataObject);
                    break;
            }
        }

        public static void BuildTargetUpdate(BuildTarget newTarget)
        {
            BuildHelperData data = BuildHelperData.GetDataBehaviour();
            if (!data) return;

            AutonomousBuildInformation buildInfo = data.dataObject.autonomousBuild;
            if (!buildInfo.activeBuild) return;
            
            switch (buildInfo.progress)
            {
                case Progress.PostInitialBuild:
                    buildInfo.progress = Progress.PreSecondaryBuild;
                    StartNewBuild();
                    break;
            }
        }

        public static async void StartNewBuild()
        {
            AutonomousBuilderStatus status = AutonomousBuilderStatus.ShowStatus();
            status.currentState = AutonomousBuildState.waitingForApi;

            if (await VRChatApiTools.VRChatApiTools.TryAutoLoginAsync())
            {
                status.currentState = AutonomousBuildState.building;
                await Task.Delay(500);
                BuildHelperBuilder.ReloadNewBuild(FinishedBuild);
            }
            else
            {
                status.currentState = AutonomousBuildState.failed;
                status.failReason = "Login failed";
            }
        }

        public static async void UploadTask(BranchStorageObject data)
        {
            AutonomousBuilderStatus status = AutonomousBuilderStatus.ShowStatus();

            VRChatApiTools.VRChatApiTools.ClearCaches();
            await Task.Delay(100);
            if (!await VRChatApiTools.VRChatApiTools.TryAutoLoginAsync()) return;
            
            PipelineManager pipelineManager = VRChatApiTools.VRChatApiTools.FindPipelineManager();
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
                    Logger.Log("Found world record");
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
            
            //Assign a new blueprint ID if this is a new world
            if (string.IsNullOrEmpty(apiWorld.id))
            {
                pipelineManager.AssignId();
                apiWorld.id = pipelineManager.blueprintId;
            }

            status.currentPlatform = data.autonomousBuild.initialTarget;
            status.currentState = AutonomousBuildState.uploading;
            await UploadBuild(data.autonomousBuild.initialBuildPath, data.CurrentBranch, apiWorld, data.autonomousBuild.initialTarget, isUpdate);
            
            status.currentPlatform = data.autonomousBuild.secondaryTarget;
            status.currentState = AutonomousBuildState.uploading;
            await UploadBuild(data.autonomousBuild.secondaryBuildPath, data.CurrentBranch, apiWorld, data.autonomousBuild.secondaryTarget, true);

            status.currentState = AutonomousBuildState.finished;
        }

        private static async Task UploadBuild(string buildPath, Branch targetBranch, ApiWorld apiWorld, Platform platform, bool isUpdate)
        {
            //Prepare asset bundle
            string blueprintId = apiWorld.id;
            int version = Mathf.Max(1, apiWorld.version + 1);

            string uploadVrcPath = VRChatApiUploaderAsync.PrepareVRCPathForS3(buildPath, blueprintId, version, platform, ApiWorld.VERSION);

            VRChatApiUploaderAsync uploader = new VRChatApiUploaderAsync();
            uploader.UseStatusWindow();
            await uploader.UploadWorldData(apiWorld, "", uploadVrcPath, isUpdate, targetBranch);
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