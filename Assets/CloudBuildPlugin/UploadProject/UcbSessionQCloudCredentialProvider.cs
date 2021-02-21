using System.Text;
using COSXML.Auth;
using COSXML.CosException;
using COSXML.Utils;
using UploadProject;

namespace CloudBuildPlugin.UploadProject
{
    public class UcbSessionQCloudCredentialProvider : QCloudCredentialProvider
    {
        private string tmpSecretId;
        private string tmpSecretKey;
        private string keyTime;
        private string token;
        private string projectId;
        private UcbFacade ucbFacade;
        
        public UcbSessionQCloudCredentialProvider(
            string tmpSecretId,
            string tmpSecretKey,
            long tmpExpiredTime,
            string sessionToken)
        {
            this.tmpSecretId = tmpSecretId;
            this.tmpSecretKey = tmpSecretKey;
            this.keyTime = $"{TimeUtils.GetCurrentTime(TimeUnit.SECONDS)};{tmpExpiredTime}";
            this.token = sessionToken;
        }
        
        public UcbSessionQCloudCredentialProvider(
            string tmpSecretId,
            string tmpSecretKey,
            long tmpExpiredTime,
            string sessionToken,
            string projectId,
            UcbFacade ucbFacade)
            : this(tmpSecretId, tmpSecretKey, tmpExpiredTime, sessionToken)
        {
            
            this.projectId = projectId;
            this.ucbFacade = ucbFacade;
        }

        public void SetUcbFacade(string projectId, UcbFacade ucbFacade)
        {
            this.projectId = projectId;
            this.ucbFacade = ucbFacade;
        }
        
        public override void Refresh()
        {
            if (ucbFacade == null || projectId == null)
                return;
            CosInfo cosInfo = ucbFacade.GetCredential(projectId);
            tmpSecretId = cosInfo.secretId;
            tmpSecretKey = cosInfo.secretKey;
            keyTime = $"{TimeUtils.GetCurrentTime(TimeUnit.SECONDS)};{cosInfo.expireTime}";
            token = cosInfo.token;
        }
        
        public bool IsNeedUpdateNow()
        {
            if (string.IsNullOrEmpty(keyTime) || string.IsNullOrEmpty(tmpSecretId) || string.IsNullOrEmpty(tmpSecretKey) || string.IsNullOrEmpty(token))
                return true;
            int num = keyTime.IndexOf(';');
            long result = -1;
            long.TryParse(keyTime.Substring(num + 1), out result);
            long currentTime = TimeUtils.GetCurrentTime(TimeUnit.SECONDS);
            return result <= currentTime + 120;
        }
        
        public override QCloudCredentials GetQCloudCredentials()
        {
            if (IsNeedUpdateNow())
                Refresh();
            if (tmpSecretId == null)
                throw new CosClientException(10001, "secretId == null");
            if (tmpSecretKey == null)
                throw new CosClientException(10001, "secretKey == null");
            if (keyTime == null)
                throw new CosClientException(10001, "keyTime == null");
            return new SessionQCloudCredentials(tmpSecretId, DigestUtils.GetHamcSha1ToHexString(keyTime, Encoding.UTF8, tmpSecretKey, Encoding.UTF8), token, keyTime);
        }
    }
}