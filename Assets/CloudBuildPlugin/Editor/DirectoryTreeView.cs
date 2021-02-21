#if (UNITY_EDITOR) 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetStreaming.Editor.View.TreeView;
using CloudBuildPlugin.Common;
using CloudBuildPlugin.Editor.TreeView;
using CloudBuildPlugin.Editor.TreeView.TreeDataModel;
using SimpleJSON;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace CloudBuildPlugin.Editor
{
	public class MultiColumnTreeView : TreeViewWithTreeModel<PackDirectoriesTreeElement>
	{
		const float kRowHeights = 20f;
		const float kToggleWidth = 18f;
		public bool showControls = true;


		// All columns
		enum AssetStreamingColumns
		{
			Directory,
			IsChecked
		}

		public enum SortOption
		{
			Directory,
			IsChecked
		}

		// Sort options per column
		SortOption[] m_SortOptions = 
		{
			SortOption.Directory,
			SortOption.IsChecked
		};

		public static void TreeToList (TreeViewItem root, IList<TreeViewItem> result)
		{
			if (root == null)
				throw new NullReferenceException("root");
			if (result == null)
				throw new NullReferenceException("result");

			result.Clear();
	
			if (root.children == null)
				return;

			Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
			for (int i = root.children.Count - 1; i >= 0; i--)
				stack.Push(root.children[i]);

			while (stack.Count > 0)
			{
				TreeViewItem current = stack.Pop();
				result.Add(current);

				if (current.hasChildren && current.children[0] != null)
				{
					for (int i = current.children.Count - 1; i >= 0; i--)
					{
						stack.Push(current.children[i]);
					}
				}
			}
		}

		public MultiColumnTreeView (TreeViewState state, MultiColumnHeader multicolumnHeader,TreeModel<PackDirectoriesTreeElement> model) : base (state, multicolumnHeader, model)
		{
			Assert.AreEqual(m_SortOptions.Length , Enum.GetValues(typeof(AssetStreamingColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

			// Custom setup
			rowHeight = kRowHeights;
			columnIndexForTreeFoldouts = 0;
			showAlternatingRowBackgrounds = true;
			showBorder = true;
			customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
			extraSpaceBeforeIconAndLabel = kToggleWidth;
			multicolumnHeader.sortingChanged += OnSortingChanged;

			// init checkbox
//			Common.Debug.Log("dirs: " + PackingDirectories.Directories.ToString());
			List<string> directories = PackingDirectories.Directories;
			if (directories != null && model.root.children != null)
			{
				foreach (var treeElement in model.root.children)
				{
					PackDirectoriesTreeElement dirTreeElement = (PackDirectoriesTreeElement) treeElement;
					dirTreeElement.isChecked = PackingDirectories.IsBannedDirectory(dirTreeElement.fullPath)
						? false
						: directories.Contains(dirTreeElement.fullPath);
					dirTreeElement.enabled = !PackingDirectories.IsReservedDirectory(dirTreeElement.fullPath) 
					                         && !PackingDirectories.IsBannedDirectory(dirTreeElement.fullPath);
				}
			}

			Reload();
		}


		// Note we We only build the visible rows, only the backend has the full tree information. 
		// The treeview only creates info for the row list.
		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			var rows = base.BuildRows (root);
			SortIfNeeded (root, rows);
			return rows;
		}

		void OnSortingChanged (MultiColumnHeader multiColumnHeader)
		{
			SortIfNeeded (rootItem, GetRows());
		}

		void SortIfNeeded (TreeViewItem root, IList<TreeViewItem> rows)
		{
			if (rows.Count <= 1)
				return;
			
			if (multiColumnHeader.sortedColumnIndex == -1)
			{
				return; // No column to sort for (just use the order the data are in)
			}
			
			// Sort the roots of the existing tree items
			SortByMultipleColumns ();
			TreeToList(root, rows);
			Repaint();
		}

		void SortByMultipleColumns ()
		{
			var sortedColumns = multiColumnHeader.state.sortedColumns;

			if (sortedColumns.Length == 0)
				return;

			var myTypes = rootItem.children.Cast<TreeViewItem<PackDirectoriesTreeElement> >();
			var orderedQuery = InitialOrder (myTypes, sortedColumns);
			for (int i=1; i<sortedColumns.Length; i++)
			{
				SortOption sortOption = m_SortOptions[sortedColumns[i]];
				bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

				switch (sortOption)
				{
					case SortOption.Directory:
						orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
						break;
					case SortOption.IsChecked:
						orderedQuery = orderedQuery.ThenBy(l => l.data.isChecked, ascending);
						break;
				}
			}

			rootItem.children = orderedQuery.Cast<TreeViewItem> ().ToList ();
		}

		IOrderedEnumerable<TreeViewItem<PackDirectoriesTreeElement>> InitialOrder(IEnumerable<TreeViewItem<PackDirectoriesTreeElement>> myTypes, int[] history)
		{
			SortOption sortOption = m_SortOptions[history[0]];
			bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
			switch (sortOption)
			{
				case SortOption.Directory:
					return myTypes.Order(l => l.data.name, ascending);
				case SortOption.IsChecked:
					return myTypes.Order(l => l.data.isChecked, ascending);
					break;
				default:
					Assert.IsTrue(false, "Unhandled enum");
					break;
			}

			// default
			return myTypes.Order(l => l.data.name, ascending);
		}

		protected override void RowGUI (RowGUIArgs args)
		{
			var item = (TreeViewItem<PackDirectoriesTreeElement>) args.item;

			for (int i = 0; i < args.GetNumVisibleColumns (); ++i)
			{
				GUI.enabled = item.data.enabled;
				CellGUI(args.GetCellRect(i), item, (AssetStreamingColumns)args.GetColumn(i), ref args);
				GUI.enabled = true;
			}
		}

		void CellGUI (Rect cellRect, TreeViewItem<PackDirectoriesTreeElement> item, AssetStreamingColumns column, ref RowGUIArgs args)
		{
			// Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
			CenterRectUsingSingleLineHeight(ref cellRect);

			switch (column)
			{
				case AssetStreamingColumns.Directory:
				{
					args.rowRect = cellRect;
					base.RowGUI(args);
				}
					break;

				case AssetStreamingColumns.IsChecked:
				{
					bool newValue = EditorGUI.Toggle(cellRect, item.data.isChecked); // hide when outside cell rect
					if (newValue != item.data.isChecked)
					{
						item.data.isChecked = newValue;
						PackingDirectories.OnDirectoryPackingStatusChange(item.data.fullPath, newValue);
					}
				}
					break;
			}
		}

		protected override Rect GetRenameRect (Rect rowRect, int row, TreeViewItem item)
		{
			Rect cellRect = GetCellRectForTreeFoldouts (rowRect);
			CenterRectUsingSingleLineHeight(ref cellRect);
			return base.GetRenameRect (cellRect, row, item);
		}

		// Misc
		//--------

		protected override bool CanMultiSelect (TreeViewItem item)
		{
			return false;
		}

		public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
		{
			var columns = new[] 
			{
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("Directory"),
					headerTextAlignment = TextAlignment.Left,
					sortedAscending = true,
					width = 160, 
					minWidth = 160,
					autoResize = true,
				},
				new MultiColumnHeaderState.Column 
				{
					headerContent = new GUIContent("Pack"),
					headerTextAlignment = TextAlignment.Left,
					width = 160,
					minWidth = 160,
					maxWidth = 160,
				}
			};

			Assert.AreEqual(columns.Length, Enum.GetValues(typeof(AssetStreamingColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

			var state =  new MultiColumnHeaderState(columns);
			return state;
		}
		
	}

	static class MyExtensionMethods
	{
		public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
		{
			if (ascending)
			{
				return source.OrderBy(selector);
			}
			else
			{
				return source.OrderByDescending(selector);
			}
		}

		public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
		{
			if (ascending)
			{
				return source.ThenBy(selector);
			}
			else
			{
				return source.ThenByDescending(selector);
			}
		}
	}
}
#endif
