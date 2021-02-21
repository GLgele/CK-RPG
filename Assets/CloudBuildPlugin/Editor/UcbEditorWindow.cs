 
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
#if (UNITY_EDITOR)
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CloudBuildPlugin.Common;
using CloudBuildPlugin.Editor.TreeView;
using CloudBuildPlugin.Editor.TreeView.TreeDataModel;
using CloudBuildPlugin.Enums;
using CloudBuildPlugin.UploadProject;
using SimpleJSON;
using UcbEditorWindow;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UploadProject;
using BuildTarget = CloudBuildPlugin.Enums.BuildTarget;
using Debug = CloudBuildPlugin.Common.Debug;

namespace CloudBuildPlugin.Editor
{

    public class UcbEditorWindow : EditorWindow
    {
        private Vector2 scrollPos;
        
        private static string unityVersion
        {
            get { return Utils.UnityVersion; }
        }

        bool showAdvancedSettings = false;
        string _apiHost, _apiPort;

        string apiHost
        {
            get { return _apiHost; }
            set
            {
                _apiHost = value;
                UcbFacade.GetInstance().UpdateHost(_apiHost, _apiPort);
            }
        }
        
        string apiPort
        {
            get { return _apiPort; }
            set
            {
                _apiPort = value;
                UcbFacade.GetInstance().UpdateHost(_apiHost, _apiPort);
            }
        }
        
//        UcbFacade _ucbFacade;
        UcbFacade ucbFacade
        {
            get { return UcbFacade.GetInstance(); }
        }

        FTP _ftp;
        FTP ftpInstance {
            get
            {
                if (_ftp == null)
                {
                    _ftp = new FTP();
                }
                _ftp.setRemoteHost(ftpHost);
                _ftp.setUsername(ftpUserName);
                _ftp.setPassword(ftpPassword);
                return _ftp;
            }
            set
            {
                _ftp = value;
            }
        }

        double lastSyncTaskTime = 0;
        private string cosEndPoint;
        
        //ftp related
        private string ftpHost = "129.211.136.209", ftpPort = "21", ftpUserName = "ucbFtp", ftpPassword = "ucbFtpPassword";
//        private string ftpHost = "129.211.136.209", ftpPort = "21", ftpUserName = "ucbUploader", ftpPassword = "ucbUploaderPassword";

        private AnimBool showExtraFields_Upload;

        //List<SceneAsset> m_SceneAssets = new List<SceneAsset>();

        int gitCredentialModeFlag = 0;
        string[] gitCredentialOptions = Enum.GetNames(typeof(GitCredentialMode));

        int transferModeFlag = 0;
        string[] transferModeOptions = Enum.GetNames(typeof(TransferMode));
        
//        int cosUploadModeFlag = 0;
        string[] cosUploadModeOptions = Enum.GetNames(typeof(CosUploadMode));

        Dictionary<int, CloudBuildContext> ContextForMode;
        private Color guiDefaultColor;
        
        
        UcbEditorWindow()
        {
            InitContext();

            titleContent = new GUIContent("Cloud Build");
            ContextForMode[transferModeFlag].UpdateSyncTaskInterval();
        }

        [MenuItem("Window/Cloud Build")]
        public static void showWindow()
        {
            Debug.Log("Unity Version: " + unityVersion);
            
            if (UcbFacade.GetInstance().CheckUnityVersion(unityVersion))
            {
                EditorWindow.GetWindow(typeof(UcbEditorWindow));
            }
            else
            {
                EditorUtility.DisplayDialog("Unsupported Unity Version",
                    "You are using a version of Unity Editor that is not supported by Cloud Build Plugin.", "OK");
            }
        }

        void InitContext()
        {
            ContextForMode = new Dictionary<int, CloudBuildContext>();
            foreach (TransferMode t in Enum.GetValues(typeof(TransferMode)))
            {
                ContextForMode[(int)t] = new CloudBuildContext(t);
                ContextForMode[(int) t].ftpInstance = ftpInstance;
            }
        }

