#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

namespace BocuD.VRChatApiTools
{
    public static class VRChatApiTools
    {
        public static Dictionary<string, Texture2D> ImageCache = new Dictionary<string, Texture2D>();

        public static List<ApiWorld> uploadedWorlds;
        public static List<ApiAvatar> uploadedAvatars;
        
        [NonSerialized] public static List<string> currentlyFetching = new List<string>();
        [NonSerialized] public static List<string> currentlyFetchingAvatars = new List<string>();

        [NonSerialized] public static List<string> invalidWorlds = new List<string>();
        [NonSerialized] public static List<string> invalidAvatars = new List<string>();

        [NonSerialized] public static Dictionary<string, ApiWorld> worldCache = new Dictionary<string, ApiWorld>();
        [NonSerialized] public static Dictionary<string, ApiAvatar> avatarCache = new Dictionary<string, ApiAvatar>();

        public static Action<string, string> DownloadImage;
        
        #region Editor Tools Menu

        [MenuItem("Tools/VRChatApiTools/Clear caches")]
        public static void MIClearCaches()
        {
            ClearCaches();
        }

        [MenuItem("Tools/VRChatApiTools/Attempt login")]
        public static void MILoginAttempt()
        {
            if (APIUser.IsLoggedIn)
            {
                Logger.Log("You are already logged in.");
                return;
            }
            
            Logger.Log("Attempting login...");
            
            VRCLogin.AttemptLogin(
                c =>
                {
                    Logger.Log($"Succesfully logged in as user: {((APIUser)c.Model).displayName}");
                },
                        
                c =>
                {
                    Logger.LogError("Automatic login failed");
                    autoLoginFailed = true;
                });
        }
        
        #endregion
        
        public static void ClearCaches()
        {
            uploadedWorlds = null;
            uploadedAvatars = null;

            ImageCache.Clear();

            currentlyFetching.Clear();
            currentlyFetchingAvatars.Clear();

            invalidWorlds.Clear();
            invalidAvatars.Clear();

            worldCache.Clear();
            avatarCache.Clear();
        }

        public static bool TryGetApiWorld(string blueprintID, out ApiWorld apiWorld)
        {
            if (!worldCache.TryGetValue(blueprintID, out apiWorld))
            {
                if (!invalidWorlds.Contains(blueprintID))
                {
                    FetchApiWorld(blueprintID);
                    return true;
                }

                return false;
            }
            
            return true;
        }

        public static void FetchApiWorld(string blueprintID)
        {
            if (currentlyFetching.Contains(blueprintID)) return;

            currentlyFetching.Add(blueprintID);
            
            ApiWorld world = API.FromCacheOrNew<ApiWorld>(blueprintID);
            
            world.Fetch(null,
                c => AddWorldToCache(blueprintID, c.Model as ApiWorld),
                c =>
                {
                    if (c.Code == 404)
                    {
                        currentlyFetching.Remove(world.id);
                        invalidWorlds.Add(world.id);
                        Logger.Log($"World '{blueprintID}' doesn't exist so couldn't be loaded.");
                        ApiCache.Invalidate<ApiWorld>(blueprintID);
                    }
                    else
                        currentlyFetching.Remove(world.id);
                });
        }

        public static async Task<ApiWorld> FetchApiWorldAsync(string blueprintID)
        {
            ApiWorld world = API.Fetch<ApiWorld>(blueprintID);
            ApiContainer result = new ApiContainer();
            bool wait = true;
            
            world.Fetch(null,
                c =>
                {
                    result = c;
                    wait = false;
                },
                c =>
                {
                    if (c.Code == 404)
                    {
                        Logger.Log($"World '{blueprintID}' doesn't exist so couldn't be loaded.");
                        ApiCache.Invalidate<ApiWorld>(blueprintID);
                    }
                    else
                    {
                        Logger.LogError(c.Error);
                    }
                    
                    result = c;
                    wait = false;
                });

            while (wait)
            {
                await Task.Delay(100);
            }

            return result.Model as ApiWorld;
        }

        public static void FetchApiAvatar(string blueprintID)
        {
            if (currentlyFetchingAvatars.Contains(blueprintID)) return;

            currentlyFetchingAvatars.Add(blueprintID);

            ApiAvatar avatar = API.FromCacheOrNew<ApiAvatar>(blueprintID);

            avatar.Fetch(c => AddAvatarToCache(blueprintID, c.Model as ApiAvatar),
                c =>
                {
                    if (c.Code == 404)
                    {
                        currentlyFetchingAvatars.Remove(avatar.id);
                        invalidAvatars.Add(avatar.id);
                        Logger.Log($"Avatar '{blueprintID}' doesn't exist so couldn't be loaded.");
                        ApiCache.Invalidate<ApiAvatar>(blueprintID);
                    }
                    else
                        currentlyFetchingAvatars.Remove(avatar.id);
                });
        }
        
        public static async Task<ApiContainer> FetchApiAvatarAsync(string blueprintID)
        {
            ApiAvatar avatar = API.Fetch<ApiAvatar>(blueprintID);
            ApiContainer result = new ApiContainer();
            bool wait = true;
            
            avatar.Fetch(
                c =>
                {
                    result = c;
                    wait = false;
                },
                c =>
                {
                    if (c.Code == 404)
                    {
                        Logger.Log($"Avatar '{blueprintID}' doesn't exist so couldn't be loaded.");
                        ApiCache.Invalidate<ApiAvatar>(blueprintID);
                    }
                    
                    result = c;
                    wait = false;
                });

            while (wait)
            {
                await Task.Delay(100);
            }

            return result;
        }

