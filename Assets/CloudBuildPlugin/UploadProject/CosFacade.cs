using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using CloudBuildPlugin.Common;
using CloudBuildPlugin.UploadProject;
using COSXML;
using COSXML.Auth;
using COSXML.Callback;
using COSXML.CosException;
using COSXML.Model;
using COSXML.Model.Object;
using COSXML.Transfer;
using COSXML.Utils;
using librsync.net;
using Newtonsoft.Json;
using static UploadProject.Helper;

namespace UploadProject
{
    public class CosFacade
    {
        private UcbFacade ucbFacade;
        private QCloudCredentialProvider cosProvider;
        private CosXmlConfig cosConfig;
        private CosXmlServer cosXml;
        private TransferManager transferManager;
        private string sourceBucket;
        private string deltaBucket;

        private static CosFacade instance;

        private CosFacade(string projectId)
        {
            init(projectId);
        }

        public static CosFacade GetInstance(string projectId)
        {
            lock (typeof(CosFacade))
            {
                if (instance == null)
                {
                    instance = new CosFacade(projectId);
                }
            }

            return instance;
        }

        private void init(string projectId)
        {
            ucbFacade = new UcbFacade();
            // ucbFacade.setHost("http://localhost");
           ucbFacade.setHost("https://api.ccb.unity.cn");
            ucbFacade.setPort("443");

            CosInfo cosInfo = ucbFacade.GetCredential(projectId);
            sourceBucket = cosInfo.bucket;
            deltaBucket = cosInfo.deltaBucket;
            cosConfig = new CosXmlConfig.Builder()
                .SetConnectionTimeoutMs(60000) //ms
                .SetReadWriteTimeoutMs(40000) //ms
                .IsHttps(true)
                .SetAppid(cosInfo.appId)
                .SetRegion(cosInfo.region)
                .SetDebugLog(true)
                .Build();
            cosProvider = new UcbSessionQCloudCredentialProvider(cosInfo.secretId, cosInfo.secretKey,
                cosInfo.expireTime, cosInfo.token, projectId, ucbFacade);
            cosXml = new CosXmlServer(cosConfig, cosProvider);
            transferManager = new TransferManager(cosXml, new TransferConfig());
        }

        private bool CheckFileExists(string bucket, string fileKey)
        {
            try
            {
                HeadObjectRequest request = new HeadObjectRequest(bucket, fileKey);
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), COS_REQUEST_DEFAULT_DURATION);
                HeadObjectResult result = cosXml.HeadObject(request);
                return result.httpCode.Equals(COS_STATUS.OK);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void UploadFile(string bucket, string fileKey, string filePath,
            OnProgressCallback progressCb, OnSuccessCallback<CosResult> successCb, OnFailedCallback failedCb)
        {
            COSXMLUploadTask uploadTask = new COSXMLUploadTask(bucket, null, fileKey)
            {
                progressCallback = progressCb, successCallback = successCb, failCallback = failedCb
            };
            uploadTask.SetSrcPath(filePath);
            transferManager.Upload(uploadTask);
        }

        private void syncDownloadFile(string bucket, string fileKey, string localDir, string localFileName,
            OnProgressCallback progressCb, OnSuccessCallback<CosResult> successCb, OnFailedCallback failedCb)
        {
            try
            {
                GetObjectRequest request = new GetObjectRequest(bucket, fileKey, localDir, localFileName);
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);
                request.SetCosProgressCallback(progressCb);
                GetObjectResult result = cosXml.GetObject(request);
            }
            catch (CosClientException clientEx)
            {
                failedCb(clientEx, null);
            }
            catch (CosServerException serverEx)
            {
                failedCb(null, serverEx);
            }
        }

