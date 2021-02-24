using System.Collections.Generic;
using com.unity.cloudbase;
using UnityEditor;
using UnityEngine;

namespace com.unity.cloudbase {
    public class UserInfo {
        public string organizationId;
        public string organizationName;
         public string projectId;
        public string projectName;
        public string userId;
        public string userName;
    }

    [ExecuteInEditMode, InitializeOnLoad]
    internal class TcbClient {
        // private static UserInfo _userInfo;
        private static Dictionary<string, dynamic> _userInfo;
        static TcbClient () {
            EditorApplication.update += InitUserInfo;
        }

        static void InitUserInfo () {
            if (_userInfo == null) {
                _userInfo = new Dictionary<string, dynamic> { 
                    { "organizationId", CloudProjectSettings.organizationId },
                    { "organizationName", CloudProjectSettings.organizationName },
                    { "projectId", CloudProjectSettings.projectId },
                    { "projectName", CloudProjectSettings.projectName },
                    { "userId", CloudProjectSettings.userId },
                    { "userName", CloudProjectSettings.userName },
                    { "serviceType", "tcb"}
                };
            }
        }

        async public static void updateUserInfo () {
            CloudBaseApp app = CloudBaseApp.Tcb ("59eb4700a3c34", 3000);
            AuthState state = await app.Auth.GetAuthStateAsync ();

            if (state == null) {
                // 匿名登录
                state = await app.Auth.SignInAnonymouslyAsync ();
            }
            Debug.Log(_userInfo["userId"]);

            // 调用云函数
            FunctionResponse res = await app.Function.CallFunctionAsync ("updateUserInfo", _userInfo);
        }
    }
}