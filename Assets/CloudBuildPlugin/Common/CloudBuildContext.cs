using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using CloudBuildPlugin.Enums;
using COSXML.CosException;
using COSXML.Model;
using SimpleJSON;
using UnityEditor;
using UnityEngine;
using UploadProject;
using BuildTarget = CloudBuildPlugin.Enums.BuildTarget;

namespace CloudBuildPlugin.Common
{
    public class CloudBuildContext
    {
        const float SYNC_TASK_INTERVAL_AT_RUNNING = 3f, SYNC_TASK_INTERVAL_OUT_OF_RUNNING = 60f;
        

        public TransferMode transferMode;
        
        UcbFacade ucbFacade
        {
            get { return UcbFacade.GetInstance(); }
        }

        public float syncTaskInfoInterval = SYNC_TASK_INTERVAL_OUT_OF_RUNNING;
        public string progressTitle, taskId;
        public string projectId
        {
            get { return Utils.GetProjectId(); }
        }

        public bool isProgressBarVisible, isUploading;
        public float progressValue, forceRepaintProgress;
        private JSONNode _taskInfo;
        public string repoUrl, repoUsername, repoPassword, repoToken, repoBranch = "master", relativePath = "/";
        public bool projectUploaded;
        public FTP ftpInstance;
        
        public int cosUploadModeFlag = 0;

        public JSONNode taskInfo
        {
            get { return _taskInfo; }
            set
            {
                _taskInfo = value;
                UpdateSyncTaskInterval();
            }
        }

        public List<IPolling> PollingPool;
        private Polling pollingSyncTask, pollingPatchStatus;

        public Dictionary<string, bool> jobActionOpened = new Dictionary<string, bool>();
        public bool[] buildTargetSelection = new bool[Enum.GetNames(typeof(BuildTarget)).Length];

        public CloudBuildContext(TransferMode t)
        {
            transferMode = t;
            
            pollingSyncTask = new Polling(SyncBuildTaskInfo);
            pollingSyncTask.isActive = true;
            pollingPatchStatus = new Polling(SyncPatchStatus);
            pollingPatchStatus.SetFrequently(true);
            pollingPatchStatus.isActive = false;
            PollingPool = new List<IPolling> {pollingSyncTask, pollingPatchStatus};
        }
        
        public void ResetJobsWithActionsOpened()
        {
            jobActionOpened = new Dictionary<string, bool>();
        }

        public void UpdateSyncTaskInterval()
        {
//            syncTaskInfoInterval = IsBuildTaskRunning() ? SYNC_TASK_INTERVAL_AT_RUNNING : SYNC_TASK_INTERVAL_OUT_OF_RUNNING;
            
            pollingSyncTask.SetFrequently(IsBuildTaskRunning());
        }

        public bool IsBuildTaskRunning()
        {
            if (taskInfo == null || taskInfo["jobs"].IsNull)
            {
                return false;
            }

            bool result = false;
            for (int i = 0; i < taskInfo["jobs"].Count; i++)
            {
                try
                {
                    if ((int) Enum.Parse(typeof(JobStatus), taskInfo["jobs"][i]["status"]) <= (int) JobStatus.RUNNING)
                    {
                        result = true;
                    }
                }
                catch (Exception ex)
                {

                }
            }

            return result;
        }


        public void SyncPatchStatus()
        {
            if (!string.IsNullOrEmpty(projectId) && !string.IsNullOrEmpty(PathHelper.ProjectZipFullPath))
            {
                CosFacade.GetInstance(projectId).GetUcbPatchResult(Utils.GetProjectHash(PathHelper.ProjectZipFullPath),
                    OnCosUploadSuccess, OnCosUploadFail);
            }
        }
        //
        // Collecting informations
        //
        public void SyncBuildTaskInfo()
        {
            if (!string.IsNullOrEmpty(taskId))
            {
                try
                {
                    taskInfo = ucbFacade.GetTaskDetail(taskId);
                }
                catch (WebException ex)
                {
                    var response = (HttpWebResponse)ex.Response;
                    //task not found on server
                    if (ex.Message.Contains("not found"))
                    {
                        Debug.LogError("Sync Task Info Failed. [Task Id not found]");
                        taskId = null;
                    }
                    else
                    {
                        Debug.LogError("Sync Task Info Failed. " + ex.Message);
                    }
                }
                finally
                {
                    UpdateSyncTaskInterval();
                }
            }
        }
        
        public void StartUploadProject(string projectZipFullName, FTP ftp)
        {
            ftpInstance = ftp;
            PathHelper.ProjectZipFullPath = projectZipFullName;
            StartUploadProject();
        }
        
        public void StartUploadProject()
        {
            isUploading = true;
            ShowProgress("Uploading to " + Enum.GetName(typeof(TransferMode), transferMode));
            try
            {
                UploadProject(PathHelper.ProjectZipFullPath);
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(CosFileExistsException))
                {
                    projectUploaded = true;
                    FinalizeProgress("File Exists");
                    throw;
                }
                else
                {
                    FinalizeProgress("Upload Failed");
                    throw;
                }
            }
        }

