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
using System.Globalization;
using System.IO;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase.Editor;

namespace BocuD.BuildHelper.Editor
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
        private Texture2D _iconCloud;
        private Texture2D _iconBuild;
        private Texture2D _iconSettings;

        private Texture2D[] worldImages;
        private Texture2D[] modifiedWorldImages;

        private Vector2 scrollPosition;
        private bool settings = false;
        private bool dirty = false;

        private PipelineManager pipelineManager;

        [MenuItem("Window/VR Build Helper")]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(BuildHelperWindow));
            window.titleContent = new GUIContent("VR Build Helper");
            window.minSize = new Vector2(500, 650);
            window.Show();
        }

        private void OnEnable()
        {
            buildHelperData = FindObjectOfType<BuildHelperData>();
            pipelineManager = FindObjectOfType<PipelineManager>();

            worldImages = new Texture2D[20];
            modifiedWorldImages = new Texture2D[20];

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

            if (BuildPipeline.isBuildingPlayer) return;

            DrawBanner();

            if (DrawSettings()) return;

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

            if (buildHelperData.currentBranchIndex >= buildHelperData.branches.Length)
                buildHelperData.currentBranchIndex = 0;

            DrawSwitchBranchButton();

            PipelineChecks();

            if (branchList.index != -1 && buildHelperData.branches.Length > 0)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none,
                    GUI.skin.verticalScrollbar);

                DrawBranchEditor();

                GUILayout.EndScrollView();

                DisplayBuildButtons();
            }
        }

        private bool DrawSettings()
        {
            if (!settings) return false;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) {richText = true};
            EditorGUILayout.LabelField("<b>VR Build Helper Options</b>", labelStyle);

            if (buildHelperData)
            {
                if (buildHelperData.gameObject.hideFlags == HideFlags.None)
                {
                    EditorGUILayout.HelpBox("The VRBuildHelper Data object is currently not hidden.",
                        MessageType.Warning);
                    if (GUILayout.Button("Hide VRBuildHelper Data object"))
                    {
                        buildHelperData.gameObject.hideFlags = HideFlags.HideInHierarchy;
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }
                else
                {
                    if (GUILayout.Button("Show VRBuildHelper Data object (Not recommended)"))
                    {
                        buildHelperData.gameObject.hideFlags = HideFlags.None;
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }
            }

            if (GUILayout.Button("Remove VRBuildHelper from this scene"))
            {
                bool confirm = EditorUtility.DisplayDialog("Build Helper",
                    "Are you sure you want to remove Build Helper from this scene? All stored information will be lost permanently.",
                    "Yes",
                    "Cancel");

                if (confirm)
                {
                    if (FindObjectOfType<BuildHelperData>() != null)
                    {
                        if (buildHelperData != null)
                        {
                            buildHelperData.DeleteJSON();
                        }
                        DestroyImmediate(FindObjectOfType<BuildHelperData>().gameObject);
                    }
                }
            }

            if (buildHelperData)
            {

                EditorGUI.BeginChangeCheck();
                buildHelperData.autoSave = EditorGUILayout.Toggle("Auto save", buildHelperData.autoSave);
                if (EditorGUI.EndChangeCheck())
                {
                    if (buildHelperData.autoSave) Save();
                }
            }
            
            if (GUILayout.Button("Close"))
            {
                settings = false;
            }

            return true;
        }

        private void DrawSwitchBranchButton()
        {
            if (buildHelperData.branches.Length > 0 && branchList.index != -1)
            {
                Rect buttonRectBase = GUILayoutUtility.GetLastRect();

                Rect buttonRect = new Rect(5, buttonRectBase.y, 250, EditorGUIUtility.singleLineHeight);

                if (GUI.Button(buttonRect, $"Switch to {buildHelperData.branches[branchList.index].name}"))
                {
                    SwitchBranch(buildHelperData, branchList.index);
                }
            }
        }
        
        private void PipelineChecks()
        {
            pipelineManager = FindObjectOfType<PipelineManager>();

            if (buildHelperData.currentBranch == null) return;
            
            if (pipelineManager != null)
            {
                //dumb check to prevent buildhelper from throwing an error when it doesn't need to
                if (buildHelperData.currentBranch.blueprintID.Length > 1)
                {
                    if (pipelineManager.blueprintId != buildHelperData.currentBranch.blueprintID)
                    {
                        EditorGUILayout.HelpBox(
                            "The scene descriptor blueprint ID currently doesn't match the branch blueprint ID. VR Build Helper will not function properly.",
                            MessageType.Error);
                        if (GUILayout.Button("Auto fix"))
                        {
                            ApplyPipelineID(buildHelperData.currentBranch.blueprintID);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "To properly use VR Build Helper you need a VRC Scene Decriptor in the scene. Please add a VRC Scene Descriptor.",
                    MessageType.Error);
                
                EditorGUI.BeginDisabledGroup(true);

                GUIContent autoFix = new GUIContent("Auto fix", "This will be added in a future version.");
                
                if (GUILayout.Button(autoFix))
                {
                    ApplyPipelineID(buildHelperData.currentBranch.blueprintID);
                }
                EditorGUI.EndDisabledGroup();
            }
        }
        
        public static void SwitchBranch(BuildHelperData data, int targetBranch)
        {
            //prevent indexoutofrangeexception
            if (data.currentBranchIndex < data.branches.Length && data.currentBranchIndex > -1)
            {
                if (data.branches[data.currentBranchIndex].hasOverrides)
                    data.overrideContainers[data.currentBranchIndex].ResetStateChanges();
            }

            data.currentBranchIndex = targetBranch;
            data.PrepareExcludedGameObjects();

            data.SaveToJSON();

            if (data.branches[data.currentBranchIndex].hasOverrides)
                data.overrideContainers[data.currentBranchIndex].ApplyStateChanges();

            ApplyPipelineID(data.branches[data.currentBranchIndex].blueprintID);
        }

        public static void ApplyPipelineID(string blueprintID)
        {
            if (FindObjectOfType<VRCSceneDescriptor>())
            {
                VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();
                PipelineManager pipelineManager = sceneDescriptor.GetComponent<PipelineManager>();

                pipelineManager.blueprintId = "";
                pipelineManager.completedSDKPipeline = false;

                EditorUtility.SetDirty((UnityEngine.Object) pipelineManager);
                EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                sceneDescriptor.apiWorld = null;
                
                pipelineManager.blueprintId = blueprintID;
                pipelineManager.completedSDKPipeline = true;

                EditorUtility.SetDirty((UnityEngine.Object) pipelineManager);
                EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                if (pipelineManager.blueprintId == "") return;
                
                ApiWorld world = API.FromCacheOrNew<ApiWorld>(pipelineManager.blueprintId);
                world.Fetch(null,
                    (c) => sceneDescriptor.apiWorld = c.Model as ApiWorld,
                    (c) =>
                    {
                        if (c.Code == 404)
                        {
                            Logger.LogError($"[<color=blue>API</color>] Could not load world {pipelineManager.blueprintId} because it didn't exist.");
                            ApiCache.Invalidate<ApiWorld>(pipelineManager.blueprintId);
                        }
                        else
                            Logger.LogError($"[<color=blue>API</color>] Could not load world {pipelineManager.blueprintId} because {c.Error}");
                    });
                sceneDescriptor.apiWorld = world;
            }
        }

        private bool deploymentEditor = false, gameObjectOverrides = false;

        private void DrawBranchEditor()
        {
            Branch selectedBranch = buildHelperData.branches[branchList.index];

            GUILayout.Label($"<b>Branch Editor</b>", styleRichTextLabel);

            EditorGUI.BeginChangeCheck();

            selectedBranch.name = EditorGUILayout.TextField("Branch name:", selectedBranch.name);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            selectedBranch.blueprintID = EditorGUILayout.TextField("Blueprint ID:", selectedBranch.blueprintID);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Change", GUILayout.Width(55)))
            {
                //spawn editor window
                BlueprintIDEditor.SpawnEditor(buildHelperData, selectedBranch);
            }

            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) TrySave();

            DrawVRCWorldEditor(selectedBranch);

            DrawGameObjectEditor(selectedBranch);
            DrawDeploymentEditorPreview(selectedBranch);
            DrawUdonLinkEditor(selectedBranch);

            GUILayout.FlexibleSpace();

            DisplayBuildInformation(selectedBranch);

            DrawBuildVersionWarnings(selectedBranch);

            EditorGUILayout.Space();
        }

        private void DrawGameObjectEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");
            selectedBranch.hasOverrides = EditorGUILayout.Toggle("GameObject Overrides", selectedBranch.hasOverrides);
            if (selectedBranch.hasOverrides) gameObjectOverrides = EditorGUILayout.Foldout(gameObjectOverrides, "");
            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }

            if (gameObjectOverrides && selectedBranch.hasOverrides)
            {
                EditorGUILayout.HelpBox(
                    "GameObject overrides are rules that can be set up for a branch to exclude GameObjects from builds for that or other branches. Exclusive GameObjects are only included on branches which have them added to the exclusive list. Excluded GameObjects are excluded for branches that have them added.",
                    MessageType.Info);

                _overrideContainer = buildHelperData.overrideContainers[branchList.index];

                if (currentGameObjectContainerIndex != branchList.index) InitGameObjectContainerLists();
                if (exclusiveGameObjectsList == null) InitGameObjectContainerLists();
                if (excludedGameObjectsList == null) InitGameObjectContainerLists();

                buildHelperDataSO.Update();

                exclusiveGameObjectsList.DoLayoutList();
                excludedGameObjectsList.DoLayoutList();

                buildHelperDataSO.ApplyModifiedProperties();
            }

            GUILayout.EndVertical();
        }


        private Vector2 deploymentScrollArea;

        private void DrawDeploymentEditorPreview(Branch selectedBranch)
        {
            GUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            selectedBranch.hasDeploymentData = EditorGUILayout.Toggle("Deployment Manager", selectedBranch.hasDeploymentData);
            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }
            
            if (selectedBranch.hasDeploymentData)
            {
                if (selectedBranch.deploymentData.initialBranchName == "")
                {
                    selectedBranch.deploymentData.initialBranchName = selectedBranch.name;
                    TrySave();
                }
            }

            EditorGUI.BeginDisabledGroup(!selectedBranch.hasDeploymentData);
            if (GUILayout.Button("Open Deployment Manager", GUILayout.Width(200)))
            {
                DeploymentManagerEditor.OpenDeploymentManager(buildHelperData, branchList.index);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasDeploymentData)
            {
                EditorGUILayout.BeginHorizontal();
                deploymentEditor = EditorGUILayout.Foldout(deploymentEditor, "");
                if (deploymentEditor)
                {
                    if (GUILayout.Button("Force Refresh", GUILayout.Width(100)))
                    {
                        DeploymentManager.RefreshDeploymentData(selectedBranch);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (deploymentEditor)
                {
                    if (selectedBranch.deploymentData.deploymentPath == "")
                    {
                        EditorGUILayout.HelpBox(
                            "The Deployment Manager automatically saves uploaded builds so you can revisit or reupload them later.\nTo start using the Deployment Manager, please set a location to store uploaded builds.",
                            MessageType.Info);
                        if (GUILayout.Button("Set deployment path..."))
                        {
                            string selectedFolder = EditorUtility.OpenFolderPanel("Set deployment folder location...",
                                Application.dataPath, "Deployments");
                            if (!string.IsNullOrEmpty(selectedFolder))
                            {
                                if (selectedFolder.StartsWith(Application.dataPath))
                                {
                                    selectedBranch.deploymentData.deploymentPath =
                                        selectedFolder.Substring(Application.dataPath.Length);
                                }
                                else
                                {
                                    Logger.LogError("Please choose a location within the Assets folder");
                                }
                            }
                        }
                        GUILayout.EndVertical();

                        return;
                    }

                    DeploymentManager.RefreshDeploymentData(selectedBranch);
                    deploymentScrollArea = EditorGUILayout.BeginScrollView(deploymentScrollArea);

                    if (selectedBranch.deploymentData.units.Length < 1)
                    {
                        EditorGUILayout.HelpBox(
                            "No builds have been saved yet. To save a build for this branch, upload your world.",
                            MessageType.Info);
                    }

                    bool pcUploadKnown = false, androidUploadKnown = false;

                    foreach (DeploymentUnit deploymentUnit in selectedBranch.deploymentData.units)
                    {
                        Color backgroundColor = GUI.backgroundColor;

                        bool isLive = false;

                        if (deploymentUnit.platform == Platform.mobile)
                        {
                            if (selectedBranch.buildData.androidUploadedBuildVersion != -1)
                            {
                                DateTime androidUploadTime = DateTime.Parse(selectedBranch.buildData.androidUploadTime,
                                    CultureInfo.InvariantCulture);
                                if (Mathf.Abs((float) (androidUploadTime - deploymentUnit.buildDate).TotalSeconds) <
                                    300 &&
                                    !androidUploadKnown)
                                {
                                    androidUploadKnown = true;
                                    isLive = true;
                                }
                            }
                        }
                        else
                        {
                            if (selectedBranch.buildData.pcUploadedBuildVersion != -1)
                            {
                                DateTime pcUploadTime = DateTime.Parse(selectedBranch.buildData.pcUploadTime,
                                    CultureInfo.InvariantCulture);
                                if (Mathf.Abs((float) (pcUploadTime - deploymentUnit.buildDate).TotalSeconds) < 300 &&
                                    !pcUploadKnown)
                                {
                                    pcUploadKnown = true;
                                    isLive = true;
                                }
                            }
                        }

                        if (isLive) GUI.backgroundColor = new Color(0.2f, 0.92f, 0.2f);

                        GUILayout.BeginVertical("GroupBox");

                        GUI.backgroundColor = backgroundColor;

                        EditorGUILayout.BeginHorizontal();
                        GUIContent icon = EditorGUIUtility.IconContent(deploymentUnit.platform == Platform.PC
                            ? "BuildSettings.Metro On"
                            : "BuildSettings.Android On");
                        EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                        GUILayout.Label("Build " + deploymentUnit.buildNumber, GUILayout.Width(60));

                        EditorGUI.BeginDisabledGroup(true);
                        Rect fieldRect = EditorGUILayout.GetControlRect();
                        GUI.TextField(fieldRect, deploymentUnit.fileName);
                        EditorGUI.EndDisabledGroup();

                        GUIStyle selectButtonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 60};
                        if (GUILayout.Button("Select", selectButtonStyle))
                        {
                            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(
                                $"Assets/{selectedBranch.deploymentData.deploymentPath}/" + deploymentUnit.fileName);
                        }

                        EditorGUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawUdonLinkEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();
            selectedBranch.hasUdonLink = EditorGUILayout.Toggle("Udon Link", selectedBranch.hasUdonLink);
            EditorGUI.BeginDisabledGroup(!selectedBranch.hasUdonLink || buildHelperData.linkedBehaviour == null);
            if (GUILayout.Button("Open inspector", GUILayout.Width(200)))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                Selection.objects = new UnityEngine.Object[] {buildHelperData.linkedBehaviourGameObject};
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasUdonLink)
            {
                if (buildHelperData.linkedBehaviourGameObject != null)
                {
                    buildHelperData.linkedBehaviour = buildHelperData.linkedBehaviourGameObject
                        .GetUdonSharpComponent<BuildHelperUdon>();
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                buildHelperDataSO.Update();
                EditorGUILayout.PropertyField(buildHelperDataSO.FindProperty("linkedBehaviour"));
                buildHelperDataSO.ApplyModifiedProperties();

                if (EditorGUI.EndChangeCheck())
                {
                    if (buildHelperData.linkedBehaviour == null)
                    {
                        buildHelperData.linkedBehaviourGameObject = null;
                    }
                    else buildHelperData.linkedBehaviourGameObject = buildHelperData.linkedBehaviour.gameObject;
                }

                if (buildHelperData.linkedBehaviourGameObject == null)
                {
                    if (GUILayout.Button("Create new", GUILayout.Width(100)))
                    {
                        GameObject buildHelperUdonGameObject = new GameObject("BuildHelperUdon");
                        buildHelperUdonGameObject.AddUdonSharpComponent<BuildHelperUdon>();
                        buildHelperData.linkedBehaviourGameObject = buildHelperUdonGameObject;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox(
                        "There is no BuildHelperUdon behaviour selected for this scene right now.\nSelect an existing behaviour or create a new one.",
                        MessageType.Info);
                }
                else EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedBranch.hasUdonLink)
                {

                }

                TrySave();
            }
        }

        private bool editMode;

        private void DrawVRCWorldEditor(Branch branch)
        {
            GUILayout.BeginVertical("Helpbox");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"VRChat World Editor");
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            if (GUILayout.Button("Force Refresh", GUILayout.Width(100)))
            {
                VRChatApiTools.RefreshData();
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();
            
            ApiWorld apiWorld = null;
            bool apiWorldLoaded = true;
            bool isNewWorld = false;
            bool loadError = false;

            if (branch.blueprintID != "")
            {
                if (Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Loading world information from VRChat in playmode is not supported.", MessageType.Info);
                    apiWorldLoaded = false;
                }
                else if (!VRChatApiTools.worldCache.TryGetValue(branch.blueprintID, out apiWorld))
                {
                    if (!VRChatApiTools.invalidWorlds.Contains(branch.blueprintID))
                        VRChatApiTools.FetchApiWorld(branch.blueprintID);
                    else isNewWorld = true;
                }

                if (VRChatApiTools.invalidWorlds.Contains(branch.blueprintID))
                {
                    loadError = true;
                    EditorGUILayout.HelpBox("Couldn't load world information. This can happen if the blueprint ID is invalid, or if the world was deleted.", MessageType.Error);
                } 
                else if (!isNewWorld && apiWorld == null && !Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Loading world information...");
                    apiWorldLoaded = false;
                }
            }
            else
            {
                isNewWorld = true;
                apiWorldLoaded = false;
            }

            if (loadError)
            {
                GUILayout.Label("Unknown VRChat World", styleRichTextLabelBig);
            }
            else if (isNewWorld)
            {
                GUILayout.Label("Unpublished VRChat World", styleRichTextLabelBig);
            }
            else
            {
                string headerText = apiWorldLoaded ? $"<b>{apiWorld.name}</b> by {apiWorld.authorName}" : branch.cachedName;
                GUILayout.Label(headerText, styleRichTextLabelBig);
            }

            float imgWidth = 170;
            
            float width = position.width - imgWidth - 20;
            
            GUIStyle worldInfoStyle = new GUIStyle(GUI.skin.label) {wordWrap = true, fixedWidth = width, richText = true};
            
            GUILayout.BeginHorizontal();

            if (editMode)
            {
                GUILayout.BeginVertical(GUILayout.Width(width));

                branch.editedName = EditorGUILayout.TextField("Name:", branch.editedName);
                branch.editedDescription = EditorGUILayout.TextField("Description:", branch.editedDescription);
                branch.editedCap = EditorGUILayout.IntField($"Capacity:", branch.editedCap);
                branch.editedTags = EditorGUILayout.TextField($"Tags:", branch.editedTags);
                EditorGUILayout.LabelField(apiWorldLoaded ? $"Release: " + apiWorld.releaseStatus : branch.cachedRelease, worldInfoStyle);

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 100};

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save", buttonStyle))
                {
                    editMode = false;
                    bool changesDetected = true;
                    if (branch.editedName == apiWorld.name)
                    {
                        if (branch.editedDescription == branch.cachedDescription)
                        {
                            if (branch.editedCap == branch.cachedCap)
                            {
                                if (branch.editedTags == branch.cachedTags)
                                {
                                    changesDetected = false;
                                }
                            }
                        }
                    }

                    if (changesDetected)
                        branch.vrcDataHasChanges = true;

                    TrySave();
                }

                if (GUILayout.Button("Revert", buttonStyle))
                {
                    branch.editedName = apiWorld.name;
                    branch.editedDescription = apiWorld.description;
                    branch.editedCap = apiWorld.capacity;
                    branch.editedTags = apiWorld.tags.ToString();
                    branch.vrcDataHasChanges = false;
                    editMode = false;
                    
                    TrySave();
                }

                if (GUILayout.Button("Replace image", buttonStyle))
                {
                    string[] allowedFileTypes = {"png"};
                    imageBranch = branch;
                    NativeFilePicker.PickFile(OnImageSelected, allowedFileTypes);
                }

                EditorGUILayout.EndHorizontal();

                if (branch.vrcImageHasChanges)
                {
                    if (GUILayout.Button("Revert image", buttonStyle))
                    {
                        branch.vrcImageHasChanges = false;
                        branch.vrcImageWarning = "";
                        modifiedWorldImages[branchList.index] = null;

                        string oldImagePath = "Assets/Resources/BuildHelper/" + imageBranch.name + "_" +
                                              buildHelperData.currentBranch.blueprintID + "-edit.png";

                        Texture2D oldImage =
                            AssetDatabase.LoadAssetAtPath(oldImagePath, typeof(Texture2D)) as Texture2D;
                        if (oldImage != null)
                        {
                            AssetDatabase.DeleteAsset("Assets/Resources/BuildHelper/" + imageBranch.name + "_" +
                                                      buildHelperData.currentBranch.blueprintID + "-edit.png");
                        }
                    }
                }
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical(GUILayout.Width(width));

                if (apiWorldLoaded && !loadError)
                {
                    bool localDataOutdated = false;

                    //cache world information
                    if (branch.cachedName != apiWorld.name)
                    {
                        branch.cachedName = apiWorld.name;
                        localDataOutdated = true;
                    }

                    if (branch.cachedDescription != apiWorld.description)
                    {
                        branch.cachedDescription = apiWorld.description;
                        localDataOutdated = true;
                    }

                    if (branch.cachedCap != apiWorld.capacity)
                    {
                        branch.cachedCap = apiWorld.capacity;
                        localDataOutdated = true;
                    }

                    if (branch.cachedTags != apiWorld.tags.ToString())
                    {
                        branch.cachedTags = apiWorld.tags.ToString();
                        localDataOutdated = true;
                    }

                    if (branch.cachedRelease != apiWorld.releaseStatus)
                    {
                        branch.cachedRelease = apiWorld.releaseStatus;
                        localDataOutdated = true;
                    }

                    if (localDataOutdated)
                    {
                        if (branch.editedName == "notInitialised") branch.editedName = branch.cachedName;
                        if (branch.editedDescription == "notInitialised")
                            branch.editedDescription = branch.cachedDescription;
                        if (branch.editedCap == -1) branch.editedCap = branch.cachedCap;
                        if (branch.editedTags == "notInitialised") branch.editedTags = branch.cachedTags;
                    }

                    if (localDataOutdated) TrySave();
                    
                    string displayName = branch.vrcDataHasChanges && apiWorld.name != branch.editedName
                        ? $"<color=yellow>{branch.editedName}</color>"
                        : apiWorld.name;
                    string displayDesc = branch.vrcDataHasChanges && apiWorld.description != branch.editedDescription
                        ? $"<color=yellow>{branch.editedDescription}</color>"
                        : apiWorld.description;
                    string displayCap = branch.vrcDataHasChanges && apiWorld.capacity != branch.editedCap
                        ? $"<color=yellow>{branch.editedCap}</color>"
                        : apiWorld.capacity.ToString();
                    string displayTags = branch.vrcDataHasChanges && apiWorld.tags.ToString() != branch.editedTags
                        ? $"<color=yellow>{branch.editedTags}</color>"
                        : apiWorld.tags.ToString();

                    EditorGUILayout.LabelField("Name: " + displayName, worldInfoStyle);
                    EditorGUILayout.LabelField("Description: " + displayDesc, worldInfoStyle);
                    EditorGUILayout.LabelField($"Capacity: " + displayCap, worldInfoStyle);
                    EditorGUILayout.LabelField($"Tags: " + displayTags, worldInfoStyle);
                    EditorGUILayout.LabelField($"Release: " + apiWorld.releaseStatus, worldInfoStyle);

                    if (branch.vrcDataHasChanges || branch.vrcImageHasChanges)
                    {
                        GUIStyle infoStyle = new GUIStyle(EditorStyles.helpBox) {fixedWidth = width, richText = true};
                        string changesWarning = branch.vrcImageWarning +
                                                "<color=yellow>Your changes will be applied with the next upload.</color>";
                        EditorGUILayout.LabelField(changesWarning, infoStyle);
                    }
                    
                    EditorGUILayout.BeginHorizontal();
                    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 100};

                    if (GUILayout.Button("Edit", buttonStyle))
                    {
                        editMode = true;
                        if (!branch.vrcDataHasChanges)
                        {
                            branch.editedName = apiWorld.name;
                            branch.editedDescription = apiWorld.description;
                            branch.editedCap = apiWorld.capacity;
                            branch.editedTags = apiWorld.tags.ToString();
                        }
                    }

                    if (!editMode)
                    {
                        buttonStyle.fixedWidth = 200;
                        if (GUILayout.Button("View on VRChat website", buttonStyle))
                        {
                            Application.OpenURL($"https://vrchat.com/home/world/{branch.blueprintID}");
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    if (isNewWorld)
                    {
                        EditorGUILayout.LabelField("Name: ", worldInfoStyle);
                        EditorGUILayout.LabelField("Description: ", worldInfoStyle);
                        EditorGUILayout.LabelField($"Capacity: ", worldInfoStyle);
                        EditorGUILayout.LabelField($"Tags: ", worldInfoStyle);
                        EditorGUILayout.LabelField($"Release: ", worldInfoStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Name: " + branch.cachedName, worldInfoStyle);
                        EditorGUILayout.LabelField("Description: " + branch.cachedDescription, worldInfoStyle);
                        EditorGUILayout.LabelField($"Capacity: " + branch.cachedCap, worldInfoStyle);
                        EditorGUILayout.LabelField($"Tags: " + branch.cachedTags, worldInfoStyle);
                        EditorGUILayout.LabelField($"Release: " + branch.cachedRelease, worldInfoStyle);
                    }
                }
                
                GUILayout.EndVertical();
            }
            
            if (branch.vrcImageHasChanges)
            {
                if (modifiedWorldImages[branchList.index] == null)
                {
                    modifiedWorldImages[branchList.index] =
                        Resources.Load<Texture2D>(
                            $"BuildHelper/{buildHelperData.branches[branchList.index].name}_{buildHelperData.currentBranch.blueprintID}-edit");
                }

                if (modifiedWorldImages[branchList.index] != null)
                {
                    GUILayout.BeginVertical();
                    GUILayout.Box(modifiedWorldImages[branchList.index], GUILayout.Width(imgWidth), GUILayout.Height(imgWidth / 4 * 3));
                    GUILayout.Space(8);
                    GUILayout.EndVertical();
                }
            }
            else
            {
                if (apiWorldLoaded && !loadError)
                {
                    if (VRChatApiTools.ImageCache.TryGetValue(apiWorld.id, out worldImages[branchList.index]))
                    {
                        if (worldImages[branchList.index] == null) VRChatApiTools.ImageCache.Remove(apiWorld.id);
                        
                        GUILayout.BeginVertical();
                        GUILayout.Box(worldImages[branchList.index], GUILayout.Width(imgWidth), GUILayout.Height(imgWidth/4*3));
                        GUILayout.Space(8);
                        GUILayout.EndVertical();
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private Branch imageBranch;

        public void OnImageSelected(string filePath)
        {
            if (File.Exists(filePath))
            {
                if (imageBranch != null)
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D overrideImage = new Texture2D(2, 2);
                    
                    overrideImage.LoadImage(fileData); //..this will auto-resize the texture dimensions.

                    //check aspect ratio and resolution
                    if (overrideImage.width * 3 != overrideImage.height * 4)
                    {
                        if (overrideImage.width < 1200)
                        {
                            imageBranch.vrcImageWarning = "<color=yellow>" +
                                                          "For best results, use a 4:3 image that is at least 1200x900.\n" +
                                                          "</color>";
                        }
                        else
                        {
                            imageBranch.vrcImageWarning =
                                "<color=yellow>" + "For best results, use a 4:3 image.\n" + "</color>";
                        }
                    }
                    else
                    {
                        if (overrideImage.width < 1200)
                        {
                            imageBranch.vrcImageWarning = "<color=yellow>" +
                                                          "For best results, use an image that is at least 1200x900.\n" +
                                                          "</color>";
                        }
                        else
                        {
                            imageBranch.vrcImageWarning = "<color=green>" +
                                                          "Your new image has the correct aspect ratio and is high resolution. Nice!\n" +
                                                          "</color>";
                        }
                    }

                    byte[] worldImagePNG = overrideImage.EncodeToPNG();

                    string dirPath = Application.dataPath + "/Resources/BuildHelper/";
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }

                    string savePath = dirPath + imageBranch.name + "_" + imageBranch.blueprintID +
                                      "-edit.png";
                    File.WriteAllBytes(savePath, worldImagePNG);

                    savePath = "Assets/Resources/BuildHelper/" + imageBranch.name + "_" +
                               imageBranch.blueprintID + "-edit.png";

                    AssetDatabase.WriteImportSettingsIfDirty(savePath);
                    AssetDatabase.ImportAsset(savePath);

                    TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(savePath);
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 2048;
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(savePath);

                    AssetDatabase.ImportAsset(savePath);

                    imageBranch.vrcImageHasChanges = true;

                    TrySave();
                }
                else
                {
                    Logger.LogError("Target branch for image processor doesn't exist anymore, was it deleted?");
                }
            }
            else
            {
                Logger.LogError("Null filepath was passed to image processor, skipping process steps");
            }
        }

        private void DisplayBuildInformation(Branch branch)
        {
            BuildData buildData = branch.buildData;

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
                $"Last PC build: {(buildData.pcBuildVersion == -1 ? "Unknown" : $"build {buildData.pcBuildVersion} ({buildData.pcBuildTime})")}",
                buildStatusStyle);
            BuildStatusTextRect.y += EditorGUIUtility.singleLineHeight * 1f;
            GUI.Label(BuildStatusTextRect,
                $"Last Android build: {(buildData.androidBuildVersion == -1 ? "Unknown" : $"build {buildData.androidBuildVersion} ({buildData.androidBuildTime})")}",
                buildStatusStyle);
            BuildStatusTextRect.y += EditorGUIUtility.singleLineHeight * 1.64f;
            GUI.Label(BuildStatusTextRect,
                $"Last PC upload: {(buildData.pcUploadedBuildVersion == -1 ? "Unknown" : $"build {buildData.pcUploadedBuildVersion} ({buildData.pcUploadTime})")}",
                buildStatusStyle);
            BuildStatusTextRect.y += EditorGUIUtility.singleLineHeight * 1f;
            GUI.Label(BuildStatusTextRect,
                $"Last Android upload: {(buildData.androidUploadedBuildVersion == -1 ? "Unknown" : $"build {buildData.androidUploadedBuildVersion} ({buildData.androidUploadTime})")}",
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

        private static void DrawBuildVersionWarnings(Branch selectedBranch)
        {
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
        }

        private void DisplayBuildButtons()
        {
            GUILayout.Label("<b>Build Options</b>", styleRichTextLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Build options are unavailable in play mode.", MessageType.Error);
                return;
            }

            if (!APIUser.IsLoggedIn)
            {
                EditorGUILayout.HelpBox(
                    "You need to be logged in to build or publish. Try opening and closing the VRChat SDK menu.",
                    MessageType.Error);
                if (GUILayout.Button("Open VRCSDK Control Panel"))
                {
                    VRChatApiTools.TryAutoLogin(this);
                }

                return;
            }

            if (branchList.index != buildHelperData.currentBranchIndex)
            {
                EditorGUILayout.HelpBox(
                    "Please select the current branch before building or switch to the desired branch.",
                    MessageType.Error);
                return;
            }

            DrawBuildTargetSwitcher();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Number of Clients", GUILayout.Width(140));
            VRCSettings.NumClients = EditorGUILayout.IntField(VRCSettings.NumClients, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Force no VR", GUILayout.Width(140));
            VRCSettings.ForceNoVR = EditorGUILayout.Toggle(VRCSettings.ForceNoVR, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 140};

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Local test in VRChat");
            if (GUILayout.Button("Last Build", buttonStyle))
            {
                if (CheckLastBuild())
                {
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
                    if (CheckAccount())
                    {
                        BuildHelperBuilder.PublishLastBuild();
                    }
                }
            }

            if (GUILayout.Button("New Build", buttonStyle))
            {
                if (CheckAccount())
                {
                    BuildHelperBuilder.PublishNewBuild();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            GUILayout.Label("<b>Autonomous build</b>", styleRichTextLabel);
            EditorGUILayout.HelpBox(
                "Autonomous build can be used to publish your world for both PC and Android automatically with one button press.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUIStyle autoButtonStyle = new GUIStyle(GUI.skin.button) {fixedHeight = 40};
            string platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "PC";
            if (GUILayout.Button($"Build and publish for {platform}", autoButtonStyle))
            {
                if (CheckAccount())
                {
                    if (InitAutonomousBuild(true))
                    {
                        buildHelperData.SaveToJSON();

                        AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                        statusWindow.AddLog("Initiating autonomous builder...");
                        statusWindow.currentPlatform = buildHelperData.autonomousBuild.initialTarget;
                        statusWindow.currentState = AutonomousBuildState.building;

                        BuildHelperBuilder.PublishNewBuild();
                    }
                }
            }

            if (GUILayout.Button("Build and publish for PC and Android", autoButtonStyle))
            {
                if (CheckAccount())
                {
                    if (InitAutonomousBuild(false))
                    {
                        buildHelperData.SaveToJSON();

                        AutonomousBuilderStatus statusWindow = AutonomousBuilderStatus.ShowStatus();
                        statusWindow.AddLog("Initiating autonomous builder...");
                        statusWindow.currentPlatform = buildHelperData.autonomousBuild.initialTarget;
                        statusWindow.currentState = AutonomousBuildState.building;

                        BuildHelperBuilder.PublishNewBuild();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawBuildTargetSwitcher()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Build Target: " + EditorUserBuildSettings.activeBuildTarget);
            
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 &&
                GUILayout.Button("Switch Build Target to Android", GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher",
                    "Are you sure you want to switch your build target to Android? This could take a while.", "Confirm",
                    "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android &&
                GUILayout.Button("Switch Build Target to Windows", GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher",
                    "Are you sure you want to switch your build target to Windows? This could take a while.", "Confirm",
                    "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneWindows64);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool CheckLastBuild()
        {
            if (buildHelperData.lastBuiltBranch != buildHelperData.currentBranchIndex)
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

        //TODO: make this static
        private bool CheckAccount()
        {
            if (buildHelperData.currentBranch.blueprintID == "")
            {
                return true;
            }

            ApiWorld apiWorld;
            if (VRChatApiTools.worldCache.TryGetValue(buildHelperData.currentBranch.blueprintID, out apiWorld))
            {
                if (APIUser.CurrentUser.id != apiWorld.authorId)
                {
                    if (EditorUtility.DisplayDialog("Build Helper",
                        "The world author for the selected branch doesn't match the currently logged in user. Publishing will result in an error. Do you still want to continue?",
                        "Yes", "No"))
                    {
                        return true;
                    }

                    return false;
                }
                return true;
            }

            if (EditorUtility.DisplayDialog("Build Helper",
                "Couldn't verify the world author for the selected branch. Do you want to try publishing anyways?",
                "Yes", "No"))
            {
                return true;
            }

            return false;
        }

        private bool InitAutonomousBuild(bool singleTarget)
        {
            if (buildHelperData.currentBranch.blueprintID == "")
            {
                Logger.LogError("Publishing with the autonomous builder for new worlds is currently not supported.");
                return false;
                // if (EditorUtility.DisplayDialog("Build Helper",
                //     "You are trying to use the autonomous builder with an unpublished build. Are you sure you want to continue?",
                //     "Yes", "No"))
                // {
                //     if (buildHelperData.currentBranch.vrcDataHasChanges &&
                //         buildHelperData.currentBranch.editedName != "" && buildHelperData.currentBranch.editedCap != -1)
                //     {
                //         return true;
                //     }
                //     Logger.LogError("You need to specify at least a name and player capacity for your world before being able to publish it.");
                // }
                //
                // return false;
            }
            
            string platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "PC";

            string message = singleTarget
                ? $"Build Helper will initiate a build and publish cycle for {platform}"
                : "Build Helper will initiate a build and publish cycle for both PC and mobile in succesion";

            if (!EditorUtility.DisplayDialog("Build Helper", message, "Proceed", "Cancel"))
            {
                return false;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            switch (target)
            {
                case BuildTarget.Android:
                    buildHelperData.autonomousBuild.initialTarget = Platform.mobile;
                    buildHelperData.autonomousBuild.secondaryTarget = Platform.PC;
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    buildHelperData.autonomousBuild.initialTarget = Platform.PC;
                    buildHelperData.autonomousBuild.secondaryTarget = Platform.mobile;
                    break;
                default:
                    return false;
            }

            buildHelperData.autonomousBuild.activeBuild = true;
            buildHelperData.autonomousBuild.singleTarget = singleTarget;
            buildHelperData.autonomousBuild.progress = AutonomousBuildInformation.Progress.PreInitialBuild;
            return true;
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
                if (buildHelperData)
                    buildHelperData.LoadFromJSON();
        }

        private void OnDestroy()
        {
            Save();
        }

        private void TrySave()
        {
            if (buildHelperData.autoSave)
            {
                Save();
            }
            else
            {
                dirty = true;
            }
        }

        private void Save()
        {
            if (buildHelperData != null)
                buildHelperData.SaveToJSON();
            else Logger.LogError("Error while saving, Data Object not found");
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

            branchList.drawHeaderCallback = (rect) =>
            {
                EditorGUI.LabelField(rect, "World branches");
            };
            branchList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty property = branchList.serializedProperty.GetArrayElementAtIndex(index);

                SerializedProperty branchName = property.FindPropertyRelative("name");
                SerializedProperty worldID = property.FindPropertyRelative("blueprintID");

                Rect nameRect = new Rect(rect)
                    {y = rect.y + 1.5f, width = 110, height = EditorGUIUtility.singleLineHeight};
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

                if (buildHelperData.currentBranchIndex == index)
                    EditorGUI.LabelField(selectedRect, "current branch");
            };
            branchList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                Undo.RecordObject(buildHelperData, "Create new branch");
                Branch newBranch = new Branch {name = "new branch", buildData = new BuildData()};
                ArrayUtility.Add(ref buildHelperData.branches, newBranch);

                OverrideContainer newContainer = new OverrideContainer
                    {ExclusiveGameObjects = new GameObject[0], ExcludedGameObjects = new GameObject[0]};
                ArrayUtility.Add(ref buildHelperData.overrideContainers, newContainer);

                list.index = Array.IndexOf(buildHelperData.branches, newBranch);
                TrySave();
            };
            branchList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                string branchName = buildHelperData.branches[list.index].name;
                if (EditorUtility.DisplayDialog("Build Helper",
                    $"Are you sure you want to delete the branch '{branchName}'? This can not be undone.", "Yes", "No"))
                {
                    ArrayUtility.RemoveAt(ref buildHelperData.branches, list.index);
                }

                list.index = 0;
                TrySave();
            };

            branchList.index = buildHelperData.currentBranchIndex;
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
                exclusiveGameObjectsList = new ReorderableList(buildHelperDataSO,
                    buildHelperDataSO.FindProperty("overrideContainers").GetArrayElementAtIndex(branchList.index)
                        .FindPropertyRelative("ExclusiveGameObjects"), true,
                    true, true, true);

                exclusiveGameObjectsList.drawHeaderCallback =
                    (rect) => EditorGUI.LabelField(rect, "Exclusive GameObjects");
                exclusiveGameObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    SerializedProperty property =
                        exclusiveGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(buildHelperData, "Modify GameObject list");
                        TrySave();
                    }
                };
                exclusiveGameObjectsList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    Undo.RecordObject(buildHelperData, "Add GameObject to list");
                    ArrayUtility.Add(ref _overrideContainer.ExclusiveGameObjects, null);
                    TrySave();
                };
                exclusiveGameObjectsList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    Undo.RecordObject(buildHelperData, "Remove GameObject from list");

                    GameObject toRemove = _overrideContainer.ExclusiveGameObjects[exclusiveGameObjectsList.index];

                    bool existsInOtherList = false;

                    foreach (OverrideContainer container in buildHelperData.overrideContainers)
                    {
                        if (container == _overrideContainer) continue;
                        if (container.ExclusiveGameObjects.Contains(toRemove)) existsInOtherList = true;
                    }

                    if (!existsInOtherList) OverrideContainer.EnableGameObject(toRemove);

                    ArrayUtility.RemoveAt(ref _overrideContainer.ExclusiveGameObjects, exclusiveGameObjectsList.index);
                    TrySave();
                };

                //setup exclude list
                excludedGameObjectsList = new ReorderableList(buildHelperDataSO,
                    buildHelperDataSO.FindProperty("overrideContainers").GetArrayElementAtIndex(branchList.index)
                        .FindPropertyRelative("ExcludedGameObjects"), true,
                    true, true, true);

                excludedGameObjectsList.drawHeaderCallback =
                    (rect) => EditorGUI.LabelField(rect, "Excluded GameObjects");
                excludedGameObjectsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    SerializedProperty property =
                        excludedGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(buildHelperData, "Modify GameObject list");
                        TrySave();
                    }
                };
                excludedGameObjectsList.onAddCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    Undo.RecordObject(buildHelperData, "Add GameObject to list");
                    ArrayUtility.Add(ref _overrideContainer.ExcludedGameObjects, null);
                    TrySave();
                };
                excludedGameObjectsList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
                {
                    GameObject toRemove = _overrideContainer.ExcludedGameObjects[excludedGameObjectsList.index];

                    Undo.RecordObject(buildHelperData, "Remove GameObject from list");

                    OverrideContainer.EnableGameObject(toRemove);

                    ArrayUtility.RemoveAt(ref _overrideContainer.ExclusiveGameObjects, excludedGameObjectsList.index);
                    TrySave();
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
            styleBox.padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2,
                GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2);
            styleBox.margin = new RectOffset(0, 0, 4, 4);

            styleRichTextLabel = new GUIStyle(GUI.skin.label);
            styleRichTextLabel.richText = true;

            styleRichTextLabelBig = new GUIStyle(GUI.skin.label);
            styleRichTextLabelBig.richText = true;
            styleRichTextLabelBig.fontSize = 20;
            styleRichTextLabelBig.wordWrap = true;

            styleRichTextButton = new GUIStyle(GUI.skin.button);
            styleRichTextButton.richText = true;
        }

        private void GetUIAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
            _iconCloud = Resources.Load<Texture2D>("Icons/Cloud-32px");
            _iconBuild = Resources.Load<Texture2D>("Icons/Build-32px");
            _iconSettings = Resources.Load<Texture2D>("Icons/Settings-32px");
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label("<b>VR Build Helper</b>", styleRichTextLabel);

            GUILayout.FlexibleSpace();

            float iconSize = EditorGUIUtility.singleLineHeight;

            if (dirty && !buildHelperData.autoSave)
            {
                if (GUILayout.Button("Save Changes"))
                {
                    Save();
                }
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

            GUILayout.Space(iconSize / 4);

            GUIContent buttonSettings = new GUIContent("", "Settings");
            GUIStyle styleSettings = new GUIStyle(GUI.skin.box);
            if (_iconSettings != null)
            {
                buttonSettings = new GUIContent(_iconSettings, "Settings");
                styleSettings = GUIStyle.none;
            }

            if (GUILayout.Button(buttonSettings, styleSettings, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                settings = true;
            }

            GUILayout.EndHorizontal();
        }

        #endregion
    }
}