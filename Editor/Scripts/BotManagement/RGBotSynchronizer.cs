using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.Types;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using File = UnityEngine.Windows.File;
#if UNITY_EDITOR
using System;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
#endif

namespace RegressionGames.Editor.BotManagement
{
    public class RGBotSynchronizer
    {
#if UNITY_EDITOR
        private static readonly RGBotSynchronizer _this = new ();
        
        private static RGServiceManager _rgServiceManager = new (); // editor, not game/scene so don't look for one, make one

        // This must match RGBotRuntimeManagement.cs
        private const string BOTS_PATH = "Assets/RegressionGames/Runtime/Bots";

        private const string ZIP_TEMP_FOLDER_NAME = "RegressionGamesBotZipTemp";
        private static string PARENT_DIRECTORY_PATH = Directory.GetParent(Application.dataPath).FullName;
        private static string ZIP_TEMP_FOLDER_PATH = Path.Combine(PARENT_DIRECTORY_PATH, ZIP_TEMP_FOLDER_NAME);

        private static string ZipPathForBot(long botId)
        {
            return Path.Combine(PARENT_DIRECTORY_PATH, ZIP_TEMP_FOLDER_NAME, $"Bot_{botId}.zip");
        }
        
        [MenuItem("Regression Games/Create New Bot")]
        private static void CreateNewBot()
        {
            var botId = RGSettings.GetOrCreateSettings().GetNextBotId();
            
            // create a new bot folder
            var folderName = _this.CreateBotFolder("NewRGBot", botId);

            // create the assets
            _this.CreateNewBotAssets(folderName, "NewRGBot", botId);
        }

        /**
         * Synchronizes local bots with RG remote server if connected.
         * This is a long one, it...
         * - Discovers all local bots
         * - Gets the local Unity bot records that exist on RG 
         * - Creates any bots on RG that don't already exist
         * - Creates a zip file of local bots (excluding some key files)
         * - Gets and compares the MD5 of the local bots to those on RG 
         *   - Uploads any zips that need updating
         *   - NOTE: Currently this always pushes from local to remote assuming the local is the preferred record for bots that you already have.
         *   -       if you want the new remote bot, you should delete your local bot before syncing.
         * - Creates new local bot records for any bots on RG that are missing locally  
         */
        [MenuItem("Regression Games/Synchronize Bots with RG")]
        private static async void SynchronizeBots()
        {
            try
            {
                if (!_rgServiceManager.IsAuthed() && !await _rgServiceManager.TryAuth())
                {
                    RGDebug.LogWarning("Unable to synchronize bots with Regression Games.  Check your configuration and ensure that your network connection can reach the configured Regression Games server.");
                    return;
                }
                
                RGDebug.LogInfo("Synchronizing Bots with Regression Games Server...");
                
                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots", "Refreshing Assets", 0.1f);

                // force asset refresh
                AssetDatabase.Refresh();
                
                // refresh the local bot asset records
                RGBotAssetsManager.GetInstance()?.RefreshAvailableBots();

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Retrieving current bots from Regression Games", 0.2f);

                var localBots = RGBotAssetsManager.GetInstance()?.GetAvailableBots() ?? new List<RGBot>();
                
                var remoteBots = await _this.GetBotsFromRG();

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Creating new bots in Regression Games", 0.3f);

                var countCreatedRemote = 0;
                
                var botsNeedingCreation =
                    localBots.Where(lb => remoteBots.FirstOrDefault(rb => rb.id == lb.id) == null);
                foreach (var rgBot in botsNeedingCreation)
                {
                    await _rgServiceManager.CreateBot(
                        new RGCreateBotRequest(rgBot.name),
                        (botResult) =>
                        {
                            // update the id of the local bot
                            rgBot.id = botResult.id;
                            ++countCreatedRemote;
                        }, () => { }
                    )!;
                }

                // save updated ids
                AssetDatabase.SaveAssets();
                
                // refresh the local bot asset records to match the new Ids
                RGBotAssetsManager.GetInstance()?.RefreshAvailableBots();

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Creating zip files for local bots", 0.4f);

                _this.RemoveZipTempDirectory();
                _this.CreateZipTempDirectory();

                var localMD5 = _this.CreateLocalBotZipFiles(localBots);
                
                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Uploading local bot zip files to Regression Games", 0.5f);

                var countUpdated = 0;
                foreach (var (botId, md5) in localMD5)
                {
                    EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                        $"Updating remote code for Bot id: {botId}", 0.55f);
                    if (await _this.UpdateBotZipOnRG(botId, md5))
                    {
                        ++countUpdated;
                    }
                }

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Downloading new bots from Regression Games", 0.7f);

                // create local record for any new remote bots + pull down their zips
                var newRemoteBots =
                    remoteBots.Where(rb => localBots.FirstOrDefault(bnc => bnc.id == rb.id) == null);
                foreach (var newRemoteBot in newRemoteBots)
                {
                    EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                        $"Downloading zip for Bot id: {newRemoteBot.id}", 0.75f);
                    await _this.CreateLocalBotRecordFromRG(newRemoteBot);
                }

