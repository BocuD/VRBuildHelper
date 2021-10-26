#if UNITY_EDITOR

using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace BocuD.BuildHelper
{
    
    /*TODO: use [PostProcessBuildAttribute(0)] public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {}
     in combination with IVRCSDKBuildRequestedCallback instead of only relying on one; using PostProcessBuildAttribute lets us verify
     if the build was actually succesful
    */
    public class BuildHelperVRCCallback : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 10;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar) return true;

            BuildHelperData buildHelperData;
            BuildData buildData;
            
            if (UnityEngine.Object.FindObjectOfType<BuildHelperData>())
            {
                buildHelperData = UnityEngine.Object.FindObjectOfType<BuildHelperData>();
                buildData = buildHelperData.currentBranch.buildData;
            }
            else
            {
                return true;
            }

            buildHelperData.PrepareExcludedGameObjects();
            buildHelperData.overrideContainers[buildHelperData.currentBranchIndex].ApplyStateChanges();
            
#if UNITY_ANDROID //Platform is Android
            buildData.androidBuildTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);

            //we are building for android. if the *last* pc version is newer then this android version, we need to ask the user if the android version is going to be equivalent or newer
            if (buildData.pcBuildVersion > buildData.androidBuildVersion && buildData.pcBuildVersion != -1)
            {
                //we have never built for this platform before, so assume its a new build
                if (buildData.androidBuildVersion == -1)
                {
                    buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                }
                else
                {
                    //if we are doing an autonomous build for both platforms we know the build numbers should match
                    if (buildHelperData.autonomousBuild.activeBuild && !buildHelperData.autonomousBuild.singleTarget)
                    {
                        //this is the first build of the autonomousbuild process, make sure it gets a new number
                        if(buildHelperData.autonomousBuild.progress == AutonomousBuildInformation.Progress.PreInitialBuild && buildHelperData.autonomousBuild.initialTarget == Platform.mobile)
                            buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                        //this is the second build, we want the build number to match that of the first
                        else if(buildHelperData.autonomousBuild.progress == AutonomousBuildInformation.Progress.PreSecondaryBuild && buildHelperData.autonomousBuild.secondaryTarget == Platform.mobile)
                            buildData.androidBuildVersion = buildData.pcBuildVersion;
                    }
                    else
                    {
                        //if the time between the two builds is short, that probably means the user is uploading for both at the same time right now
                        DateTime pcBuildTime = DateTime.Parse(buildData.pcBuildTime, CultureInfo.InvariantCulture);
                        if ((DateTime.Now - pcBuildTime).TotalMinutes > 5)
                        {
                            if (buildHelperData.autonomousBuild.activeBuild)
                            {
                                buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                            }
                            else
                            {
                                bool newBuild = EditorUtility.DisplayDialog("Build Helper",
                                    $"Your last build for PC (build {buildData.pcBuildVersion}, {buildData.pcBuildTime}) is significantly older than the Android build you are about to do. Should Build Helper mark your current Android build as a newer build, or as equivalent to your last PC build?",
                                    "New build", "Equivalent build");

                                if (newBuild)
                                {
                                    buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                                }
                                else
                                {
                                    buildData.androidBuildVersion = buildData.pcBuildVersion;
                                }
                            }
                        }
                        else //in an autonomous build we don't want to ask the user about this, because it should be unattended, so assume a new version
                        {
                            buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                        }
                    }
                }
            }

            else buildData.androidBuildVersion++;
#else //Platform is PC
            buildData.pcBuildTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);

            //if the version we are about to build is *older* than the existing build for the other platform, 
            if (buildData.androidBuildVersion > buildData.pcBuildVersion && buildData.androidBuildVersion != -1)
            {
                //we have never built for this platform before, so assume its a new build
                if (buildData.pcBuildVersion == -1)
                {
                    buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                }
                else
                {
                    //if we are doing an autonomous build for both platforms we know the build numbers should match
                    if (buildHelperData.autonomousBuild.activeBuild && !buildHelperData.autonomousBuild.singleTarget)
                    {
                        //this is the first build of the autonomousbuild process, make sure it gets a new number
                        if(buildHelperData.autonomousBuild.progress == AutonomousBuildInformation.Progress.PreInitialBuild && buildHelperData.autonomousBuild.initialTarget == Platform.PC)
                            buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                        //this is the second build, we want the build number to match that of the first
                        else if(buildHelperData.autonomousBuild.progress == AutonomousBuildInformation.Progress.PreSecondaryBuild && buildHelperData.autonomousBuild.secondaryTarget == Platform.PC)
                            buildData.pcBuildVersion = buildData.androidBuildVersion;
                    }
                    else
                    {
                        DateTime androidBuildTime = DateTime.Parse(buildData.androidBuildTime, CultureInfo.InvariantCulture);
                        if ((DateTime.Now - androidBuildTime).TotalMinutes > 5 &&
                            !buildHelperData.autonomousBuild.activeBuild)
                        {
                            if (buildHelperData.autonomousBuild.activeBuild)
                            {
                                buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                            }
                            else
                            {
                                bool newBuild = EditorUtility.DisplayDialog("Build Helper",
                                    $"Your last build for Android (build {buildData.androidBuildVersion}, {buildData.androidBuildTime}) is significantly older than the PC build you are about to do. Should Build Helper mark your current PC build as a newer build, or as equivalent to your last Android build?",
                                    "New build",
                                    "Equivalent build");

                                if (newBuild)
                                {
                                    buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                                }
                                else
                                {
                                    buildData.pcBuildVersion = buildData.androidBuildVersion;
                                }
                            }
                        }
                        else //in an autonomous build we don't want to ask the user about this, because it should be unattended, so assume a new version
                        {
                            buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                        }
                    }
                }
            }

            else buildData.pcBuildVersion++;
#endif
            buildHelperData.lastBuiltBranch = buildHelperData.currentBranchIndex;
            buildHelperData.SaveToJSON();

            return true;
        }
    }
}
#endif