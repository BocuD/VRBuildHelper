#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    public static class VRCLogin
    {
        private static bool loginInProgress;

        public static void AttemptLogin(Action<ApiModelContainer<APIUser>> onSucces,
            Action<ApiModelContainer<APIUser>> onError)
        {
            if (loginInProgress) return;

            if (!ApiCredentials.Load())
                Logger.LogError("You are currently not logged in. Please log in using the VRChat SDK Control panel.");
            else
            {
                loginInProgress = true;
                APIUser.InitialFetchCurrentUser(
                    c =>
                    {
                        onSucces(c);
                        loginInProgress = false;
                    },
                    c =>
                    {
                        onError(c);
                        loginInProgress = false;
                    }
                );
            }
        }
    }
}
#endif