        private static void AddWorldToCache(string blueprintID, ApiWorld world)
        {
            currentlyFetching.Remove(world.id);
            worldCache.Add(blueprintID, world);
            
            DownloadImage?.Invoke(blueprintID, world.thumbnailImageUrl);
        }

        private static void AddAvatarToCache(string blueprintID, ApiAvatar avatar)
        {
            currentlyFetchingAvatars.Remove(avatar.id);
            avatarCache.Add(blueprintID, avatar);
            
            DownloadImage?.Invoke(blueprintID, avatar.thumbnailImageUrl);
        }

        public static bool autoLoginFailed;
        public static void TryAutoLogin([CanBeNull]Action onSucces = null)
        {
            if (!APIUser.IsLoggedIn)
            {
                if (!autoLoginFailed)
                {
                    VRCLogin.AttemptLogin(
                        c =>
                        {
                            Logger.Log($"Succesfully logged in as user: {((APIUser)c.Model).displayName}");
                            onSucces?.Invoke();
                        },
                        
                        c =>
                        {
                            Logger.LogError($"Automatic login failed: {c.Error}");
                            autoLoginFailed = true;
                        });
                }
            }
            else
            {
                autoLoginFailed = false;
            }
        }
        
        public static async Task<bool> TryAutoLoginAsync()
        {
            bool succes = false;
            bool wait = true;
            
            if (!APIUser.IsLoggedIn)
            {
                if (!autoLoginFailed)
                {
                    VRCLogin.AttemptLogin(
                        c =>
                        {
                            Logger.Log($"Succesfully logged in as user: {((APIUser)c.Model).displayName}");
                            succes = true;
                            wait = false;
                        },
                        
                        c =>
                        {
                            Logger.LogError($"Automatic login failed: {c.Error}");
                            autoLoginFailed = true;
                            wait = false;
                        });
                }
            }
            else
            {
                autoLoginFailed = false;
                succes = true;
                wait = false;
            }

            while (wait)
            {
                await Task.Delay(100);
            }

            return succes;
        }
        
        public static void SetupWorldData(List<ApiWorld> worlds)
        {
            if (worlds == null || uploadedWorlds == null)
                return;

            worlds.RemoveAll(w => w?.name == null || uploadedWorlds.Any(w2 => w2.id == w.id));

            if (worlds.Count <= 0) return;
            
            uploadedWorlds.AddRange(worlds);
            
            foreach (ApiWorld world in uploadedWorlds)
            {
                if (!worldCache.TryGetValue(world.id, out ApiWorld test))
                {
                    worldCache.Add(world.id, world);
                }
            }
        }

        public static void SetupAvatarData(List<ApiAvatar> avatars)
        {
            if (avatars == null || uploadedAvatars == null)
                return;

            avatars.RemoveAll(a => a?.name == null || uploadedAvatars.Any(a2 => a2.id == a.id));

            if (avatars.Count <= 0) return;
            
            uploadedAvatars.AddRange(avatars);

            foreach (ApiAvatar avatar in uploadedAvatars)
            {
                if (!avatarCache.TryGetValue(avatar.id, out ApiAvatar test))
                {
                    avatarCache.Add(avatar.id, avatar);
                }
            }
        }
        
        public static PipelineManager FindPipelineManager()
        {
            Scene currentScene = SceneManager.GetActiveScene();

            VRCSceneDescriptor[] sceneDescriptors = Object.FindObjectsOfType<VRCSceneDescriptor>()
                .Where(x => x.gameObject.scene == currentScene).ToArray();

            if (sceneDescriptors.Length == 0) return null;
            
            if (sceneDescriptors.Length == 1)
                return sceneDescriptors[0].GetComponent<PipelineManager>();
            if (sceneDescriptors.Length > 1)
            {
                Logger.LogError("Multiple scene descriptors found. Make sure you only have one scene descriptor.");
                return sceneDescriptors[0].GetComponent<PipelineManager>();
            }

            return null;
        }
        
        public static string DisplayTags(List<string> tags)
        {
            string output = "";
            
            foreach (string tag in tags)
            {
                output += $"{tag.ReplaceFirst("author_tag_", "")}, ";
            }

            if (output.Contains(", "))
                output = output.Substring(0, output.Length - 2);
            
            return output;
        }
        
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }
        
        [Serializable]
        public class WorldInfo
        {
            public string name = "";
            public string description = "";
            public List<string> tags = new List<string>();
            public int capacity;

            public string blueprintID = "";

            public string newImagePath = "";
        }
        
        public enum Platform
        {
            Windows,
            Android,
            unknown
        }

        public static Platform CurrentPlatform()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            switch (target)
            {
                case BuildTarget.Android:
                    return Platform.Android;
                
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return Platform.Windows;
                
                default:
                    return Platform.unknown;
            }
        }

        public static string ToApiString(this Platform input)
        {
            switch (input)
            {
                case Platform.Windows:
                    return "standalonewindows";
                case Platform.Android:
                    return "android";
                default:
                    return "unknownplatform";
            }
        }

        public static string GetFriendlyAvatarFileName(string type, string blueprintID, Platform platform) =>
            $"Avatar - {blueprintID} - {type} - {Application.unityVersion}_{ApiWorld.VERSION.ApiVersion}_{platform.ToApiString()}_{API.GetServerEnvironmentForApiUrl()}";

        public static string GetFriendlyWorldFileName(string type, ApiWorld apiWorld, Platform platform) =>
            $"World - {apiWorld.name} - {type} - {Application.unityVersion}_{ApiWorld.VERSION.ApiVersion}_{platform.ToApiString()}_{API.GetServerEnvironmentForApiUrl()}";
    }
}

#endif