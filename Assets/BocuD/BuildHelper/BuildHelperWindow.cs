#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase.Editor;

namespace BocuD.BuildHelper
{
    public class BuildHelperWindow : EditorWindow
    {
        private GUIStyle styleHelpBox;
        private GUIStyle styleBox;
        private GUIStyle styleRichTextLabel;
        private GUIStyle styleRichTextLabelBig;
        private GUIStyle styleRichTextButton;
    
        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;
        private Texture2D _iconTrash;
        private Texture2D _iconCloud;
        private Texture2D _iconBuild;
        private Texture2D[] worldImages;

        private Vector2 scrollPosition;
    
        [MenuItem ("Window/VR Build Helper")]
        public static void ShowWindow ()
        {
            var window = GetWindow(typeof(BuildHelperWindow));
            window.titleContent = new GUIContent("VR Build Helper");
            window.minSize = new Vector2(500, 500);
            window.Show();
        }

        private void OnEnable()
        {
            buildHelperData = FindObjectOfType<BuildHelperData>();
            worldImages = new Texture2D[20];
            if (buildHelperData)
            {
                buildHelperData.LoadFromJSON();
            
                InitBranchList();
            }

            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private void OnGUI()
        {
            if (styleRichTextLabel == null) InitializeStyles();
            if (_iconVRChat == null) GetUIAssets();

            DrawBanner();
        
            if (buildHelperData == null)
            {
                OnEnable();

                if (buildHelperData == null)
                {
                    EditorGUILayout.HelpBox("Build Helper has not been set up in this scene.", MessageType.Info);

                    if (GUILayout.Button("Set up Build Helper in this scene"))
                    {
                        ResetData();
                    }
                    else return;
                }
            }

            if (buildHelperDataSO == null)
            {
                OnEnable();
            }
        
            buildHelperDataSO.Update();
            branchList.DoLayoutList();
            buildHelperDataSO.ApplyModifiedProperties();

            if (buildHelperData.currentBranch >= buildHelperData.branches.Length) buildHelperData.currentBranch = 0;

            DrawSwitchBranchButton();
        
            if (branchList.index != -1 && buildHelperData.branches.Length > 0)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

                DrawBranchEditor();
            
                GUILayout.EndScrollView();
            
                DisplayBuildButtons();
            }
        }

        private void DrawSwitchBranchButton()
        {
            if (buildHelperData.branches.Length > 0 && branchList.index != -1)
            {
                Rect buttonRectBase = EditorGUILayout.GetControlRect();

                Rect buttonRect = new Rect(5, buttonRectBase.y - EditorGUIUtility.singleLineHeight, 150,
                    EditorGUIUtility.singleLineHeight);

                if (GUI.Button(buttonRect, $"Switch to {buildHelperData.branches[branchList.index].name}"))
                {
                    if(buildHelperData.branches[buildHelperData.currentBranch].hasOverrides) _overrideContainer.ResetStateChanges();
                
                    buildHelperData.currentBranch = branchList.index;
                    InitGameObjectContainerLists();
                    buildHelperData.PrepareExcludedGameObjects();
                    
                    Save();
                
                    if(buildHelperData.branches[buildHelperData.currentBranch].hasOverrides) _overrideContainer.ApplyStateChanges();

                    VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();
                    if (sceneDescriptor &&
                        buildHelperData.branches[buildHelperData.currentBranch].blueprintID.Length > 0)
                    {
                        PipelineManager pipelineManager = sceneDescriptor.GetComponent<PipelineManager>();

                        pipelineManager.blueprintId = "";
                        pipelineManager.completedSDKPipeline = false;

                        EditorUtility.SetDirty(pipelineManager);
                        EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                        EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                        pipelineManager.blueprintId =
                            buildHelperData.branches[buildHelperData.currentBranch].blueprintID;
                        pipelineManager.completedSDKPipeline = true;

                        EditorUtility.SetDirty(pipelineManager);
                        EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                        EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);
                    }
                }
            }
        }

        private void DrawBranchEditor()
        {
            Branch selectedBranch = buildHelperData.branches[branchList.index];

            GUILayout.Label($"<b>Branch Editor</b>", styleRichTextLabel);
        
            EditorGUI.BeginChangeCheck();
            
            selectedBranch.name = EditorGUILayout.TextField("Branch name:", selectedBranch.name);

            EditorGUILayout.BeginHorizontal();
            selectedBranch.blueprintID = EditorGUILayout.TextField("Blueprint ID:", selectedBranch.blueprintID);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 170};

