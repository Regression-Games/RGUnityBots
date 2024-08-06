﻿#if UNITY_EDITOR
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
        private Button _analyzeBtn;
        private ToolbarSearchField _searchField;
        private ScrollView _actionsPane;
        private ScrollView _detailsPane;

        [MenuItem("Regression Games/Configure Bot Actions")]
        public static void OpenActionManagerUI()
        {
            EditorWindow wnd = GetWindow<RGActionManagerUI>();
            wnd.titleContent = new GUIContent("RG Action Manager");
            wnd.minSize = new Vector2(500.0f, 600.0f);
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
                    if (action.ObjectType.FullName.ToLower().Contains(searchQuery))
                    {
                        shouldInclude = true;
                    }
                    if (action.DisplayName.ToLower().Contains(searchQuery))
                    {
                        shouldInclude = true;
                    }

                    foreach (var (_, attrValue) in action.GetDisplayActionAttributes())
                    {
                        if (attrValue.ToLower().Contains(searchQuery))
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

        private List<ActionTreeNode> BuildActionTrees()
        {
            var actionsByTargetObject = new Dictionary<Type, List<RGGameAction>>();
            foreach (RGGameAction action in EnumerateActions())
            {
                List<RGGameAction> actions;
                if (!actionsByTargetObject.TryGetValue(action.ObjectType, out actions))
                {
                    actions = new List<RGGameAction>();
                    actionsByTargetObject.Add(action.ObjectType, actions);
                }
                actions.Add(action);
            }

            List<ActionTreeNode> result = new List<ActionTreeNode>();
            foreach (var entry in actionsByTargetObject)
            {
                var objectType = entry.Key;
                var actions = entry.Value;
                ActionTreeNode node = new ActionTreeNode(new string[] { objectType.FullName });
                node.children = new List<ActionTreeNode>(actions.Select(act => 
                    new ActionTreeNode(new[] {node.path[0], act.DisplayName}) { action = act } ));
                node.children.Sort((a, b) => a.path[1].CompareTo(b.path[1]));
                result.Add(node);
            }

            // always have Unity UI elements at the bottom of the list
            List<ActionTreeNode> sorted =
                new List<ActionTreeNode>(result.Where(node => !node.path[0].StartsWith("UnityEngine.UI.")));
            sorted.Sort((a, b) => a.path[0].CompareTo(b.path[0]));
            List<ActionTreeNode> uiSorted =
                new List<ActionTreeNode>(result.Where(node => node.path[0].StartsWith("UnityEngine.UI.")));
            uiSorted.Sort((a, b) => a.path[0].CompareTo(b.path[0]));
            sorted.AddRange(uiSorted);
            
            return sorted;
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
                        if (leafNode == null)
                            return;
                        
                        foreach (string[] actionPath in leafNode.action.Paths)
                        {
                            string path = string.Join("/", actionPath);
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
                        }
                        RGActionManager.Settings.MarkDirty();
                        RGActionManager.SaveSettings(RGActionManager.Settings);
                    });

                    return item;
                };

                listView.bindItem = (item, index) =>
                {
                    ActionTreeNode leafNode = node.children[index];
                    Toggle checkbox = (Toggle)item[0];
                    Label actionName = (Label)item[1];
                    checkbox.value = !leafNode.action.Paths.All(path =>
                        RGActionManager.Settings.DisabledActionPaths.Contains(string.Join("/", path)));
                    actionName.text = leafNode.path.Last();
                    checkbox.userData = leafNode;

                    // changing the action manager settings during play mode is not allowed
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        actionName.SetEnabled(false);
                        checkbox.SetEnabled(false);
                    }
                };

#if UNITY_2022_2_OR_NEWER
                listView.selectionChanged += (items) =>
#else
                listView.onSelectionChange += (items) =>
#endif
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
            var rootNodes = BuildActionTrees();
            IList<ListView> listViews = new List<ListView>();
            foreach (var rootNode in rootNodes)
            {
                CreateActionTreeElements(_actionsPane, rootNode, listViews);
            }
            _analyzeBtn.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private void ShowActionDetails(ActionTreeNode leafNode)
        {
            Debug.Assert(leafNode.IsLeaf);
            _detailsPane.Clear();
            RGGameAction action = leafNode.action;

            Label actionName = new Label();
            actionName.text = "Action: " + leafNode.path.Last();
            actionName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailsPane.Add(actionName);

            foreach ((string attrName, string attrValue) in action.GetDisplayActionAttributes())
            {
                Label attrLabel = new Label();
                attrLabel.text = attrName + ": " + attrValue;
                _detailsPane.Add(attrLabel);
            }
        }

        public void CreateGUI()
        {
            _searchField = new ToolbarSearchField();
            _searchField.RegisterValueChangedCallback(evt =>
            {
                UpdateGUI();
            });
            _searchField.style.width = StyleKeyword.Auto;
            rootVisualElement.Add(_searchField);

            _analyzeBtn = new Button();
            _analyzeBtn.text = "Analyze Actions";
            _analyzeBtn.clicked += () =>
            {
                var analysis = new RGActionAnalysis(displayProgressBar: true);
                if (analysis.RunAnalysis())
                {
                    RGActionManager.ReloadActions();
                }
            };
            rootVisualElement.Add(_analyzeBtn);
                
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
