using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CloudBuildPlugin.Common
{
    public class PathHelper
    {
        private static string _cloudBuildTempPath;

        static string cloudBuildTempPath = Constants.PACK_TEMP_DIRECTORY;
            //=  Constants.PACK_TEMP_DIRECTORY;
        static string projectConfigPath;
        static string projectConfigFileName = Constants.PROJECT_CONFIG_FILE_NAME;
        static string globalConfigFileName = Constants.GLOBAL_CONFIG_FILE_NAME;
        static string osxGlobalConfigPath = Constants.OSX_GLOBAL_CONFIG_DIRECTORY;
        
        
        private static string globalConfigPath;

        private static string projectPath, projectZipFullPath;

        private static void InitProjectPath()
        {
            projectPath = Application.dataPath;
            DirectoryInfo projectInfo = Directory.GetParent(projectPath);
            projectPath = projectInfo.FullName;
            Debug.Log("Project path: " + projectPath);

            if (!Directory.Exists(projectPath + cloudBuildTempPath))
            {
                Directory.CreateDirectory(projectPath + cloudBuildTempPath);
            }
        }

        private static void InitGlobalConfigPath()
        {
            //use project temp path for undetermined OS
            globalConfigPath = PathHelper.ZipDirectory + globalConfigFileName;

            if (SystemInfo.operatingSystem.Contains("Windows"))
            {
                globalConfigPath = Application.persistentDataPath.Substring(0, Application.persistentDataPath.LastIndexOf("LocalLow"));
                
                string patternForDirVersion = @"([0-9]{4}.[0-9]*)";
                string newVersion = new Regex(patternForDirVersion).Match(Utils.UnityVersion).Value;
                
                globalConfigPath += "Local/Unity/" + newVersion + "/Cloud Build/";
            }
            else if (SystemInfo.operatingSystem.Contains("Mac OS"))
            {
                globalConfigPath = osxGlobalConfigPath;
            }

            if (!Directory.Exists(globalConfigPath))
            {
                Directory.CreateDirectory(globalConfigPath);
            }

            globalConfigPath += globalConfigFileName;
            globalConfigPath = RemoveBackslash(globalConfigPath);
            
            Debug.Log(globalConfigPath);
        }

        public static string ProjectDirectory
        {
            get
            {
                if (projectPath == null)
                {
                    InitProjectPath();
                }

                return projectPath;
            }
        }

        public static string ZipDirectory
        {
            get { return ProjectDirectory + cloudBuildTempPath; }
        }

        public static string ProjectZipFullPath
        {
            get { return projectZipFullPath; }
            set { projectZipFullPath = RemoveBackslash(value); }
        }

        public static string ProjectConfigPath
        {
            get
            {
                if (projectConfigPath == null)
                {
                    DirectoryInfo appDataPath = Directory.GetParent(Application.dataPath);
                    string pattern = "*" + Path.DirectorySeparatorChar + "CloudBuildPlugIn" +
                                     Path.DirectorySeparatorChar + "Common" + "*";
                    DirectoryInfo[] filesInDir = appDataPath.GetDirectories("*UCBConfig*", SearchOption.AllDirectories);

                    if (filesInDir.Length > 0)
                    {
                        foreach (DirectoryInfo foundFile in filesInDir)
                        {
                            projectConfigPath = foundFile.FullName + Path.DirectorySeparatorChar;
                        }
                    }
                    else
                    {
                        projectConfigPath = ProjectDirectory + Constants.PACK_TEMP_DIRECTORY;
                    }
                }
                Debug.Log("Project Config Path: " + projectConfigPath);
                return projectConfigPath + projectConfigFileName;
            }
        }

        public static string GlobalConfigPath {
            get
            {
                if (globalConfigPath == null)
                {
                    InitGlobalConfigPath();
                }

                return globalConfigPath;
            }
        }

        public static string RemoveBackslash(string fileName)
        {
            return fileName.Replace("\\", "/");
        }

        public static string GetProjectRelativePath(string fullPath)
        {
            if (fullPath.Contains(ProjectDirectory))
            {
                return fullPath.Replace(ProjectDirectory, "");
            }

            Debug.LogWarning(fullPath + " - does not contain project directory");
            return fullPath;
        }
        
        public static string GetFullPath(string projectRelativePath)
        {
            string sep = projectRelativePath[0].Equals(Path.DirectorySeparatorChar) ? "" : "" + Path.DirectorySeparatorChar;
            return ProjectDirectory + sep + projectRelativePath;
        }
    }
}