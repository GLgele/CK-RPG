using System;
namespace UploadProject
{
    [System.Serializable]
    public class CosInfo
    {
        public string appId;
        public string region;
        public string secretId;
        public string secretKey;
        public string token;
        public long expireTime;
        public string bucket;
        public string deltaBucket;
    }
}
