
using System;

namespace CloudBuildPlugin.Common
{
    public static class Debug
    {
        private const bool DebugMode = false;

        private static string AddPrefix(string message)
        {
            return " [Cloud Build Plugin]-->" + message;
        }

        //print log ignoring debugMode
        public static void Info(object message)
        {
            UnityEngine.Debug.Log(AddPrefix(message.ToString()));
        }
        public static void LogWarning(object message)
        {
            UnityEngine.Debug.LogWarning(AddPrefix(message.ToString()));
        }

        public static void Log(object message)
        {
            if (DebugMode)
            {
                UnityEngine.Debug.Log(AddPrefix(message.ToString()));
            }
        }

        public static void LogError(object message)
        {
            UnityEngine.Debug.LogError(AddPrefix(message.ToString()));
        }
    }
}