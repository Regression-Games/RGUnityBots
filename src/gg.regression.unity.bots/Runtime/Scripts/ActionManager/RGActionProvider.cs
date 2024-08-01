using System;
using System.Collections.Generic;
using System.IO;
using RegressionGames.ActionManager.JsonConverters;
using UnityEngine;
using Newtonsoft.Json;

namespace RegressionGames.ActionManager
{
    
    /// <summary>
    /// This class reads and makes available the actions produced by RGActionAnalysis.
    /// </summary>
    public class RGActionProvider
    {
        public static readonly string ANALYSIS_RESULT_DIRECTORY = "Assets/Resources";
        public static readonly string ANALYSIS_RESULT_NAME = "RGActionAnalysisResult";
        public static readonly string ANALYSIS_RESULT_PATH = $"{ANALYSIS_RESULT_DIRECTORY}/{ANALYSIS_RESULT_NAME}.txt";
        
        public static readonly JsonConverter[] JSON_CONVERTERS = { new RGGameActionJsonConverter(), new RGValueRangeJsonConverter() };

        /// <summary>
        /// Provides the static set of all action types identified in the game.
        /// </summary>
        public IEnumerable<RGGameAction> Actions => _actions;

        public bool IsAvailable { get; private set; }

        private IList<RGGameAction> _actions;

        public RGActionProvider()
        {
            _actions = new List<RGGameAction>();

            var jsonText = GetResultsJson();
            if (jsonText != null)
            {
                try
                {
                    var result = JsonConvert.DeserializeObject<RGActionAnalysisResult>(jsonText, JSON_CONVERTERS);
                    if (result.ApiVersion != RGActionAnalysisResult.CURRENT_API_VERSION)
                    {
                        throw new Exception(
                            $"API version mismatch (current is {RGActionAnalysisResult.CURRENT_API_VERSION}, got {result.ApiVersion})");
                    }
                    _actions = result.Actions;
                    IsAvailable = true;
                }
                catch (Exception e)
                {
                    RGDebug.LogWarning("Failed to read action analysis results (analysis needs to be re-run)\n" + e.Message + "\n" + e.StackTrace);
                    IsAvailable = false;
                }
            }
            else
            {
                IsAvailable = false;
            }
        }

        private string GetResultsJson()
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
            return jsonText;
        }
    }
}