            if (GUILayout.Button("Copy from Scene Descriptor", buttonStyle))
            {
                VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();
                if (sceneDescriptor)
                {
                    PipelineManager pipelineManager = sceneDescriptor.GetComponent<PipelineManager>();
                    selectedBranch.blueprintID = pipelineManager.blueprintId;
                    pipelineManager.AssignId();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            selectedBranch.hasOverrides = EditorGUILayout.Toggle("GameObject Overrides", selectedBranch.hasOverrides);
            if(EditorGUI.EndChangeCheck()) Save();

            if (selectedBranch.hasOverrides)
            {
                EditorGUILayout.HelpBox("GameObject overrides are rules that can be set up for a branch to exclude GameObjects from builds for that or other branches. Exclusive GameObjects are only included on branches which have them added to the exclusive list. Excluded GameObjects are excluded for branches that have them added.", MessageType.Info);
            
                _overrideContainer = buildHelperData.overrideContainers[branchList.index];
            
                if(currentGameObjectContainerIndex != branchList.index) InitGameObjectContainerLists();
                if(exclusiveGameObjectsList == null) InitGameObjectContainerLists();
                if(excludedGameObjectsList == null) InitGameObjectContainerLists();
            
                buildHelperDataSO.Update();
            
                exclusiveGameObjectsList.DoLayoutList();
                excludedGameObjectsList.DoLayoutList();
            
                buildHelperDataSO.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            DisplayBuildInformation(selectedBranch);

            BuildData buildData = selectedBranch.buildData;

            if (buildData.pcUploadedBuildVersion != buildData.androidUploadedBuildVersion)
            {
                if (buildData.pcUploadedBuildVersion > buildData.androidUploadedBuildVersion)
                {
                    if (buildData.androidUploadedBuildVersion != -1)
                    {
                        EditorGUILayout.HelpBox(
                            "Your uploaded PC and Android builds currently don't match. The last uploaded PC build is newer than the last uploaded Android build. You should consider reuploading for Android to make them match.",
                            MessageType.Warning);
                    }
                }
                else
                {
                    if (buildData.pcUploadedBuildVersion != -1)
                    {
                        EditorGUILayout.HelpBox(
                            "Your uploaded PC and Android builds currently don't match. The last uploaded Android build is newer than the last uploaded PC build. You should consider reuploading for PC to make them match.",
                            MessageType.Warning);
                    }
                }
            }
            else
            {
                if (buildData.pcUploadedBuildVersion != -1 && buildData.androidUploadedBuildVersion != -1)
                {
                    EditorGUILayout.HelpBox(
                        "Your uploaded PC and Android builds match. Awesome!",
                        MessageType.Info);
                }
            }
        
            EditorGUILayout.Space();
        }
    
        private void DisplayBuildInformation(Branch branch)
        {
            BuildData buildData = branch.buildData;
            
            if (branch.VRCDataInitialised)
            {
                styleRichTextLabelBig.fixedWidth = position.width - 15;
            
                GUILayout.Label(branch.VRCName, styleRichTextLabelBig);

                float imgStartPos = EditorGUILayout.GetControlRect().y;

                GUIStyle worldInfoStyle = new GUIStyle(GUI.skin.label) {wordWrap = true, fixedWidth = position.width / 2 - 40};

                EditorGUILayout.LabelField("Name: " + branch.VRCName, worldInfoStyle);
                EditorGUILayout.LabelField("Description: " + branch.VRCDesc, worldInfoStyle);
                EditorGUILayout.LabelField($"Capacity: {branch.VRCPlayerCount}");
                EditorGUILayout.LabelField($"Release: {(branch.vrcReleaseState ? "Public" : "Private")}");
                EditorGUILayout.LabelField($"Tags: {branch.vrcTags}");

                float maxHeight = EditorGUILayout.GetControlRect().y + 130 - imgStartPos;
                float width = position.width / 2 - 40;
                float x = position.width / 2 + 20;

                if (width > maxHeight / 3 * 4)
                    width = maxHeight / 3 * 4;

                Rect imageRect = new Rect( position.width - width - 10, imgStartPos + 10, width, width / 4 * 3);
            
                if (worldImages[branchList.index] == null)
                {
                    worldImages[branchList.index] = Resources.Load<Texture2D>($"BuildHelper/{buildHelperData.branches[branchList.index].name}");
                }
                if(worldImages[branchList.index] != null) GUI.DrawTexture(imageRect, worldImages[branchList.index]);
            }
            else
            {
                GUILayout.Label("<b>Unpublished VRChat World</b>", styleRichTextLabelBig);
                EditorGUILayout.LabelField($"Upload this branch at least once to populate these fields");
                EditorGUILayout.LabelField("Name: ");
                EditorGUILayout.LabelField("Description: ");
                EditorGUILayout.LabelField($"Capacity: ");
                EditorGUILayout.LabelField($"Release: ");
                EditorGUILayout.LabelField($"Tags: ");
            }
        
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        
            GUILayout.Label("<b>Branch build information</b>", styleRichTextLabel);
        
            Rect buildRectBase = EditorGUILayout.GetControlRect();
            Rect buildRect = new Rect(5, buildRectBase.y + 7, 32,
                32);
            GUI.DrawTexture(buildRect, _iconBuild);
            buildRect.y += 48;
            GUI.DrawTexture(buildRect, _iconCloud);
            
            Rect BuildStatusTextRect = new Rect(buildRect.x + 37, buildRect.y - 51, position.width,
                EditorGUIUtility.singleLineHeight);

            GUIStyle buildStatusStyle = new GUIStyle(GUI.skin.label);
            buildStatusStyle.wordWrap = false;
            buildStatusStyle.fixedWidth = 400;

            GUI.Label(BuildStatusTextRect,
                $"Last PC build: {(buildData.pcBuildVersion == -1 ? "never" : $"build {buildData.pcBuildVersion} ({buildData.pcBuildTime})")}",
                buildStatusStyle);
            BuildStatusTextRect.y += EditorGUIUtility.singleLineHeight * 1f;
            GUI.Label(BuildStatusTextRect,
                $"Last Android build: {(buildData.androidBuildVersion == -1 ? "never" : $"build {buildData.androidBuildVersion} ({buildData.androidBuildTime})")}",
                buildStatusStyle);
            BuildStatusTextRect.y += EditorGUIUtility.singleLineHeight * 1.64f;
            GUI.Label(BuildStatusTextRect,
                $"Last PC upload: {(buildData.pcUploadedBuildVersion == -1 ? "never" : $"build {buildData.pcUploadedBuildVersion} ({buildData.pcUploadTime})")}",
                buildStatusStyle);
            BuildStatusTextRect.y += EditorGUIUtility.singleLineHeight * 1f;
            GUI.Label(BuildStatusTextRect,
                $"Last Android upload: {(buildData.androidUploadedBuildVersion == -1 ? "never" : $"build {buildData.androidUploadedBuildVersion} ({buildData.androidUploadTime})")}",
                buildStatusStyle);
        
            //ayes
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        private void DisplayBuildButtons()
        {
            GUILayout.Label("<b>Build Options</b>", styleRichTextLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Build options are unavailable in play mode.", MessageType.Error);
                return;
            }

            if (!APIUser.IsLoggedIn) {
                EditorGUILayout.HelpBox("You need to be logged in to build. Try opening and closing the VRChat SDK menu.", MessageType.Error);
                if (GUILayout.Button("Open VRCSDK Control Panel"))
                {
                    EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
                }
                return;
            }

            if (branchList.index != buildHelperData.currentBranch)
            {
                EditorGUILayout.HelpBox("Please select the current branch before building or switch to the desired branch.", MessageType.Error);
                return;
            }
        
            DrawBuildTargetSwitcher();
        
            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Number of Clients");
            VRCSettings.NumClients = EditorGUILayout.IntField(VRCSettings.NumClients);
            EditorGUILayout.EndHorizontal();
        
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Force no VR");
            VRCSettings.ForceNoVR = EditorGUILayout.Toggle(VRCSettings.ForceNoVR);

            EditorGUILayout.EndHorizontal();

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 140};        
        
            EditorGUILayout.BeginHorizontal();
        
            EditorGUILayout.LabelField("Local test in VRChat");

            if (GUILayout.Button("Last Build", buttonStyle))
            {
                if(CheckLastBuild()) {
                    BuildHelperBuilder.TestLastBuild();
                }
            }
        
            if (GUILayout.Button("New Build", buttonStyle))
            {
                BuildHelperBuilder.TestNewBuild();
            }
        
            EditorGUILayout.EndHorizontal();
        
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Reload local test clients");
        
            if (GUILayout.Button("Last Build", buttonStyle))
            {
                if (CheckLastBuild())
                {
                    BuildHelperBuilder.ReloadLastBuild();
                }
            }
        
            if (GUILayout.Button("New Build", buttonStyle))
            {
                BuildHelperBuilder.ReloadNewBuild();
            }
        
            EditorGUILayout.EndHorizontal();
        
            EditorGUILayout.BeginHorizontal();
        
            EditorGUILayout.LabelField("Publish to VRChat");
        
            if (GUILayout.Button("Last Build", buttonStyle))
            {
                if (CheckLastBuild())
                {
                    BuildHelperBuilder.PublishLastBuild();
                }
            }

            if (GUILayout.Button("New Build", buttonStyle))
            {
                BuildHelperBuilder.PublishNewBuild();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            GUILayout.Label("<b>Autonomous build</b>", styleRichTextLabel);
            
            EditorGUILayout.HelpBox("Autonomous build can be used to publish your world for both PC and Android automatically with one button press.", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            
            GUIStyle autoButtonStyle = new GUIStyle(GUI.skin.button) {fixedHeight = 40};
            
            string platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "PC";

            if (GUILayout.Button($"Build and publish for {platform}", autoButtonStyle))
            {
                if (InitAutonomousBuild(true))
                {
                    buildHelperData.SaveToJSON();
                    
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                    statusWindow.currentPlatform = buildHelperData.autonomousBuild.initialTarget;
                    statusWindow.currentState = AutonomousBuildState.building;
                    
                    BuildHelperBuilder.PublishNewBuild();
                }
            }
            
            if (GUILayout.Button("Build and publish for PC and Android", autoButtonStyle))
            {
                if (InitAutonomousBuild(false))
                {
                    buildHelperData.SaveToJSON();
                    
                    AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                    statusWindow.currentPlatform = buildHelperData.autonomousBuild.initialTarget;
                    statusWindow.currentState = AutonomousBuildState.building;
                    
                    BuildHelperBuilder.PublishNewBuild();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool CheckLastBuild()
        {
            if (buildHelperData.lastBuiltBranch != buildHelperData.currentBranch)
            {
                if (EditorUtility.DisplayDialog("Build Helper",
                    "The last detected build was for a different branch. Are you sure you want to continue? The build for the other branch will be used instead.",
                    "Yes", "No"))
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        private bool InitAutonomousBuild(bool singleTarget)
        {
            if (!singleTarget)
            {
                if (!EditorUtility.DisplayDialog("Build Helper",
                    "Build Helper will initiate a build and publish cycle for both PC and mobile in succesion",
                    "Proceed",
                    "Cancel"))
                {
                    return false;
                }
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            switch (target)
            {
                case BuildTarget.Android:
                    buildHelperData.autonomousBuild.initialTarget = AutonomousBuildInformation.Platform.mobile;
                    buildHelperData.autonomousBuild.secondaryTarget = AutonomousBuildInformation.Platform.PC;
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    buildHelperData.autonomousBuild.initialTarget = AutonomousBuildInformation.Platform.PC;
                    buildHelperData.autonomousBuild.secondaryTarget = AutonomousBuildInformation.Platform.mobile;
                    break;
                default:
                    return false;
            }

            buildHelperData.autonomousBuild.activeBuild = true;
            buildHelperData.autonomousBuild.singleTarget = singleTarget;
            buildHelperData.autonomousBuild.progress = AutonomousBuildInformation.Progress.PreInitialBuild;
            return true;
        }
    
        public static void DrawBuildTargetSwitcher()
        {
            EditorGUILayout.LabelField("Active Build Target: " + EditorUserBuildSettings.activeBuildTarget);
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 && GUILayout.Button("Switch Build Target to Android"))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher", "Are you sure you want to switch your build target to Android? This could take a while.", "Confirm", "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android && GUILayout.Button("Switch Build Target to Windows"))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher", "Are you sure you want to switch your build target to Windows? This could take a while.", "Confirm", "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                }
            }
        }

        private void ResetData()
        {
            if (FindObjectOfType<BuildHelperData>() != null)
            {
                DestroyImmediate(FindObjectOfType<BuildHelperData>().gameObject);
            }

            GameObject dataObj = new GameObject("BuildHelperData");
        
            buildHelperData = dataObj.AddComponent<BuildHelperData>();
            buildHelperData.branches = new Branch[0];
            buildHelperData.overrideContainers = new OverrideContainer[0];
        
            dataObj.AddComponent<BuildHelperRuntime>();
            dataObj.hideFlags = HideFlags.HideInHierarchy;
            dataObj.tag = "EditorOnly";
            buildHelperData.SaveToJSON();
            OnEnable();
        }
    
        private void LogPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                if(buildHelperData) buildHelperData.LoadFromJSON();
        }
    
        private void OnDestroy()
        {
            Save();
        }

        private void Save()
        {
            if (buildHelperData != null)
                buildHelperData.SaveToJSON();
        }
    
        #region Reorderable list initialisation
    
        private BuildHelperData buildHelperData = null;
    
        private SerializedObject buildHelperDataSO = null;
        private ReorderableList branchList = null;
    
        private void InitBranchList()
        {
            buildHelperDataSO = new SerializedObject(buildHelperData);
        
            branchList = new ReorderableList(buildHelperDataSO, buildHelperDataSO.FindProperty("branches"), true,
                true, true, true);

            branchList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "World branches");
            branchList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty property = branchList.serializedProperty.GetArrayElementAtIndex(index);

                SerializedProperty branchName = property.FindPropertyRelative("name");
                SerializedProperty worldID = property.FindPropertyRelative("blueprintID");

                Rect nameRect = new Rect(rect) {y = rect.y + 1.5f, width = 110, height = EditorGUIUtility.singleLineHeight};
                Rect blueprintIDRect = new Rect(rect)
                {
                    x = 115, y = rect.y + 1.5f, width = EditorGUIUtility.currentViewWidth - 115,
                    height = EditorGUIUtility.singleLineHeight
                };
                Rect selectedRect = new Rect(rect)
                {
                    x = EditorGUIUtility.currentViewWidth - 95, y = rect.y + 1.5f, width = 90,
                    height = EditorGUIUtility.singleLineHeight
                };

                EditorGUI.LabelField(nameRect, branchName.stringValue);
                EditorGUI.LabelField(blueprintIDRect, worldID.stringValue);

                if (buildHelperData.currentBranch == index)
                    EditorGUI.LabelField(selectedRect, "current branch");
            };
            branchList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                Branch newBranch = new Branch {name = "new branch", buildData = new BuildData()};
                ArrayUtility.Add(ref buildHelperData.branches, newBranch);

                OverrideContainer newContainer = new OverrideContainer
                    {ExclusiveGameObjects = new GameObject[0], ExcludedGameObjects = new GameObject[0]};
                ArrayUtility.Add(ref buildHelperData.overrideContainers, newContainer);

                list.index = Array.IndexOf(buildHelperData.branches, newBranch);
            };

            branchList.index = buildHelperData.currentBranch;
        }

        private OverrideContainer _overrideContainer;
        private int currentGameObjectContainerIndex;
        private ReorderableList excludedGameObjectsList = null;
        private ReorderableList exclusiveGameObjectsList = null;

        private void InitGameObjectContainerLists()
        {
            if (buildHelperData)
            {
                //setup exclusive list
                exclusiveGameObjectsList = new ReorderableList(buildHelperDataSO, buildHelperDataSO.FindProperty("overrideContainers").GetArrayElementAtIndex(branchList.index).FindPropertyRelative("ExclusiveGameObjects"), true,
                    true, true, true);

                exclusiveGameObjectsList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Exclusive GameObjects");
                exclusiveGameObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    SerializedProperty property = exclusiveGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);

                    EditorGUI.PropertyField(rect, property);
                };
                exclusiveGameObjectsList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    ArrayUtility.Add(ref _overrideContainer.ExclusiveGameObjects, null);
                };
                exclusiveGameObjectsList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    GameObject toRemove = _overrideContainer.ExclusiveGameObjects[exclusiveGameObjectsList.index];

                    bool existsInOtherList = false;
                
                    foreach (OverrideContainer container in buildHelperData.overrideContainers)
                    {
                        if (container == _overrideContainer) continue;
                        if (container.ExclusiveGameObjects.Contains(toRemove)) existsInOtherList = true;
                    }
                
                    if(!existsInOtherList) OverrideContainer.EnableGameObject(toRemove);
                
                    ArrayUtility.RemoveAt(ref _overrideContainer.ExclusiveGameObjects, exclusiveGameObjectsList.index);
                };
            
