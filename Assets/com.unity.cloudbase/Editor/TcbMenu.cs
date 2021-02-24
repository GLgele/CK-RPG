using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace com.unity.cloudbase {
    internal static class TcbMenu {
        public const string TcbRootMenu = "云开发 CloudBase";

        [MenuItem (TcbRootMenu + "/云开发 CloudBase 控制台")]
        private static void RedirectToTencentCloud () {
            var task = Task.Run(TcbClient.updateUserInfo);
            // task.Wait();
            Application.OpenURL ("https://console.cloud.tencent.com/tcb?from=12359&channel=unity");
        }

        [MenuItem (TcbRootMenu + "/ 快速开始")]
        private static void RedirectToUnity () {
             var task = Task.Run(TcbClient.updateUserInfo);
            Application.OpenURL ("https://docs.cloudbase.net/quick-start/dotnet.html?from=12359&channel=unity");
        }

         [MenuItem (TcbRootMenu + "/ SDK 文档")]
        private static void RedirectToSDK () {
             var task = Task.Run(TcbClient.updateUserInfo);
            Application.OpenURL ("https://docs.cloudbase.net/api-reference/dotnet/initialization.html?from=12359&channel=unity");
        }
    }
}