        public void UploadProject(string fileName, string projectId,
            OnProgressCallback progressCb, OnMessageCallback messageCallback, OnSuccessCallback<CosResult> successCb,
            OnFailedCallback failedCb)
        {
            var filePath = Path.GetFullPath(fileName);
            var fileKey = $"{projectId}/{Path.GetFileName(fileName)}";

            if (CheckFileExists(sourceBucket, fileKey))
            {
                successCb(CreateTaskResult(COS_STATUS.SKIP,
                    string.Format(COS_MSG.INFO_TARGET_EXIST_ON_SERVER, fileName)));
                return;
            }

            _InitProject(filePath, projectId, progressCb, messageCallback,
                successCb, failedCb);
        }

        public Exception DeltaUploadProject(string fileName, string projectId,
            OnProgressCallback progressCb, OnMessageCallback messageCallback, OnSuccessCallback<CosResult> successCb,
            OnFailedCallback failedCb)
        {
            var filePath = Path.GetFullPath(fileName);
            if (!File.Exists(filePath))
            {
                failedCb(
                    new CosClientException(COS_STATUS.CLIENT_ERR,
                        string.Format(COS_MSG.ERR_CLIENT_TARGET_NOT_EXIST, fileName)),
                    new CosServerException(COS_STATUS.CLIENT_ERR, COS_MSG.ERR_CLIENT)
                );
            }

            var fileKey = $"{projectId}/{Path.GetFileName(fileName)}";
            if (CheckFileExists(sourceBucket, fileKey))
            {
                successCb(CreateTaskResult(COS_STATUS.SKIP,
                    string.Format(COS_MSG.INFO_TARGET_EXIST_ON_SERVER, fileName)));
                return null;
            }

            if (CheckFileExists(deltaBucket, $"{projectId}/{BASE_META}"))
            {
                _DeltaUpdateProject(filePath, projectId, progressCb, messageCallback, successCb, failedCb);
            }
            else
            {
                _InitProject(filePath, projectId, progressCb, messageCallback, successCb, failedCb);
            }

            return new Exception();
        }

        public void AsyncUploadProject(string fileName, string projectId,
            OnProgressCallback progressCb, OnMessageCallback messageCallback, OnSuccessCallback<CosResult> successCb,
            OnFailedCallback failedCb)
        {
            Func<string, string, OnProgressCallback, OnMessageCallback, OnSuccessCallback<CosResult>, OnFailedCallback,
                Exception> fun = DeltaUploadProject;
            fun.BeginInvoke(fileName, projectId, progressCb, messageCallback, successCb, failedCb,
                ar => { Console.WriteLine(ar.AsyncState); }, fun);
        }

        private void _InitProject(string filePath, string projectId,
            OnProgressCallback progressCb, OnMessageCallback messageCallback, OnSuccessCallback<CosResult> successCb,
            OnFailedCallback failedCb)
        {
            var localDir = Path.GetDirectoryName(filePath);
            var fileKey = $"{projectId}/{Path.GetFileName(filePath)}";
            var signatureKey = $"{fileKey}{SIG_SUFFIX}";
            var signaturePath = $"{filePath}{SIG_SUFFIX}";
            var baseMetaKey = $"{projectId}/{BASE_META}";
            var baseMetaPath = $"{localDir}/{BASE_META}";

            clearOldFiles(baseMetaPath, signaturePath);
            AutoResetEvent signatureEvent = new AutoResetEvent(false);
            AutoResetEvent baseMetaEvent = new AutoResetEvent(false);

            messageCallback(COS_MSG.TITLE_UPLOADING_PROJECT,
                string.Format(COS_MSG.INFO_UPLOADING_PROJECT, fileKey));
            UploadFile(sourceBucket, fileKey, filePath, progressCb,
                delegate(CosResult result1)
                {
                    signatureEvent.WaitOne();
                    messageCallback(COS_MSG.TITLE_UPLOADING_SIG_FILE,
                        string.Format(COS_MSG.INFO_UPLOADING_SIG_FILE, signatureKey));
                    UploadFile(deltaBucket, signatureKey, signaturePath, progressCb,
                        delegate(CosResult result)
                        {
                            baseMetaEvent.WaitOne();
                            messageCallback(COS_MSG.TITLE_UPLOADING_META_FILE,
                                string.Format(COS_MSG.INFO_UPLOADING_META_FILE, baseMetaKey));
                            UploadFile(deltaBucket, baseMetaKey, baseMetaPath, progressCb,
                                delegate(CosResult cosResult)
                                {
                                    clearOldFiles(baseMetaPath, signaturePath);
                                    successCb(
                                        CreateTaskResult(COS_STATUS.OK,
                                            string.Format(COS_MSG.INFO_INIT_SUCCESS, filePath, projectId)));
                                }, failedCb);
                        }, failedCb);
                }, failedCb);

            AsyncCreateSignature(filePath, signaturePath, () => { signatureEvent.Set(); });
            AsyncCreateBaseMeta(filePath, fileKey, signatureKey, projectId, baseMetaPath,
                () => { baseMetaEvent.Set(); });
        }

