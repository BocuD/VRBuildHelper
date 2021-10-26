#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using VRC.Core;
using static BocuD.BuildHelper.AutonomousBuildInformation;
using Object = UnityEngine.Object;

namespace BocuD.BuildHelper
{
    [ExecuteInEditMode]
    public class AutonomousBuilder : MonoBehaviour
    {
        [SerializeField] private BuildHelperData buildHelperData;
        
        private void Awake()
        {
            loginCheckerActive = false;
            if (FindObjectOfType<BuildHelperData>())
            {
                buildHelperData = FindObjectOfType<BuildHelperData>();
            }
        }

        private void Update()
        {
            if (Application.isPlaying) return;
            
            if (buildHelperData == null)
            {
                if (FindObjectOfType<BuildHelperData>())
                {
                    buildHelperData = FindObjectOfType<BuildHelperData>();
                }
                else return;
            }

            if (!buildHelperData.autonomousBuild.activeBuild) return;
            if (buildHelperData.autonomousBuild.progress != Progress.PostPlatformSwitch) return;
            
            if (!APIUser.IsLoggedIn)
            {
                EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
                
                if (!loginCheckerActive)
                {
                    timeOut = 0;
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                    
                    if (statusWindow.abort)
                    {
                        buildHelperData.autonomousBuild.activeBuild = false;
                        buildHelperData.SaveToJSON();
                        statusWindow.currentState = AutonomousBuildState.aborted;
                    }
                    else
                    {
                        EditorApplication.update += LoginStateChecker;
                        statusWindow.currentState = AutonomousBuildState.waitingForApi;
                    }
                }
            }
        }

        [SerializeField]private bool loginCheckerActive;
        private int timeOut;
        private void LoginStateChecker()
        {
            if (APIUser.IsLoggedIn)
            {
                EditorApplication.update -= LoginStateChecker;
                loginCheckerActive = false;
                
                AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                
                if (statusWindow.abort)
                {
                    buildHelperData.autonomousBuild.activeBuild = false;
                    buildHelperData.SaveToJSON();
                    statusWindow.currentState = AutonomousBuildState.aborted;
                }
                else
                {
                    buildHelperData.autonomousBuild.progress = Progress.PreSecondaryBuild;
                    buildHelperData.SaveToJSON();
                    statusWindow.currentPlatform = buildHelperData.autonomousBuild.secondaryTarget;
                    statusWindow.currentState = AutonomousBuildState.building;
                    BuildHelperBuilder.PublishNewBuild();
                }
            }

            timeOut++;
            if (timeOut > 5000)
            {
                EditorApplication.update -= LoginStateChecker;
                loginCheckerActive = false;
                
                //reset build state
                buildHelperData.autonomousBuild.activeBuild = false;
                buildHelperData.SaveToJSON();
                
                Debug.LogError("Timed out waiting for login");
            }
        }
    }
    
    
    public class AutonomousBuilderStatus : EditorWindow
    {
        private AutonomousBuildState _currentState;
        private string log;
        private Vector2 logScroll;
        private BuildHelperData buildHelperData;
        public bool abort = false;
        
