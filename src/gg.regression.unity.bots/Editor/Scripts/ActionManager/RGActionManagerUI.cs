#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace RegressionGames.ActionManager
{
    internal class ActionTreeNode
    {
        public string[] path;
        public List<ActionTreeNode> children;
        public RGGameAction action;
        public bool IsLeaf => action != null;

        public ActionTreeNode(string[] path)
        {
            this.path = path;
        }
    }

    public class RGActionManagerUI : EditorWindow
    {
        private ToolbarSearchField _searchField;
        private ScrollView _actionsPane;
        private ScrollView _detailsPane;
        
        [MenuItem("Regression Games/Configure Bot Actions")]
        public static void OpenActionManagerUI()
        {
            if (RGActionManager.IsAvailable)
            {
                EditorWindow wnd = GetWindow<RGActionManagerUI>();
                wnd.titleContent = new GUIContent("RG Action Manager");
                wnd.minSize = new Vector2(500.0f, 600.0f);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Action manager is currently unavailable", "OK");
            }
        }

        private IEnumerable<RGGameAction> EnumerateActions()
        {
            string searchQuery = _searchField.value.Trim().ToLower();
            foreach (RGGameAction action in RGActionManager.Actions)
            {
                bool shouldInclude = true;

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    shouldInclude = false;
                    foreach (string[] path in action.Paths)
                    {
                        if (string.Join("/", path).ToLower().Contains(searchQuery))
                        {
                            shouldInclude = true;
                            break;
                        }
                    }
                }
                if (shouldInclude)
                {
                    yield return action;
                }
            }
        }

        private void BuildActionTree(ActionTreeNode node)
        {
            ISet<string> children = new HashSet<string>();
            foreach (RGGameAction action in EnumerateActions())
            {
                foreach (string[] path in action.Paths)
                {
                    if (path.SequenceEqual(node.path))
                    {
                        if (node.IsLeaf)
                        {
                            throw new ArgumentException($"Invalid duplicate path: {string.Join("/", path)}");
                        }
                        node.action = action;
                    }
                    else
                    {
                        bool startsWith = true;
                        for (int i = 0; i < node.path.Length; ++i)
                        {
                            if (path[i] != node.path[i])
                            {
                                startsWith = false;
                                break;
                            }
                        }
                        if (startsWith)
                        {
                            children.Add(path[node.path.Length]);
                        }
                    }
                }
            }

            if (children.Count > 0)
            {
                if (node.IsLeaf)
                {
                    throw new ArgumentException($"Leaf node cannot have children: {string.Join("/", node.path)}");
                }
                node.children = new List<ActionTreeNode>(children.Count);
                List<string> childrenSorted = new List<string>(children);
                childrenSorted.Sort();
                foreach (string child in childrenSorted)
                {
                    string[] childPath = new string[node.path.Length + 1];
                    for (int i = 0; i < node.path.Length; ++i)
                    {
                        childPath[i] = node.path[i];
                    }
                    childPath[node.path.Length] = child;
                    ActionTreeNode childNode = new ActionTreeNode(childPath);
                    BuildActionTree(childNode);
                    node.children.Add(childNode);
                }
            }
        }

        private ActionTreeNode[] BuildActionTrees()
        {
            ISet<string> roots = new HashSet<string>();
            foreach (RGGameAction action in EnumerateActions())
            {
                foreach (string[] path in action.Paths)
                {
                    roots.Add(path[0]);
                }
            }

            string unityUIRoot = "Unity UI";
            bool hasUnityUI = roots.Remove(unityUIRoot);
            List<string> rootList = new List<string>(roots);
            rootList.Sort();
            if (hasUnityUI)
            {
                rootList.Add(unityUIRoot);
            }

            ActionTreeNode[] result = new ActionTreeNode[rootList.Count];
            for (int i = 0; i < rootList.Count; ++i)
            {
                ActionTreeNode node = new ActionTreeNode(new[] { rootList[i] });
                BuildActionTree(node);
                result[i] = node;
            }

            return result;
        }
        
        private void CreateActionTreeElements(VisualElement container, ActionTreeNode node, IList<ListView> listViews)
        {
            if (node.IsLeaf)
            {
                throw new ArgumentException("Leaf node not allowed in root");
            }
            bool areChildrenLeaves = node.children.All(child => child.IsLeaf);
            if (areChildrenLeaves)
            {
                var foldout = new Foldout() { text = string.Join(" / ", node.path), value = true };
                var listView = new ListView();

                listView.makeItem = () =>
                {
                    VisualElement item = new VisualElement();
                    item.style.flexDirection = FlexDirection.Row;

                    Toggle checkbox = new Toggle();
                    item.Add(checkbox);

                    Label actionName = new Label();
                    actionName.style.unityTextAlign = TextAnchor.MiddleCenter;
                    item.Add(actionName);

                    checkbox.RegisterValueChangedCallback(evt =>
                    {
                        ActionTreeNode leafNode = (ActionTreeNode)checkbox.userData;
                        string path = string.Join("/", leafNode.path);
                        if (evt.newValue)
                        {
                            RGActionManager.Settings.DisabledActionPaths.Remove(path);
                        }
                        else
                        {
                            if (!RGActionManager.Settings.DisabledActionPaths.Contains(path))
                            {
                                RGActionManager.Settings.DisabledActionPaths.Add(path);
                            }
                        }
                        RGActionManager.Settings.MarkDirty();
                        RGActionManager.SaveSettings();
                    });

                    return item;
                };

                listView.bindItem = (item, index) =>
                {
                    ActionTreeNode leafNode = node.children[index];
                    Toggle checkbox = (Toggle)item[0];
                    Label actionName = (Label)item[1];
                    checkbox.value =
                        !RGActionManager.Settings.DisabledActionPaths.Contains(string.Join("/", leafNode.path));
                    actionName.text = leafNode.path.Last();
                    checkbox.userData = leafNode;

                    // changing the action manager settings during play mode is not allowed
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        actionName.SetEnabled(false);
                        checkbox.SetEnabled(false);
                    }
                };

                listView.onSelectionChange += (items) =>
                {
                    if (items.Any())
                    {
                        foreach (var lv in listViews)
                        {
                            if (lv != listView)
                            {
                                lv.ClearSelection();
                            }
                        }

                        ActionTreeNode selectedNode = (ActionTreeNode)items.First();
                        ShowActionDetails(selectedNode);
                    }
                };

                listView.itemsSource = node.children;
                listView.selectionType = SelectionType.Single;
                listViews.Add(listView);
                foldout.Add(listView);
                container.Add(foldout);
            }
            else
            {
                foreach (var child in node.children)
                {
                    CreateActionTreeElements(container, child, listViews);
                }
            }
        }

        private void UpdateGUI()
        {
            _actionsPane.Clear();
            _detailsPane.Clear();
            ActionTreeNode[] rootNodes = BuildActionTrees();
            IList<ListView> listViews = new List<ListView>();
            foreach (ActionTreeNode rootNode in rootNodes)
            {
                CreateActionTreeElements(_actionsPane, rootNode, listViews);
            }
        }

        private void ShowActionDetails(ActionTreeNode leafNode)
        {
            Debug.Assert(leafNode.IsLeaf);
            _detailsPane.Clear();
            RGGameAction action = leafNode.action;
            
            Label actionName = new Label();
            actionName.text = "Action: " + string.Join("/", leafNode.path);
            actionName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailsPane.Add(actionName);

            Label targetObject = new Label();
            targetObject.text = "Target Object Type: " + action.ObjectType.FullName;
            _detailsPane.Add(targetObject);

            Label paramRange = new Label();
            paramRange.text = "Parameter Range: " + action.ParameterRange;
            _detailsPane.Add(paramRange);
        }

        public void CreateGUI()
        {
            if (!RGActionManager.IsAvailable)
            {
                Close();
                return;
            }
            
            _searchField = new ToolbarSearchField();
            _searchField.RegisterValueChangedCallback(evt =>
            {
                UpdateGUI();
            });
            _searchField.style.width = StyleKeyword.Auto;
            rootVisualElement.Add(_searchField);

            var refreshBtn = new Button();
            refreshBtn.text = "Refresh";
            refreshBtn.clicked += () =>
            {
                new RGActionAnalysis().RunAnalysis();
            };
            rootVisualElement.Add(refreshBtn);
                
            var splitView = new TwoPaneSplitView(1, 250.0f, TwoPaneSplitViewOrientation.Vertical);
            rootVisualElement.Add(splitView);
            
            _actionsPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            splitView.Add(_actionsPane);

            _detailsPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            splitView.Add(_detailsPane);
            _detailsPane.style.paddingTop = 8;
            _detailsPane.style.paddingLeft = 8;
            _detailsPane.style.paddingRight = 8;
            _detailsPane.style.paddingBottom = 8;
            
            UpdateGUI();

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RGActionManager.ActionsChanged += OnActionsChanged;
        }

        public void OnDestroy()
        {
            RGActionManager.ActionsChanged -= OnActionsChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange mode)
        {
            UpdateGUI();
        }
        
        private void OnActionsChanged()
        {
            UpdateGUI();
        }
    }
}
#endif