        private void _DeltaUpdateProject(string filePath, string projectId,
            OnProgressCallback progressCb, OnMessageCallback messageCb, OnSuccessCallback<CosResult> successCb,
            OnFailedCallback failedCb)
        {
            var localDir = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            var fileKey = $"{projectId}/{fileName}";
            var baseMetaKey = $"{projectId}/{BASE_META}";
            var baseMetaPath = $"{localDir}/{BASE_META}";
            var deltaKey = $"{projectId}/{fileName}{DELTA_SUFFIX}";
            var deltaPath = $"{filePath}{DELTA_SUFFIX}";
            var baseSignature = $"{fileName}{SIG_SUFFIX}";
            var baseSignaturePath = $"{localDir}/{baseSignature}";
            string hashPattern = @"(?<=" + projectId + @"/)(.*)(?=\.zip$)";
            string projectHash = new Regex(hashPattern).Match(fileKey).Value;

            clearOldFiles(baseMetaPath, baseSignaturePath, deltaPath);

            messageCb(COS_MSG.TITLE_DOWNLOADING_META_FILE,
                string.Format(COS_MSG.INFO_DOWNLOADING_META_FILE, baseMetaKey, $"{localDir}/{BASE_META}"));
            syncDownloadFile(deltaBucket, baseMetaKey, localDir, BASE_META,
                progressCb, DoNothing, failedCb);
            var metaInfo = JsonConvert.DeserializeObject<MetaInfo>(File.ReadAllText(baseMetaPath));
            if ((float) new FileInfo(filePath).Length / metaInfo.baseFileSize > 1 + DELTA_THRESHOLD)
            {
                clearOldFiles(baseMetaPath);
                _InitProject(filePath, projectId, progressCb, messageCb, successCb, failedCb);
                return;
            }

            ucbFacade.InitDeltaUpload(new DeltaInfo
            {
                baseFileKey = metaInfo.baseFileName,
                deltaFileKey = $"{fileKey}{DELTA_SUFFIX}",
                newFileKey = fileKey,
                projectId = projectId,
                fileHash = projectHash
            });
            messageCb(COS_MSG.TITLE_DOWNLOADING_SIG_FILE,
                string.Format(COS_MSG.INFO_DOWNLOADING_SIG_FILE, metaInfo.signatureKey, $"{localDir}/{baseSignature}"));
            syncDownloadFile(deltaBucket, metaInfo.signatureKey, localDir, baseSignature,
                progressCb, DoNothing, failedCb);

            messageCb(COS_MSG.TITLE_CREATING_DELTA_FILE,
                string.Format(COS_MSG.INFO_CREATING_DELTA_FILE, deltaPath, filePath, baseSignaturePath));
            CreateDelta(filePath, baseSignaturePath, deltaPath);
            if ((float) new FileInfo(deltaPath).Length / metaInfo.baseFileSize > DELTA_THRESHOLD)
            {
                clearOldFiles(baseMetaPath, baseSignaturePath, deltaPath);
                _InitProject(filePath, projectId, progressCb, messageCb, successCb, failedCb);
                return;
            }

            messageCb(COS_MSG.TITLE_UPLOADING_DELTA_FILE,
                string.Format(COS_MSG.INFO_UPLOADING_DELTA_FILE, deltaKey));
            UploadFile(deltaBucket, deltaKey, deltaPath, progressCb,
                delegate(CosResult uploadResult)
                {
                    deltaPatch(metaInfo.baseFileName, fileKey, projectId, projectHash);
                    clearOldFiles(baseMetaPath, baseSignaturePath, deltaPath);
                    messageCb(COS_MSG.TITLE_PATCHING_DELTA_FILE, COS_MSG.INFO_PATCHING_DELTA_FILE);
                }, failedCb);
        }

