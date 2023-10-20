using System.IO;
using System.Text;
using RegressionGames;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.Types;
using UnityEngine;
using File = UnityEngine.Windows.File;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace RegressionGames.Editor.BotManagement
{
    public class RGBotSynchronizer
    {
#if UNITY_EDITOR
        private static readonly RGBotSynchronizer _this = new ();

        // This must match RGBotRuntimeManagement.cs
        public static readonly string BOTS_PATH = "Assets/RegressionGames/Runtime/Bots";
        
        [MenuItem("Regression Games/Create New Bot")]
        private static void CreateNewBot()
        {
            var botId = RGSettings.GetOrCreateSettings().GetNextBotId();
            
            // create a new bot folder
            var folderName = _this.CreateBotFolder("NewRGBot", botId);

            // create the assets
            _this.CreateNewBotAssets(folderName, "NewRGBot", botId);
        }
        
        //[MenuItem("Regression Games/Synchronize Bots with RG")]
        //TODO (REG-1306): Implement me
        private static void SynchronizeBots()
        {
            // force script compilation
            
            // zip up bots
            
            // check for and create 'new' bots
            
            // if old compare bot checksums
            
            // push bot zips
            
        }
        
        private static void CreateParentFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/RegressionGames"))
            {
                AssetDatabase.CreateFolder("Assets", "RegressionGames");
            }

            if (!AssetDatabase.IsValidFolder("Assets/RegressionGames/Runtime"))
            {
                AssetDatabase.CreateFolder("Assets/RegressionGames", "Runtime");
            }
            
            if (!AssetDatabase.IsValidFolder(BOTS_PATH))
            {
                AssetDatabase.CreateFolder("Assets/RegressionGames/Runtime", "Bots");
            }
        }

        private string CreateBotFolder(string botName, long botId)
        {
            CreateParentFolders();
            var folderString = $"{BOTS_PATH}/{botName}_{botId}".Replace('-','n');
            AssetDatabase.CreateFolder(BOTS_PATH, $"{botName}_{botId}".Replace('-','n'));
            return folderString;
        }

        private void CreateNewBotAssets(string folderName, string botName, long botId)
        {
            RGDebug.Log($"Creating new Regression Games Unity bot at path {folderName}");
            var botFolderShortName = folderName.Substring(folderName.LastIndexOf(Path.DirectorySeparatorChar)+1);
            
            // create `Bot` record asset
            var botRecordAssetPath = $"{folderName}/BotRecord.asset";
            if ( AssetDatabase.GetMainAssetTypeAtPath( botRecordAssetPath ) == null)
            {
                RGDebug.Log($"Writing {botRecordAssetPath}");
                RGBot botRecord = new RGBot()
                {
                    id = botId,
                    name = botName,
                    gameEngine = "UNITY",
                    programmingLanguage = "CSHARP",
                    codeSourceType = "ZIPFILE"
                };
                RGBotAsset botRecordAsset = ScriptableObject.CreateInstance<RGBotAsset>();
                botRecordAsset.Bot = botRecord;
                AssetDatabase.CreateAsset(botRecordAsset, botRecordAssetPath );
                AssetDatabase.SaveAssets();
            }
            
            var entryPointPath = $"{folderName}/BotEntryPoint.cs";
            RGDebug.Log($"Writing {entryPointPath}");
            // create bot starter script (copy the entry point script and fix the namespace in the file)
            var bytes = File.ReadAllBytes("Packages/gg.regression.unity.bots/Runtime/Scripts/RGBotLocalRuntime/Template/BotEntryPoint.cs.template");
            var utfString = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            utfString = utfString.Replace("<TEMPLATE_NAMESPACE>", botFolderShortName);
            
            File.WriteAllBytes(entryPointPath, Encoding.UTF8.GetBytes(utfString, 0, utfString.Length));
            RGDebug.Log($"Regression Games Unity bot successfully created at path {folderName}");

            CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();
        }
    }
#endif
}