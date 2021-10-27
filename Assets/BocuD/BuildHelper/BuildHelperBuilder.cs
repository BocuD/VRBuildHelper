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
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using VRC.Core;
using VRC.SDK3.Editor.Builder;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace BocuD.BuildHelper
{
    public static class BuildHelperBuilder
    {
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
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                AssetExporter.CleanupUnityPackageExport(); // force unity package rebuild on next publish
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
                Debug.LogWarning($"Cannot find last built scene, please Rebuild.");
            }
        }

        public static void ReloadNewBuild()
        {
            bool buildTestBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            if (!buildTestBlocked)
            {
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                AssetExporter.CleanupUnityPackageExport(); // force unity package rebuild on next publish
                VRC_SdkBuilder.PreBuildBehaviourPackaging();

                VRC_SdkBuilder.ExportSceneResource();
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
                Debug.LogError("You need to be logged in to publish a world");
            }
        }
    
        public static void PublishNewBuild()
        {
            bool buildBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            if (!buildBlocked)
            {
                if (APIUser.CurrentUser.canPublishWorlds)
                {
                    //EnvConfig.ConfigurePlayerSettings();
                    EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders", false);

                    VRC_SdkBuilder.shouldBuildUnityPackage = false; //VRCSdkControlPanel.FutureProofPublishEnabled;
                    VRC_SdkBuilder.PreBuildBehaviourPackaging();
                    VRC_SdkBuilder.ExportAndUploadSceneBlueprint();
                }
                else
                {
                    Debug.LogError("You need to be logged in to publish a world");
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
            //string actualLastBuild = EditorPrefs.GetString("lastVRCPath");
            
            EditorPrefs.SetString("lastVRCPath", deploymentUnit.filePath);
            EditorPrefs.SetString("currentBuildingAssetBundlePath", UnityWebRequest.UnEscapeURL(deploymentUnit.filePath));
            AssetExporter.CleanupUnityPackageExport();
            VRCWorldAssetExporter.LaunchSceneBlueprintUploader();
            
            //EditorPrefs.SetString("lastVRCPath", actualLastBuild);
        }
    }
}

#endif