        private void deltaPatch(string baseFileKey, string fileKey, string projectId, string fileHash)
        {
            ucbFacade.DeltaUpload(new DeltaInfo
            {
                baseFileKey = baseFileKey,
                deltaFileKey = $"{fileKey}{DELTA_SUFFIX}",
                newFileKey = fileKey,
                projectId = projectId,
                fileHash = fileHash
            });
        }

        public void GetUcbPatchResult(string fileHash, OnSuccessCallback<CosResult> successCb,
            OnFailedCallback failedCb)
        {
            var status = ucbFacade.GetPatchStatus(fileHash);
            if (UCB_PATCH_STATUS.COMPLETED.Equals(status))
            {
                successCb(CreateTaskResult(COS_STATUS.OK, COS_MSG.INFO_UPLOAD_SUCCESS));
                return;
            }

            if (UCB_PATCH_STATUS.FAILED.Equals(status))
            {
                failedCb(
                    new CosClientException(COS_STATUS.SERVER_ERR, COS_MSG.ERR_SERVER),
                    new CosServerException(COS_STATUS.SERVER_ERR, COS_MSG.ERR_SERVER_PATCH_FAIL)
                );
            }
        }

        private void clearOldFiles(params string[] paths)
        {
            foreach (var t in paths)
            {
                deleteFileIfExists(t);
            }
        }

        private void deleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void AsyncCreateBaseMeta(string filePath, string fileKey, string signatureKey, string projectId,
            string baseMetaPath, VoidCallback voidCb)
        {
            Func<string, string, string, string, string, Exception> func = CreateBaseMeta;
            func.BeginInvoke(filePath, fileKey, signatureKey, projectId, baseMetaPath,
                ar => { Console.WriteLine(ar.AsyncState); }, func);
            voidCb();
        }

        private Exception CreateBaseMeta(string filePath, string fileKey, string signatureKey, string projectId,
            string baseMetaPath)
        {
            var md5Hash = ZipHandler.CalculateMD5(filePath);
            var metaInfo = new MetaInfo
            {
                baseBucket = deltaBucket,
                nameSpace = projectId,
                baseFileName = fileKey,
                baseFileSize = new FileInfo(filePath).Length,
                md5 = md5Hash,
                signatureKey = signatureKey
            };
            File.WriteAllText(baseMetaPath, JsonConvert.SerializeObject(metaInfo));
            return new Exception();
        }

        private void AsyncCreateSignature(string filePath, string signaturePath, VoidCallback voidCb)
        {
            Func<string, string, Exception> func = CreateSignature;
            func.BeginInvoke(filePath, signaturePath, ar => { Console.WriteLine(ar.AsyncState); }, func);
            voidCb();
        }

        private Exception CreateSignature(string filePath, string signaturePath)
        {
            var fileIn = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var fileOut = new FileStream(signaturePath, FileMode.OpenOrCreate, FileAccess.Write);
            Librsync.ComputeSignature(fileIn).CopyTo(fileOut);
            fileOut.Close();
            fileIn.Close();
            return new Exception();
        }

        private void CreateDelta(string filePath, string signaturePath, string deltaPath)
        {
            var baseSignatureFileIn = new FileStream(signaturePath, FileMode.Open, FileAccess.Read);
            var fileIn = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var deltaOut = new FileStream(deltaPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            Librsync.ComputeDelta(baseSignatureFileIn, fileIn).CopyTo(deltaOut);
            baseSignatureFileIn.Close();
            fileIn.Close();
            deltaOut.Close();
        }
    }
}