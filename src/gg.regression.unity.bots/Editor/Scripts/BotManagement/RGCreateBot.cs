using System.Collections;
using System.Collections.Generic;
using RegressionGames.Editor;
using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    public class RGCreateBot
    {
        private const string BEHAVIOR_PATH = "Assets/RegressionGames/Runtime/Bots";

        [MenuItem("Regression Games/Create New MonoBehavior Bot")]
        private static void CreateNewBot()
        {
            RGEditorUtils.CreateAllAssetFolders(BEHAVIOR_PATH);
            
            // prompt the user to choose a folder
            var newBotPath = EditorUtility.SaveFilePanelInProject(
                "Create a new Bot",
                "NewRGBot",
                "",
                "Enter the name of the new bot.",
                BEHAVIOR_PATH);
            if (string.IsNullOrEmpty(newBotPath))
            {
                // The user cancelled
                return;
            }
        }
    }
}