                //setup exclude list
                excludedGameObjectsList = new ReorderableList(buildHelperDataSO, buildHelperDataSO.FindProperty("overrideContainers").GetArrayElementAtIndex(branchList.index).FindPropertyRelative("ExcludedGameObjects"), true,
                    true, true, true);
            
                excludedGameObjectsList.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Excluded GameObjects");
                excludedGameObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    SerializedProperty property = excludedGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                
                    EditorGUI.PropertyField(rect, property);
                };
                excludedGameObjectsList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    ArrayUtility.Add(ref _overrideContainer.ExcludedGameObjects, null);
                };
                excludedGameObjectsList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    GameObject toRemove = _overrideContainer.ExcludedGameObjects[excludedGameObjectsList.index];

                    OverrideContainer.EnableGameObject(toRemove);
                
                    ArrayUtility.RemoveAt(ref _overrideContainer.ExclusiveGameObjects, excludedGameObjectsList.index);
                };

                currentGameObjectContainerIndex = branchList.index;
            }
        }
    
        #endregion
        #region Editor GUI Helper Functions
    
        private void InitializeStyles()
        {
            // EditorGUI
            styleHelpBox = new GUIStyle(EditorStyles.helpBox);
            styleHelpBox.padding = new RectOffset(0, 0, styleHelpBox.padding.top, styleHelpBox.padding.bottom + 3);
        
            // GUI
            styleBox = new GUIStyle(GUI.skin.box);
            styleBox.padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2, GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2);
            styleBox.margin = new RectOffset(0, 0, 4, 4);
        
            styleRichTextLabel = new GUIStyle(GUI.skin.label);
            styleRichTextLabel.richText = true;
        
            styleRichTextLabelBig = new GUIStyle(GUI.skin.label);
            styleRichTextLabelBig.richText = true;
            styleRichTextLabelBig.fontSize = 25;
            styleRichTextLabelBig.wordWrap = true;
        
            styleRichTextButton = new GUIStyle(GUI.skin.button);
            styleRichTextButton.richText = true;
        }
    
        private void GetUIAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
            _iconTrash = Resources.Load<Texture2D>("Icons/Trash-Icon-32px");
            _iconCloud = Resources.Load<Texture2D>("Icons/Cloud-32px");
            _iconBuild = Resources.Load<Texture2D>("Icons/Build-32px");
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label("<b>VR Build Helper</b>", styleRichTextLabel);

            GUILayout.FlexibleSpace();

            float iconSize = EditorGUIUtility.singleLineHeight;

            if (buildHelperData != null)
            {
                GUIContent buttonDelete = new GUIContent("", "Delete all data");
                GUIStyle styleDelete = new GUIStyle(GUI.skin.box);
                if (_iconTrash != null)
                {
                    buttonDelete = new GUIContent(_iconTrash, "Delete all data");
                    styleDelete = GUIStyle.none;
                }

                if (GUILayout.Button(buttonDelete, styleDelete, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                {
                    bool confirm = EditorUtility.DisplayDialog("Build Helper",
                        "Are you sure you want to remove Build Helper from this scene? All stored information will be lost permanently.",
                        "Yes",
                        "Cancel");

                    if (confirm)
                    {
                        if (FindObjectOfType<BuildHelperData>() != null)
                        {
                            DestroyImmediate(FindObjectOfType<BuildHelperData>().gameObject);
                        }
                    }
                }
                
                GUILayout.Space(iconSize / 4);
            }

            GUIContent buttonVRChat = new GUIContent("", "VRChat");
            GUIStyle styleVRChat = new GUIStyle(GUI.skin.box);
            if (_iconVRChat != null)
            {
                buttonVRChat = new GUIContent(_iconVRChat, "VRChat");
                styleVRChat = GUIStyle.none;
            }
        
            if (GUILayout.Button(buttonVRChat, styleVRChat, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://vrchat.com/home/user/usr_3a5bf7e4-e569-41d5-b70a-31304fd8e0e8");
            }
            
            GUILayout.Space(iconSize / 4);

            GUIContent buttonGitHub = new GUIContent("", "Github");
            GUIStyle styleGitHub = new GUIStyle(GUI.skin.box);
            if (_iconGitHub != null)
            {
                buttonGitHub = new GUIContent(_iconGitHub, "Github");
                styleGitHub = GUIStyle.none;
            }
        
            if (GUILayout.Button(buttonGitHub, styleGitHub, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://github.com/BocuD/VRBuildHelper");
            }

            GUILayout.EndHorizontal();
        }
        #endregion
    }
}

#endif