using System;
using System.IO;
using System.IO.Compression;
using CloudBuildPlugin.Enums;
using static UploadProject.CosFacade;

namespace UploadProject
{
    class MainClass
    {
        public static void UploadProject(TransferMode mode, string fileName, string projectId)
        {
            switch (mode)
            {
                case TransferMode.FTP:
                    FTP ftp = new FTP();
                    ftp.setRemoteHost("101.132.99.72");
                    ftp.setUsername("test_ftp");
                    ftp.setPassword("#Bugsfor$");

                    //ftp.UploadProject(fileName, projectId, new BasicProgress<double>(p => Console.WriteLine($"{p:P2} uploaded to ftp server.")));
                    break;

                case TransferMode.COS:
                    CosFacade cos = GetInstance(projectId);
                    cos.UploadProject(fileName, projectId, null, null, null, null);
                    break;

                default:
                    Console.WriteLine("Not supported mode!");
                    return;
            }
        }

        public static void Download(TransferMode mode, string downloadPath, string localDir, string localFileName)
        {
            switch (mode)
            {
                case TransferMode.FTP:
                    //todo
                    break;

                case TransferMode.COS:
                    CosFacade cos = CosFacade.GetInstance("test");
                    //cos.DownloadFile(downloadPath, localDir, localFileName);
                    break;

                default:
                    Console.WriteLine("Not supported mode!");
                    return;
            }
        }


        public static void Main(string[] args)
        {
            string source = "/Users/Shared/Unity/Standard Assets Example Project";
            string target = "/Users/Shared/Unity/Standard Assets Example Project.zip";
            string projectId = "987654321";

            //string newFileName = ZipHelper.CompressProject(source, target, null);

            //UploadProject(TransferMode.FTP, newFileName, projectId);
            //UploadProject(TransferMode.COS, newFileName, projectId);

//            File.Delete(newFileName);

            //string downloadPath = String.Format("{0}/{1}.apk", projectId, Path.GetFileNameWithoutExtension(newFileName));
            string downloadPath = String.Format("{0}/{1}", "test", "test.apk");

            Download(TransferMode.COS, downloadPath, "/Users/Shared/Unity/", "test.apk");

            Console.ReadKey();
        }
    }
}