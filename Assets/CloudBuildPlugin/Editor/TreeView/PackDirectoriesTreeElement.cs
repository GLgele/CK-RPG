using System;
using CloudBuildPlugin.Editor.TreeView.TreeDataModel;

namespace CloudBuildPlugin.Editor.TreeView
{

	[Serializable]
	public class PackDirectoriesTreeElement : TreeElement
	{
		public string fullPath;
		public bool enabled, isChecked;

		public PackDirectoriesTreeElement (string name, int depth, int id) : base (name, depth, id)
		{
			fullPath = "";
			enabled = true;
		}
	
		public PackDirectoriesTreeElement (string name, string path, bool selected, bool isEnabled,  int depth, int id) : base (name, depth, id)
		{
			isChecked = selected;
			fullPath = path;
			enabled = isEnabled;
		}
	}
}