        //
        // Life cycle
        //
        private void OnEnable()
        {
            guiDefaultColor = GUI.color;
            
            autoRepaintOnSceneChange = true;
            minSize = new Vector2(350, 650);

            try
            {
                GetLatestZip();
                LoadProjectConfig();
                LoadGlobalConfig();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }

            showExtraFields_Upload = new AnimBool(true);
            showExtraFields_Upload.valueChanged.AddListener(Repaint);

            SyncBuildTaskInfo();
        }

        private void Update()
        {
            double curTime = EditorApplication.timeSinceStartup;
            foreach (IPolling polling in ContextForMode[transferModeFlag].PollingPool)
            {
                polling.TryPolling(curTime);
            }

            ResolveMessageQueue(curTime);
        }

        
        void ResolveMessageQueue(double curTime)
        {
            // show notification from threads
            if (curTime - MessageQueue.latestDequeueTime > MessageQueue.DEQUEUE_INTERVAL)
            {
                UcbNotificationMessage msg = MessageQueue.Dequeue();
                if (msg != null)
                {
                    switch (msg.type)
                    {
                        case UcbNotificationMessageTypes.Info:
                            ShowNotification(new GUIContent(msg.content));
                            break;
                        case UcbNotificationMessageTypes.Error:
                            EditorUtility.DisplayDialog("Error", msg.content, "OK");
                            break;
                    }
                }

                MessageQueue.latestDequeueTime = curTime;
            }
        }

        void OnProjectChange()
        {

        }
        
        //
        // UI
        //
        void OnGUI()
        {
            scrollPos =
                EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Width(position.width), GUILayout.Height(position.height));
            
            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            int newMode = EditorGUILayout.Popup("Cloud Build Mode:",
                transferModeFlag, transferModeOptions, EditorStyles.popup);
            if (newMode != transferModeFlag)
            {
                transferModeFlag = newMode;
                OnTransferModeChange();
            }

            if (TransferMode.FTP.Equals(ContextForMode[transferModeFlag].transferMode))
            {
                EditorGUILayout.LabelField("* for internal use only");
            }
            EditorGUI.indentLevel--;