                _this.RemoveZipTempDirectory();

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots", "Refreshing Assets", 0.9f);
                
                // force asset refresh for any new bots
                AssetDatabase.Refresh();
                
                RGBotAssetsManager.GetInstance()?.RefreshAvailableBots();

                var endingLocalBots = RGBotAssetsManager.GetInstance()?.GetAvailableBotAssets();

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots", "Complete", 1f);

                RGDebug.LogInfo($"Synchronizing Bots with Regression Games Server... Complete!\r\n" +
                                $"Created {endingLocalBots.Count - localBots.Count} new local bot entries\r\n" +
                                $"Created {countCreatedRemote} new remote bot entries\r\n" +
                                $"Updated {countUpdated} remote bot entries\r\n");
                                

                if (endingLocalBots.Count != localBots.Count)
                {
                    var newBot = endingLocalBots.FirstOrDefault(nb => localBots.FirstOrDefault(lb => lb.id == nb.Bot.id) == null);
                    if (newBot != null)
                    {
                        //If we created any new bot entries, open that directory in the editor
                        AssetDatabase.OpenAsset(newBot);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private async Task CreateLocalBotRecordFromRG(RGBot newRemoteBot)
        {
            //Create directory
            string botFolderPath = _this.CreateBotFolder(newRemoteBot.name, newRemoteBot.id);

            string zipDownloadPath = ZipPathForBot(newRemoteBot.id);

            if (File.Exists(zipDownloadPath))
            {
                File.Delete(zipDownloadPath);
            }
                    
            //download and extract zip
            await _rgServiceManager.DownloadBotCode(newRemoteBot.id,
                zipDownloadPath,
                () =>
                {
                    // unpack the zip to the bot folder - overwrite existing files
                    ZipFile.ExtractToDirectory(zipDownloadPath, botFolderPath, true);
                },
                () => { }
            );
            
            //write asset record
            _this.CreateBotAssetFile(botFolderPath, newRemoteBot.name, newRemoteBot.id);
        }
        
        private async Task<List<RGBot>> GetBotsFromRG()
        {
            var remoteBots = new List<RGBot>();

            // check for and create 'new' bots on regression games
            await _rgServiceManager.GetBotsForCurrentUser(
                (existingRGBots) =>
                {
                    remoteBots = existingRGBots
                        .Where(v => v.IsUnityBot && v.IsLocal && v.codeSourceType == "ZIPFILE").ToList();
                }, () => { }
            );
            return remoteBots;
        }

        private Dictionary<long, string> CreateLocalBotZipFiles(IEnumerable<RGBot> localBots)
        {
            var localMD5 = new Dictionary<long, string>();

            // create zip files for all the bots I have locally
            foreach (var rgBot in localBots)
            {
                var botId = rgBot.id;
                var localBot = RGBotAssetsManager.GetInstance()?.GetBotAssetRecord(botId);
                var md5 = ZipLocalBot(botId, localBot.Path);
                localMD5[botId] = md5;
            }

            return localMD5;
        }

        private async Task<bool> UpdateBotZipOnRG(long botId, string md5)
        {
            var didUpdate = false;
            // if zip checksums don't match.. push new zip files
            // Get remote zip details, check md5sum, decide if need to push
            var needsToUpload = false;

            await _rgServiceManager.GetBotCodeDetails(
                botId,
                (botCodeDetails) =>
                {
                    if (md5 != botCodeDetails.md5)
                    {
                        RGDebug.LogDebug(
                            $"RG Bot Id: {botId} needs to update remote zip; local md5: {md5} != remote md5: {botCodeDetails.md5}");
                        needsToUpload = true;
                    }
                },
                () =>
                {
                    // not found.. need to push for first time
                    needsToUpload = true;
                }
            )!;

            if (needsToUpload)
            {
                string zipFilePath = ZipPathForBot(botId);
                await _rgServiceManager.UpdateBotCode(
                    botId,
                    zipFilePath,
                    (botCodeDetails) =>
                    {
                        didUpdate = true;
                    },
                    () => { }
                );
            }

            return didUpdate;
        }

        private void CreateZipTempDirectory()
        {
            // Make it new/empty
            if (!Directory.Exists(ZIP_TEMP_FOLDER_PATH))
            {
                Directory.CreateDirectory(ZIP_TEMP_FOLDER_PATH);
            }
        }

        private void RemoveZipTempDirectory()
        {
            // Cleanup/delete temp zip directory
            if (Directory.Exists(ZIP_TEMP_FOLDER_PATH))
            {
                Directory.Delete(ZIP_TEMP_FOLDER_PATH, true);
            }
        }

        private string ZipLocalBot(long botId, string sourcePath)
        {
            string zipPath = ZipPathForBot(botId);

            // Check if the zip file already exists and delete it
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            
            // Exclude `BotRecord.asset` and `*.meta` files from zip so md5s will match
            ZipHelper.CreateFromDirectory(
                sourcePath,
                zipPath,
                exclusionFilter: (t) => t.EndsWith("BotRecord.asset") || t.EndsWith(".meta")
            );

            var md5 = CalculateMD5(zipPath);
            
            RGDebug.LogDebug($"Successfully Generated {zipPath} - md5: {md5}");
            return md5;
        }

        static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = System.IO.File.OpenRead(filename);
            
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        
        private void CreateParentFolders()
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

        /**
         * <summary>Creates a folder and any needed parent folders for the given botname/botId pair.</summary>
         * <returns>String path of the folder</returns>
         */
        private string CreateBotFolder(string botName, long botId)
        {
            CreateParentFolders();
            var folderString = $"{BOTS_PATH}/{botName}_{botId}".Replace('-','n');
            AssetDatabase.CreateFolder(BOTS_PATH, $"{botName}_{botId}".Replace('-','n'));
            return folderString;
        }

        private void CreateNewBotAssets(string folderName, string botName, long botId)
        {
            RGDebug.LogInfo($"Creating new Regression Games Unity bot at path {folderName}");
            var botFolderShortName = folderName.Substring(folderName.LastIndexOf(Path.DirectorySeparatorChar)+1);
            
            // create `Bot` record asset
            CreateBotAssetFile(folderName, botName, botId);
            
            var entryPointPath = $"{folderName}/BotEntryPoint.cs";
            RGDebug.LogDebug($"Writing {entryPointPath}");
            // create bot starter script (copy the entry point script and fix the namespace in the file)
            var bytes = File.ReadAllBytes("Packages/gg.regression.unity.bots/Runtime/Scripts/RGBotLocalRuntime/Template/BotEntryPoint.cs.template");
            var utfString = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            utfString = utfString.Replace("<TEMPLATE_NAMESPACE>", botFolderShortName);
            
            File.WriteAllBytes(entryPointPath, Encoding.UTF8.GetBytes(utfString, 0, utfString.Length));
            RGDebug.LogInfo($"Regression Games Unity bot successfully created at path {folderName}");

            CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();
        }

        private void CreateBotAssetFile(string folderName, string botName, long botId)
        {
            var botRecordAssetPath = $"{folderName}/BotRecord.asset";
            if ( AssetDatabase.GetMainAssetTypeAtPath( botRecordAssetPath ) == null)
            {
                RGDebug.LogDebug($"Writing {botRecordAssetPath}");
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
        }
    }
    
    // Extension code borrowed from StackOverflow to allow creating a zip of directory
    // while filtering out certain contents
    public static class ZipHelper {
        public static void CreateFromDirectory(string sourceDirectoryName,
            string destinationArchiveFileName,
            CompressionLevel compressionLevel = CompressionLevel.Fastest,
            bool includeBaseDirectory = false,
            Predicate<string> exclusionFilter = null
        )
        {
            if (string.IsNullOrEmpty(sourceDirectoryName)) {
                throw new ArgumentNullException("sourceDirectoryName");
            }
            if (string.IsNullOrEmpty(destinationArchiveFileName)) {
                throw new ArgumentNullException("destinationArchiveFileName");
            }
            var filesToAdd = Directory.GetFiles(sourceDirectoryName, "*", SearchOption.AllDirectories);
            var entryNames = GetEntryNames(filesToAdd, sourceDirectoryName, includeBaseDirectory);
            using var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create);
            using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create);
            for (var i = 0; i < filesToAdd.Length; i++)
            {
                // if none of the exclusion filters match.. add this file
                if (exclusionFilter == null || !exclusionFilter(filesToAdd[i]))
                {
                    archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
                }
            }
        }
        
        private static string[] GetEntryNames(string[] names, string sourceFolder, bool includeBaseName)
        {
            if (names == null || names.Length == 0)
            {
                return Array.Empty<string>();
            }

            if (includeBaseName)
            {
                sourceFolder = Path.GetDirectoryName(sourceFolder);
            }

            var length = string.IsNullOrEmpty(sourceFolder) ? 0 : sourceFolder.Length;
            if (length > 0 && sourceFolder != null && sourceFolder[length - 1] != Path.DirectorySeparatorChar &&
                sourceFolder[length - 1] != Path.AltDirectorySeparatorChar)
            {
                length++;
            }

            var result = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = names[i].Substring(length);
            }

            return result;
        }
    }
#endif
}