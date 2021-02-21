namespace CloudBuildPlugin.Common
{
    public sealed class Constants
    {
        public static readonly string WEAPP_QR_PREFIX = "https://api.ccb.unity.cn/weapp/task/";

        public static readonly string PACK_TEMP_DIRECTORY =  "/Library/CloudBuildPlugin/";
        public static readonly string PROJECT_CONFIG_FILE_NAME = "project-config.conf";
        public static readonly string GLOBAL_CONFIG_FILE_NAME = "config.conf";
        public static readonly string OSX_GLOBAL_CONFIG_DIRECTORY = "/Library/Application Support/Unity/Cloud Build/";

        
        private Constants()
        {
            // private ctor
        }
    }
}