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
using System.Collections.Generic;
using System.Globalization;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;

namespace BocuD.BuildHelper.Editor
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

            BuildHelperData buildHelperData = BuildHelperData.GetDataBehaviour();
            BuildData buildData;
            
            if (buildHelperData)
            {
                buildData = buildHelperData.dataObject.CurrentBranch.buildData;
            }
            else
            {
                return true;
            }

            buildHelperData.PrepareExcludedGameObjects();
            buildHelperData.overrideContainers[buildHelperData.dataObject.currentBranch].ApplyStateChanges();
            
            AutonomousBuildInformation buildInformation = buildHelperData.dataObject.autonomousBuild;

            //todo: make this less of a mess
            
            //Platform is mobile
            if (BuildHelperRuntime.CurrentPlatform() == Platform.mobile)
            {
                buildData.androidBuildTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);

                //if we are doing an autonomous build for both platforms we know the build numbers should match
                if (buildInformation.activeBuild && !buildInformation.singleTarget)
                {
                    //this is the first build of the autonomousbuild process, make sure it gets a new number
                    if (buildInformation.progress ==
                        AutonomousBuildInformation.Progress.PreInitialBuild &&
                        buildInformation.initialTarget == Platform.mobile)
                        buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                    //this is the second build, we want the build number to match that of the first
                    else if (buildInformation.progress ==
                             AutonomousBuildInformation.Progress.PreSecondaryBuild &&
                             buildInformation.secondaryTarget == Platform.mobile)
                        buildData.androidBuildVersion = buildData.pcBuildVersion;
                }
                else
                {
                    //we are building for android. if the *last* pc version is newer then this android version, we need to figure out if the android version is going to be equivalent or newer
                    if (buildData.pcBuildVersion > buildData.androidBuildVersion && buildData.pcBuildVersion != -1)
                    {
                        //we have never built for this platform before, so assume its a new build
                        if (buildData.androidBuildVersion == -1)
                        {
                            buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                        }
                        else
                        {
                            //if the time between the two builds is short, that probably means the user is uploading for both at the same time right now
                            DateTime pcBuildTime = DateTime.Parse(buildData.pcBuildTime, CultureInfo.InvariantCulture);
                            if ((DateTime.Now - pcBuildTime).TotalMinutes > 5)
                            {
                                if (buildInformation.activeBuild)
                                {
                                    buildData.androidBuildVersion = buildData.pcBuildVersion + 1;
                                }
                                else
                                {
                                    bool newBuild = EditorUtility.DisplayDialog("Build Helper",
                                        $"Your last build for PC (build {buildData.pcBuildVersion}, {buildData.pcBuildTime}) is significantly older than the Android build you are about to do, but your last Android build is even older. Should Build Helper mark your current Android build as a newer build, or as equivalent to your last PC build? Marking them equivalent will make the version numbers match.",
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
                    else buildData.androidBuildVersion++;
                }
            }
            
            //Platform is PC
            else
            {
                buildData.pcBuildTime = DateTime.Now.ToString(CultureInfo.InvariantCulture);

                //if we are doing an autonomous build for both platforms we know the build numbers should match
                if (buildInformation.activeBuild && !buildInformation.singleTarget)
                {
                    //this is the first build of the autonomousbuild process, make sure it gets a new number
                    if (buildInformation.progress ==
                        AutonomousBuildInformation.Progress.PreInitialBuild &&
                        buildInformation.initialTarget == Platform.PC)
                        buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                    //this is the second build, we want the build number to match that of the first
                    else if (buildInformation.progress ==
                             AutonomousBuildInformation.Progress.PreSecondaryBuild &&
                             buildInformation.secondaryTarget == Platform.PC)
                        buildData.pcBuildVersion = buildData.androidBuildVersion;
                }
                else
                {
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
                            DateTime androidBuildTime =
                                DateTime.Parse(buildData.androidBuildTime, CultureInfo.InvariantCulture);
                            if ((DateTime.Now - androidBuildTime).TotalMinutes > 5 &&
                                !buildInformation.activeBuild)
                            {
                                if (buildInformation.activeBuild)
                                {
                                    buildData.pcBuildVersion = buildData.androidBuildVersion + 1;
                                }
                                else
                                {
                                    bool newBuild = EditorUtility.DisplayDialog("Build Helper",
                                        $"Your last build for Android (build {buildData.androidBuildVersion}, {buildData.androidBuildTime}) is significantly older than the PC build you are about to do, but your last PC build is even older. Should Build Helper mark your current PC build as a newer build, or as equivalent to your last Android build? Marking them equivalent will make the version numbers match.",
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
                    else buildData.pcBuildVersion++;
                }
            }

            buildHelperData.dataObject.lastBuiltBranch = buildHelperData.dataObject.CurrentBranch.branchID;
            buildHelperData.dataObject.lastBuiltPlatform = (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                ? Platform.mobile
                : Platform.PC;
            buildHelperData.SaveToJSON();

            if (buildHelperData.dataObject.CurrentBranch.hasUdonLink && buildHelperData.linkedBehaviourGameObject != null)
            {
                BuildHelperUdon linkedUdon = buildHelperData.linkedBehaviourGameObject.GetUdonSharpComponent<BuildHelperUdon>();
                linkedUdon.UpdateProxy();
                linkedUdon.branchName = buildHelperData.dataObject.CurrentBranch.name;
                
                if(BuildHelperRuntime.CurrentPlatform() == Platform.mobile)
                    linkedUdon.buildNumber = buildData.androidBuildVersion;

#if UNITY_ANDROID
#else
                linkedUdon.buildNumber = buildData.pcBuildVersion;
#endif
                linkedUdon.buildDate = DateTime.Now;
                linkedUdon.ApplyProxyModifications();
            } else if (!buildHelperData.dataObject.CurrentBranch.hasUdonLink)
            {
                Scene currentScene = SceneManager.GetActiveScene();
                List<BuildHelperUdon> foundBehaviours = new List<BuildHelperUdon>();

                foreach (GameObject obj in currentScene.GetRootGameObjects())
                {
                    BuildHelperUdon[] behaviours = obj.GetUdonSharpComponentsInChildren<BuildHelperUdon>();
                    foundBehaviours.AddRange(behaviours);
                }
                    
                if (foundBehaviours.Count > 0)
                {
                    foreach (BuildHelperUdon behaviour in foundBehaviours)
                    {
                        behaviour.enabled = false;
                        behaviour.gameObject.SetActive(false);
                    }
                }
            }

            return true;
        }
    }
    
    //todo: add option that verifies builds before updating build numbers in JSON file
    public static class BuildHelperBuildPostProcessor {
        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {
            Logger.Log("Detected successful build");
            Debug.Log( pathToBuiltProject );
        }
    }
}