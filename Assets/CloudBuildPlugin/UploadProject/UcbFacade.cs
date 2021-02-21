using System;
using System.IO;
using System.Net;
using SimpleJSON;
using System.Text;
using CloudBuildPlugin.Common;
using Newtonsoft.Json;
using UnityEditor;

namespace UploadProject
{
    public class UcbFacade
    {
        const string defaultApiHost = "https://api.ccb.unity.cn";
        const string defaultApiPort = "443";

        private string host;
        private string port;

        private static UcbFacade instance;

        public string getHost()
        {
            return host;
        }

        public void setHost(string host)
        {
            this.host = host;
        }

        public string getPort()
        {
            return port;
        }

        public void setPort(string port)
        {
            this.port = port;
        }

        public static UcbFacade GetInstance()
        {
            lock (typeof(UcbFacade))
            {
                if (instance == null)
                {
                    instance = new UcbFacade();
                }
            }
            return instance;
        }

        public UcbFacade()
        {
            setHost(defaultApiHost);
            setPort(defaultApiPort);
        }

        public void UpdateHost(string apiHost, string apiPort)
        {
            if (string.IsNullOrEmpty(apiHost) || string.IsNullOrEmpty(apiPort))
            {
                setHost(defaultApiHost);
                setPort(defaultApiPort);
            }
            else
            {
                setHost(apiHost);
                setPort(apiPort);
            }
        }

        public CosInfo GetCredential(string projectId)
        {
            Uri uri = new Uri($"{host}:{port}/v1/credential/{projectId}");
            Console.WriteLine(uri);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";
            request.Headers.Add("Authorization", "Bearer " + Utils.TryGetCloudProjectSettings("accessToken"));
            request.Headers.Add("x-user-id", Utils.TryGetCloudProjectSettings("userId"));
            
            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Console.WriteLine("Success to get credential from ucb server.");
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            var temp = reader.ReadToEnd();
                            return JsonConvert.DeserializeObject<CosInfo>(temp);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error to get credential from ucb server.");
                        return null;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;
                IsForbidden(response);
                throw;
            }
        }

        private bool IsForbidden(HttpWebResponse response)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var msg = reader.ReadToEnd();
                    JSONNode result = JSONNode.Parse(msg);
                    MessageQueue.Enqueue(new UcbNotificationMessage(UcbNotificationMessageTypes.Error, result["errorMessage"]));
                }
                
                return true;
            }

            return false;
        }

        public JSONNode PostTask(JSONNode data)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}", host, port, @"v1/task"));
            Console.WriteLine(uri);
            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", "Bearer " + Utils.TryGetCloudProjectSettings("accessToken"));
            Debug.Log("Post Url: " + uri.ToString());
            Debug.Log(data.ToString());
            byte[] dataBytes = Encoding.UTF8.GetBytes(data.ToString());
            request.ContentLength = dataBytes.Length;
            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(dataBytes, 0, dataBytes.Length);
                reqStream.Close();
            }
            
            return TryGetResponse(request);
        }

        public JSONNode GetTaskDetail(string taskId)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}/{3}", host, port, @"v1/task", taskId));
//            Debug.Log("Get Task Url: " + uri.ToString());
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            return TryGetResponse(request);
        }

        
        public JSONNode CancelTask(string taskId)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}/{3}/cancel", host, port, @"v1/task", taskId));
            Console.WriteLine(uri);
            //Debug.Log("Get Task Url: " + uri.ToString());
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.Accept = "application/json";
            
            return TryGetResponse(request);
        }

        public bool CheckUnityVersion(string unityVersion)
        {
#if UNITY_2018_1_OR_NEWER
#else
            ServicePointManager.ServerCertificateValidationCallback =
                delegate(object s, X509Certificate certificate,
                    X509Chain chain, SslPolicyErrors sslPolicyErrors)
                { return true; };
#endif
            
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}/pre-check", host, port, @"v1/build-task"));
            Console.WriteLine(uri);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.ContentType = "application/json";

            JSONNode data = JSON.Parse("{}");
            data["unityVersion"] = unityVersion;
            byte[] dataBytes = Encoding.UTF8.GetBytes(data.ToString());
            request.ContentLength = dataBytes.Length;
            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(dataBytes, 0, dataBytes.Length);
                reqStream.Close();
            }
            
            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            bool result = Boolean.Parse(reader.ReadToEnd());
                            return result;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                using (var stream = ex.Response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    JSONNode result = JSON.Parse(reader.ReadToEnd());
                    throw new WebException(result["errorMessage"]);
                }
            }

            return false;
        }

        
        private JSONNode TryGetResponse(HttpWebRequest request)
        {
            try
            {
                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            JSONNode result = JSON.Parse(reader.ReadToEnd());
//                            Debug.Log(result);
                            return result;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                using (var stream = ex.Response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    JSONNode result = JSON.Parse(reader.ReadToEnd());
                    throw new WebException(result["errorMessage"], ex);
                }
            }

            return null;
        }
        
        public void DeltaUpload(DeltaInfo deltaInfo)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}", host, port, @"v1/delta-upload"));

            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.ContentType = "application/json";
            byte[] byteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(deltaInfo));
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
        }
        
        public string GetPatchStatus(string fileMD5)
        {
            Uri uri = new Uri($"{host}:{port}/v1/delta-upload/{fileMD5}");

            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        return reader.ReadToEnd();
                    }
                }
                return null;
            }
        }

        public void InitDeltaUpload(DeltaInfo deltaInfo)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}", host, port, @"v1/delta-upload-init"));

            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.ContentType = "application/json";
            byte[] byteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(deltaInfo));
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
        }
    }
}
