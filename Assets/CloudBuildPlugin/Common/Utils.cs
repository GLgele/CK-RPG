using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CloudBuildPlugin.Editor;
using SimpleJSON;
using UnityEditor;
using UnityEngine;
using BuildTarget = CloudBuildPlugin.Enums.BuildTarget;

namespace CloudBuildPlugin.Common
{
    public static class JsonHelper
    {
        public static JSONArray ToJsonArray(string [] input)
        {
            if (input == null)
            {
                return null;
            }
            
            JSONArray result = JSON.Parse("[]").AsArray;
            for (int i=0; i<input.Length; i++)
            {
                result[i] = input[i];
            }

            return result;
        }
        
        public static JSONArray ToJsonArray(List<string> input)
        {
            if (input == null)
            {
                return null;
            }
            
            JSONArray result = JSON.Parse("[]").AsArray;
            for (int i=0; i<input.Count; i++)
            {
                result[i] = input[i];
            }

            return result;
        }
        
        public static string[] ToStringArray(JSONArray input)
        {
            if (input == null)
            {
                return null;
            }
            
            string[] array = new string[input.Count];
            int index = 0;
            foreach (JSONNode node in input)
            {
                array[index] = node.Value;
                index += 1;
            }

            return array;
        }
        
        public static List<string> ToStringList(JSONArray input)
        {
            if (input == null)
            {
                return null;
            }
            
            List<string> array = new List<string>();
            foreach (JSONNode node in input)
            {
                array.Add(node.Value);
            }

            return array;
        }
    }
    
    public static class Utils
    {
        public static string UnityVersion
        {
            get
            {
                string patternForVersion = @"(.*)(?=[a-z]+[0-9]*)";
                return new Regex(patternForVersion).Match(Application.unityVersion).Value;
            }
        }

        public static string GetProjectHash(string zipFullPath)
        {
            string hashPattern = @"(?<=Library\/CloudBuildPlugin\/)(.*)(?=\.zip$)";
            if (!string.IsNullOrEmpty(zipFullPath))
            {
                return new Regex(hashPattern).Match(zipFullPath).Value;
            }

            return null;
        }

        

        public static string TryGetCloudProjectSettings(string propName)
        {
            var cloudProjectSettingsType = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in assembly.GetTypes()
                where type.Name == "CloudProjectSettings" && type.GetProperties().Any(m => m.Name == propName)
                select type).FirstOrDefault();
            
            PropertyInfo field = cloudProjectSettingsType.GetProperty(propName);
            
            return (field == null
                ? null
                : field.GetValue(Activator.CreateInstance(cloudProjectSettingsType)).ToString());
        }

        public static JSONNode GetProjectSettingsJsonNode()
        {
            try
            {
                string configJsonString = File.ReadAllText(PathHelper.ProjectConfigPath);
                JSONNode configJson = JSON.Parse(configJsonString);
                return configJson;
            }
            catch (Exception ex)
            {
                Debug.Info("No Project Config Loaded.");
                return null;
            }
        }

        public static void SaveProjectSettings(JSONNode configJson)
        {
            File.WriteAllText(PathHelper.ProjectConfigPath, configJson.ToString());
        }
        
        public static string GetProjectId()
        {
            //try get cloud settings
            var projectId = TryGetCloudProjectSettings("projectId");
            
            if (string.IsNullOrEmpty(projectId))
            {   
                //use saved config
                JSONNode configJson = GetProjectSettingsJsonNode();
                if (configJson != null)
                {
                    projectId = configJson["projectSlug"];
                }
                
                if (string.IsNullOrEmpty(projectId))
                {
                    //generate new and save to config
                    projectId = Guid.NewGuid().ToString();
                    if (configJson == null)
                    {
                        configJson = JSONNode.Parse("{}");
                    }
                    configJson["projectSlug"] = projectId;
                    SaveProjectSettings(configJson);
                }
            }
            return projectId;
        }

        public static string TryGetBuildTargetGroup(string buildTarget)
        {
            string btName = buildTarget.ToLower();
            foreach (string btGroupName in Enum.GetNames(typeof(BuildTargetGroup)))
            {
                string lowerBtGroupName = btGroupName.ToLower();
                if (btName.Contains(lowerBtGroupName))
                {
                    return btGroupName;
                }
            }

            return null;
        }

        public static bool IL2CPP()
        {
            bool badTarget = false;
            for (int i=0; i < Enum.GetValues(typeof(BuildTarget)).Length; i++)
            {
                string btGroupName = Utils.TryGetBuildTargetGroup(Enum.GetName(typeof(BuildTarget), i));
                BuildTargetGroup btGroup = (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), btGroupName);
                if (i != (int)BuildTarget.WebGL && PlayerSettings.GetScriptingBackend(btGroup) > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
