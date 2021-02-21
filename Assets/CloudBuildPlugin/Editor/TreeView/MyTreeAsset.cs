#if (UNITY_EDITOR) 
using System.Collections.Generic;
using UnityEngine;

namespace CloudBuildPlugin.Editor.TreeView
{
	
	[CreateAssetMenu (fileName = "TreeDataAsset", menuName = "Tree Asset", order = 1)]
	public class MyTreeAsset : ScriptableObject
	{
		[SerializeField] List<PackDirectoriesTreeElement> m_TreeElements = new List<PackDirectoriesTreeElement> ();

		internal List<PackDirectoriesTreeElement> treeElements
		{
			get { return m_TreeElements; }
			set { m_TreeElements = value; }
		}

		void Awake ()
		{
			if (m_TreeElements.Count == 0)
				m_TreeElements = PackDirectoriesTreeElementGenerator.Generate();
		}
	}
}
#endif
