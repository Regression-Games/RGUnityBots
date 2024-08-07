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
            foreach (RGGameAction action in RGActionManager.OriginalActions)
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

                    foreach (var prop in RGActionProperty.GetProperties(action))
                    {
                        if (prop.GetDisplayValue().Contains(searchQuery))
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

        private void SetPropertyOverride(RGActionPropertyInstance prop, object newValue)
        {
            prop.SetValue(newValue);
            RGActionManager.Settings.StoreProperty(prop);
            RGActionManager.SaveSettings(RGActionManager.Settings);
        }

        private void ResetProperty(RGActionPropertyInstance prop, object origValue)
        {
            prop.SetValue(origValue);
            RGActionManager.Settings.ResetProperty(prop);
            RGActionManager.SaveSettings(RGActionManager.Settings);
        }

        private IEnumerable<VisualElement> EnumerateDescendents(params VisualElement[] elems)
        {
            foreach (var elem in elems)
            {
                yield return elem;
                foreach (var child in elem.Children())
                {
                    foreach (var desc in EnumerateDescendents(child))
                    {
                        yield return desc;
                    }
                }
            }
        }

        private VisualElement CreateControlForProperty(RGActionPropertyInstance prop, object origValue, Action valueChangedCallback, out Action onPropReset)
        {
            const int numericFieldWidth = 60;
            var valueType = prop.GetValue().GetType();
            if (valueType == typeof(string))
            {
                TextField textField = new TextField();
                textField.value = (string)prop.GetValue();
                textField.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    string value = evt.newValue;
                    if ((string)origValue != value)
                        SetPropertyOverride(prop, value);
                    else
                        ResetProperty(prop, origValue);
                    valueChangedCallback();
                });
                onPropReset = () =>
                {
                    textField.value = (string)prop.GetValue();
                };
                return textField;
            } else if (valueType == typeof(float))
            {
                FloatField floatField = new FloatField();
                floatField.style.width = numericFieldWidth;
                floatField.value = (float)prop.GetValue();
                floatField.RegisterCallback<ChangeEvent<float>>(evt =>
                {
                    float value = evt.newValue;
                    if (!Mathf.Approximately(value, (float)origValue))
                    {
                        SetPropertyOverride(prop, value);
                    }
                    else
                    {
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                });
                onPropReset = () =>
                {
                    floatField.value = (float)prop.GetValue();
                };
                return floatField;
            } else if (valueType == typeof(RGVoidRange))
            {
                onPropReset = null;
                return null; // nothing to configure
            } else if (valueType == typeof(RGBoolRange))
            {
                RGBoolRange boolRange = (RGBoolRange)prop.GetValue();

                VisualElement control = new VisualElement();
                control.style.flexDirection = FlexDirection.Row;
                control.style.alignItems = Align.Center;
                
                IntegerField minField = new IntegerField();
                IntegerField maxField = new IntegerField();
                minField.style.width = numericFieldWidth;
                maxField.style.width = numericFieldWidth;
                control.Add(new Label("Min"));
                control.Add(minField);
                control.Add(new Label("Max"));
                control.Add(maxField);
                minField.value = (bool)boolRange.MinValue ? 1 : 0;
                maxField.value = (bool)boolRange.MaxValue ? 1 : 0;

                void OnChange(int newMin, int newMax)
                {
                    if (newMin >= 0 && newMin <= 1 && newMax >= 0 && newMax <= 1 && newMin <= newMax)
                    {
                        var newRange = new RGBoolRange(newMin != 0, newMax != 0);
                        if (!newRange.RangeEquals((RGBoolRange)origValue))
                        {
                            SetPropertyOverride(prop, newRange);
                        }
                        else
                        {
                            ResetProperty(prop, origValue);
                        }
                    }
                    else
                    {
                        // revert invalid input
                        var origRange = (RGBoolRange)origValue;
                        minField.value = (bool)origRange.MinValue ? 1 : 0;
                        maxField.value = (bool)origRange.MaxValue ? 1 : 0;
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                }
                
                minField.RegisterCallback<ChangeEvent<int>>(evt =>
                {
                    OnChange(evt.newValue, maxField.value);
                });
                maxField.RegisterCallback<ChangeEvent<int>>(evt =>
                {
                    OnChange(minField.value, evt.newValue);
                });
                onPropReset = () =>
                {
                    RGBoolRange boolRange = (RGBoolRange)prop.GetValue();
                    minField.value = (bool)boolRange.MinValue ? 1 : 0;
                    maxField.value = (bool)boolRange.MaxValue ? 1 : 0;
                };
                
                return control;
            } else if (valueType == typeof(RGVector2IntRange))
            {
                RGVector2IntRange range = (RGVector2IntRange)prop.GetValue();
                VisualElement control = new VisualElement();
                control.style.flexDirection = FlexDirection.Column;
                control.style.alignItems = Align.Center;
                
                VisualElement minRow = new VisualElement();
                Vector2IntField minField = new Vector2IntField();
                minRow.style.flexDirection = FlexDirection.Row;
                var minLabel = new Label("Min: ");
                minLabel.style.alignSelf = Align.Center;
                minRow.Add(minLabel);
                minRow.Add(minField);
                minField.value = (Vector2Int)range.MinValue;
                control.Add(minRow);
                
                VisualElement maxRow = new VisualElement();
                Vector2IntField maxField = new Vector2IntField();
                maxRow.style.flexDirection = FlexDirection.Row;
                var maxLabel = new Label("Max: ");
                maxLabel.style.alignSelf = Align.Center;
                maxRow.Add(maxLabel);
                maxRow.Add(maxField);
                maxField.value = (Vector2Int)range.MaxValue;
                control.Add(maxRow);
                
                foreach (var elem in EnumerateDescendents(minField, maxField))
                {
                    if (elem is IntegerField field)
                    {
                        field.style.width = numericFieldWidth;
                    }
                }

                void OnChange(Vector2Int newMin, Vector2Int newMax)
                {
                    if (newMin.x <= newMax.x && newMin.y <= newMax.y)
                    {
                        var newRange = new RGVector2IntRange(newMin, newMax);
                        if (!newRange.RangeEquals((RGVector2IntRange)origValue))
                        {
                            SetPropertyOverride(prop, newRange);
                        }
                        else
                        {
                            ResetProperty(prop, origValue);
                        }
                    }
                    else
                    {
                        // revert invalid input
                        var origRange = (RGVector2IntRange)origValue;
                        minField.value = (Vector2Int)origRange.MinValue;
                        maxField.value = (Vector2Int)origRange.MaxValue;
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                }
                
                minField.RegisterCallback<ChangeEvent<Vector2Int>>(evt =>
                {
                    OnChange(evt.newValue, maxField.value);
                });
                maxField.RegisterCallback<ChangeEvent<Vector2Int>>(evt =>
                {
                    OnChange(minField.value, evt.newValue);
                });
                onPropReset = () =>
                {
                    RGVector2IntRange range = (RGVector2IntRange)prop.GetValue();
                    minField.value = (Vector2Int)range.MinValue;
                    maxField.value = (Vector2Int)range.MaxValue;
                };
                return control;
            } else if (valueType == typeof(RGVector3IntRange))
            {
                RGVector3IntRange range = (RGVector3IntRange)prop.GetValue();
                VisualElement control = new VisualElement();
                control.style.flexDirection = FlexDirection.Column;
                control.style.alignItems = Align.Center;
                
                VisualElement minRow = new VisualElement();
                Vector3IntField minField = new Vector3IntField();
                minRow.style.flexDirection = FlexDirection.Row;
                var minLabel = new Label("Min: ");
                minLabel.style.alignSelf = Align.Center;
                minRow.Add(minLabel);
                minRow.Add(minField);
                minField.value = (Vector3Int)range.MinValue;
                control.Add(minRow);
                
                VisualElement maxRow = new VisualElement();
                Vector3IntField maxField = new Vector3IntField();
                maxRow.style.flexDirection = FlexDirection.Row;
                var maxLabel = new Label("Max: ");
                maxLabel.style.alignSelf = Align.Center;
                maxRow.Add(maxLabel);
                maxRow.Add(maxField);
                maxField.value = (Vector3Int)range.MaxValue;
                control.Add(maxRow);
                
                foreach (var elem in EnumerateDescendents(minField, maxField))
                {
                    if (elem is IntegerField field)
                    {
                        field.style.width = numericFieldWidth;
                    }
                }

                void OnChange(Vector3Int newMin, Vector3Int newMax)
                {
                    if (newMin.x <= newMax.x && newMin.y <= newMax.y)
                    {
                        var newRange = new RGVector3IntRange(newMin, newMax);
                        if (!newRange.RangeEquals((RGVector3IntRange)origValue))
                        {
                            SetPropertyOverride(prop, newRange);
                        }
                        else
                        {
                            ResetProperty(prop, origValue);
                        }
                    }
                    else
                    {
                        // revert invalid input
                        var origRange = (RGVector3IntRange)origValue;
                        minField.value = (Vector3Int)origRange.MinValue;
                        maxField.value = (Vector3Int)origRange.MaxValue;
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                }
                
                minField.RegisterCallback<ChangeEvent<Vector3Int>>(evt =>
                {
                    OnChange(evt.newValue, maxField.value);
                });
                maxField.RegisterCallback<ChangeEvent<Vector3Int>>(evt =>
                {
                    OnChange(minField.value, evt.newValue);
                });
                onPropReset = () =>
                {
                    RGVector3IntRange range = (RGVector3IntRange)prop.GetValue();
                    minField.value = (Vector3Int)range.MinValue;
                    maxField.value = (Vector3Int)range.MaxValue;
                };
                return control;
            } else if (valueType == typeof(RGIntRange))
            {
                RGIntRange intRange = (RGIntRange)prop.GetValue();

                VisualElement control = new VisualElement();
                control.style.flexDirection = FlexDirection.Row;
                control.style.alignItems = Align.Center;
                
                IntegerField minField = new IntegerField();
                IntegerField maxField = new IntegerField();
                minField.style.width = numericFieldWidth;
                maxField.style.width = numericFieldWidth;
                control.Add(new Label("Min"));
                control.Add(minField);
                control.Add(new Label(" Max"));
                control.Add(maxField);
                minField.value = (int)intRange.MinValue;
                maxField.value = (int)intRange.MaxValue;

                void OnChange(int newMin, int newMax)
                {
                    if (newMin <= newMax)
                    {
                        var newRange = new RGIntRange(newMin, newMax);
                        if (!newRange.RangeEquals((RGIntRange)origValue))
                        {
                            SetPropertyOverride(prop, newRange);
                        }
                        else
                        {
                            ResetProperty(prop, origValue);
                        }
                    }
                    else
                    {
                        // revert invalid input
                        var origRange = (RGIntRange)origValue;
                        minField.value = (int)origRange.MinValue;
                        maxField.value = (int)origRange.MaxValue;
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                }
                
                minField.RegisterCallback<ChangeEvent<int>>(evt =>
                {
                    OnChange(evt.newValue, maxField.value);
                });
                maxField.RegisterCallback<ChangeEvent<int>>(evt =>
                {
                    OnChange(minField.value, evt.newValue);
                });
                onPropReset = () =>
                {
                    RGIntRange intRange = (RGIntRange)prop.GetValue();
                    minField.value = (int)intRange.MinValue;
                    maxField.value = (int)intRange.MaxValue;
                };
                
                return control;
            } else if (valueType == typeof(RGFloatRange))
            {
                RGFloatRange floatRange = (RGFloatRange)prop.GetValue();
                VisualElement control = new VisualElement();
                control.style.flexDirection = FlexDirection.Row;
                control.style.alignItems = Align.Center;
                
                FloatField minField = new FloatField();
                FloatField maxField = new FloatField();
                minField.style.width = numericFieldWidth;
                maxField.style.width = numericFieldWidth;
                control.Add(new Label("Min"));
                control.Add(minField);
                control.Add(new Label("Max"));
                control.Add(maxField);
                minField.value = (float)floatRange.MinValue;
                maxField.value = (float)floatRange.MaxValue;

                void OnChange(float newMin, float newMax)
                {
                    if (newMin <= newMax)
                    {
                        var newRange = new RGFloatRange(newMin, newMax);
                        if (!newRange.RangeEquals((RGFloatRange)origValue))
                        {
                            SetPropertyOverride(prop, newRange);
                        }
                        else
                        {
                            ResetProperty(prop, origValue);
                        }
                    }
                    else
                    {
                        // revert invalid input
                        var origRange = (RGFloatRange)origValue;
                        minField.value = (float)origRange.MinValue;
                        maxField.value = (float)origRange.MaxValue;
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                }
                
                minField.RegisterCallback<ChangeEvent<float>>(evt =>
                {
                    OnChange(evt.newValue, maxField.value);
                });
                maxField.RegisterCallback<ChangeEvent<float>>(evt =>
                {
                    OnChange(minField.value, evt.newValue);
                });
                onPropReset = () =>
                {
                    RGFloatRange floatRange = (RGFloatRange)prop.GetValue();
                    minField.value = (float)floatRange.MinValue;
                    maxField.value = (float)floatRange.MaxValue;
                };
                
                return control;
            } else if (valueType == typeof(RGVector2Range))
            {
                RGVector2Range range = (RGVector2Range)prop.GetValue();
                VisualElement control = new VisualElement();
                control.style.flexDirection = FlexDirection.Column;
                control.style.alignItems = Align.Center;
                
                VisualElement minRow = new VisualElement();
                Vector2Field minField = new Vector2Field();
                minRow.style.flexDirection = FlexDirection.Row;
                var minLabel = new Label("Min: ");
                minLabel.style.alignSelf = Align.Center;
                minRow.Add(minLabel);
                minRow.Add(minField);
                minField.value = (Vector2)range.MinValue;
                control.Add(minRow);
                
                VisualElement maxRow = new VisualElement();
                Vector2Field maxField = new Vector2Field();
                maxRow.style.flexDirection = FlexDirection.Row;
                var maxLabel = new Label("Max: ");
                maxLabel.style.alignSelf = Align.Center;
                maxRow.Add(maxLabel);
                maxRow.Add(maxField);
                maxField.value = (Vector2)range.MaxValue;
                control.Add(maxRow);
                
                foreach (var elem in EnumerateDescendents(minField, maxField))
                {
                    if (elem is FloatField field)
                    {
                        field.style.width = numericFieldWidth;
                    }
                }

                void OnChange(Vector2 newMin, Vector2 newMax)
                {
                    if (newMin.x <= newMax.x && newMin.y <= newMax.y)
                    {
                        var newRange = new RGVector2Range(newMin, newMax);
                        if (!newRange.RangeEquals((RGVector2Range)origValue))
                        {
                            SetPropertyOverride(prop, newRange);
                        }
                        else
                        {
                            ResetProperty(prop, origValue);
                        }
                    }
                    else
                    {
                        // revert invalid input
                        var origRange = (RGVector2Range)origValue;
                        minField.value = (Vector2)origRange.MinValue;
                        maxField.value = (Vector2)origRange.MaxValue;
                        ResetProperty(prop, origValue);
                    }
                    valueChangedCallback();
                }
                
                minField.RegisterCallback<ChangeEvent<Vector2>>(evt =>
                {
                    OnChange(evt.newValue, maxField.value);
                });
                maxField.RegisterCallback<ChangeEvent<Vector2>>(evt =>
                {
                    OnChange(minField.value, evt.newValue);
                });
                onPropReset = () =>
                {
                    RGVector2Range range = (RGVector2Range)prop.GetValue();
                    minField.value = (Vector2)range.MinValue;
                    maxField.value = (Vector2)range.MaxValue;
                };
                return control;
            }
            onPropReset = null;
            return null;
        }

        private VisualElement CreateGap()
        {
            VisualElement divider = new VisualElement();
            divider.style.marginTop = 3;
            divider.style.marginBottom = 3;
            return divider;
        }

        private void ShowActionDetails(ActionTreeNode leafNode)
        {
            Debug.Assert(leafNode.IsLeaf);
            _detailsPane.Clear();
            RGGameAction origAction = leafNode.action;
            RGGameAction action = RGActionManager.Settings.ApplySettings(origAction);

            Label actionName = new Label();
            actionName.text = "Action: " + leafNode.path.Last();
            actionName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailsPane.Add(actionName);
            
            _detailsPane.Add(CreateGap());

            Label actionPaths = new Label();
            actionPaths.text = (action.Paths.Count == 1 ? "Path: " : "Paths: ") + (action.Paths.Count > 1
                ? "\n" + string.Join("\n", action.Paths.Select((path, idx) => (idx + 1) + ". " + string.Join("/", path)))
                : string.Join("/", action.Paths[0]));
            _detailsPane.Add(actionPaths);
            
            _detailsPane.Add(CreateGap());

            Label actionTargetObject = new Label();
            actionTargetObject.text = "Target Object Type: " + action.ObjectType.FullName;
            _detailsPane.Add(actionTargetObject);
            
            IEnumerable<RGActionPropertyInstance> EnumerateProperties()
            {
                // group configurable and non-configurable properties together
                foreach (var prop in RGActionProperty.GetProperties(action))
                {
                    if (!prop.Attribute.UserConfigurable)
                    {
                        yield return prop;
                    }
                }
                foreach (var prop in RGActionProperty.GetProperties(action))
                {
                    if (prop.Attribute.UserConfigurable)
                    {
                        yield return prop;
                    }
                }
            }

            foreach (var prop in EnumerateProperties())
            {
                _detailsPane.Add(CreateGap());
                
                var origProp = RGActionProperty.FindProperty(origAction, prop.Name);
                var origValue = origProp.GetValue();
                if (origValue == null)
                    continue;
                
                VisualElement propRow = new VisualElement();
                propRow.style.flexDirection = FlexDirection.Row;
                propRow.style.alignItems = Align.Center;

                Label propNameLabel = new Label(prop.Attribute.DisplayName + ": ");
                propRow.Add(propNameLabel);

                bool canConfigure = false;
                if (prop.Attribute.UserConfigurable)
                {
                    Button resetBtn = new Button();
                    resetBtn.text = "Reset";

                    Action updateOverriddenState = () =>
                    {
                        bool haveOverride = RGActionManager.Settings.HavePropertySetting(prop);
                        resetBtn.SetEnabled(haveOverride);
                        if (haveOverride)
                        {
                            propNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        }
                        else
                        {
                            propNameLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                        }
                    };
                    
                    var control = CreateControlForProperty(prop, origValue, updateOverriddenState, 
                        out var onPropChange);
                    if (control != null)
                    {
                        resetBtn.clicked += () =>
                        {
                            ResetProperty(prop, origValue);
                            if (onPropChange != null)
                            {
                                onPropChange();
                            }

                            updateOverriddenState();
                        };
                        updateOverriddenState();
                        
                        propRow.Add(control);
                        propRow.Add(resetBtn);
                        
                        canConfigure = true;
                    }
                }
                
                if (!canConfigure)
                {
                    Label propValueLabel = new Label();
                    propValueLabel.text = prop.GetDisplayValue();
                    propRow.Add(propValueLabel);
                }
                
                _detailsPane.Add(propRow);
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
