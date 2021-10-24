#if UNITY_EDITOR

using System;
using System.IO;
using BocuD.BuildHelper;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;
using static BocuD.BuildHelper.AutonomousBuildInformation;

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
    }
}

#endif