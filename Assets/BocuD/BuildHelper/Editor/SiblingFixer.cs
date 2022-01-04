using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BocuD.BuildHelper.Editor
{
    public static class SiblingFixer
    {
        [MenuItem("Tools/Build Helper/Fix sibling transforms")]
        public static void FixSiblings()
        {
            bool doit = EditorUtility.DisplayDialog("VRBuildHelper",
                "This will scan your scene for any Transforms sharing the same path and rename them to fix it.", "Continue", "Cancel");

            if (!doit) return;
        
            float progress = 0;
            EditorUtility.DisplayProgressBar("Memes", "Doing the shit", progress);

            GameObject[] GameObjectList = Object.FindObjectsOfType<GameObject>();
            int goCount = GameObjectList.Length;
            int fixedCount = 0;
        
            try
            {
                for (int index = 0; index < goCount; index++)
                {
                    GameObject go = GameObjectList[index];
                
                    progress = (float)index / goCount;
                    EditorUtility.DisplayProgressBar("VRBuildHelper", "Fixing sibling transform names..", progress);

                    if (go.transform.parent == null) continue;
                
                    for (int idx = 0; idx < go.transform.parent.childCount; ++idx)
                    {
                        Transform t = go.transform.parent.GetChild(idx);
                        if (t == go.transform)
                            continue;

                        if (t.name != go.transform.name) continue;

                        string path = t.name;
                        Transform p = t.parent;
                        while (p != null)
                        {
                            path = p.name + "/" + path;
                            p = p.parent;
                        }

                        List<Object> gos = new List<Object>();
                        for (int c = 0; c < go.transform.parent.childCount; ++c)
                            if (go.transform.parent.GetChild(c).name == go.name)
                                gos.Add(go.transform.parent.GetChild(c).gameObject);
                    
                        for (int i = 0; i < gos.Count; ++i)
                            gos[i].name = gos[i].name + "-" + i.ToString("00");
                    }

                    fixedCount++;
                }

                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("VRBuildHelper", $"Scanned {goCount} Transforms, fixed {fixedCount} names",
                    "Ok");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
