using System.Collections.Generic;
using JetBrains.Annotations;
using RegressionGames.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RegressionGames.Editor.Validation
{
    public class RGValidationPane : EditorWindow
    {
        private ScrollView _mainScrollPane;
        
        [MenuItem("Regression Games/Validation Results")]
        public static void ShowMyEditor()
        {
            // This method is called when the user selects the menu item in the Editor
            EditorWindow wnd = GetWindow<RGValidationPane>();
            wnd.titleContent = new GUIContent("RG Validation Results");
            wnd.Focus();
        }
        
        void CreateGUI()
        {
            
            _mainScrollPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            rootVisualElement.Add(_mainScrollPane);
            
            UpdateGUI();
            
        }

        void UpdateGUI()
        {
            
            _mainScrollPane.Clear();
            UnsubscribeFromValidationEvents();
            
            var validationScripts = FindObjectsOfType<RGValidateBehaviour>();
            
            foreach (var validationScript in validationScripts)
            {
                var scriptContainer = new VisualElement();
                scriptContainer.Add(new Label("Suite: " + validationScript.GetType().Name));
                _mainScrollPane.Add(scriptContainer);
                    
                validationScript.OnValidationsUpdated += UpdateGUI;
                
                foreach (var validator in validationScript.Validators)
                {
                    var validatorContainer = new VisualElement();
                    validatorContainer.style.paddingLeft = 20;
                    
                    scriptContainer.Add(validatorContainer);

                    var testText = "";
                    Color? color = null;
                    
                    switch (validator.Result)
                    {
                        case RGValidatorResult.NOT_SET:
                            testText += "? ";
                            break;
                        case RGValidatorResult.PASSED:
                            // If pass, show a green checkmark
                            testText += "PASS ";
                            color = Color.green;
                            break;
                        case RGValidatorResult.FAILED:
                            testText += "FAIL ";
                            color = Color.red;
                            break;
                    }

                    testText += validator.Method.Name;
                    var testLabel = new Label(testText);
                    if (color != null)
                    {
                        testLabel.style.color = color.Value;
                    }
                    
                    validatorContainer.Add(testLabel);
                }
            }

        }

        private void UnsubscribeFromValidationEvents()
        {
            var validationScripts = FindObjectsOfType<RGValidateBehaviour>();
            foreach (var validationScript in validationScripts)
            {
                validationScript.OnValidationsUpdated -= UpdateGUI;
            }
        }
        
    }
}