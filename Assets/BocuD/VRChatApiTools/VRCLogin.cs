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
#endif