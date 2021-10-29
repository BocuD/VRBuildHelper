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
using UnityEditor;
using UnityEngine;

namespace BocuD.BuildHelper
{
    public class AutonomousBuilderStatus : EditorWindow
    {
        private AutonomousBuildState _currentState;
        private string log;
        public string failReason;
        private Vector2 logScroll;
        private BuildHelperData buildHelperData;
        public bool abort = false;

        public AutonomousBuildState currentState
        {
            set
            {
                AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus));
                _currentState = value;
                AddLog(GetStateString(_currentState));
                window.Repaint();
            }
            get => _currentState;
        }

        public Platform currentPlatform;

        public void AddLog(string contents)
        {
            log += $"\n[{DateTime.Now:HH:mm:ss}]: {contents}";
        }

        private void OnEnable()
        {
            Application.logMessageReceived += Log;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= Log;
        }

        public static AutonomousBuilderStatus ShowStatus()
        {
            AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus), true);

            window.titleContent = new GUIContent("Autonomous Builder");
            window.maxSize = new Vector2(400, 200);
            window.minSize = window.maxSize;
            window.autoRepaintOnSceneChange = true;

            // if (!window.logWatcher)
            // {
            //     Application.logMessageReceived += window.Log;
            //     window.logWatcher = true;
            // }

            window.Show();
            window.Repaint();

            return window;
        }

        private string GetStateString(AutonomousBuildState state)
        {
            switch (state)
            {
                case AutonomousBuildState.building:
                    return $"Building for {currentPlatform}";
                case AutonomousBuildState.waitingForApi:
                    return "Waiting for VRChat Api..";
                case AutonomousBuildState.switchingPlatform:
                    return $"Switching platform to {currentPlatform}";
                case AutonomousBuildState.uploading:
                    return $"Uploading for {currentPlatform}";
                case AutonomousBuildState.finished:
                    return "Finished!";
                case AutonomousBuildState.aborting:
                    return "Aborting...";
                case AutonomousBuildState.aborted:
                    return "Aborted";
                case AutonomousBuildState.failed:
                    return "Failed";
            }

            return "Unknown status";
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Autonomous Builder Status:");
            if (_currentState == AutonomousBuildState.aborting)
                if (GUILayout.Button("Force close"))
                {
                    if (EditorUtility.DisplayDialog("Autonomous Builder",
                        "Are you sure you want to close the Autonomous Builder? Only do this if its stuck, it might continue building anyways if its not 'hanging'.",
                        "Yes", "No"))
                    {
                        currentState = AutonomousBuildState.finished;
                        DestroyImmediate(this);
                    }
                }

            EditorGUILayout.EndHorizontal();

            GUIStyle stateLabel = new GUIStyle(GUI.skin.label) {fontSize = 24, wordWrap = true};
            GUIContent icon;
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label) {fixedHeight = 30};

            switch (_currentState)
            {
                case AutonomousBuildState.building:
                    icon = EditorGUIUtility.IconContent(currentPlatform == Platform.PC
                        ? "BuildSettings.Metro On"
                        : "BuildSettings.Android On");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.waitingForApi:
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    break;
                case AutonomousBuildState.switchingPlatform:
                    icon = EditorGUIUtility.IconContent("RotateTool On@2x");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.uploading:
                    icon = EditorGUIUtility.IconContent("UpArrow");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.finished:
                    icon = EditorGUIUtility.IconContent("d_Toggle Icon");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.aborting:
                case AutonomousBuildState.aborted:
                    icon = EditorGUIUtility.IconContent("Error@2x");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.failed:
                    icon = EditorGUIUtility.IconContent("Error@2x");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState) + ": " + failReason, stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
            }

            GUIStyle logStyle = new GUIStyle(EditorStyles.label)
                {wordWrap = true, richText = true, alignment = TextAnchor.LowerLeft, fixedWidth = position.width - 33};
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.FlexibleSpace();
            logScroll = EditorGUILayout.BeginScrollView(logScroll);
            EditorGUILayout.LabelField(log, logStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void Log(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error)
            {
                AddLog($"<color=red>{logString}</color>");
            }

            if (type == LogType.Error && logString.Contains("Building AssetBundles was canceled.") &&
                _currentState != AutonomousBuildState.failed)
            {
                failReason = "Build was cancelled";
                currentState = AutonomousBuildState.failed;
            }

            if (type == LogType.Error && logString.Contains("Error building Player") &&
                _currentState != AutonomousBuildState.failed)
            {
                failReason = "Error building Player";
                currentState = AutonomousBuildState.failed;
            }

            if (type == LogType.Error && logString.Contains("Export Exception") &&
                _currentState != AutonomousBuildState.failed)
            {
                failReason = "Export Exception";
                currentState = AutonomousBuildState.failed;
            }

            logScroll.y += 1000;
        }

        private void OnDestroy()
        {
            //Application.logMessageReceived -= Log;

            switch (_currentState)
            {
                case AutonomousBuildState.aborting:
                    //spawn new window if we still need to process the abort
                    if (FindObjectOfType<BuildHelperData>())
                    {
                        if (FindObjectOfType<BuildHelperData>().autonomousBuild.activeBuild)
                        {
                            AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                            status.ShowUtility();
                            status.titleContent = new GUIContent("Autonomous Builder");
                            status.log = log;
                            status.currentPlatform = currentPlatform;
                            status._currentState = _currentState;
                            status.Repaint();
                            return;
                        }
                    }

                    //spawn new window if we are still in the build process
                    if (BuildPipeline.isBuildingPlayer)
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status._currentState = _currentState;
                        status.Repaint();
                        return;
                    }

                    break;

                case AutonomousBuildState.failed:
                case AutonomousBuildState.finished:
                case AutonomousBuildState.aborted:
                    return;

                default:
                    if (EditorUtility.DisplayDialog("Autonomous Builder",
                        "Are you sure you want to cancel your autonomous build?",
                        "Continue build", "Abort build"))
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status._currentState = _currentState;
                        status.Repaint();
                    }
                    else
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status.currentState = AutonomousBuildState.aborting;
                        status.abort = true;
                    }

                    break;
            }
        }
    }

    public enum AutonomousBuildState
    {
        building,
        waitingForApi,
        switchingPlatform,
        uploading,
        finished,
        aborting,
        aborted,
        failed
    }
}

#endif