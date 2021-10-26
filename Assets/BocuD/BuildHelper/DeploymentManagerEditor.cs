/*
 Original Copyright Notice:

 MIT License

 Copyright (c) 2020 Haï~ (@vr_hai github.com/hai-vr)

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

/*
 Rewritten for use in VR Build Helper by BocuD on 26-10-2021
 Copyright (c) 2021 BocuD (github.com/BocuD)
*/

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BocuD.BuildHelper;
using UnityEditor;
using UnityEngine;
using VRC.Core;

public class DeploymentManagerEditor : EditorWindow
{
    public Branch branch;
    private Vector2 deploymentScrollArea;
    
    public static void OpenDeploymentManager(Branch b)
    {
        DeploymentManagerEditor window = (DeploymentManagerEditor) GetWindow(typeof(DeploymentManagerEditor), true);
        window.branch = b;
        
        window.titleContent = new GUIContent($"Deployment Manager for {b.name}");
        window.minSize = new Vector2(400, 200);
        window.autoRepaintOnSceneChange = true;
        
        DeploymentManager.RefreshDeploymentData(b);
            
        window.Show();
    }
    
    private void OnGUI()
    {
        if (branch.deploymentData.deploymentPath == "")
        {
            if (GUILayout.Button("Set deployment path..."))
            {
                string selectedFolder = EditorUtility.OpenFolderPanel("Set deployment folder location...",
                    Application.dataPath, "Deployments");
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    if (selectedFolder.StartsWith(Application.dataPath))
                    {
                        branch.deploymentData.deploymentPath = selectedFolder.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        Debug.LogError("Please choose a location within the Assets folder");
                    }
                }
            }

            return;
        }

        if (GUILayout.Button("Force Refresh"))
        {
            DeploymentManager.RefreshDeploymentData(branch);
        }
        
        deploymentScrollArea = EditorGUILayout.BeginScrollView(deploymentScrollArea);

        if (branch.deploymentData.units.Length < 1)
        {
            EditorGUILayout.HelpBox("No builds have been saved yet. To save a build for this branch, upload your world.", MessageType.Info);
        }

        foreach (DeploymentUnit deploymentUnit in branch.deploymentData.units)
        {
            GUILayout.BeginVertical("GroupBox");

            EditorGUILayout.BeginHorizontal();
            GUIContent icon = EditorGUIUtility.IconContent(deploymentUnit.platform == Platform.PC
                ? "BuildSettings.Metro On"
                : "BuildSettings.Android On");
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));

            EditorGUI.BeginDisabledGroup(true);
            Rect fieldRect = EditorGUILayout.GetControlRect();
            GUI.TextField(fieldRect, deploymentUnit.fileName);
            EditorGUI.EndDisabledGroup();

            GUIStyle selectButtonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 60};
            if (GUILayout.Button("Select", selectButtonStyle))
            {
                Selection.activeObject =
                    AssetDatabase.LoadMainAssetAtPath($"Assets/{branch.deploymentData.deploymentPath}/" +
                                                      deploymentUnit.fileName);
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Build " + deploymentUnit.buildNumber);
            GUILayout.Label("Build date: " + deploymentUnit.buildDate);
            GUILayout.Label("Modified on: " + deploymentUnit.modifiedDate);
            GUILayout.Label("Filesize: " + BytesToString(deploymentUnit.fileSize));
            GUILayout.Label("Git hash: " + deploymentUnit.gitHash);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Delete build"))
            {
                if (EditorUtility.DisplayDialog("Build Helper",
                    $"Are you sure you want to delete '{deploymentUnit.fileName}'? This can not be undone.", "Yes",
                    "No"))
                {
                    File.Delete(deploymentUnit.filePath);
                    File.Delete(deploymentUnit.filePath + ".meta");
                    AssetDatabase.Refresh();
                    DeploymentManager.RefreshDeploymentData(branch);
                    return;
                }
            }
            
            EditorGUI.BeginDisabledGroup(deploymentUnit.platform == Platform.mobile);
            if (GUILayout.Button("Test locally in VRChat"))
            {
                BuildHelperBuilder.TestExistingBuild(deploymentUnit);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(!APIUser.IsLoggedIn);
            if (GUILayout.Button("Publish this build"))
            {
                BuildHelperBuilder.PublishExistingBuild(deploymentUnit);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
        
        if (!APIUser.IsLoggedIn) {
            EditorGUILayout.HelpBox("You need to be logged in to publish. Try opening and closing the VRChat SDK menu.", MessageType.Error);
            if (GUILayout.Button("Open VRCSDK Control Panel"))
            {
                EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
            }
            return;
        }
    }
    
    private static string BytesToString(long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
        if (byteCount == 0)
            return "0" + suf[0];
        long bytes = Math.Abs(byteCount);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 2);
        return (Math.Sign(byteCount) * num).ToString(CultureInfo.InvariantCulture) + suf[place];
    }
}

public static class DeploymentManager
{
    public static void RefreshDeploymentData(Branch branch)
    {
        string[] files = Directory.GetFiles(Application.dataPath + branch.deploymentData.deploymentPath, "*.vrcw", SearchOption.TopDirectoryOnly);

        branch.deploymentData.units = files
            .Select(filePath =>
            {
                string fileName = Path.GetFileName(filePath);
                char[] splitChars = {'[', ']'};
                string dateString = fileName.Split(splitChars)[1];
                DateTime buildDate = DateTime.ParseExact(dateString, "yyyy'-'MM'-'dd HH'-'mm'-'ss",
                    CultureInfo.InvariantCulture);
                DateTime lastWriteTime = File.GetLastWriteTime(filePath);

                string gitHash = ResolveGitHash(fileName);
                Platform platform = fileName.Contains("_mobile_") ? Platform.mobile : Platform.PC;
                bool autoUploader = fileName.Contains("auto_");
                string buildNumberString = fileName.Substring(fileName.IndexOf("build") + 5);
                buildNumberString = buildNumberString.Substring(0, buildNumberString.IndexOf('_'));
                int buildNumber = int.Parse(buildNumberString);
                long fileSize = new FileInfo(filePath).Length;

                return new DeploymentUnit
                {
                    autoUploader = autoUploader,
                    fileName = fileName,
                    modifiedDate = lastWriteTime,
                    gitHash = gitHash,
                    platform = platform,
                    buildDate = buildDate,
                    modifiedFileTime = lastWriteTime.ToFileTime(),
                    filePath = filePath,
                    buildNumber = buildNumber,
                    fileSize = fileSize
                };
            })
            .OrderByDescending(unit => unit.modifiedFileTime)
            .Where(unit => unit.fileName.Contains(branch.name))
            .ToArray();
    }
    
    //I'm not sure why this is here, but welp, there ya go lol
    public static string ResolveGitHash(string fileName)
    {
        // This should have been a regex capture
        var lastUnderscoreOrMinusOne = fileName.LastIndexOf("_", StringComparison.Ordinal);
        if (lastUnderscoreOrMinusOne == -1) return "";

        var trailing = fileName.Substring(lastUnderscoreOrMinusOne + 1);
        if (trailing.Length < 8) return "";

        var hash = trailing.Substring(0, 8);
        if (!Regex.IsMatch(hash, "[0-9a-f]{8}")) return "";

        return hash;
    }
}

#endif