        private void UploadProject(string fileName)
        {
            string pid = this.projectId;
            switch (transferMode)
            {
                case TransferMode.FTP:
                    ftpInstance.AsyncUploadProject(fileName, pid, new BasicProgress<double>(p => OnFtpProgressChange((float)p)),
                        OnFtpUploadCompleted);
                    break;

                case TransferMode.COS:
                    if (cosUploadModeFlag == (int) CosUploadMode.Delta)
                    {
                        Debug.Log("start delta upload:" + fileName);
                        CosFacade.GetInstance(pid).AsyncUploadProject(fileName, pid, OnCosUploadProgressChange, OnCosUploadMessage, OnCosUploadSuccess, OnCosUploadFail);
                    }
                    else if (cosUploadModeFlag == (int) CosUploadMode.Full)
                    {
                        CosFacade.GetInstance(pid).UploadProject(fileName, pid, OnCosUploadProgressChange, OnCosUploadMessage, OnCosUploadSuccess, OnCosUploadFail);    
                    }
                    break;

                default:
                    Console.WriteLine("Not supported mode!");
                    return;
            }
        }
        
        
        void OnCosUploadProgressChange(long completed, long total)
        {
            UpdateProgress((float)completed / total);
        }

        void OnCosUploadMessage(string birefMessage, string fullMessage)
        {
            if (Helper.COS_MSG.TITLE_PATCHING_DELTA_FILE.Equals(birefMessage))
            {
                pollingPatchStatus.isActive = true;
            }
            UpdateProgressingTitle(birefMessage + " ...");
            Debug.Log("[Cos Message Callback]" + fullMessage);
        }

        void OnCosUploadSuccess(CosResult cosResult)
        {
            FinalizeProgress("Upload Done");
            Debug.Log("Upload Succeeded.");
            Debug.Log(cosResult.GetResultInfo());
            if (Helper.COS_STATUS.SKIP.Equals(cosResult.httpCode))
            {
                MessageQueue.Enqueue(new UcbNotificationMessage(UcbNotificationMessageTypes.Info, "File exists, skip uploading."));
            }
            projectUploaded = true;
            pollingPatchStatus.isActive = false;
        }

        void OnCosUploadFail(CosClientException clientEx, CosServerException serverEx)
        {
            pollingPatchStatus.isActive = false;
            FinalizeProgress("Upload Failed");
            
            Debug.Log("Upload Failed.");
            if (clientEx != null)
            {
                Debug.LogError("CosClientExceptionMessage: " + clientEx.Message);
                Debug.LogError("CosClientException: " + clientEx.StackTrace);
            }
            if (serverEx != null)
            {
                Debug.LogError("CosServerException: " + serverEx.GetInfo());
            }
            Debug.LogError(String.Format("currentThread id = {0}", Thread.CurrentThread.ManagedThreadId));
        }
        //
        // Progress bar
        //
        public void ShowProgress(string withTitle)
        {
            isProgressBarVisible = true;
            UpdateProgress(0f);
            forceRepaintProgress = 0f;
            progressTitle = withTitle;
        }

        public void UpdateProgressingTitle(string withTitle)
        {
            UpdateProgress(1f);
            progressTitle = withTitle;
        }
        
        public void UpdateProgress(float progress)
        {
            progressValue = progress;
            if (progressValue - forceRepaintProgress > 0.01f)
            {
//                Debug.Log($"progress = {progressValue * 100.0:##}%");
                forceRepaintProgress = progressValue;
            }
        }

        public void FinalizeProgress(string withTitle)
        {
            isUploading = false;
            UpdateProgress(1f);
            progressTitle = withTitle;
        }

        public void HideProgress()
        {
            isProgressBarVisible = false;
            isUploading = false;
        }

        void OnFtpUploadCompleted(Exception ex)
        {
            if (ex == null)
            {
                projectUploaded = true;
                FinalizeProgress("Upload Done");
            } else if (ex.GetType() == typeof(CosFileExistsException))
            {
                FinalizeProgress("File Exists");
            }
            else
            {
                Debug.LogError(ex);
                FinalizeProgress("Upload Failed");
                throw ex;
            }
        }


        void OnFtpProgressChange(float progress)
        {
            UpdateProgress(progress);
        }
        
        public bool BadScriptingBackend()
        {
            bool badTarget = false;
            for (int i=0; i < buildTargetSelection.Length; i++)
            {
                string btGroupName = Utils.TryGetBuildTargetGroup(Enum.GetName(typeof(BuildTarget), i));
                BuildTargetGroup btGroup = (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), btGroupName);
                if (buildTargetSelection[i] && i != (int)BuildTarget.WebGL && PlayerSettings.GetScriptingBackend(btGroup) > 0)
                {
                    badTarget = true;
                    break;
                }
            }

            return badTarget;
        }
    }
}
