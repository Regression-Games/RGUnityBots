using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RegressionGames.Editor.Validation
{
    public class RGValidationPane : EditorWindow
    {
        
        [MenuItem("Regression Games/Validation Results")]
        public static void ShowMyEditor()
        {
            // This method is called when the user selects the menu item in the Editor
            EditorWindow wnd = GetWindow<RGValidationPane>();
            wnd.titleContent = new GUIContent("Regression Games Validation Results");
        }
        
    }
}