using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleJSON;

namespace CloudBuildPlugin.Common
{
    public class PackingDirectories
    {
        public static string[] reserved =
        {
            Path.DirectorySeparatorChar + "Assets",
            Path.DirectorySeparatorChar + "Packages",
            Path.DirectorySeparatorChar + "ProjectSettings"
        };
        public static string[] banned =
        {
            Path.DirectorySeparatorChar + "Temp",
        };

        private static List<string> _directories;
        public static List<string> Directories {
            get
            {
                if (_directories == null || _directories.Count == 0)
                {
                    InitPackingDirectories();
                }

                return _directories;
            }
            set => _directories = value;
        }

        public static string[] GetAllProjectDirectories()
        {
            string projectDir = PathHelper.ProjectDirectory;
            string[] allDirs = Directory.GetDirectories(projectDir);
            List<string> result = new List<string>();
            foreach (var dir in allDirs)
            {
                DirectoryInfo info = new DirectoryInfo(dir);
                if (!info.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    result.Add(PathHelper.GetProjectRelativePath(dir));
                }                
            }
            return result.ToArray();
        }
        
        public static bool IsReservedDirectory(string fullPath)
        {
            string projectDir = PathHelper.ProjectDirectory;
            string rDir = PathHelper.GetProjectRelativePath(fullPath);
            return reserved.Contains(rDir);
        }
        
        public static bool IsBannedDirectory(string fullPath)
        {
            string projectDir = PathHelper.ProjectDirectory;
            string rDir = PathHelper.GetProjectRelativePath(fullPath);
            return banned.Contains(rDir);
        }
        
        public static void InitPackingDirectories()
        {
            _directories = new List<string>();
            string projectDir = PathHelper.ProjectDirectory;
            string[] directories = GetAllProjectDirectories();
            
            foreach (string directory in directories)
            {
                if (reserved.Contains(directory))
                {
                    _directories.Add(directory);
                }
            }
        }

        public static void OnDirectoryPackingStatusChange(string dirRelativePath, bool toValue)
        {
            if (toValue == false)
            {
                _directories.Remove(_directories.Find(dir => dir.Equals(dirRelativePath)));
            }
            else
            {
                string existDir = _directories.Find(directory => directory.Equals(dirRelativePath));
                if (existDir == null)
                {
                    _directories.Add(dirRelativePath);
                }
            }

            SaveProjectConfig();
        }

        private static void SaveProjectConfig()
        {
            JSONNode configJson = Utils.GetProjectSettingsJsonNode();
            if (configJson == null)
            {
                configJson = JSONNode.Parse("{}");
            }
            configJson["PackingDirectories"] = JsonHelper.ToJsonArray(Directories);
            Debug.Log("Save Project Config: " + configJson);
            Utils.SaveProjectSettings(configJson);
        }

        public static bool IsFileInWhiteList(string fileFullName)
        {
            foreach (string dir in _directories)
            {
                if (fileFullName.Contains(dir))
                {
                    return true;
                }
            }

            return false;
        }
    }

}