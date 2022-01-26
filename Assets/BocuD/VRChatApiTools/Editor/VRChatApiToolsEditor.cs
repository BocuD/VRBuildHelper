using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    [InitializeOnLoad]
    public static class VRChatApiToolsEditor
    {
        public static EditorCoroutine fetchingWorlds;
        public static EditorCoroutine fetchingAvatars;
        
        static VRChatApiToolsEditor()
        {
            VRChatApiTools.DownloadImage = DownloadImage;
        }
        
        [MenuItem("Tools/VRChatApiTools/Refresh data")]
        public static void RefreshData()
        {
            Logger.Log("Refreshing data...");
            VRChatApiTools.ClearCaches();
            EditorCoroutine.Start(FetchUploadedData());
        }
        
        public static IEnumerator FetchUploadedData()
        {
            VRChatApiTools.uploadedWorlds = new List<ApiWorld>();
            VRChatApiTools.uploadedAvatars = new List<ApiAvatar>();
            
            if (!ConfigManager.RemoteConfig.IsInitialized())
                ConfigManager.RemoteConfig.Init();

            if (!APIUser.IsLoggedIn)
                yield break;

            ApiCache.ClearResponseCache();
            VRCCachedWebRequest.ClearOld();

            if (fetchingAvatars == null)
                fetchingAvatars = EditorCoroutine.Start(() => FetchAvatars());
            
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
                            VRChatApiTools.SetupWorldData(list);
                            FetchWorlds(offset + count);
                        });
                    else
                    {
                        fetchingWorlds = null;

                        foreach (ApiWorld w in VRChatApiTools.uploadedWorlds)
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
                null,
                "",
                ApiWorld.ReleaseStatus.All,
                null,
                null,
                true,
                false);
        }

        public static void FetchAvatars(int offset = 0)
        {
            ApiAvatar.FetchList(
                delegate(IEnumerable<ApiAvatar> avatars)
                {
                    if (avatars.FirstOrDefault() != null)
                        fetchingAvatars = EditorCoroutine.Start(() =>
                        {
                            var list = avatars.ToList();
                            int count = list.Count;
                            VRChatApiTools.SetupAvatarData(list);
                            FetchAvatars(offset + count);
                        });
                    else
                    {
                        fetchingAvatars = null;

                        foreach (ApiAvatar a in VRChatApiTools.uploadedAvatars)
                            DownloadImage(a.id, a.thumbnailImageUrl);
                    }
                },
                delegate(string obj)
                {
                    Logger.LogError("Couldn't fetch avatar list:\n" + obj);
                    fetchingAvatars = null;
                },
                ApiAvatar.Owner.Mine,
                ApiAvatar.ReleaseStatus.All,
                null,
                20,
                offset,
                ApiAvatar.SortHeading.None,
                ApiAvatar.SortOrder.Descending,
                null,
                null,
                true,
                false,
                null,
                false
            );
        }
        
        public static void DownloadImage(string blueprintID, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (VRChatApiTools.ImageCache.ContainsKey(blueprintID) && VRChatApiTools.ImageCache[blueprintID] != null) return;

            EditorCoroutine.Start(VRCCachedWebRequest.Get(url, succes));

            void succes(Texture2D texture)
            {
                if (texture != null)
                {
                    VRChatApiTools.ImageCache[blueprintID] = texture;
                }
                else if (VRChatApiTools.ImageCache.ContainsKey(blueprintID))
                {
                    VRChatApiTools.ImageCache.Remove(blueprintID);
                }
            }
        }
    }
}