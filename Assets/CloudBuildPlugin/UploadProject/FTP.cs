using System;
using System.IO;
using System.Net;
using CloudBuildPlugin.Common;
using Debug = CloudBuildPlugin.Common.Debug;

namespace UploadProject
{
    public class FTP
    {
        public delegate void OnCompleteCallback(Exception ex);
        
        private string remoteHost;
        private string remotePort;
        private string username;
        private string password;

        public FTP()
        {
            this.remoteHost = string.Empty;
            this.remotePort = "21";
            this.username = string.Empty;
            this.password = string.Empty;
        }

        public FTP(string remoteHost, string username, string password)
        {
            this.remoteHost = remoteHost;
            this.remotePort = "21";
            this.username = username;
            this.password = password;
        }

        public string getRemoteHost()
        {
            return remoteHost;
        }

        public void setRemoteHost(string remoteHost)
        {
            this.remoteHost = remoteHost;
        }

        public string getRemotePort()
        {
            return remotePort;
        }

        public void setRemotePort(string remotePort)
        {
            this.remotePort = remotePort;
        }

        public string getUsername()
        {
            return username;
        }

        public void setUsername(string username)
        {
            this.username = username;
        }

        public string getPassword()
        {
            return password;
        }

        public void setPassword(string password)
        {
            this.password = password;
        }

        public string GetEditorLog(string logUrl)
        {
            Uri uri = new Uri(logUrl);
            Console.WriteLine("ftp url is {0} - {1}", uri, DateTime.Now);
            WebClient request = new WebClient();

            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential(username, password);
            try
            {
                byte[] newFileData = request.DownloadData(logUrl);
                string fileString = System.Text.Encoding.UTF8.GetString(newFileData);
                return fileString;
            }
            catch (WebException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public void DownloadProject(string downloadPath, string localStoragePath, IProgress<double> progress)
        {
            Console.WriteLine("Start to download apk from FTP server - {0}", DateTime.Now);

            Uri uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}", remoteHost, remotePort, downloadPath));
            Console.WriteLine("ftp url is {0} - {1}", uri, DateTime.Now);

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = false;
            request.KeepAlive = true;
            request.UseBinary = true;

            request.Method = WebRequestMethods.Ftp.DownloadFile;

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            long totalBytes = response.ContentLength, currentBytes = 0;

            Stream responseStream = response.GetResponseStream();

            using (FileStream fileStream = new FileStream(localStoragePath, FileMode.Create))
            {
                int bufferSize = 2048;
                int readCount;
                byte[] buffer = new byte[bufferSize];

                readCount = responseStream.Read(buffer, 0, bufferSize);
                while (readCount > 0)
                {
                    currentBytes += readCount;
                    progress.Report(currentBytes / (double)totalBytes);

                    fileStream.Write(buffer, 0, readCount);
                    readCount = responseStream.Read(buffer, 0, bufferSize);
                }
            }

            Console.WriteLine("Download Complete, status {0} - {1}", response.StatusDescription, DateTime.Now);

            response.Close();
        }

//        public async Task AsyncUploadProject(string fileName, string projectId, IProgress<double> progress)
//        {
//            AsyncFunction(fileName, projectId, progress);
//        }


//        private Task AsyncFunction(string fileName, string projectId, IProgress<double> progress)
//        {
//            return Task.Run(() =>
//            {
//                UploadProject(fileName, projectId, progress);
//                return;
//            });
//        }

        public void AsyncUploadProject(string fileName, string projectId, IProgress<double> progress, OnCompleteCallback onComplete)
        {
            Func<string, string, IProgress<double>, Exception> fun = t_UploadProject;
            fun.BeginInvoke(fileName, projectId, progress, (ar =>
            {
                Debug.Log("Async FTP Upload Ended.");
                if (ar == null)
                    throw new ArgumentNullException("ar");
                Func<string, string, IProgress<double>, Exception> dl = (Func<string, string, IProgress<double>, Exception>)ar.AsyncState;
                Exception result = dl.EndInvoke(ar);
                onComplete(result);    
            }), fun);
            Debug.Log("Async FTP Upload Start.");
        }

        private Exception t_UploadProject(string fileName, string projectId, IProgress<double> progress)
        {
            try
            {
                UploadProject(fileName, projectId, progress);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
        
        public void UploadProject(string fileName, string projectId, IProgress<double> progress)
        {
            long offset = GetFileOffset(fileName, projectId);
            Console.WriteLine("file offset {0}", offset);
            long fileLength = new FileInfo(fileName).Length;
            if (offset == fileLength)
            {
                throw new CosFileExistsException("File Exists");
            }

            Debug.Log(string.Format(@"Start to upload zip file to ftp server - {0}", DateTime.Now));

            Uri uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}/{3}", remoteHost, remotePort, projectId, Path.GetFileName(fileName)));
            Debug.Log(string.Format(@"ftp url is {0} - {1}", uri, DateTime.Now));

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = false;
            request.KeepAlive = true;
            request.UseBinary = true;

            request.ContentOffset = offset;

            if (offset > 0L) {
                request.Method = WebRequestMethods.Ftp.AppendFile;
            } else
            {
                request.Method = WebRequestMethods.Ftp.UploadFile;
            }

            Debug.Log(string.Format(@"*** {0} ***", DateTime.Now));
            Stream dest = request.GetRequestStream();
            Debug.Log(string.Format(@"*** {0} ***", DateTime.Now));

            FileStream src = File.OpenRead(fileName);
            src.Position = offset;

            int bufSize = (int)Math.Min(src.Length, 2048);
            byte[] buffer = new byte[bufSize];
            int bytesRead = 0;
            long currentBytes = 0;

            do
            {
                bytesRead = src.Read(buffer, 0, bufSize);
                dest.Write(buffer, 0, bufSize);

                currentBytes += bytesRead;
                progress.Report(currentBytes / (double)src.Length);
            }
            while (bytesRead != 0);

            dest.Close();
            src.Close();

            Debug.Log(string.Format(@"Finish to upload zip file to ftp server - {0}", DateTime.Now));

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                Debug.Log("Upload File Complete, status " + response.StatusDescription);
            }
        }

        private long GetFileOffset(string fileName, string projectId)
        {
            Uri uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}", remoteHost, remotePort, projectId));
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            request.UsePassive = false;
            request.UseBinary = true;
            request.Credentials = new NetworkCredential(username, password);

            try
            {
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    Console.WriteLine(@"Make directory {0}, status {1}", projectId, response.StatusDescription);
                    return 0L;
                }

            }
            catch (WebException ex)
            {
                FtpWebResponse resp = (FtpWebResponse)ex.Response;
                if (resp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}/{3}", remoteHost, remotePort, projectId, Path.GetFileName(fileName)));
                    request = (FtpWebRequest)WebRequest.Create(uri);
                    request.Method = WebRequestMethods.Ftp.GetFileSize;
                    request.UsePassive = false;
                    request.UseBinary = true;
                    request.Credentials = new NetworkCredential(username, password);

                    try
                    {
                        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                        {
//                            Debug.Log(response.LastModified);  // for resume uploading
                            return response.ContentLength;
                        }
                    }
                    catch (WebException e)
                    {
                        Console.WriteLine(e);
                        return 0L;
                    }
                }
            }

            return 0L;
        }
    }
}
