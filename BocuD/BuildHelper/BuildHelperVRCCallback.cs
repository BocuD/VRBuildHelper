#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

public class BuildHelperVRCCallback : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 10;

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Avatar) return true;

        BuildHelperData buildHelperData = UnityEngine.Object.FindObjectOfType<BuildHelperData>();
        Branch branch = buildHelperData.branches[buildHelperData.currentBranch];
        BuildData buildData = branch.buildData;

        if (buildHelperData == null)
        {
            Debug.Log($"Build Helper was not found in this scene");
            return true;
        }

        buildHelperData.PrepareExcludedGameObjects();
        buildHelperData.overrideContainers[buildHelperData.currentBranch].ApplyStateChanges();

#if UNITY_ANDROID
        //HACK this is retarded like wth
        buildData.androidBuildTime = $"{DateTime.Now}";

        //we are building for android. if the *last* pc version is newer then this android version, we need to ask the user if the android version is going to be equivalent or newer
        if (buildData.pcBuildVersion > buildData.androidBuildVersion && buildData.pcBuildVersion != -1)
        {
            //if we have never uploaded for android before we should just make these versions match, they will probably be equivalent
            if (buildData.androidBuildVersion == -1)
            {
                buildData.androidBuildVersion = buildData.pcBuildVersion;
            }
            else
            {
                //if the time between the two builds is short, that probably means the user is uploading for both at the same time right now
                DateTime pcBuildTime = DateTime.Parse(buildData.pcBuildTime);
                if ((DateTime.Now - pcBuildTime).TotalMinutes > 5)
                {
                    bool newBuild = EditorUtility.DisplayDialog("Build Helper",
                        $"Your last build for PC (build {buildData.pcBuildVersion}, {buildData.pcBuildTime}) is significantly older than the Android build you are about to do. Should Build Helper mark your current Android build as a newer build, or as equivalent to your last PC build?",
                        "New build", "Equivalent build");

                    if (newBuild)
                    {
                        buildData.androidBuildVersion = buildData.pcBuildVersion + 2;
                    }
                    else
                    {
                        buildData.androidBuildVersion = buildData.pcBuildVersion;
                    }
                }
                else
                {
                    buildData.androidBuildVersion = buildData.pcBuildVersion;
                }
            }
        }

        else buildData.androidBuildVersion++;
#else
        //HACK this is retarded like wth
        buildData.pcBuildTime = $"{DateTime.Now}";

        if (buildData.androidBuildVersion > buildData.pcBuildVersion && buildData.androidBuildVersion != -1)
        {
            if (buildData.pcBuildVersion == -1)
            {
                buildData.pcBuildVersion = buildData.androidBuildVersion;
            }
            else
            {
                DateTime androidBuildTime = DateTime.Parse(buildData.androidBuildTime);
                if ((DateTime.Now - androidBuildTime).TotalMinutes > 5)
                {
                    bool newBuild = EditorUtility.DisplayDialog("Build Helper",
                        $"Your last build for Android (build {buildData.androidBuildVersion}, {buildData.androidBuildTime}) is significantly older than the PC build you are about to do. Should Build Helper mark your current PC build as a newer build, or as equivalent to your last Android build?",
                        "New build",
                        "Equivalent build");

                    if (newBuild)
                    {
                        buildData.pcBuildVersion = buildData.androidBuildVersion + 2;
                    }
                    else
                    {
                        buildData.pcBuildVersion = buildData.androidBuildVersion;
                    }
                }
                else
                {
                    buildData.pcBuildVersion = buildData.androidBuildVersion;
                }
            }
        }

        else buildData.pcBuildVersion++;
#endif
        buildHelperData.SaveToJSON();

        return true;
    }
}
#endif