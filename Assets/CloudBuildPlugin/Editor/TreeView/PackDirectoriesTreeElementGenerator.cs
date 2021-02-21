using System.Collections.Generic;
using CloudBuildPlugin.Common;
using CloudBuildPlugin.Editor.TreeView.TreeDataModel;
using SimpleJSON;

namespace CloudBuildPlugin.Editor.TreeView
{
	static class PackDirectoriesTreeElementGenerator
	{
		static int _idCounter;

		public static List<PackDirectoriesTreeElement> Generate()
		{
			var treeElements = new List<PackDirectoriesTreeElement>();
			var root = new PackDirectoriesTreeElement("Root", "", true, true, -1, _idCounter);
			treeElements.Add(root);
			
			foreach (string directoryRelativePath in PackingDirectories.GetAllProjectDirectories())
			{
				AddDirectoryAsChildren(directoryRelativePath, root, treeElements);
			}

			return treeElements;
		}
		
		static void AddDirectoryAsChildren(string directoryRelativePath, TreeElement element, List<PackDirectoriesTreeElement> treeElements)
		{
			string projectDir = PathHelper.ProjectDirectory;
//			string dirName = directoryRelativePath.Replace(projectDir, "");
			var dirElement = new PackDirectoriesTreeElement(directoryRelativePath, directoryRelativePath, true, true, element.depth + 1, ++_idCounter);
			treeElements.Add(dirElement);
		}
	}
}
