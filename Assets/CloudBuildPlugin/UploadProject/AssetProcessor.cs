using System;
using System.IO;
using System.Linq;

namespace UploadProject
{
    static class AssetProcessor
    {
        private static string textureFlag = "TextureImporter";

        public static void filterTexture(string sourcePath, string targetPath)
        {
            FileInfo[] fileInfos = new DirectoryInfo(sourcePath).GetFiles("*", SearchOption.AllDirectories);

            foreach (FileInfo fileInfo in fileInfos) {
                //Console.WriteLine(fileInfo.FullName);

                if (fileInfo.FullName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = Path.Combine(Path.GetDirectoryName(fileInfo.FullName), Path.GetFileNameWithoutExtension(fileInfo.FullName));
                    if (File.GetAttributes(fileName).HasFlag(FileAttributes.Directory))
                    {
                        Directory.CreateDirectory(fileName.Replace(sourcePath, targetPath));

                    } else
                    {
                        if (File.ReadLines(fileInfo.FullName).Any(line => line.Contains(textureFlag)))
                        {
                            File.Copy(fileName, fileName.Replace(sourcePath, targetPath));
                        }
                    }

                    File.Copy(fileInfo.FullName, fileInfo.FullName.Replace(sourcePath, targetPath));
                }
            }
        }

        public static void splitAsset(string dirPath, int i, int total)
        {
            FileInfo[] fileInfos = new DirectoryInfo(dirPath).GetFiles("*", SearchOption.AllDirectories);

            int count = 0;
            foreach (FileInfo fileInfo in fileInfos)
            {
                if (fileInfo.FullName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                count++;
                Console.WriteLine(fileInfo.FullName);

                if (count != i)
                {
                    File.Delete(fileInfo.FullName);
                    string metaFilePath = fileInfo.FullName + ".meta";
                    File.Delete(metaFilePath);
                }

                if (count == total)
                    count = 0;
            }
        }
    }
}
