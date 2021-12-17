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
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UdonSharpEditor;
using UnityEngine.SceneManagement;

#endif

namespace BocuD.BuildHelper
{
    [DefaultExecutionOrder(-1000)]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BuildHelperUdon : UdonSharpBehaviour
    {
        public string branchName;
        public DateTime buildDate;
        public int buildNumber;

        public bool setProgramVariable;
        public Component[] targetBehaviours;
        public string[] targetTypes;
        public string[] targetVariableNames;
        public int[] sourceEnum;

        public bool sendEvents;
        public Component[] eventBehaviours;
        public string[] eventNames;
        
        public bool useTMP;
        public TextMeshPro targetTextMeshPro;

        private void Start()
        {
            if (setProgramVariable)
            {
                if (targetBehaviours != null && targetTypes != null && targetVariableNames != null && sourceEnum != null)
                {
                    for (int index = 0; index < targetBehaviours.Length; index++)
                    {
                        if (sourceEnum[index] == 0) continue;
                        if (targetBehaviours[index] == null) continue;
                        if (targetTypes[index] == null) continue;
                        if (targetVariableNames[index] == null) continue;
                        
                        Component component = targetBehaviours[index];
                        UdonBehaviour ub = (UdonBehaviour) component;
                        switch (sourceEnum[index])
                        {
                            //branch name
                            case 1:
                                ub.SetProgramVariable(targetVariableNames[index], branchName);
                                break;
                            //build number
                            case 2:
                                object dataToWrite = buildNumber;
                                switch (targetTypes[index])
                                {
                                    case "System.int":
                                        dataToWrite = (int) dataToWrite;
                                        break;
                                    case "System.uint":
                                        dataToWrite = (uint) dataToWrite;
                                        break;
                                    case "System.long":
                                        dataToWrite = (long) dataToWrite;
                                        break;
                                    case "System.ulong":
                                        dataToWrite = (ulong) dataToWrite;
                                        break;
                                    case "System.short":
                                        dataToWrite = (short) dataToWrite;
                                        break;
                                    case "System.ushort":
                                        dataToWrite = (ushort) dataToWrite;
                                        break;
                                }
                                ub.SetProgramVariable(targetVariableNames[index], dataToWrite);
                                break;
                            //build date
                            case 3:
                                ub.SetProgramVariable(targetVariableNames[index], buildNumber);
                                break;
                        }
                    }
                }
            }
            if (useTMP)
                targetTextMeshPro.text = $"{branchName}\nBuild {buildNumber}\n{buildDate}";
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

    [CustomEditor(typeof(BuildHelperUdon))]
    public class BuildHelperUdonEditor : Editor
    {
        #region Convertable type lists
        private static Type[] convertableTypes = new Type[]
        {
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),
            typeof(DateTime),
            typeof(string),
        };
        
        private static Type[] convertableTypesString = new Type[]
        {
            typeof(string),
        };
        
        private static Type[] convertableTypesInt = new Type[]
        {
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),
        };
        
        private static Type[] convertableTypesDateTime = new Type[]
        {
            typeof(DateTime),
        };
        
        private static Type[] convertableTypesBool = new Type[]
        {
            typeof(bool),
        };

        private static Dictionary<Type, string> FancyTypeToLabel = new Dictionary<Type, string>()
        {
            {typeof(int), "<color=#9999FF>int</color>"},
            {typeof(uint), "<color=#9999FF>uint</color>"},
            {typeof(long), "<color=#9999FF>long</color>"},
            {typeof(ulong), "<color=#9999FF>ulong</color>"},
            {typeof(short), "<color=#9999FF>short</color>"},
            {typeof(ushort), "<color=#9999FF>ushort</color>"},
            {typeof(DateTime), "<color=#AAFFAA>DateTime</color>"},
            {typeof(string), "<color=#9999FF>string</color>"},
            {typeof(bool), "<color=#9999FF>bool</color>"},
        };
        
        private static Dictionary<Type, string> TypeToLabel = new Dictionary<Type, string>()
        {
            {typeof(int), "int"},
            {typeof(uint), "uint"},
            {typeof(long), "long"},
            {typeof(ulong), "ulong"},
            {typeof(short), "short"},
            {typeof(ushort), "ushort"},
            {typeof(DateTime), "DateTime"},
            {typeof(string), "string"},
            {typeof(bool), "bool"},
        };
        
        #endregion
        
        private static Dictionary<VariableInstruction.Source, Type[]> validTypesDictionary = new Dictionary<VariableInstruction.Source, Type[]>()
        {
            {VariableInstruction.Source.none, convertableTypes},
            {VariableInstruction.Source.branchName, convertableTypesString},
            {VariableInstruction.Source.buildDate, convertableTypesDateTime},
            {VariableInstruction.Source.buildNumber, convertableTypesInt},
        };

        private BuildHelperUdon inspectorBehaviour;
        private SerializedObject behaviourSO;

        private class VariableInstruction
        {
            public UdonBehaviour targetBehaviour;
            public int variableIndex;
            
            public string[] variableNames;
            public string[] FancyVariableNames;
            public Type[] variableTypes;

            public enum Source
            {
                none,
                branchName,
                buildNumber,
                buildDate,
            }

            public Source source;

            public VariableInstruction()
            {
                targetBehaviour = null;
                variableIndex = -1;
                variableNames = new string[0];
                FancyVariableNames = new string[0];
                variableTypes = new Type[0];
            }
            
            public void PrepareLabels()
            {
                string[] newLabels = new string[Math.Min(variableNames.Length, variableTypes.Length)];
                string[] fancyLabels = new string[Math.Min(variableNames.Length, variableTypes.Length)];
                for (int i = 0; i < fancyLabels.Length; i++)
                {
                    if (FancyTypeToLabel.TryGetValue(variableTypes[i], out string label))
                        fancyLabels[i] = $"{label} {variableNames[i]}";
                    else fancyLabels[i] = $"{variableTypes[i]} {variableNames[i]}";
                    if (TypeToLabel.TryGetValue(variableTypes[i], out string label2))
                        newLabels[i] = $"{label2} {variableNames[i]}";
                    else newLabels[i] = $"{variableTypes[i]} {variableNames[i]}";
                }
                VariableLabels = newLabels;
                FancyVariableNames = fancyLabels;
            }

            public string[] VariableLabels = new string[0];
        }

        private VariableInstruction[] variableInstructions;

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target) || target == null) return;

            if (target != null)
            {
                //yes this is bad and performance shit but muh undo
                inspectorBehaviour = (BuildHelperUdon) target;
                behaviourSO = new SerializedObject(inspectorBehaviour);
                variableInstructions = ImportFromUdonBehaviour();
            }
            else return;

            //DrawDefaultInspector();
            
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) {richText = true, fontSize = 15};
            EditorGUILayout.LabelField("<b>VR Build Helper Udon Link</b>", headerStyle);
            
            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUI.BeginChangeCheck();
            bool setProgramVariable = EditorGUILayout.Toggle("SetProgramVariable", inspectorBehaviour.setProgramVariable);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Change BuildHelper settings");
                inspectorBehaviour.setProgramVariable = setProgramVariable;
            }
            
            if (setProgramVariable)
            {
                //variableInstructions = ImportFromUdonBehaviour();
                EditorGUILayout.HelpBox("You can assign UdonBehaviours here to have the appropriate variables set on Start()", MessageType.Info);
                EditorGUILayout.BeginVertical("Helpbox");
                
                //begin header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target UdonBehaviour", GUILayout.Width(160));
                EditorGUILayout.LabelField("Variable to write", GUILayout.Width(100));
                EditorGUILayout.LabelField("Target location", GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    Undo.RecordObject(inspectorBehaviour, "Add new behaviour to list");
                    ArrayUtility.Add(ref variableInstructions, new VariableInstruction());
                    ExportToUdonBehaviour(variableInstructions);
                }
                EditorGUILayout.EndHorizontal();
                //end header

                for (int index = 0; index < variableInstructions.Length; index++)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUI.BeginChangeCheck();
                    VariableInstruction instruction = variableInstructions[index];
                    
                    instruction.targetBehaviour = (UdonBehaviour) EditorGUILayout.ObjectField(
                        instruction.targetBehaviour, typeof(UdonBehaviour),
                        true, GUILayout.Width(160));

                    if (EditorGUI.EndChangeCheck()) 
                    {
                        Undo.RecordObject(inspectorBehaviour, "Modify target behaviour");
                        RegenerateValidVariables(index);
                        ExportToUdonBehaviour(variableInstructions);
                    }

                    if (variableInstructions[index].targetBehaviour != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        variableInstructions[index].source =
                            (VariableInstruction.Source) EditorGUILayout.EnumPopup(variableInstructions[index].source,
                                GUILayout.Width(100));

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(inspectorBehaviour, "Modify source variable");
                            ExportToUdonBehaviour(variableInstructions);
                        }

                        if (variableInstructions[index].source != VariableInstruction.Source.none)
                        {
                            RegenerateValidVariables(index);

                            EditorGUI.BeginChangeCheck();
                            GUIStyle popupStyle = new GUIStyle(EditorStyles.popup)
                            {
                                richText = true,
                                normal = {textColor = new Color(1, 1, 1, 0)},
                                hover = {textColor = new Color(1, 1, 1, 0)},
                                focused = {textColor = new Color(1, 1, 1, 0)}
                            };

                            variableInstructions[index].variableIndex = EditorGUILayout.Popup(
                                variableInstructions[index].variableIndex, variableInstructions[index].VariableLabels,
                                popupStyle);

                            Rect labelRect = GUILayoutUtility.GetLastRect();
                            labelRect.x += 2;
                            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) {richText = true};
                            if (variableInstructions[index].variableIndex != -1)
                            {
                                GUI.Label(labelRect, variableInstructions[index]
                                    .FancyVariableNames[variableInstructions[index].variableIndex], labelStyle);
                            }

                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(inspectorBehaviour, "Modify target variable");
                                ExportToUdonBehaviour(variableInstructions);
                            }
                        }
                    }
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        Undo.RecordObject(inspectorBehaviour, "Remove behaviour from behaviour list");
                        ArrayUtility.RemoveAt(ref variableInstructions, index);
                        ExportToUdonBehaviour(variableInstructions);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUI.BeginChangeCheck();
            bool sendEvents = EditorGUILayout.Toggle("Send Events", inspectorBehaviour.sendEvents);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Change BuildHelper Event settings");
                inspectorBehaviour.sendEvents = sendEvents;
            }

            if (sendEvents)
            {
                
            }
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUI.BeginChangeCheck();
            bool useTMP = EditorGUILayout.Toggle("Print to TextMeshPro", inspectorBehaviour.useTMP);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Change BuildHelper TMP settings");
                inspectorBehaviour.useTMP = useTMP;
            }
            
            if (useTMP)
            {
                EditorGUI.BeginChangeCheck();
                behaviourSO.Update();
                EditorGUILayout.ObjectField(behaviourSO.FindProperty("targetTextMeshPro"));
                behaviourSO.ApplyModifiedProperties();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspectorBehaviour, "Change target TMP");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void RegenerateValidVariables(int index)
        {
            GetValidVariables(variableInstructions[index].targetBehaviour, variableInstructions[index].source,
                out List<string> vars, out List<Type> types);

            string[] newValidNames = vars.ToArray();
            Type[] newValidTypes = types.ToArray();

            int newVariableIndex = variableInstructions[index].variableIndex < 0
                ? -1
                : Array.IndexOf(newValidNames,
                    variableInstructions[index].variableNames[variableInstructions[index].variableIndex]);

            if (newVariableIndex < 0)
                variableInstructions[index].variableIndex = -1;
            else
                variableInstructions[index].variableIndex =
                    newValidTypes[newVariableIndex] == variableInstructions[index]
                        .variableTypes[variableInstructions[index].variableIndex]
                        ? newVariableIndex
                        : -1;

            variableInstructions[index].variableNames = newValidNames;
            variableInstructions[index].variableTypes = newValidTypes;
            variableInstructions[index].PrepareLabels();

            ExportToUdonBehaviour(variableInstructions);
        }

        private VariableInstruction[] ImportFromUdonBehaviour()
        {
            inspectorBehaviour.UpdateProxy();

            VariableInstruction[] output = new VariableInstruction[0];
            
            for (int index = 0; index < inspectorBehaviour.targetBehaviours.Length; index++)
            {
                UdonBehaviour behaviour = (UdonBehaviour)inspectorBehaviour.targetBehaviours[index];

                GetValidVariables(behaviour, (VariableInstruction.Source) inspectorBehaviour.sourceEnum[index],
                    out List<string> vars, out List<Type> types);

                string[] newValidNames = vars.ToArray();
                Type[] newValidTypes = types.ToArray();
                int variableIndex = -1;

                if (inspectorBehaviour.targetVariableNames.Length > index)
                    variableIndex = Array.IndexOf(newValidNames, inspectorBehaviour.targetVariableNames[index]);

                VariableInstruction newInstruction = new VariableInstruction()
                {
                    targetBehaviour = behaviour,
                    variableNames = newValidNames,
                    variableTypes = newValidTypes,
                    variableIndex = variableIndex,
                    source = (VariableInstruction.Source)inspectorBehaviour.sourceEnum[index]
                };
                newInstruction.PrepareLabels();
                
                ArrayUtility.Add(ref output, newInstruction);
            }
            
            return output;
        }

        private void ExportToUdonBehaviour(VariableInstruction[] toExport)
        {
            inspectorBehaviour.UpdateProxy();
            inspectorBehaviour.targetBehaviours = new Component[toExport.Length];
            inspectorBehaviour.targetTypes = new string[toExport.Length];
            inspectorBehaviour.targetVariableNames = new string[toExport.Length];
            inspectorBehaviour.sourceEnum = new int[toExport.Length];

            for (int index = 0; index < toExport.Length; index++)
            {
                VariableInstruction instruction = toExport[index];
                if (instruction.targetBehaviour == null) continue;
                
                inspectorBehaviour.targetBehaviours[index] = instruction.targetBehaviour;
                inspectorBehaviour.sourceEnum[index] = (int)instruction.source;
                if (instruction.variableIndex == -1) continue;
                
                inspectorBehaviour.targetTypes[index] = instruction.variableTypes[instruction.variableIndex].ToString();
                inspectorBehaviour.targetVariableNames[index] = instruction.variableNames[instruction.variableIndex];
            }

            inspectorBehaviour.ApplyProxyModifications();
        }

        private void GetValidVariables(UdonBehaviour udon, VariableInstruction.Source source, out List<string> vars, out List<Type> types)
        {
            vars = new List<string>();
            types = new List<Type>();
            if (udon == null) return;

            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable =
                udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            List<string> programVariablesNames = symbolTable.GetSymbols().ToList();
            List<KeyValuePair<string, Type>> toSort = new List<KeyValuePair<string, Type>>();

            foreach (string variableName in programVariablesNames)
            {
                if (variableName.StartsWith("__")) continue;

                Type variableType = symbolTable.GetSymbolType(variableName);

                validTypesDictionary.TryGetValue(source, out Type[] validTypesArray);
                if (validTypesArray == null) continue;
                
                int typeIndex = Array.IndexOf(validTypesArray, variableType);
                if (typeIndex > -1)
                {
                    toSort.Add(new KeyValuePair<string, Type>(variableName, variableType));
                }
            }

            List<KeyValuePair<string, Type>> sorted = toSort.OrderBy(kvp => kvp.Key).ToList();

            foreach (KeyValuePair<string, Type> item in sorted)
            {
                vars.Add(item.Key);
                types.Add(item.Value);
            }
        }
    }
    
    [InitializeOnLoadAttribute]
    public static class BuildHelperUdonPlaymodeStateWatcher
    {
        // register an event handler when the class is initialized
        static BuildHelperUdonPlaymodeStateWatcher()
        {
            EditorApplication.playModeStateChanged += PlayModeStateUpdate;
        }

        private static void PlayModeStateUpdate(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                if (UnityEngine.Object.FindObjectOfType<BuildHelperData>() == null) return;
                
                BuildHelperData buildHelperData = UnityEngine.Object.FindObjectOfType<BuildHelperData>();

                if (buildHelperData.currentBranch.hasUdonLink)
                {
                    if (buildHelperData.linkedBehaviourGameObject != null)
                    {
                        BuildHelperUdon buildHelperUdon = buildHelperData.linkedBehaviourGameObject
                            .GetUdonSharpComponent<BuildHelperUdon>();
                        
                        buildHelperUdon.UpdateProxy();
                        buildHelperUdon.branchName = buildHelperData.currentBranch.name;
                        buildHelperUdon.buildDate = DateTime.Now;
#if UNITY_ANDROID
                        buildHelperUdon.buildNumber = buildHelperData.currentBranch.buildData.androidBuildVersion;
#else
                        buildHelperUdon.buildNumber = buildHelperData.currentBranch.buildData.pcBuildVersion;
#endif
                        buildHelperUdon.ApplyProxyModifications();
                    }
                }
                else
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
            }
        }
    }
#endif
}