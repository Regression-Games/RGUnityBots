using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.Types;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;
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
            // prompt the user to choose a folder
            var newBotPath = EditorUtility.SaveFilePanelInProject(
                "Create a new RegressionGames Bot",
                "NewRGBot",
                "",
                "Enter the name of the new bot.",
                BOTS_PATH);
            if (string.IsNullOrEmpty(newBotPath))
            {
                // The user cancelled
                return;
            }

            var botName = RGEditorUtils.GetAssetPathLeaf(newBotPath);
            var botParentPath = RGEditorUtils.GetAssetPathParent(newBotPath);
            RGEditorUtils.CreateAllAssetFolders(botParentPath);
            var botFolderGuid = AssetDatabase.CreateFolder(botParentPath, botName);
            var botFolder = AssetDatabase.GUIDToAssetPath(botFolderGuid);

            // Create the bot assets
            var botId = RGSettings.GetOrCreateSettings().GetNextBotId();
            _this.CreateNewBotAssets(botFolder, botName, botId);

            // Make sure we can find the entry point.
            var entryPointPath = $"{botFolder}/BotEntryPoint.cs";
            var entryPointScript = AssetDatabase.LoadAssetAtPath<MonoScript>(entryPointPath);
            if (entryPointScript != null)
            {
                // If we can, ask the user if they want to open it.
                var openNow = EditorUtility.DisplayDialog("Bot Created",
                    $"Created a new RegressionGames bot at {newBotPath}. Would you like to open the entrypoint script now?",
                    "Yes",
                    "No");
                if (openNow)
                {
                    AssetDatabase.OpenAsset(entryPointScript);
                }
            }
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
         * - Creates new local bot records and downloads the files for any bots on RG that are missing locally
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

                var localBotAssets = RGBotAssetsManager.GetInstance()?.GetAvailableBotAssets() ?? new List<RGBotAsset>();
                var localBots = localBotAssets.Select(b => b.Bot).ToList();

                var remoteBots = await _this.GetBotsFromRG();

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Creating new bots in Regression Games", 0.3f);

                // create remote bot records for any local bots that don't exist on RG
                var countCreatedRemote = 0;
                var botsNeedingCreation =
                    localBotAssets.Where(lb => remoteBots.FirstOrDefault(rb => rb.id == lb.Bot.id) == null);
                foreach (var botAsset in botsNeedingCreation)
                {
                    await _rgServiceManager.CreateBot(
                        new RGCreateBotRequest(botAsset.Bot.name),
                        (botResult) =>
                        {
                            // update the id of the local bot
                            botAsset.Bot.id = botResult.id;
                            EditorUtility.SetDirty(botAsset);
                            ++countCreatedRemote;
                            remoteBots.Add(botResult);
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

                var localBotZips = _this.CreateLocalBotZipFiles(localBots);

                EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                    "Uploading local bot zip files to Regression Games", 0.5f);

                var countUpdated = 0;
                foreach (var (botId, (localBot, md5, localLastUpdated)) in localBotZips)
                {
                    if(remoteBots.FirstOrDefault(b => b.id == botId) is not {} remoteBot)
                    {
                        RGDebug.LogError($"Unable to find remote bot with id {botId} to update.");
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Synchronizing Regression Games Bots",
                        $"Updating remote code for Bot id: {botId}", 0.55f);
                    if (await _this.SyncBotCode(remoteBot, localBot, md5, localLastUpdated))
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

                    // We set 'replace' to false so we don't replace existing files at this path, if any.
                    await _this.CreateLocalBotRecordFromRG(newRemoteBot, replace: false);
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

        /// <summary>
        /// Creates, or recreates, the local bot asset for the given remote bot.
        /// </summary>
        /// <remarks>
        /// If the bot already exists locally, it will be deleted and recreated.
        /// </remarks>
        /// <param name="newRemoteBot">The remote bot to fetch.</param>
        /// <param name="replace">If <c>true</c>, any existing content in the bot's target directory will be replaced.</param>
        private async Task CreateLocalBotRecordFromRG(RGBot newRemoteBot, bool replace)
        {
            //Create directory
            string botFolderPath = _this.CreateDefaultBotFolder(newRemoteBot.name, replace);

            string zipDownloadPath = ZipPathForBot(newRemoteBot.id);

            if (File.Exists(zipDownloadPath))
            {
                // this shouldn't be reached 99.999% of the time but is here to avoid getting stuck
                // if someone happened to have a file open in the temp directory and blocked it from cleaning up properly
                File.Delete(zipDownloadPath);
            }

            //download and extract zip
            string botChecksum = null;
            await _rgServiceManager.DownloadBotCode(newRemoteBot.id,
                zipDownloadPath,
                () =>
                {
                    botChecksum = RGUtils.CalculateMD5(zipDownloadPath);
                    // unpack the zip to the bot folder - overwrite existing files
                    ZipFile.ExtractToDirectory(zipDownloadPath, botFolderPath, true);
                },
                () => { }
            );

            //write asset record
            _this.CreateBotAssetFile(botFolderPath, newRemoteBot.name, newRemoteBot.id, botChecksum);
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

        private Dictionary<long, (RGBot Bot, string Checksum, DateTimeOffset? LastUpdatedDate)> CreateLocalBotZipFiles(IEnumerable<RGBot> localBots) =>
            // create zip files for all the bots I have locally
            localBots.ToDictionary(
                rgBot => rgBot.id,
                rgBot =>
                {
                    var botId = rgBot.id;
                    var localBot = RGBotAssetsManager.GetInstance()?.GetBotAssetRecord(botId);
                    var md5 = ZipLocalBot(botId, localBot.Path);
                    var lastUpdated = RGUtils.GetLatestWriteDate(localBot.Path);
                    return (rgBot, md5, lastUpdated);
                });

        private async Task<bool> SyncBotCode(RGBot remoteBot, RGBot localBot, string localMd5, DateTimeOffset? localLastUpdated)
        {
            bool ShouldUpload(RGBotCodeDetails rgBotCodeDetails)
            {
                if (rgBotCodeDetails == null)
                {
                    // No code on the server? Definitely upload.
                    return true;
                }

                // Server-owned code should always be downloaded.
                if (remoteBot.CodeIsServerOwned)
                {
                    RGDebug.LogVerbose($"Bot {remoteBot.name} ({remoteBot.id}) is server-owned. Downloading code and overwriting local changes.");
                    return false;
                }

                // Otherwise, we compare timestamps.
                var localTime = localLastUpdated is null
                    ? "<<unknown>>" // Very unlikely, indicates the local files don't exist.
                    : localLastUpdated.Value.ToLocalTime().ToString("ddd MMM d, yyyy HH:mm:ss tt");
                var remoteTime = rgBotCodeDetails.modifiedDate is null
                    ? "<<unknown>>" // Very unlikely, indicates the server is misbehaving.
                    : rgBotCodeDetails.modifiedDate.Value.ToLocalTime().ToString("ddd MMM d, yyyy HH:mm:ss tt");

                // If the checksums don't match, we need to decide which to treat as the source of truth.
                // For now, we prompt the user to decide.
                // We can iterate and add more detailed conflict resolution later.
                var conflictMessage =
                    $"Bot '{localBot.name}' has changed from the last time it was synchronized with the server.  Which version should be used?" +
                    Environment.NewLine +
                    $"The local bot was last updated: {localTime}" + Environment.NewLine +
                    $"The remote bot was last updated: {remoteTime}";
                return EditorUtility.DisplayDialog(
                    "Bot Code Conflict",
                    conflictMessage,
                    "Local",
                    "Remote"
                );
            }

            // Fetch code details from the server.
            RGBotCodeDetails botCodeDetails = null;
            await _rgServiceManager.GetBotCodeDetails(
                remoteBot.id,
                (details) =>
                {
                    botCodeDetails = details;
                },
                () => { }
            );

            // If the checksums match, no sync is required at all
            if (botCodeDetails != null && localMd5 == botCodeDetails.md5)
            {
                RGDebug.LogDebug(
                    $"RG Bot Id: {remoteBot.id} is up to date; local md5: {localMd5} == remote md5: {botCodeDetails.md5}");
                return false;
            }

            var didUpdate = false;
            if (ShouldUpload(botCodeDetails))
            {
                // Upload bot code
                string zipFilePath = ZipPathForBot(remoteBot.id);
                await _rgServiceManager.UpdateBotCode(
                    remoteBot.id,
                    zipFilePath,
                    (_) =>
                    {
                        didUpdate = true;
                    },
                    () =>
                    {
                        RGDebug.LogError("Error updating bot code");
                    }
                );
            }
            else
            {
                // Download bot code!
                // We can replace the local bot folder with newer content because we know the local bot folder _is_
                // actually a Bot, with a BotRecord.asset and all.
                await CreateLocalBotRecordFromRG(remoteBot, replace: true);
                didUpdate = true;
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

            var md5 = RGUtils.CalculateMD5(zipPath);

            RGDebug.LogDebug($"Successfully Generated {zipPath} - md5: {md5}");
            return md5;
        }

        /**
         * <summary>Creates a folder and any needed parent folders for the given botname in the default bot path.</summary>
         * <param name="replace">If <c>true</c>, any existing content in this directory will be replaced.</param>
         * <returns>String path of the folder</returns>
         */
        private string CreateDefaultBotFolder(string botName, bool replace)
        {
            RGEditorUtils.CreateAllAssetFolders(BOTS_PATH);

            var botFolderName = $"{botName}".Replace('-', 'n');
            var folderString = $"{BOTS_PATH}/{botFolderName}";

            // If we're explicitly being told to replace the folder, and it exists, delete it first.
            if (replace && AssetDatabase.IsValidFolder(folderString))
            {
                // Clean the old folder out, if it exists
                AssetDatabase.DeleteAsset(folderString);
            }

            var createdFolderGuid = AssetDatabase.CreateFolder(BOTS_PATH, botFolderName);

            // If there's already a folder named 'botFolderName', Unity will deduplicate the name and append a number.
            // It then returns the GUID of the created asset folder (which may have a different name).
            return AssetDatabase.GUIDToAssetPath(createdFolderGuid);
        }

        private void CreateNewBotAssets(string folderName, string botName, long botId)
        {
            RGDebug.LogInfo($"Creating new Regression Games Unity bot at path {folderName}");

            if (!AssetDatabase.IsValidFolder(folderName))
            {
                // This is a precondition the caller should check.
                throw new InvalidOperationException($"Bot folder {folderName} should already exist.");
            }

            // create `Bot` record asset
            CreateBotAssetFile(folderName, botName, botId, null);

            var entryPointPath = $"{folderName}/BotEntryPoint.cs";
            RGDebug.LogDebug($"Writing {entryPointPath}");
            // create bot starter script (copy the entry point script and fix the namespace in the file)
            var bytes = File.ReadAllBytes("Packages/gg.regression.unity.bots/Runtime/Scripts/RGBotLocalRuntime/Template/BotEntryPoint.cs.template");
            var utfString = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            utfString = utfString.Replace("<TEMPLATE_NAMESPACE>", botName);

            File.WriteAllBytes(entryPointPath, Encoding.UTF8.GetBytes(utfString, 0, utfString.Length));
            RGDebug.LogInfo($"Regression Games Unity bot successfully created at path {folderName}");

            CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();
        }

        private void CreateBotAssetFile(string folderName, string botName, long botId, string botChecksum)
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
                botRecordAsset.ChecksumAtLastSync = botChecksum;
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
#endif
    }
}
