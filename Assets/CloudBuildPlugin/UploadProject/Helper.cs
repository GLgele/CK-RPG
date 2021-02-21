using System;
using COSXML.Callback;
using COSXML.Model;
using COSXML.Transfer;

namespace UploadProject
{
    public class Helper
    {
        public const int COS_REQUEST_DEFAULT_DURATION = 600;
        public const int UCB_PATCH_DEFAULT_DURATION   = 500 * 1000;
        public const int UCB_PATCH_DEFAULT_BATCH_SIZE = 1000;
        public static class COS_STATUS
        {
            public const int OK         = 200;
            public const int CLIENT_ERR = 400;
            public const int SERVER_ERR = 500;
            public const int SKIP       = 600;
            public const int STAGE      = 700;
        }
        public static class COS_MSG
        {
            public const string TITLE_CREATING_SIG_FILE     = "creating signature file";
            public const string TITLE_CREATING_META_FILE    = "creating meta file";
            public const string TITLE_CREATING_DELTA_FILE   = "creating delta file";
            public const string TITLE_UPLOADING_SIG_FILE    = "uploading signature file";
            public const string TITLE_UPLOADING_META_FILE   = "uploading meta file";
            public const string TITLE_UPLOADING_PROJECT     = "uploading project";
            public const string TITLE_UPLOADING_DELTA_FILE  = "uploading delta file";
            public const string TITLE_DOWNLOADING_META_FILE = "downloading meta file";
            public const string TITLE_DOWNLOADING_SIG_FILE  = "downloading signature file";
            public const string TITLE_PATCHING_DELTA_FILE   = "patching delta file";
            public const string INFO_TARGET_EXIST_ON_SERVER = "Target file:{0} exists on server.";
            public const string INFO_INIT_SUCCESS           = "Target file:{0} successfully initialized under project:{1}";
            public const string INFO_UPLOAD_SUCCESS         = "Uploading successfully completed.";
            public const string INFO_CREATING_SIG_FILE      = "Creating signature file on: {0}";
            public const string INFO_CREATING_META_FILE     = "Creating meta file on: {0}";
            public const string INFO_CREATING_DELTA_FILE    = "Creating delta file on: {0} from project: {1} and signature: {2}";
            public const string INFO_UPLOADING_SIG_FILE     = "Uploading signature file to: {0}";
            public const string INFO_UPLOADING_META_FILE    = "Uploading meta file to: {0}";
            public const string INFO_UPLOADING_PROJECT      = "Uploading project to: {0}";
            public const string INFO_UPLOADING_DELTA_FILE   = "Uploading delta file to: {0}";
            public const string INFO_DOWNLOADING_META_FILE  = "Downloading meta file: {0} to local path: {1}";
            public const string INFO_DOWNLOADING_SIG_FILE   = "Downloading signature file: {0} to local path: {1}";
            public const string INFO_PATCHING_DELTA_FILE    = "Patching delta file on remote";
            public const string ERR_CLIENT                  = "Client error.";
            public const string ERR_CLIENT_TARGET_NOT_EXIST = "Target file:{0} doesn't exist on local.";
            public const string ERR_SERVER                  = "Server error.";
            public const string ERR_SERVER_PATCH_FAIL       = "Patch process failed.";
            public const string ERR_SERVER_DOWNLOAD_FAIL    = "Download file:{0} failed.";
            public const string ERR_SERVER_UPLOAD_FAIL      = "Upload file:{0} failed.";
        }

        public static class UCB_PATCH_STATUS
        {
            public const string COMPLETED  = "COMPLETED";
            public const string PROCESSING = "PROCESSING";
            public const string FAILED     = "FAILED";
        }

        public const string SIG_SUFFIX = ".sig";
        public const string DELTA_SUFFIX = ".delta";
        public const string BASE_META = "base.meta";
        
        public const float DELTA_THRESHOLD = 0.4f;
        
        private const long WEIGHTED_TOTAL = 2 << 14;
        private const long FULL_WEIGHT = 100;
        
        public static COSXMLUploadTask.UploadTaskResult CreateTaskResult(int status, string message)
        {
            var result = new COSXMLUploadTask.UploadTaskResult();
            result.httpCode = status;
            result.httpMessage = message;
            return result;
        }

        public static void DoNothing(CosResult result) {}

        public static void WeightedProgressCallBack(long completed, long total, int start, int end, OnProgressCallback mProgressCallback)
        {
            var weightedCompleted =
                WEIGHTED_TOTAL * (Math.Max(0, start) + (Math.Min(FULL_WEIGHT, end) - Math.Max(0, start)) * completed / total) /
                FULL_WEIGHT;
            mProgressCallback(weightedCompleted, WEIGHTED_TOTAL);
        }

        public delegate void OnMessageCallback(string briefMessage, string fullMessage);
        public delegate void VoidCallback();
    }
}