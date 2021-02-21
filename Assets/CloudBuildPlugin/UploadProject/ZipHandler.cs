using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CloudBuildPlugin.Common;
using Ionic.Zip;

namespace CloudBuildPlugin.UploadProject
{
    public static class Extension
    {    
        public static IEnumerable<FileInfo> GetFilesByPackDirectories(this DirectoryInfo dirInfo)
        {
            List<string> pDirs = PackingDirectories.Directories;

            return dirInfo.GetFiles("*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    foreach (string pDir in pDirs)
                    {
                        if (f.FullName.Contains(PathHelper.GetFullPath(pDir)))
                        {
                            return true;
                        }
                    }

                    return false;
                });
        }
    }

    public class ZipHandler
    {
        public static string[] fileFilter = { ".csproj", ".sln", ".suo", ".tmp", ".userprefs", ".app", ".VC.", ".DS_Store", ".swp", ".log", ".pyc", ".git", ".svn", ".hg", ".vs", "CloudBuildPlugin.meta", "ShaderCache.db", "UnityLockfile" };
        public static string[] dirFilter = { "/Build/", "/Builds/", "/temp/", "/build/", "/builds/", "/CloudBuildPlugin/" };

        public static string CompressProject(string source, string target, IProgress<double> progress)
        {
            try
            {
                File.Delete(target);
            }
            catch (IOException ex)
            {
                Debug.LogError(ex);
            }

            Console.WriteLine("Start to compress project - {0}", DateTime.Now);
            CreateZip(source, target, true, fileInfo => !SkipToCompress(fileInfo), progress);
            Console.WriteLine("Finish to compress project - {0}", DateTime.Now);

            //md5 hash
            string MD5Hash = CalculateMD5(target);
            Console.WriteLine("MD5Hash is {0}", MD5Hash);

            string newFileName = String.Format("{0}/{1}.zip", Path.GetDirectoryName(target), MD5Hash);
            Console.WriteLine("Renamed zip file is in {0}", newFileName);
            File.Move(target, newFileName);

            return newFileName;
        }
    
    
    
        public static void CreateZip(string sourceDirectoryName, string destinationArchiveFileName, bool includeBaseDirectory, Predicate<FileInfo> filter, IProgress<double> progress)
        {
            FileInfo[] sourceFiles = new DirectoryInfo(sourceDirectoryName).GetFilesByPackDirectories().ToArray();//.GetFiles("*", SearchOption.AllDirectories);

            double totalBytes = sourceFiles.Sum(f => {
                if (!filter(f))
                {
                    return 0L;
                }
                else
                {
                    return f.Length;
                }
            });
            long currentBytes = 0;

#if UNITY_2018_1_OR_NEWER
            using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                {
                    foreach (FileInfo file in sourceFiles)
                    {
                        if (!filter(file))
                        {
                            Console.WriteLine(file.FullName);
                            continue;
                        }

                        string entryName = GetEntryName(file.FullName, sourceDirectoryName, includeBaseDirectory);
                        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                        entry.LastWriteTime = file.LastWriteTime;

                        using (Stream inputStream = File.OpenRead(file.FullName))
                        using (Stream outputStream = entry.Open())
                        {
                            Stream progressStream = new global::UploadProject.StreamWithProgress(inputStream,
                                new global::UploadProject.BasicProgress<int>(i =>
                                {
                                    currentBytes += i;
                                    progress.Report(currentBytes / totalBytes);
                                }), null);

                            progressStream.CopyTo(outputStream);
                            inputStream.Dispose();
                        }
                    }
                }
            }
#else
        using (ZipFile zip = new ZipFile(Encoding.UTF8))
        {
            zip.UseZip64WhenSaving = Zip64Option.AsNecessary;
            foreach (FileInfo file in sourceFiles)
            {
                if (!filter(file))
                {
                    Console.WriteLine(file.FullName);
                    continue;
                }

                string entryName = GetEntryName(file.FullName, sourceDirectoryName, includeBaseDirectory);
                zip.AddFile(file.FullName, Path.GetDirectoryName(entryName));

                currentBytes += file.Length;
                progress.Report(currentBytes / (double)totalBytes);
            }

            zip.Save(destinationArchiveFileName);
        }
#endif
        }

        private static string GetEntryName(string name, string sourceFolder, bool includeBaseName)
        {
            if (name == null || name.Length == 0)
                return String.Empty;

            if (includeBaseName)
                sourceFolder = Path.GetDirectoryName(sourceFolder);

            int length = string.IsNullOrEmpty(sourceFolder) ? 0 : sourceFolder.Length;
            if (length > 0 && sourceFolder != null && sourceFolder[length - 1] != Path.DirectorySeparatorChar && sourceFolder[length - 1] != Path.AltDirectorySeparatorChar)
                length++;

            return name.Substring(length);
        }

        private static bool SkipToCompress(FileInfo file)
        {
//            if (file.Attributes.HasFlag(FileAttributes.Hidden))
//            {
//                return true;
//            }

            string fileName = PathHelper.RemoveBackslash(file.FullName);

            return fileFilter.Any(c => fileName.Contains(c)) || dirFilter.Any(c => fileName.Contains(c));
        }

        public static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    StringBuilder sBuilder = new StringBuilder();

                    for (int i = 0; i < hash.Length; i++)
                    {
                        sBuilder.Append(hash[i].ToString("x2"));
                    }

                    return sBuilder.ToString();
                }
            }
        }
        
        public static void CleanPreviousZip(string dir)
        {
            string[] zipList = Directory.GetFiles(dir, "*.zip");
            foreach (string f in zipList)
            {
                try
                {
                    File.Delete(f);
                }
                catch (IOException ex)
                {
                    Debug.Log(ex);
                }
            }
        }

    }
}