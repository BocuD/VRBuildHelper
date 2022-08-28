using System;
using System.Collections.Generic;
using BocuD.VRChatApiTools;
using UnityEditor;
using UnityEngine;
using VRC.Core;

namespace BocuD.BuildHelper.Editor
{
    public class CommunityLabsPublisher : EditorWindow
    {
        public static void PublishWorld(ApiWorld target)
        {
            CommunityLabsPublisher publisher = CreateInstance<CommunityLabsPublisher>();
            publisher.world = target;
            publisher.publish = true;
            publisher.minSize = new Vector2(400, 300);
            publisher.currentState = CurrentState.fetching;
            publisher.CheckTrustLevel();
            publisher.ShowUtility();
        }

        public static void UnpublishWorld(ApiWorld target)
        {
            CommunityLabsPublisher publisher = CreateInstance<CommunityLabsPublisher>();
            publisher.world = target;
            publisher.publish = false;
            publisher.minSize = new Vector2(440, 300);
            publisher.currentState = CurrentState.waiting;
            publisher.ShowUtility();
        }
        
        private ApiWorld world;
        private bool publish;
        private string errorString;

        private enum CurrentState
        {
            fetching,
            waiting,
            active,
            failed,
            finished
        }

        private CurrentState currentState;

        private void OnGUI()
        {
            if (world == null)
            {
                DestroyImmediate(this);
            }
            
            EditorGUILayout.LabelField("Community Labs Publisher", new GUIStyle (EditorStyles.label) {fontSize = 20});

            switch (currentState)
            {
                case CurrentState.fetching:
                    EditorGUILayout.LabelField($"Fetching community labs status for {APIUser.CurrentUser.displayName}...");
                    VRChatApiToolsGUI.DrawBlueprintInspector(world);
                    break;
                
                case CurrentState.waiting:
                    if (publish)
                    {
                        EditorGUILayout.LabelField("You are about to publish the following world to community labs.");
                        VRChatApiToolsGUI.DrawBlueprintInspector(world);
                        EditorGUILayout.HelpBox("Publishing this world will move it to Community Labs.\n" +
                                                "Other users will be able to see your world, and it will either stay in Labs, get promoted to Public status, or be set back to Private, based on community response. " +
                                                "Make sure your world follows the VRChat Community Guidelines.\n" +
                                                "Publishing to community labs can only be done once a week.",
                            MessageType.Info);

                        if (APIUser.CurrentUser.hasKnownTrustLevel)
                        {
                            bool allowed = APIUser.CurrentUser.hasKnownTrustLevel && canPublishToLabs;

                            using (new EditorGUI.DisabledScope(!allowed))
                            {
                                if (GUILayout.Button(new GUIContent("Publish this world to community labs",
                                        canPublishToLabs
                                            ? ""
                                            : "You seem to have already published a world during this week. Try again later!")))
                                {
                                    currentState = CurrentState.active;

                                    world.PublishWorldToCommunityLabs(
                                        container => { currentState = CurrentState.finished; }, error =>
                                        {
                                            errorString = error;
                                            currentState = CurrentState.failed;
                                        });
                                }
                            }
                        }
                        else
                        {
                            EditorGUILayout.HelpBox(
                                $"The currently logged in user ({APIUser.CurrentUser.displayName}) doesn't have the required trust level to be able to publish a world to community labs.",
                                MessageType.Error);

                            using (new EditorGUI.DisabledScope(true))
                            {
                                if (GUILayout.Button(new GUIContent("Publish this world to community labs", "You don't have the required trust rank to publish a world to community labs")))
                                {
                                }
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField("You are about to unpublish the following world.");
                        VRChatApiToolsGUI.DrawBlueprintInspector(world);
                        EditorGUILayout.HelpBox("Unpublishing this world will remove it from Public.\n" +
                                                "Other users will not be able to see it anymore or create public instances. If you want to make it public again, it will need to go through Community Labs again as well.\n" +
                                                "You're only allowed to publish one world per week, and when you remove a world from the labs, you don't get your one world back. Are you sure?",
                            MessageType.Warning);
                        if (GUILayout.Button("Unpublish this world"))
                        {
                            currentState = CurrentState.active;
                            
                            world.UnPublishWorld(container =>
                            {
                                currentState = CurrentState.finished;
                            }, error =>
                            {
                                errorString = error;
                                currentState = CurrentState.failed;
                            });
                        }
                    }
                    
                    break;

                case CurrentState.active:
                    EditorGUILayout.LabelField("Please wait...");
                    VRChatApiToolsGUI.DrawBlueprintInspector(world);
                    break;
                
                case CurrentState.failed:
                    EditorGUILayout.LabelField(publish ? $"Publishing failed: {errorString}" : $"Unpublishing world failed: {errorString}");
                    VRChatApiToolsGUI.DrawBlueprintInspector(world);
                    break;
                
                case CurrentState.finished:
                    EditorGUILayout.LabelField(publish ? "Your world was successfully published to community labs!" : "Your world was successfully unpublished.");
                    VRChatApiToolsGUI.DrawBlueprintInspector(world);
                    break;
            }
        }

        private bool canPublishToLabs;
        
        private void CheckTrustLevel()
        {
            bool canPublish;
            
            APIUser.FetchPublishWorldsInformation(
                dict =>
                {
                    try
                    {
                        if (dict["canPublish"].Type == BestHTTP.JSON.Json.TokenType.Boolean)
                        {
                            canPublish = (bool) dict["canPublish"];
                        }
                        else
                            canPublish = false;
                    }
                    catch (Exception)
                    {
                        canPublish = false;
                    }

                    canPublishToLabs = canPublish;
                    currentState = CurrentState.waiting;
                },
                error =>
                {
                    errorString = "Couldn't fetch user publishing rights: " + error;
                    currentState = CurrentState.failed;
                }
            );
        }
    }
}