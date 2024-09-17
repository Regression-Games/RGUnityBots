#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEngine;
using StateRecorder.BotSegments.Models;
using UnityEditor.Compilation;
#endif

namespace RegressionGames.Editor
{
#if UNITY_EDITOR

    [InitializeOnLoad]
    public class IRGBotSequencesFinder : UnityEditor.Editor
    {
        static IRGBotSequencesFinder()
        {
            CompilationPipeline.compilationStarted -= CreateBotSegmentsAsset;
            CompilationPipeline.compilationStarted += CreateBotSegmentsAsset;
        }

        // ReSharper disable once Unity.IncorrectMethodSignature
        private static void CreateBotSegmentsAsset(object o)
        {
            // Find the IRGBotList ScriptableObject in your project
            IRGBotSequences botSequences = LoadBotSegmentsList();

            if (botSequences == null)
            {
                return;
            }

            botSequences.sequences.Clear();
            botSequences.segmentLists.Clear();
            botSequences.segments.Clear();

            // search the Assets/RegressionGames folder for .json files.. then test if they are our type
            // load the path strings as though they will be loaded as resources
            var jsonFilePaths = Directory.GetFiles("Assets/RegressionGames", "*.json", SearchOption.AllDirectories);

            foreach (var jfp in jsonFilePaths)
            {
                var jsonFilePath = jfp.Replace('\\', '/');
                var index = jsonFilePath.LastIndexOf("Resources/");
                if ( index>=0 )
                {
                    var outputPath = jsonFilePath.Substring(index + "Resources/".Length);
                    var dotIndex = outputPath.LastIndexOf(".");
                    if (dotIndex >= 0)
                    {
                        outputPath = outputPath.Substring(0, dotIndex);
                    }
                    //test
                    using var fr = new StreamReader(File.OpenRead(jsonFilePath));
                    var fileContents = fr.ReadToEnd();
                    try
                    {
                        var sequence = JsonConvert.DeserializeObject<BotSequence>(fileContents);
                        botSequences.sequences.Add(outputPath);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            var segmentList = JsonConvert.DeserializeObject<BotSegmentList>(fileContents);
                            botSequences.segmentLists.Add(outputPath);
                        }
                        catch (Exception)
                        {
                            try
                            {
                                var segment = JsonConvert.DeserializeObject<BotSegment>(fileContents);
                                botSequences.segments.Add(outputPath);
                            }
                            catch (Exception)
                            {
                                // .json file wasn't one of our types
                                RGDebug.LogInfo("Ignoring non BotSegment / BotSegmentList / BotSequence .json file");
                            }
                        }
                    }
                }
            }

            // Mark the ScriptableObject as dirty to ensure it saves
            EditorUtility.SetDirty(botSequences);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static IRGBotSequences LoadBotSegmentsList()
        {
            // Ensure the target SO directory exists
            string targetDirPath = "Assets/RegressionGames/Resources";
            RGEditorUtils.CreateAllAssetFolders(targetDirPath);

            // Define the path for the new asset
            string targetAssetPath = $"{targetDirPath}/RGBotSequences.asset";

            // Check if the asset already exists to avoid overwriting it
            if (AssetDatabase.LoadAssetAtPath<IRGBotSequences>(targetAssetPath) == null)
            {
                // Create the asset
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<IRGBotSequences>(), targetAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var sequences = AssetDatabase.LoadAssetAtPath<IRGBotSequences>(targetAssetPath);
            AssetDatabase.Refresh();
            return sequences;
        }
    }
#endif
}
