#if (UNITY_EDITOR) 

using System;
using System.Collections.Generic;
using CloudBuildPlugin.Editor.TreeView;
using CloudBuildPlugin.Editor.TreeView.TreeDataModel;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CloudBuildPlugin.Editor
{
    public class UcbPackDirectoriesWindow : EditorWindow
    {
        
        [SerializeField] TreeViewState m_TreeViewState; // Serialized in the window layout file so it survives assembly reloading
        [SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
        MultiColumnTreeView m_TreeView;
        MyTreeAsset m_MyTreeAsset;
        [NonSerialized] bool m_Initialized;

        void SetTreeAsset (MyTreeAsset myTreeAsset)
        {
            m_MyTreeAsset = myTreeAsset;
            m_Initialized = false;
        }

        Rect multiColumnTreeViewRect
        {
            get { return new Rect(0, 18, position.width, position.height-40f); }
        }

        public MultiColumnTreeView treeView
        {
            get { return m_TreeView; }
        }
        
        
        void InitTreeViewIfNeeded ()
        {
            
            if (!m_Initialized)
            {
                // Check if it already exists (deserialized from window layout file or scriptable object)
                if (m_TreeViewState == null)
                    m_TreeViewState = new TreeViewState();

                bool firstInit = m_MultiColumnHeaderState == null;
                var headerState = MultiColumnTreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
                m_MultiColumnHeaderState = headerState;

                var multiColumnHeader = new MyMultiColumnHeader(headerState)
                {
                    mode = MyMultiColumnHeader.Mode.MinimumHeaderWithoutSorting
                };
                if (firstInit)
                    multiColumnHeader.ResizeToFit ();

                var treeModel = new TreeModel<PackDirectoriesTreeElement>(GetData());
                m_TreeView = new MultiColumnTreeView(m_TreeViewState, multiColumnHeader, treeModel);
				
//				//init config file
//				m_TreeView.SaveCloudAssetConfig();
				
                m_Initialized = true;
            }
			
//            InitStyles();
        }
        IList<PackDirectoriesTreeElement> GetData ()
        {
            if (m_MyTreeAsset != null && m_MyTreeAsset.treeElements != null && m_MyTreeAsset.treeElements.Count > 0)
                return m_MyTreeAsset.treeElements;

            return PackDirectoriesTreeElementGenerator.Generate(); 
        }

        public static void showWindow()
        {
            EditorWindow.GetWindow(typeof(UcbPackDirectoriesWindow));
        }
        
        UcbPackDirectoriesWindow()
        {
            titleContent = new GUIContent("Config Pack Directories");
        }

        private void OnEnable()
        {
            autoRepaintOnSceneChange = true;
            minSize = new Vector2(350, 400);
        }

        void OnGUI()
        {
            InitTreeViewIfNeeded();
            m_TreeView.OnGUI(multiColumnTreeViewRect);
        }
    }
    
    internal class MyMultiColumnHeader : MultiColumnHeader
    {
        Mode m_Mode;

        public enum Mode
        {
            LargeHeader,
            DefaultHeader,
            MinimumHeaderWithoutSorting
        }

        public MyMultiColumnHeader(MultiColumnHeaderState state)
            : base(state)
        {
            mode = Mode.DefaultHeader;
        }

        public Mode mode
        {
            get
            {
                return m_Mode;
            }
            set
            {
                m_Mode = value;
                switch (m_Mode)
                {
                    case Mode.LargeHeader:
                        canSort = true;
                        height = 37f;
                        break;
                    case Mode.DefaultHeader:
                        canSort = true;
                        height = DefaultGUI.defaultHeight;
                        break;
                    case Mode.MinimumHeaderWithoutSorting:
                        canSort = false;
                        height = DefaultGUI.minimumHeight;
                        break;
                }
            }
        }

        protected override void ColumnHeaderGUI (MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
        {
            // Default column header gui
            base.ColumnHeaderGUI(column, headerRect, columnIndex);

            // Add additional info for large header
            if (mode == Mode.LargeHeader)
            {
                // Show example overlay stuff on some of the columns
                if (columnIndex > 2)
                {
                    headerRect.xMax -= 3f;
                    var oldAlignment = EditorStyles.largeLabel.alignment;
                    EditorStyles.largeLabel.alignment = TextAnchor.UpperRight;
                    GUI.Label(headerRect, 36 + columnIndex + "%", EditorStyles.largeLabel);
                    EditorStyles.largeLabel.alignment = oldAlignment;
                }
            }
        }
    }
}
#endif