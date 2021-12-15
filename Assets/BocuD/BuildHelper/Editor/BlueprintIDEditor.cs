using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Core;

namespace BocuD.BuildHelper.Editor
{
    using VRChatApiTools;
    
    public class BlueprintIDEditor : EditorWindow
    {
        private static string searchString = "";
        
        private BuildHelperData data;
        private Branch branch;

        public static BlueprintIDEditor SpawnEditor(BuildHelperData data, Branch branch)
        {
            BlueprintIDEditor window = (BlueprintIDEditor) GetWindow(typeof(BlueprintIDEditor), true);

            window.titleContent = new GUIContent("Blueprint ID Editor");
            window.maxSize = new Vector2(600, 500);
            window.minSize = window.maxSize;
            window.autoRepaintOnSceneChange = true;
            window.data = data;
            window.branch = branch;
            window.newID = branch.blueprintID;

            window.Show();
            window.Repaint();

            return window;
        }

        private bool dirty;
        private string newID;
        
        private void OnGUI()
        {
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) {richText = true};
            EditorGUILayout.LabelField($"'{branch.name}' Blueprint ID Editor", headerStyle);
            EditorGUILayout.BeginHorizontal();

            dirty = newID != branch.blueprintID;
            
            if (dirty)
            {
                GUIStyle richText = new GUIStyle(GUI.skin.label) {richText = true};
                EditorGUILayout.LabelField("<color=yellow>Blueprint ID: </color>", richText, GUILayout.Width(100));
                newID = EditorGUILayout.TextField(newID);
            }
            else
            {
                EditorGUILayout.LabelField("Blueprint ID: ", GUILayout.Width(100));
                newID = EditorGUILayout.TextField(newID);
            }
            
            EditorGUI.BeginDisabledGroup(!dirty);
            if (GUILayout.Button($"Apply and save", GUILayout.Width(120)))
            {
                branch.blueprintID = newID;
                
                //make sure these get cached again
                branch.cachedName = "";
                branch.cachedDescription = "";
                branch.cachedCap = -1;
                branch.cachedTags = new List<string>();
                
                branch.editedName = "notInitialised";
                branch.editedDescription = "notInitialised";
                branch.editedCap = -1;
                branch.editedTags = new List<string>();
                
                BuildHelperWindow.SwitchBranch(data, Array.IndexOf(data.branches, branch));
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            DisplayWorldList();
        }

        private void DisplayWorldList()
        {
            if (!VRChatApiToolsEditor.HandleLogin()) return;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("VRChat World List", EditorStyles.boldLabel);
            
            if (VRChatApiTools.uploadedWorlds == null)
            {
                VRChatApiTools.uploadedWorlds = new List<ApiWorld>();

                EditorCoroutine.Start(VRChatApiTools.FetchUploadedData());
            }

            if (VRChatApiTools.fetchingWorlds != null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Fetching data from VRChat Api");
            }
            else
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", GUILayout.Width(120)))
                {
                    VRChatApiTools.RefreshData();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            RenderListContents();
        }
        
        private Vector2 worldListScroll;

        private void RenderListContents()
        {
            if (VRChatApiTools.uploadedWorlds == null) return;
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Uploaded worlds", EditorStyles.boldLabel, GUILayout.Width(110));

            float searchFieldShrinkOffset = searchString == "" ? 0 : 20f;
            
            GUILayoutOption layoutOption = (GUILayout.Width(position.width - searchFieldShrinkOffset));
            searchString = EditorGUILayout.TextField(searchString, GUI.skin.FindStyle("SearchTextField"), layoutOption);
            
            GUIStyle searchButtonStyle = searchString == string.Empty
                ? GUI.skin.FindStyle("SearchCancelButtonEmpty")
                : GUI.skin.FindStyle("SearchCancelButton");
            
            if (GUILayout.Button("", searchButtonStyle))
            {
                searchString = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            worldListScroll = EditorGUILayout.BeginScrollView(worldListScroll, GUILayout.Width(position.width));

            List<ApiWorld> displayedWorlds = VRChatApiTools.uploadedWorlds.OrderByDescending(x => x.updated_at).ToList();
            
            if (displayedWorlds.Count > 0)
            {
                foreach (ApiWorld w in displayedWorlds)
                {
                    if (!w.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                    {
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(108));
                    EditorGUILayout.BeginHorizontal();

                    if (VRChatApiTools.ImageCache.ContainsKey(w.id))
                    {
                        GUILayout.Box(VRChatApiTools.ImageCache[w.id], GUILayout.Width(128), GUILayout.Height(99));
                    }
                    else
                    {
                        GUILayout.Box("Loading image...", GUILayout.Width(128), GUILayout.Height(99));
                    }

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(w.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(w.id);

                    EditorGUILayout.LabelField("Release Status: " + w.releaseStatus);

                    GUILayout.FlexibleSpace();
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Copy ID to branch", GUILayout.Width(140)))
                    {
                        newID = w.id;
                    }
                    
                    if (GUILayout.Button("Copy ID to clipboard", GUILayout.Width(160)))
                    {
                        GUIUtility.systemCopyBuffer = w.id;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}