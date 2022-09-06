using System;
using BocuD.BuildHelper;
using BuildHelper.Runtime;
using UnityEditor;
using UnityEngine;

namespace BuildHelper.Editor
{
    public class DiscordWebhookEditor : EditorWindow
    {
        private DiscordWebhookPublish.DiscordWebhookData data;
        private SerializedObject dataObject;
        private SerializedProperty discordDataProperty;
        private Branch branch;
        
        public static void OpenEditor(BuildHelperData data, Branch branch)
        {
            DiscordWebhookEditor window = GetWindow<DiscordWebhookEditor>();
            
            window.titleContent = new GUIContent("Discord Webhook Settings");
            window.dataObject = new SerializedObject(data);
            SerializedProperty dataObject = window.dataObject.FindProperty("dataObject");
            SerializedProperty branchProperty = dataObject.FindPropertyRelative("branches").GetArrayElementAtIndex(Array.IndexOf(data.dataObject.branches, branch));
            window.discordDataProperty = branchProperty.FindPropertyRelative("webhookSettings");
            window.branch = branch;
            window.data = branch.webhookSettings;
        }

        private void OnGUI()
        {
            dataObject.Update();
            EditorGUILayout.PropertyField(discordDataProperty);
            dataObject.ApplyModifiedProperties();

            if (GUILayout.Button("Send Test Message"))
            {
                DiscordWebhookPublish.SendPublishedMessage(branch, "**___This is a test message, please ignore___**\n" + data.message);
            }
        }
    }
}