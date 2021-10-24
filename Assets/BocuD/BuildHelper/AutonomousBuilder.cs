#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using static BocuD.BuildHelper.AutonomousBuildInformation;

namespace BocuD.BuildHelper
{
    public class AutonomousBuilderStatus : EditorWindow
    {
        private AutonomousBuildState _currentState;
        public AutonomousBuildState currentState
        {
            set
            {
                AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus));
                _currentState = value;
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
                    GUILayout.Label($"Building for {currentPlatform}", stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.waitingForApi:
                    GUILayout.Label($"Waiting for VRChat Api", stateLabel);
                    break;
                case AutonomousBuildState.switchingPlatform:
                    icon = EditorGUIUtility.IconContent("RotateTool On@2x");

                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Switching platform to {currentPlatform}", stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.uploading:
                    icon = EditorGUIUtility.IconContent("UpArrow");
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Uploading for {currentPlatform}", stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
                    break;
                case AutonomousBuildState.finished:
                    icon = EditorGUIUtility.IconContent("d_Toggle Icon");
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Finished!", stateLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(icon, iconStyle);
                    GUILayout.EndHorizontal();
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
        finished
    }

    [InitializeOnLoadAttribute]
    public static class BuildHelperPlaymodeStateWatcher
    {
        // register an event handler when the class is initialized
        static BuildHelperPlaymodeStateWatcher()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private static void LogPlayModeState(PlayModeStateChange state)
        {
            Debug.Log(state);

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                BuildHelperData buildHelperData = Object.FindObjectOfType<BuildHelperData>();

                if (buildHelperData == null) return;
                
                buildHelperData.LoadFromJSON();

                AutonomousBuildInformation autonomousBuild = buildHelperData.autonomousBuild;

                if (autonomousBuild.activeBuild)
                {
                    if (autonomousBuild.singleTarget)
                    {
                        if (autonomousBuild.progress == Progress.PostInitialBuild)
                        {
                            autonomousBuild.activeBuild = false;
                            autonomousBuild.progress = Progress.Finished;
                            Debug.Log("<color=green>Autonomous publish succeeded</color>");
                            
                            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                            statusWindow.currentState = AutonomousBuildState.finished;
                            
                            buildHelperData.SaveToJSON();
                        }
                    }
                    else
                    {
                        switch (autonomousBuild.progress)
                        {
                            case Progress.PostInitialBuild:
                                if (autonomousBuild.secondaryTarget == Platform.mobile) SetPlatformAndroid();
                                else SetPlatformWindows();
                                break;

                            case Progress.PostSecondaryBuild:
                                if (autonomousBuild.initialTarget == Platform.mobile) SetPlatformAndroid();
                                else SetPlatformWindows();
                                break;
                        }
                    }
                }
            }
        }

        private static void SetPlatformWindows()
        {
            Debug.Log($"Switching platform to Windows");
            
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
            statusWindow.currentPlatform = Platform.PC;
            statusWindow.currentState = AutonomousBuildState.switchingPlatform;
            
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                BuildTarget.StandaloneWindows64);
        }

        private static void SetPlatformAndroid()
        {
            Debug.Log($"Switching platform to Android");
            
            AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
            statusWindow.currentPlatform = Platform.mobile;
            statusWindow.currentState = AutonomousBuildState.switchingPlatform;

            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
        }
    }

    public class BuildHelperChangeTargetListener : IActiveBuildTargetChanged
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
                            autonomousBuild.progress = Progress.PostPlatformSwitch;
                            buildHelperData.SaveToJSON();
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