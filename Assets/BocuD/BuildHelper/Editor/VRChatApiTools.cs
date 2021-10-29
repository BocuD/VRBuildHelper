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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

namespace BocuD.BuildHelper.Editor
{
    public static class VRChatApiTools
    {
        public static List<ApiWorld> uploadedWorlds = null;
        public static Dictionary<string, Texture2D> ImageCache = new Dictionary<string, Texture2D>();
        public static EditorCoroutine fetchingWorlds = null;
        
        [NonSerialized] public static List<string> currentlyFetching = new List<string>();
        [NonSerialized] public static List<string> invalidWorlds = new List<string>();
        [NonSerialized] public static Dictionary<string, ApiWorld> worldCache = new Dictionary<string, ApiWorld>();
        
        public static void RefreshData()
        {
            uploadedWorlds = null;
            ImageCache.Clear();
            currentlyFetching.Clear();
            invalidWorlds.Clear();
            worldCache.Clear();
        }

        public static IEnumerator FetchUploadedData()
        {
            if (!ConfigManager.RemoteConfig.IsInitialized())
                ConfigManager.RemoteConfig.Init();

            if (!APIUser.IsLoggedIn)
                yield break;

            ApiCache.ClearResponseCache();
            VRCCachedWebRequest.ClearOld();

            if (fetchingWorlds == null)
                fetchingWorlds = EditorCoroutine.Start(() => FetchWorlds());
        }

        public static void FetchWorlds(int offset = 0)
        {
            ApiWorld.FetchList(
                delegate(IEnumerable<ApiWorld> worlds)
                {
                    if (worlds.FirstOrDefault() != null)
                        fetchingWorlds = EditorCoroutine.Start(() =>
                        {
                            var list = worlds.ToList();
                            int count = list.Count;
                            SetupWorldData(list);
                            FetchWorlds(offset + count);
                        });
                    else
                    {
                        fetchingWorlds = null;

                        foreach (ApiWorld w in uploadedWorlds)
                            DownloadImage(w.id, w.thumbnailImageUrl);
                    }
                },
                delegate(string obj)
                {
                    Logger.LogError("Couldn't fetch world list:\n" + obj);
                    fetchingWorlds = null;
                },
                ApiWorld.SortHeading.Updated,
                ApiWorld.SortOwnership.Mine,
                ApiWorld.SortOrder.Descending,
                offset,
                20,
                "",
                null,
                null,
                null,
                "",
                ApiWorld.ReleaseStatus.All,
                null,
                null,
                true,
                false);
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

        public static void SetupWorldData(List<ApiWorld> worlds)
        {
            if (worlds == null || uploadedWorlds == null)
                return;

            worlds.RemoveAll(w => w == null || w.name == null || uploadedWorlds.Any(w2 => w2.id == w.id));

            if (worlds.Count > 0)
            {
                uploadedWorlds.AddRange(worlds);
                foreach (ApiWorld world in uploadedWorlds)
                {
                    if (!worldCache.TryGetValue(world.id, out ApiWorld test))
                    {
                        worldCache.Add(world.id, world);
                    }
                }
            }
        }

        public static void DownloadImage(string blueprintID, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (ImageCache.ContainsKey(blueprintID) && ImageCache[blueprintID] != null) return;

            EditorCoroutine.Start(VRCCachedWebRequest.Get(url, succes));

            void succes(Texture2D texture)
            {
                if (texture != null)
                {
                    ImageCache[blueprintID] = texture;
                }
                else if (ImageCache.ContainsKey(blueprintID))
                {
                    ImageCache.Remove(blueprintID);
                }
            }
        }
        
        public static void FetchApiWorld(string blueprintID)
        {
            if (currentlyFetching.Contains(blueprintID)) return;
            
            currentlyFetching.Add(blueprintID);
            ApiWorld world = API.FromCacheOrNew<ApiWorld>(blueprintID);
            world.Fetch(null,
                (c) => AddWorldToCache(blueprintID, c.Model as ApiWorld),
                (c) =>
                {
                    if (c.Code == 404)
                    {
                        currentlyFetching.Remove(world.id);
                        invalidWorlds.Add(world.id);
                        VRC.Core.Logger.Log($"Could not load world {blueprintID} because it didn't exist.",
                            DebugLevel.All);
                        ApiCache.Invalidate<ApiWorld>(blueprintID);
                    }
                    else
                        currentlyFetching.Remove(world.id);
                });
        }

        private static void AddWorldToCache(string blueprintID, ApiWorld world)
        {
            currentlyFetching.Remove(world.id);
            worldCache.Add(blueprintID, world);
            DownloadImage(blueprintID, world.thumbnailImageUrl);
        }

        public static async void TryAutoLogin(EditorWindow repaintOnSucces = null)
        {
            VRCSdkControlPanel controlPanel = EditorWindow.GetWindow<VRCSdkControlPanel>();
            for (int i = 0; i < 50; i++) {
                if (APIUser.IsLoggedIn)
                {
                    controlPanel.Close();
                    if(repaintOnSucces != null) repaintOnSucces.Repaint();
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(0.1f));
            }
            Logger.Log("Timed out waiting for automatic login");
        }
    }
}