        public AutonomousBuildState currentState
        {
            set
            {
                AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus));
                _currentState = value;
                log += $"\n[{DateTime.Now:HH:mm:ss}]: {GetStateString(_currentState)}";
                window.Repaint();
            }
        }

        public Platform currentPlatform;

        public static AutonomousBuilderStatus ShowStatus()
        {
            AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus), true);

            window.titleContent = new GUIContent("Autonomous Builder");
            window.maxSize = new Vector2(400, 200);
            window.minSize = window.maxSize;
            window.autoRepaintOnSceneChange = true;
            
            window.Show();

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
            }

            return "Unknown status";
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField($"Autonomous Builder Status:");

            GUIStyle stateLabel = new GUIStyle(GUI.skin.label) {fontSize = 24};
            GUIContent icon;
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label) {fixedHeight = 30};
            
            switch (_currentState)
            {
                case AutonomousBuildState.building:
                    icon = EditorGUIUtility.IconContent(currentPlatform == Platform.PC ? "BuildSettings.Metro On" : "BuildSettings.Android On");

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
                    icon = EditorGUIUtility.IconContent("Error@2x");
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.aborted:
                    icon = EditorGUIUtility.IconContent("Error@2x");
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(GetStateString(_currentState), stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
            }

            GUIStyle logStyle = new GUIStyle(EditorStyles.helpBox) {wordWrap = true, alignment = TextAnchor.LowerLeft, fixedHeight = 140};
            EditorGUILayout.BeginScrollView(logScroll);
            EditorGUILayout.LabelField(log, logStyle);
            EditorGUILayout.EndScrollView();
        }

        private void OnDestroy()
        {
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
        aborted
    }
    
    [InitializeOnLoad]
    public class AutonomousBuilderStartupWatcher {
        static AutonomousBuilderStartupWatcher()
        {
            if (Object.FindObjectOfType<BuildHelperData>())
            {
                BuildHelperData data = Object.FindObjectOfType<BuildHelperData>();
                if (data.autonomousBuild.activeBuild)
                {
                    Debug.Log("clearing autonomous builder state");
                    data.autonomousBuild.activeBuild = false;
                }
            }
        }
    }

    [InitializeOnLoadAttribute]
    public static class AutonomousBuilderPlaymodeStateWatcher
    {
        // register an event handler when the class is initialized
        static AutonomousBuilderPlaymodeStateWatcher()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private static void LogPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (Object.FindObjectOfType<BuildHelperData>() == null) return;
                
                BuildHelperData buildHelperData = Object.FindObjectOfType<BuildHelperData>();
                buildHelperData.LoadFromJSON();

                if (buildHelperData.autonomousBuild.activeBuild)
                {
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                
                    if (statusWindow.abort)
                    {
                        buildHelperData.autonomousBuild.activeBuild = false;
                        buildHelperData.SaveToJSON();
                        statusWindow.currentState = AutonomousBuildState.aborted;
                        return;
                    }
                    
                    if (buildHelperData.autonomousBuild.singleTarget)
                    {
                        if (buildHelperData.autonomousBuild.progress == Progress.PostInitialBuild)
                        {
                            buildHelperData.autonomousBuild.activeBuild = false;
                            buildHelperData.autonomousBuild.progress = Progress.Finished;
                            Debug.Log("Autonomous publish succeeded");
                            
                            statusWindow.currentState = AutonomousBuildState.finished;
                            
                            buildHelperData.SaveToJSON();
                        }
                    }
                    else
                    {
                        switch (buildHelperData.autonomousBuild.progress)
                        {
                            case Progress.PostInitialBuild:
                                if (buildHelperData.autonomousBuild.secondaryTarget == Platform.mobile) SetPlatformAndroid();
                                else SetPlatformWindows();
                                break;

                            case Progress.PostSecondaryBuild:
                                if (buildHelperData.autonomousBuild.initialTarget == Platform.mobile) SetPlatformAndroid();
                                else SetPlatformWindows();
                                break;
                        }
                    }
                }
            }
        }

        private static void SetPlatformWindows()
        {
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
            statusWindow.currentPlatform = Platform.PC;
            statusWindow.currentState = AutonomousBuildState.switchingPlatform;
            
            Debug.Log($"Switching platform to Windows");
            
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                BuildTarget.StandaloneWindows64);
        }

        private static void SetPlatformAndroid()
        {
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
            statusWindow.currentPlatform = Platform.mobile;
            statusWindow.currentState = AutonomousBuildState.switchingPlatform;

            Debug.Log($"Switching platform to Windows");

            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }

    public class AutonomousBuilderTargetWatcher : IActiveBuildTargetChanged
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            Debug.Log("Switched build target to " + newTarget);

            BuildHelperData buildHelperData = UnityEngine.Object.FindObjectOfType<BuildHelperData>();
            buildHelperData.LoadFromJSON();

            AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;

            if (autonomousBuild.activeBuild)
            {
                Platform currentPlatform;

                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                switch (target)
                {
                    case BuildTarget.Android:
                        currentPlatform = Platform.mobile;
                        break;
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        currentPlatform = Platform.PC;
                        break;
                    default:
                        return;
                }

                switch (autonomousBuild.progress)
                {
                    case Progress.PostInitialBuild:
                        if (currentPlatform == autonomousBuild.secondaryTarget)
                        {
                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            if (statusWindow.abort)
                            {
                                buildHelperData.autonomousBuild.activeBuild = false;
                                buildHelperData.SaveToJSON();
                                statusWindow.currentState = AutonomousBuildState.aborted;
                            }
                            else
                            {
                                autonomousBuild.progress = Progress.PostPlatformSwitch;
                                buildHelperData.SaveToJSON();
                            }
                        }

                        break;

                    case Progress.PostSecondaryBuild:
                        if (currentPlatform == autonomousBuild.initialTarget)
                        {
                            autonomousBuild.activeBuild = false;
                            autonomousBuild.progress = Progress.Finished;
                            Debug.Log("<color=green>Autonomous publish succeeded</color>");
                            
                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            statusWindow.currentState = AutonomousBuildState.finished;
                            
                            buildHelperData.SaveToJSON();
                        }

                        break;
                }
            }
        }
    }
}
#endif