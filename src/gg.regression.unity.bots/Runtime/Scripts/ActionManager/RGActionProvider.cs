using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    [Serializable]
    public class RGActionAnalysisResult
    {
        public RGSerializedAction[] actions;
    }
    
    /// <summary>
    /// This class reads and makes available the actions produced by RGActionAnalysis.
    /// </summary>
    public class RGActionProvider
    {
        public static readonly string ANALYSIS_RESULT_DIRECTORY = "Assets/Resources";
        public static readonly string ANALYSIS_RESULT_NAME = "RGActionAnalysisResult";
        public static readonly string ANALYSIS_RESULT_PATH = $"{ANALYSIS_RESULT_DIRECTORY}/{ANALYSIS_RESULT_NAME}.txt";

        /// <summary>
        /// Provides the static set of all action types identified in the game.
        /// </summary>
        public IEnumerable<RGGameAction> Actions => _actions;

        public bool IsAvailable { get; private set; }

        private IList<RGGameAction> _actions;

        public RGActionProvider()
        {
            _actions = new List<RGGameAction>();
            
            var result = ReadAnalysisResult();
            if (result != null)
            {
                try
                {
                    foreach (var serializedAction in result.actions)
                    {
                        _actions.Add(serializedAction.Deserialize());
                    }

                    IsAvailable = true;
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning("Failed to read action analysis results (analysis needs to be re-run)\n" + e.StackTrace);
                    _actions = new List<RGGameAction>();
                    IsAvailable = false;
                }
            }
            else
            {
                IsAvailable = false;
            }
        }

        private RGActionAnalysisResult ReadAnalysisResult()
        {
            string jsonText = null;
            #if UNITY_EDITOR
            if (File.Exists(ANALYSIS_RESULT_PATH))
            {
                using (StreamReader sr = new StreamReader(ANALYSIS_RESULT_PATH))
                {
                    jsonText = sr.ReadToEnd();
                }
            }
            #else
            {
                TextAsset jsonFile = Resources.Load<TextAsset>(ANALYSIS_RESULT_NAME);
                jsonText = jsonFile?.text;
            }
            #endif
            if (jsonText != null)
            {
                return JsonUtility.FromJson<RGActionAnalysisResult>(jsonText);
            }
            else
            {
                return null;
            }
        }
    }
}