#if UNITY_EDITOR

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private Texture2D _iconSettings;
        
        private Texture2D[] worldImages;
        private Texture2D[] modifiedWorldImages;

        private Vector2 scrollPosition;
        private bool settings = false;
    
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

            if (buildHelperData.currentBranchIndex >= buildHelperData.branches.Length) buildHelperData.currentBranchIndex = 0;

            DrawSwitchBranchButton();
        
            if (branchList.index != -1 && buildHelperData.branches.Length > 0)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

                DrawBranchEditor();
            
                GUILayout.EndScrollView();
            
                DisplayBuildButtons();
            }
        }

        private bool DrawSettings()
        {
            if (!settings) return false;
            
            if (buildHelperData)
            {
                if (buildHelperData.gameObject.hideFlags == HideFlags.None)
                {
                    EditorGUILayout.HelpBox("The VRBuildHelper Data object is currently not hidden.", MessageType.Warning);
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
                        DestroyImmediate(FindObjectOfType<BuildHelperData>().gameObject);
                    }
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
                Rect buttonRectBase = EditorGUILayout.GetControlRect();

                Rect buttonRect = new Rect(5, buttonRectBase.y - EditorGUIUtility.singleLineHeight, 250,
                    EditorGUIUtility.singleLineHeight);

                if (GUI.Button(buttonRect, $"Switch to {buildHelperData.branches[branchList.index].name}"))
                {
                    if(buildHelperData.branches[buildHelperData.currentBranchIndex].hasOverrides) _overrideContainer.ResetStateChanges();
                
                    buildHelperData.currentBranchIndex = branchList.index;
                    InitGameObjectContainerLists();
                    buildHelperData.PrepareExcludedGameObjects();
                    
                    Save();
                
                    if(buildHelperData.branches[buildHelperData.currentBranchIndex].hasOverrides) _overrideContainer.ApplyStateChanges();

                    VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();
                    if (sceneDescriptor &&
                        buildHelperData.branches[buildHelperData.currentBranchIndex].blueprintID.Length > 0)
                    {
                        PipelineManager pipelineManager = sceneDescriptor.GetComponent<PipelineManager>();

                        pipelineManager.blueprintId = "";
                        pipelineManager.completedSDKPipeline = false;

                        EditorUtility.SetDirty(pipelineManager);
                        EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                        EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                        pipelineManager.blueprintId =
                            buildHelperData.branches[buildHelperData.currentBranchIndex].blueprintID;
                        pipelineManager.completedSDKPipeline = true;

                        EditorUtility.SetDirty(pipelineManager);
                        EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                        EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);
                    }
                }
            }
        }

        private bool deploymentEditor = false, gameObjectOverrides = false, udonLinkFoldout;
        
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
            if(EditorGUI.EndChangeCheck()) Save();
            
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
            if (EditorGUI.EndChangeCheck()) Save();

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
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");
            
            EditorGUILayout.BeginHorizontal();
            selectedBranch.hasDeploymentData = EditorGUILayout.Toggle("Deployment Manager", selectedBranch.hasDeploymentData);
            EditorGUI.BeginDisabledGroup(!selectedBranch.hasDeploymentData);
            if (GUILayout.Button("Open Deployment Manager"))
            {
                DeploymentManagerEditor.OpenDeploymentManager(buildHelperData, branchList.index);
            }
            if (GUILayout.Button("Force Refresh"))
            {
                DeploymentManager.RefreshDeploymentData(selectedBranch);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasDeploymentData)
            {
                deploymentEditor = EditorGUILayout.Foldout(deploymentEditor, "");
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
                                    Debug.LogError("Please choose a location within the Assets folder");
                                }
                            }
                        }

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
                            DateTime androidUploadTime = DateTime.Parse(selectedBranch.buildData.androidUploadTime,
                                CultureInfo.InvariantCulture);
                            if (Mathf.Abs((float) (androidUploadTime - deploymentUnit.buildDate).TotalSeconds) < 300 &&
                                !androidUploadKnown)
                            {
                                androidUploadKnown = true;
                                isLive = true;
                            }
                        }
                        else
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
                        // GUILayout.Label("Build date: " + deploymentUnit.buildDate);
                        // GUILayout.Label("Modified on: " + deploymentUnit.modifiedDate);
                        // GUILayout.Label("Platform: " + deploymentUnit.platform);
                        // GUILayout.Label("Git hash: " + deploymentUnit.gitHash);
                        //
                        // EditorGUILayout.BeginHorizontal();
                        //
                        // EditorGUI.BeginDisabledGroup(deploymentUnit.platform == Platform.mobile);
                        // if (GUILayout.Button("Test locally in VRChat"))
                        // {
                        //     BuildHelperBuilder.TestExistingBuild(deploymentUnit);
                        // }
                        // EditorGUI.EndDisabledGroup();
                        //
                        // EditorGUI.BeginDisabledGroup(!APIUser.IsLoggedIn);
                        // if (GUILayout.Button("Publish this build"))
                        // {
                        //     BuildHelperBuilder.PublishExistingBuild(deploymentUnit);
                        // }
                        // EditorGUI.EndDisabledGroup();
                        // EditorGUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();
                }
            }
            
            GUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedBranch.hasDeploymentData)
                {
                    if (selectedBranch.deploymentData.initialBranchName == "")
                        selectedBranch.deploymentData.initialBranchName = selectedBranch.name;
                }
                Save();
            }
        }

        private void DrawUdonLinkEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");
            
            EditorGUILayout.BeginHorizontal();
            selectedBranch.hasUdonLink = EditorGUILayout.Toggle("Udon Link", selectedBranch.hasUdonLink);
            EditorGUILayout.EndHorizontal();
            
            udonLinkFoldout = EditorGUILayout.Foldout(udonLinkFoldout, "");

            if (udonLinkFoldout)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                buildHelperDataSO.Update();
                EditorGUILayout.PropertyField(buildHelperDataSO.FindProperty("linkedBehaviour"));
                buildHelperDataSO.ApplyModifiedProperties();
                if (EditorGUI.EndChangeCheck())
                {
                    if (buildHelperData.linkedBehaviour != null)
                        buildHelperData.linkedBehaviourGameObject = buildHelperData.linkedBehaviour.gameObject;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedBranch.hasUdonLink)
                {
                    
                }
                Save();
            }
        }

        private bool editMode;
        private void DrawVRCWorldEditor(Branch branch)
        {
            GUILayout.Label($"<b>{branch.VRCName}</b>", styleRichTextLabelBig);

            float imgStartPos = GUILayoutUtility.GetLastRect().y + 25;
            float imgWidth = position.width / 2 - 80;
            float maxHeight = 220;
            if (imgWidth > maxHeight / 3 * 4)
                imgWidth = maxHeight / 3 * 4;
            float x = position.width / 2 + 40;
                
            float width = position.width - imgWidth - 20;
            
            if(editMode) {
                GUIStyle areaStyle = new GUIStyle(GUI.skin.textArea) {wordWrap = true, fixedWidth = width - 100};
                GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField) {fixedWidth = width - 100};
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label) {fixedWidth = 100};
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Name:", labelStyle);
                Rect nameRect = EditorGUILayout.GetControlRect();
                nameRect.x = width - textFieldStyle.fixedWidth;
                branch.VRCNameLocal = EditorGUI.TextArea(nameRect, branch.VRCNameLocal, textFieldStyle);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Description:", labelStyle);
                Rect descRect = EditorGUILayout.GetControlRect();
                descRect.x = width - areaStyle.fixedWidth;
                int linecount = branch.VRCDescLocal.Split('\n').Length;
                if(linecount > 1) descRect.height = linecount * EditorGUIUtility.singleLineHeight - linecount * 3;
                branch.VRCDescLocal = EditorGUI.TextArea(descRect, branch.VRCDescLocal, areaStyle);
                EditorGUILayout.EndHorizontal();
                if(linecount > 1) EditorGUILayout.Space(linecount * EditorGUIUtility.singleLineHeight - EditorGUIUtility.singleLineHeight - linecount * 3);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Capacity:", labelStyle);
                Rect capFieldRect = EditorGUILayout.GetControlRect();
                capFieldRect.x = width - textFieldStyle.fixedWidth;
                branch.VRCCapLocal =
                    EditorGUI.IntField(capFieldRect, branch.VRCCapLocal, textFieldStyle);
                EditorGUILayout.EndHorizontal();
                
                //branch.VRCNameLocal = EditorGUILayout.TextField($"Release: {(branch.vrcReleaseState ? "Public" : "Private")}");
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tags:", labelStyle);
                Rect tagsRect = EditorGUILayout.GetControlRect();
                tagsRect.x = width - textFieldStyle.fixedWidth;
                branch.vrcTagsLocal = EditorGUI.TextArea(tagsRect, branch.vrcTagsLocal, textFieldStyle);
                EditorGUILayout.EndHorizontal();

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 100};

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save", buttonStyle))
                {
                    editMode = false;
                    bool changesDetected = true;
                    if (branch.VRCNameLocal == branch.VRCName)
                    {
                        if (branch.VRCDescLocal == branch.VRCDesc)
                        {
                            if (branch.VRCCapLocal == branch.VRCCap)
                            {
                                if (branch.vrcTagsLocal == branch.vrcTags)
                                {
                                    changesDetected = false;
                                }
                            }
                        }
                    }

                    if (changesDetected)
                        branch.vrcDataHasChanges = true;
                    Save();
                }
                
                if (GUILayout.Button("Revert", buttonStyle))
                {
                    branch.VRCNameLocal = branch.VRCName;
                    branch.VRCDescLocal = branch.VRCDesc;
                    branch.VRCCapLocal = branch.VRCCap;
                    branch.vrcTagsLocal = branch.vrcTags;
                    branch.vrcDataHasChanges = false;
                    editMode = false;
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

                        string oldImagePath = "Assets/Resources/BuildHelper/" + imageBranch.name + "-edit.png";
                        
                        Texture2D oldImage = AssetDatabase.LoadAssetAtPath(oldImagePath, typeof(Texture2D)) as Texture2D;
                        if (oldImage != null)
                        {
                            AssetDatabase.DeleteAsset("Assets/Resources/BuildHelper/" + imageBranch.name + "-edit.png");
                        }
                    }
                }
            }
            else
            {
                // GUILayout.Label("<b>Unpublished VRChat World</b>", styleRichTextLabelBig);
                // EditorGUILayout.LabelField($"Upload this branch at least once to populate these fields");
                // EditorGUILayout.LabelField("Name: ");
                // EditorGUILayout.LabelField("Description: ");
                // EditorGUILayout.LabelField($"Capacity: ");
                // EditorGUILayout.LabelField($"Release: ");
                // EditorGUILayout.LabelField($"Tags: ");
                
                GUIStyle worldInfoStyle = new GUIStyle(GUI.skin.label) {wordWrap = true, fixedWidth = width, richText = true};

                string displayName = branch.VRCName != branch.VRCNameLocal ? $"<color=yellow>{branch.VRCNameLocal}</color>" : branch.VRCName;
                string displayDesc = branch.VRCDesc != branch.VRCDescLocal ? $"<color=yellow>{branch.VRCDescLocal}</color>" : branch.VRCDesc;
                string displayCap = branch.VRCCap != branch.VRCCapLocal ? $"<color=yellow>{branch.VRCCapLocal}</color>" : branch.VRCCap.ToString();
                string displayTags = branch.vrcTags != branch.vrcTagsLocal ? $"<color=yellow>{branch.vrcTagsLocal}</color>" : branch.vrcTags;

                EditorGUILayout.LabelField("Name: " + displayName, worldInfoStyle);
                EditorGUILayout.LabelField("Description: " + displayDesc, worldInfoStyle);
                EditorGUILayout.LabelField($"Capacity: " + displayCap, worldInfoStyle);
                EditorGUILayout.LabelField($"Tags: " + displayTags, worldInfoStyle);
                EditorGUILayout.LabelField($"Release: {(branch.vrcReleaseState ? "Public" : "Private")}");

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
            
            Rect imageRect = new Rect(position.width - imgWidth - 10, imgStartPos + 10, imgWidth, imgWidth / 4 * 3);

            if (branch.vrcImageHasChanges)
            {
                if (modifiedWorldImages[branchList.index] == null)
                {
                    modifiedWorldImages[branchList.index] =
                        Resources.Load<Texture2D>($"BuildHelper/{buildHelperData.branches[branchList.index].name}-edit");
                }

                if (modifiedWorldImages[branchList.index] != null) GUI.DrawTexture(imageRect, modifiedWorldImages[branchList.index]);
            }
            else
            {
                if (worldImages[branchList.index] == null)
                {
                    worldImages[branchList.index] =
                        Resources.Load<Texture2D>($"BuildHelper/{buildHelperData.branches[branchList.index].name}");
                }

                if (worldImages[branchList.index] != null) GUI.DrawTexture(imageRect, worldImages[branchList.index]);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        private Branch imageBranch;
        public void OnImageSelected(string filePath)
        {
            Texture2D overrideImage = null;
            byte[] fileData;

            if (File.Exists(filePath))
            {
                fileData = File.ReadAllBytes(filePath);
                overrideImage = new Texture2D(2, 2);
                overrideImage.LoadImage(fileData); //..this will auto-resize the texture dimensions.

                //check aspectRatio and resolution
                if (overrideImage.width * 3 != overrideImage.height * 4)
                {
                    if (overrideImage.width < 1200)
                    {
                        imageBranch.vrcImageWarning = "<color=yellow>" + "For best results, use a 4:3 image that is at least 1200x900.\n" + "</color>";
                    }
                    else
                    {
                        imageBranch.vrcImageWarning = "<color=yellow>" + "For best results, use a 4:3 image.\n" + "</color>";
                    }
                }
                else
                {
                    if (overrideImage.width < 1200)
                    {
                        imageBranch.vrcImageWarning = "<color=yellow>" + "For best results, use an image that is at least 1200x900.\n" + "</color>";
                    }
                    else
                    {
                        imageBranch.vrcImageWarning = "<color=green>" + "Your new image has the correct aspect ratio and is high resolution. Nice!\n" + "</color>";
                    }
                }
                
                byte[] worldImagePNG = overrideImage.EncodeToPNG();

                string dirPath = Application.dataPath + "/Resources/BuildHelper/";
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                string savePath = dirPath + imageBranch.name + "-edit.png";
                File.WriteAllBytes(savePath, worldImagePNG);

                savePath = "Assets/Resources/BuildHelper/" + imageBranch.name + "-edit.png";
                
                AssetDatabase.WriteImportSettingsIfDirty(savePath);
                AssetDatabase.ImportAsset(savePath);
                
                imageBranch.vrcImageHasChanges = true;
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

            if (!APIUser.IsLoggedIn) {
                EditorGUILayout.HelpBox("You need to be logged in to build or publish. Try opening and closing the VRChat SDK menu.", MessageType.Error);
                if (GUILayout.Button("Open VRCSDK Control Panel"))
                {
                    EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
                }
                return;
            }

            if (branchList.index != buildHelperData.currentBranchIndex)
            {
                EditorGUILayout.HelpBox("Please select the current branch before building or switch to the desired branch.", MessageType.Error);
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

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fixedWidth = 140 };        
        
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
        
        private static void DrawBuildTargetSwitcher()
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

        private bool InitAutonomousBuild(bool singleTarget)
        {
            string platform = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ? "Android" : "PC";
            
            string message = singleTarget ? $"Build Helper will initiate a build and publish cycle for {platform}"
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
            dataObj.AddComponent<AutonomousBuilder>();
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

                if (buildHelperData.currentBranchIndex == index)
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
            branchList.onRemoveCallback = (UnityEditorInternal.ReorderableList list) =>
            {
                string branchName = buildHelperData.branches[list.index].name;
                if(EditorUtility.DisplayDialog("Build Helper", $"Are you sure you want to delete the branch '{branchName}'? This can not be undone.", "Yes", "No"))
                {
                    ArrayUtility.RemoveAt(ref buildHelperData.branches, list.index);
                }

                list.index = 0;
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
            _iconSettings = Resources.Load<Texture2D>("Icons/Settings-32px");
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label("<b>VR Build Helper</b>", styleRichTextLabel);

            GUILayout.FlexibleSpace();

            float iconSize = EditorGUIUtility.singleLineHeight;

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

#endif