            //
            // Upload
            //
            if (TransferModeUtils.IsRepository(transferModeFlag)) 
            {
                EditorGUILayout.Separator();
                EditorGUI.indentLevel++;
                ContextForMode[transferModeFlag].repoUrl = EditorGUILayout.TextField("Repository URL:", ContextForMode[transferModeFlag].repoUrl);
                if (transferModeFlag == (int) TransferMode.GIT)
                {
                    ContextForMode[transferModeFlag].repoBranch = EditorGUILayout.TextField("Branch name:", ContextForMode[transferModeFlag].repoBranch);

                    gitCredentialModeFlag = EditorGUILayout.Popup("Credential Mode:",
                        gitCredentialModeFlag, gitCredentialOptions, EditorStyles.popup);
                }
                
                ContextForMode[transferModeFlag].relativePath = EditorGUILayout.TextField(new GUIContent("Relative Path (?):", "Unity project path relative to repository root") , ContextForMode[transferModeFlag].relativePath);
                
                if (transferModeFlag == (int) TransferMode.GIT && gitCredentialModeFlag == (int)GitCredentialMode.GitToken)
                {
                    ContextForMode[transferModeFlag].repoToken = EditorGUILayout.TextField("Git Token:", ContextForMode[transferModeFlag].repoToken);
                }
                else 
                {
                    ContextForMode[transferModeFlag].repoUsername = EditorGUILayout.TextField("Repo Username:", ContextForMode[transferModeFlag].repoUsername);
                    ContextForMode[transferModeFlag].repoPassword = EditorGUILayout.PasswordField("Repo Password:", ContextForMode[transferModeFlag].repoPassword);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Separator();
            }
            else 
            {
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Upload", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                GUIStyle wraptStyle = new GUIStyle(EditorStyles.label);
                wraptStyle.wordWrap = true;
                EditorGUILayout.LabelField("* Remember to config Build-Settings for your build target(s), and SAVE project before packing.", wraptStyle);

                if (GUILayout.Button("Config Packing Directories"))
                {
                    UcbPackDirectoriesWindow.showWindow();
                }
                
                if (GUILayout.Button("Pack"))
                {
                    if (UcbFacade.GetInstance().CheckUnityVersion(unityVersion))
                    {
                        StartPackingProject();
                    }
                    else
                    {
                        ShowNotification(new GUIContent("Unsupported Unity version"));
                    }
                    
                }
                if (!string.IsNullOrEmpty(PathHelper.ProjectZipFullPath))
                {
                    EditorGUILayout.LabelField("Latest Local Pack:", File.GetLastWriteTime(PathHelper.ProjectZipFullPath).ToString());
                }

                if (transferModeFlag == (int)TransferMode.FTP)
                {
                    ftpHost = EditorGUILayout.TextField("FTP Host:", ftpHost);
                    ftpPort = EditorGUILayout.TextField("FTP Port:", ftpPort);
                    ftpUserName = EditorGUILayout.TextField("FTP Username:", ftpUserName);
                    ftpPassword = EditorGUILayout.PasswordField("FTP Password:", ftpPassword);
                }

                if (transferModeFlag == (int)TransferMode.COS)
                {
                    ContextForMode[transferModeFlag].cosUploadModeFlag = EditorGUILayout.Popup("Upload Mode:",
                        ContextForMode[transferModeFlag].cosUploadModeFlag, cosUploadModeOptions, EditorStyles.popup);
                }
                
                string uploadBtnText = "Upload";
                if (ContextForMode[transferModeFlag].isUploading || string.IsNullOrEmpty(PathHelper.ProjectZipFullPath))
                {
                    GUI.enabled = false;
                    if (ContextForMode[transferModeFlag].isUploading)
                    {
                        uploadBtnText = "Uploading...";
                    }
                }

                if (GUILayout.Button(uploadBtnText))
                { 
                    StartUploadProject();
                }

                GUI.enabled = true;

                EditorGUI.indentLevel--;
            }
            //
            // Build
            //
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            string latestBuildTime = GetLatestBuildTimeString();
            EditorGUILayout.LabelField("Select Build Target(s)");
            EditorGUI.indentLevel++;
            for (int i=0; i< ContextForMode[transferModeFlag].buildTargetSelection.Length; i++)
            {
                string buildTargetName = Enum.GetNames(typeof(BuildTarget))[i];
                Rect statusRect = EditorGUILayout.BeginHorizontal(GUILayout.Width(290));
                EditorGUILayout.LabelField(buildTargetName);
                ContextForMode[transferModeFlag].buildTargetSelection[i] = EditorGUILayout.Toggle(ContextForMode[transferModeFlag].buildTargetSelection[i]);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;

            if (ContextForMode[transferModeFlag].IsBuildTaskRunning())
            {
                if (GUILayout.Button("Cancel Cloud Build"))
                {
                    CancelCloudBuildAction();
                }
            }
            else
            {
                if (GUILayout.Button("Start Cloud Build"))
                {
                    if (!TransferModeUtils.IsRepository(transferModeFlag) && !ContextForMode[transferModeFlag].projectUploaded)
                    {
                        ShowNotification(new GUIContent("No Project Uploaded"));
                    }
                    else if (!IsBuildTargetSelected())
                    {
                        ShowNotification(new GUIContent("Select Build Target"));
                    }
                    else
                    {
                        StartCloudBuildAction();
                    }
                }
            }

            if (!string.IsNullOrEmpty(latestBuildTime))
            {
                EditorGUILayout.LabelField("Latest Build:", latestBuildTime);
            }

            if (string.IsNullOrEmpty(ContextForMode[transferModeFlag].taskId) || ContextForMode[transferModeFlag].taskInfo == null)
            {
                EditorGUILayout.LabelField("Build Status", "Not Started");
            }
            else
            {
                EditorGUILayout.LabelField("Build Status");
                EditorGUI.indentLevel++;
                JSONArray jobs = ContextForMode[transferModeFlag].taskInfo["jobs"].AsArray;
                for (int i=0; i< jobs.Count; i++)
                {
                    string statusString = jobs[i]["status"];
                    if (statusString == "FAILED")
                    {
                        GUI.color = new Color(255, 0, 0);
                    }
                    Rect statusRect = EditorGUILayout.BeginHorizontal(GUILayout.Width(290));
                    EditorGUILayout.LabelField(jobs[i]["buildTarget"], GUILayout.Width(170));
                    EditorGUILayout.LabelField(statusString, GUILayout.Width(100));
                    GUI.color = guiDefaultColor;
                    
                    if (IsBuildJobDone(i))
                    {
                        if (GUILayout.Button("Actions", GUILayout.Width(60), GUILayout.Height(14)))
                        {
                            if (!ContextForMode[transferModeFlag].jobActionOpened.ContainsKey(jobs[i]["name"]))
                            {
                                ContextForMode[transferModeFlag].jobActionOpened[jobs[i]["name"]] = true;
                            }
                            else
                            {
                                ContextForMode[transferModeFlag].jobActionOpened[jobs[i]["name"]] =
                                    !ContextForMode[transferModeFlag].jobActionOpened[jobs[i]["name"]];
                            }
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    
                    if (ContextForMode[transferModeFlag].jobActionOpened.ContainsKey(jobs[i]["name"]) &&
                         ContextForMode[transferModeFlag].jobActionOpened[jobs[i]["name"]] == true)
                    {
                        //download button group
                        try
                        {
                            if (jobs[i]["downloadLink"] != null)
                            {
                                if (GUILayout.Button("Download"))
                                {
                                    Application.OpenURL(jobs[i]["downloadLink"]);
                                    EditorUtility.ClearProgressBar();
                                }
                                if (GUILayout.Button("Copy Download URL"))
                                {
                                    EditorGUIUtility.systemCopyBuffer = jobs[i]["downloadLink"];
                                    ShowNotification(new GUIContent("Copied URL" + Environment.NewLine + 
                                                                    " to clipboard"));
                                    EditorUtility.ClearProgressBar();
                                }

                                if (jobs[i]["downloadLink"].ToString().Contains(".apk"))
                                {
                                    if (GUILayout.Button("QR Code"))
                                    {
                                        UcbQrPopup.Open(jobs[i]["downloadLink"], "Cloud Build Download Apk", "Scan QR code to download apk" + Environment.NewLine + "direct into your mobile devices");
                                    }    
                                }
                            }
                            //
                            //build just finished
                            //
                            if (jobs[i]["exectionLog"] != null || jobs[i]["logLink"] != null)
                            {
                                if (GUILayout.Button("Print Log"))
                                {
                                    ShowNotification(new GUIContent("Printed to" + Environment.NewLine + "console"));
                                    if (jobs[i]["exectionLog"] != null)
                                    {
                                        Debug.Info("Build Execution Log :" + jobs[i]["exectionLog"]);
                                    }
                                    if (jobs[i]["logLink"] != null)
                                    {
                                        Debug.Info("Editor Log in Link:" + jobs[i]["logLink"]);
                                        Debug.Info("Editor Log Content:" + ftpInstance.GetEditorLog(jobs[i]["logLink"]));
                                    }
                                }
                                EditorGUILayout.Separator();
                            }
                        }
                        catch (NullReferenceException ex)
                        {
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            if (ContextForMode[transferModeFlag].taskId != null)
            {
                if (GUILayout.Button("Track This Task by WeChat"))
                {
                    string url = Constants.WEAPP_QR_PREFIX + ContextForMode[transferModeFlag].taskId;
                    UcbQrPopup.Open(url, "QR for WeApp Monitor", "Scan QR code by WeChat" + Environment.NewLine + "to monitor this task");
                }
            }
            
            EditorGUI.indentLevel--;
            if (ContextForMode[transferModeFlag].isProgressBarVisible)
            {
                string text = ContextForMode[transferModeFlag].progressTitle;
                if (1 - ContextForMode[transferModeFlag].progressValue > 0.0001)
                {
                    text += String.Format(" [ {0}% ]", (int)(ContextForMode[transferModeFlag].progressValue * 100));   
                }
                
                EditorGUI.ProgressBar(new Rect(3, position.height - 30, position.width - 6, 20),
                    ContextForMode[transferModeFlag].progressValue, text);
            }
            

            EditorGUILayout.Separator();
            GUIStyle foldOutStyle = new GUIStyle(EditorStyles.foldout);
            foldOutStyle.fontStyle = FontStyle.Bold;
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", foldOutStyle);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                apiHost = EditorGUILayout.TextField("API Host:", apiHost);
                apiPort = EditorGUILayout.TextField("API Port:", apiPort);
                cosEndPoint = EditorGUILayout.TextField("COS Host:", cosEndPoint);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void OnTransferModeChange()
        {
            SyncBuildTaskInfo();
        }

        private bool IsBuildTargetSelected()
        {
            return ContextForMode[transferModeFlag].buildTargetSelection.Any(target => target);
        }

        private void DrawQrCode(string content)
        {
            float margin = 10;
            float qrSize = Math.Min(position.width - margin * 2, 200);
            margin = Math.Max(margin, (position.width - qrSize) / 2);

            GUI.DrawTexture(new Rect(margin, position.height - 300, qrSize, qrSize), UcbUtils.QRHelper.generateQR(content), ScaleMode.ScaleToFit);
        }

        //
        // Pack
        //
        void StartPackingProject()
        {
            OnZipProgressChange(0f);
            try
            {
                ZipHandler.CleanPreviousZip(PathHelper.ZipDirectory);
                PathHelper.ProjectZipFullPath = 
                    ZipHandler.CompressProject(PathHelper.ProjectDirectory, PathHelper.ZipDirectory + "project-pack.zip", new BasicProgress<double>(OnZipProgressChange));
                Debug.Log(PathHelper.ProjectZipFullPath);
            }
            catch (IOException ex)
            {
                //file hash exists
                Debug.LogError(ex);
                ShowNotification(new GUIContent(ex.Message));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void OnZipProgressChange(double value)
        {
            //Debug.Log(String.Format($"{value:P2} archiving complete"));
            EditorUtility.DisplayProgressBar("Packing Project", "Don't do any modification to your project before packing finished.", (float)value);
        }

        //
        // Upload
        //
        void StartUploadProject()
        {
            SaveGlobalConfig();
            
            //check if IL2CPP
            if (Utils.IL2CPP())
            {
                EditorUtility.DisplayDialog("Warning",
                    "Scripting Backend: IL2CPP detected. Please notice, to some build targets [Standalone, OSX, Android], IL2CPP is not supported.", "OK");
            }
            
            try
            {
                ContextForMode[transferModeFlag].StartUploadProject(PathHelper.ProjectZipFullPath, ftpInstance);
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(CosFileExistsException))
                {
                    ShowNotification(new GUIContent("File Exists"));
                }
                else
                {
                    Debug.LogError(ex);
                    ShowNotification(new GUIContent("Upload Failed"));
                }
            }
        }
        
        //
        // Build Options
        //
        private JSONNode BuildStartCloudBuildOptions()
        {
            JSONNode result = JSON.Parse("{}");
            result["projectUuid"] = Utils.GetProjectId();
            
            result["type"] = "BUILD";
            result["uploadType"] = transferModeOptions[transferModeFlag];
            result["parameters"]["unityVersion"] = unityVersion;
            result["parameters"]["displayName"] = string.Format(@"{0}(v{1})", PlayerSettings.productName, PlayerSettings.bundleVersion);
            result["parameters"]["productName"] = PlayerSettings.productName;
            result["parameters"]["bundleVersion"] = PlayerSettings.bundleVersion;
            
            if (!string.IsNullOrEmpty(PathHelper.ProjectZipFullPath))
            {
                result["parameters"]["projectHash"] = Utils.GetProjectHash(PathHelper.ProjectZipFullPath);
            }

            for (int i=0; i<ContextForMode[transferModeFlag].buildTargetSelection.Length; i++)
            {
                if (ContextForMode[transferModeFlag].buildTargetSelection[i])
                {
                    result["parameters"]["buildTargets"][-1] = Enum.GetName(typeof(BuildTarget), i);
                }
            }

            if (transferModeFlag == (int)TransferMode.FTP)
            {
                result["parameters"]["ftpServer"] = ftpHost;    
                result["parameters"]["ftpPort"] = ftpPort;
                result["parameters"]["ftpUser"] = ftpUserName;
                result["parameters"]["ftpPwd"] = ftpPassword;
            }

            if (TransferModeUtils.IsRepository(transferModeFlag))
            {
                result["parameters"]["repoUrl"] = ContextForMode[transferModeFlag].repoUrl;
                result["parameters"]["repoBranch"] = ContextForMode[transferModeFlag].repoBranch;
                result["parameters"]["relativePath"] = ContextForMode[transferModeFlag].relativePath;
                if (TransferModeUtils.IsGit(transferModeFlag) && !string.IsNullOrEmpty(ContextForMode[transferModeFlag].repoToken))
                {
                    result["parameters"]["repoToken"] = ContextForMode[transferModeFlag].repoToken;
                }
                else
                {
                    result["parameters"]["repoUser"] = ContextForMode[transferModeFlag].repoUsername;
                    result["parameters"]["repoPwd"] = ContextForMode[transferModeFlag].repoPassword;
                }
            }

            return result;
        }
        

        void StartCloudBuildAction()
        {
            //check if IL2CPP
            if (ContextForMode[transferModeFlag].BadScriptingBackend())
            {
                EditorUtility.DisplayDialog("Error",
                    "Can't start Cloud Build.\nScripting Backend: IL2CPP detected. To some build targets [Standalone, OSX, Android], IL2CPP is not supported. Please check you player settings.", "OK");
                return;
            }
            
            ContextForMode[transferModeFlag].HideProgress();
            ContextForMode[transferModeFlag].ResetJobsWithActionsOpened();

            JSONNode data = BuildStartCloudBuildOptions();
            try
            {
                JSONNode response = ucbFacade.PostTask(data);

                ContextForMode[transferModeFlag].taskId = response["taskUuid"];
                ContextForMode[transferModeFlag].taskInfo = response;
                ContextForMode[transferModeFlag].UpdateSyncTaskInterval();
                SaveProjectConfig();

                Debug.Log(string.Format(@"Build Task [{0}] started with Jobs [{1}].",
                    ContextForMode[transferModeFlag].taskId, response["jobs"].ToString()));
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                ShowNotification(new GUIContent(ex.Message));
            }
        }

        void CancelCloudBuildAction()
        {
            if (!string.IsNullOrEmpty(ContextForMode[transferModeFlag].taskId))
            {
                try
                {
                    ucbFacade.CancelTask(ContextForMode[transferModeFlag].taskId);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    ShowNotification(new GUIContent(ex.Message));
                }
            }
        }
        
        bool IsBuildJobDone(int index) 
        {
            if (ContextForMode[transferModeFlag].taskInfo == null || ContextForMode[transferModeFlag].taskInfo["jobs"].IsNull)
            {
                return false;
            }

            bool result = false;
            try
            {
                int status = (int) Enum.Parse(typeof(JobStatus), ContextForMode[transferModeFlag].taskInfo["jobs"][index]["status"]);
                if (status > (int)JobStatus.RUNNING && status != (int)JobStatus.CANCELLED)
                {
                    result = true;
                }
            }
            catch ( Exception ex)
            {
                Debug.LogError(ex);
            }

            return result;
        }

        //
        // Collecting informations
        //
        void SyncBuildTaskInfo()
        {
            ContextForMode[transferModeFlag].SyncBuildTaskInfo();
        }

        string GetLatestBuildTimeString()
        {
            try
            {
                return DateTime.Parse(ContextForMode[transferModeFlag].taskInfo["createdTime"]).ToLocalTime().ToString();
            }
            catch (NullReferenceException ex)
            {
                //Debug.Log(ex);
                return null;
            }
        }

        string GetLatestZip()
        {
            string[] fileNames = Directory.GetFiles(PathHelper.ZipDirectory);
            string result = null;
            DateTime latestZipTime = DateTime.MinValue;
            foreach (string fileName in fileNames)
            {
                if (fileName.Contains(".zip"))
                {
                    DateTime thisZipTime = File.GetLastWriteTime(fileName);
                    if (thisZipTime > latestZipTime)
                    {
                        result = fileName;
                        latestZipTime = thisZipTime;
                    }
                }
            }
            if (!string.IsNullOrEmpty(result))
            {
                PathHelper.ProjectZipFullPath = result;
                Debug.Log(PathHelper.ProjectZipFullPath);
                return File.GetLastWriteTime(result).ToLongTimeString();
            }
            return null;
        }

        //
        // Project Config
        //
        void SaveProjectConfig()
        {
            JSONNode configJson = Utils.GetProjectSettingsJsonNode();
            if (configJson == null)
            {
                configJson = JSONNode.Parse("{}");
            }

            configJson["PackingDirectories"] = JsonHelper.ToJsonArray(PackingDirectories.Directories);
            
            foreach (TransferMode t in Enum.GetValues(typeof(TransferMode)))
            {
                int modeInt = (int) t;
                string modeString = Enum.GetName(typeof(TransferMode), t);
                
                configJson["taskId"][modeString] = ContextForMode[(int)t].taskId;
                configJson["repoInfo"][modeString]["repoUsername"] = ContextForMode[modeInt].repoUsername;
                configJson["repoInfo"][modeString]["repoPassword"] = ContextForMode[modeInt].repoPassword;
                configJson["repoInfo"][modeString]["repoUrl"] = ContextForMode[modeInt].repoUrl;
                configJson["repoInfo"][modeString]["repoBranch"] = ContextForMode[modeInt].repoBranch;
                configJson["repoInfo"][modeString]["repoToken"] = ContextForMode[modeInt].repoToken;
            }

            Debug.Log("Save Project Config: " + configJson);
            Utils.SaveProjectSettings(configJson);
        }

        void LoadProjectConfig()
        {
            JSONNode configJson = Utils.GetProjectSettingsJsonNode();
            if (configJson == null)
            {
                Debug.Info("Load Project Config: Nothing to lowwwwad");
                return;
            }
            
            List<string> pDirs = JsonHelper.ToStringList(configJson["PackingDirectories"].AsArray);
            if (pDirs.Count > 0 && pDirs[0].Contains(PathHelper.ProjectDirectory))
            {
                PackingDirectories.Directories = pDirs;
            }
            else 
            {
                if (pDirs.Count > 0)
                {
                    Debug.LogError("Failed loading Packing Directories. Please config it again before do Packing.");
                }
                PackingDirectories.InitPackingDirectories();
                
            }

            foreach (TransferMode t in Enum.GetValues(typeof(TransferMode)))
            {
                int modeInt = (int) t;
                string modeString = Enum.GetName(typeof(TransferMode), t);

                ContextForMode[modeInt].taskId = configJson["taskId"][modeString];
                ContextForMode[modeInt].repoUsername = configJson["repoInfo"][modeString]["repoUsername"];
                ContextForMode[modeInt].repoPassword = configJson["repoInfo"][modeString]["repoPassword"];
                ContextForMode[modeInt].repoUrl = configJson["repoInfo"][modeString]["repoUrl"];
                ContextForMode[modeInt].repoBranch = configJson["repoInfo"][modeString]["repoBranch"];
                ContextForMode[modeInt].repoToken = configJson["repoInfo"][modeString]["repoToken"];
            }
        }


        //
        // Global config
        //
        // Save user related information to appdata path of Unity editor.
        //
        

        void SaveGlobalConfig()
        {
            try
            {
                JSONNode configJson = JSON.Parse("{}");
                configJson["ftpHost"] = ftpHost;
                configJson["ftpPort"] = ftpPort;
                configJson["ftpUserName"] = ftpUserName;
                configJson["ftpPassword"] = ftpPassword;
                configJson["apiHost"] = apiHost;
                configJson["apiPort"] = apiPort;
                configJson["cosEndPoint"] = cosEndPoint;
                File.WriteAllText(PathHelper.GlobalConfigPath, configJson.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        void LoadGlobalConfig()
        {
            try
            {
                string configJsonString = File.ReadAllText(PathHelper.GlobalConfigPath);
                JSONNode configJson = JSON.Parse(configJsonString);
                ftpHost = configJson["ftpHost"];
                ftpPort = configJson["ftpPort"];
                ftpUserName = configJson["ftpUserName"];
                ftpPassword = configJson["ftpPassword"];
                apiHost = configJson["apiHost"];
                apiPort = configJson["apiPort"];
                cosEndPoint = configJson["cosEndPoint"];
            }
            catch (Exception ex)
            {
                Debug.Info("No Global Config");
            }
        }
        
        void OnInspectorUpdate()
        {
            this.Repaint();
        }
    }
}
#endif