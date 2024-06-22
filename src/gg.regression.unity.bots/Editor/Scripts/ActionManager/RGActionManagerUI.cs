using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RegressionGames.ActionManager
{
    public class RGActionManagerUI : EditorWindow
    {
        [MenuItem("Regression Games/Action Manager")]
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

        public void CreateGUI()
        {
            var splitView = new TwoPaneSplitView(1, 250.0f, TwoPaneSplitViewOrientation.Vertical);
            rootVisualElement.Add(splitView);

            var actionsPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            splitView.Add(actionsPane);

            var detailsPane = new VisualElement();
            splitView.Add(detailsPane);

            var actionsListView = new ListView();
            actionsPane.Add(actionsListView);

            List<RGGameAction> actions = new List<RGGameAction>(RGActionManager.Actions);
            actionsListView.makeItem = () =>
            {
                VisualElement container = new VisualElement();
                container.style.flexDirection = FlexDirection.Row;

                Toggle checkbox = new Toggle();
                container.Add(checkbox);

                Label actionName = new Label();
                actionName.style.unityTextAlign = TextAnchor.MiddleCenter;
                container.Add(actionName);

                return container;
            };
            actionsListView.bindItem = (item, index) =>
            {
                RGGameAction action = actions[index];
                ((Label)item[1]).text = action.Paths[0];
                ((Toggle)item[0]).value = true;
            };
            actionsListView.itemsSource = actions;
        }

        private void OnActionsChanged()
        {
            // TODO update action list
        }
    }
}