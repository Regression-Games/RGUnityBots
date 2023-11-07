using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Object = UnityEngine.Object;

namespace RegressionGames.DataCollection
{
    
    /**
     * The RGDataCollection class is responsible for collecting all of the data that is generated over multiple bot
     * instance runs. More specifically, this means collecting screenshots and replay data (which includes state,
     * action, and validation information).
     * 
     * In order to be efficient, this class does the following:
     *  - Maintains a mapping from clientIds to botInstanceIds - this allows saving the data later
     *  - Maintains a mapping from clientIds to replay data - this allows us to do a few things:
     *      - The replay data stores the validations that occurred in that tick, allowing us to save validation data later
     *      - The replay data stores... the replay data, so it can be saved as a zip replay file
     *      - The validation ticks tell us which screenshots are relevant to each bot
     *  - Takes a screenshot whenever a new validation result comes in, so later that screenshot can be uploaded, and
     *    tracks the ticks that were screenshotted
     *
     * Once all clients have been disconnected, this class can be reset and all data left over (i.e. screenshots) can
     * be deleted from the system.
     * 
     */
    public class RGDataCollection
    {

        private readonly string _sessionName;
        private Dictionary<long, RGBot> _clientIdToBots;
        private Dictionary<long, List<RGStateActionReplayData>> _clientIdToReplayData;
        private HashSet<long> _screenshottedTicks;
        private string rootPath;
        private ConcurrentBag<long> _screenshotTicksRequested;

        public RGDataCollection()
        {
            // Name the session, and setup a temporary directory for all data
            _sessionName = Guid.NewGuid().ToString();
            
            // Instantiate the dictionaries
            _clientIdToBots = new();
            _clientIdToReplayData = new();
            _screenshottedTicks = new();
            _screenshotTicksRequested = new();
            
            // We instantiate this here because it cannot be accessed in real time by non-main threads
            rootPath = Application.persistentDataPath;
        }

        /**
         * Registers a bot instance under a specific client id
         */
        public void RegisterBot(long clientId, RGBot bot)
        {
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Registering client for bot {bot.id}");
            _clientIdToBots[clientId] = bot;
        }

        /**
         * Screenshots are requested from a non-main thread, so we need to queue them up and process them on the main
         * thread only
         */
        public void ProcessScreenshotRequests()
        {
            var ticks = _screenshotTicksRequested.Distinct().ToArray();
            
            if (ticks.Length > 0)
            {
                RGDebug.LogVerbose($"Capturing screenshot for ticks {string.Join(", ", ticks)}");
                var texture = ScreenCapture.CaptureScreenshotAsTexture(1);
                foreach (var tick in ticks)
                {
                    string path = GetSessionDirectory($"screenshots/{tick}.jpg");
                    try
                    {
                        // Encode the texture into a jpg byte array
                        byte[] bytes = texture.EncodeToJPG(100);

                        // Save the byte array as a jpg file
                        File.WriteAllBytes(path, bytes);
                    }
                    finally
                    {
                        // Destroy the texture to free up memory
                        Object.Destroy(texture);
                    }
                }
            }

            _screenshotTicksRequested.Clear();
        }

        public void SaveReplayDataInfo(long clientId, RGStateActionReplayData replayData)
        {
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Saving replay data for tick {replayData.tickInfo.tick}");
            // Check if the dictionary already has the clientId key
            if (_clientIdToReplayData.TryGetValue(clientId, out List<RGStateActionReplayData> existingList))
            {
                // Key exists, so add the replayData to the existing list
                existingList.Add(replayData);
            }
            else
            {
                // Key does not exist, so create a new list and add it to the dictionary
                _clientIdToReplayData[clientId] = new List<RGStateActionReplayData> { replayData };
            }
            
            // If the replay data has a validation, also take a screenshot if not already taken
            if (replayData.validationResults?.Length > 0 && !_screenshottedTicks.Contains(replayData.tickInfo.tick))
            {
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Also saving a screenshot for tick {replayData.tickInfo.tick}");
                var validationTick = replayData.tickInfo.tick;
                _screenshotTicksRequested.Add(validationTick);
            }
        }

        public async Task SaveBotInstanceHistory(long clientId)
        {
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Starting to save bot instance history");
            
            var bot = _clientIdToBots[clientId];

            // Before we do anything, we need to verify that this bot actually exists in Regression Games
            await RGServiceManager.GetInstance()?.GetBotCodeDetails(bot.id, 
                rgBot =>
            {
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Found a bot on Regression Games with this ID");
            }, () => throw new Exception("This bot does not exist on the server. Please use the Regression Games > Synchronize Bots with RG menu option to register your bot."))!;
            
            // Next, always create a bot instance id, since this is a local bot and doesn't exist on the servers yet
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance...");
            var botInstance = await RGServiceManager.GetInstance()?.CreateBotInstance(bot.id)!;
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance, with id {botInstance.id}");
            var botInstanceId = botInstance.id;

            // First, create a bot history record for this bot
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance history...");
            await RGServiceManager.GetInstance()?.CreateBotInstanceHistory(botInstanceId)!;
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Created the record for bot instance history");
            
            // Next, save text files for each replay tick, zip it up, and then upload
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Zipping the replay data (total of {_clientIdToReplayData[clientId].Count} files)...");
            foreach (var replayData in _clientIdToReplayData[clientId])
            {
                var filePath = GetSessionDirectory($"replayData/{clientId}/rg_bot_replay_data_{replayData.tickInfo.tick}.txt");
                RGDebug.LogVerbose(JsonUtility.ToJson(replayData));
                RGDebug.LogVerbose(filePath);
                await File.WriteAllTextAsync(filePath, JsonUtility.ToJson(replayData));
            }
            ZipHelper.CreateFromDirectory(
                GetSessionDirectory($"replayData/{clientId}/"),
                GetSessionDirectory($"replayData/rg_bot_replay_data-{botInstanceId}.zip")
            );
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Zipped data, now uploading...");
            await RGServiceManager.GetInstance()?.UploadReplayData(
                botInstanceId, GetSessionDirectory($"replayData/rg_bot_replay_data-{botInstanceId}.zip"),
                () => { }, () => { })!;
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Successfully uploaded replay data");
            
            // Now save all of the validation data (i.e. the validation summary and validations file overall)
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading validation data...");
            var validations = _clientIdToReplayData[clientId]
                .Where(rd => rd.validationResults?.Length > 0)
                .SelectMany(rd => rd.validationResults)
                .ToArray();
            var passed = validations.Count(rd => rd.result == RGValidationResultType.PASS);
            var failed = validations.Count(rd => rd.result == RGValidationResultType.FAIL);
            var warnings = validations.Count(rd => rd.result == RGValidationResultType.WARNING);
            var validationSummary = new RGValidationSummary(passed, failed, warnings);
            await RGServiceManager.GetInstance()?.UploadValidations(botInstanceId, validations, () => { }, () => { })!;
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Saved validations, now uploading validation summary...");
            await RGServiceManager.GetInstance()?.UploadValidationSummary(botInstanceId, validationSummary, _ => { }, () => { })!;
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Validation summary uploaded successfully");

            // Then, upload all of the screenshots for this client. Only upload the screenshots for ticks that had
            // validation results. Also, only upload 5 at a time.
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading screenshots...");
            var uploadSemaphore = new SemaphoreSlim(5);
            var validationTicks = validations.Select(rd => rd.tick).Distinct().ToArray();
            List<Task> uploadScreenshotTasks = new ();
            foreach (var tick in validationTicks)
            {
                var screenshotFilePath = GetSessionDirectory($"screenshots/{tick}.jpg");
                if (File.Exists(screenshotFilePath))
                {
                    var task = RGServiceManager.GetInstance()?.UploadScreenshot(
                        botInstanceId, tick, screenshotFilePath, 
                        () =>
                        {
                            RGDebug.LogVerbose($"DataCollection[{clientId}] - Successfully uploaded screenshot {tick}");
                        },
                        () =>
                        {
                        
                        }, uploadSemaphore)!;
                    uploadScreenshotTasks.Add(task);
                }
                else
                {
                    RGDebug.LogWarning($"Expected to find screenshot for tick {tick}, but it was not found");
                }
            }

            // Wait for all screenshots to upload
            await Task.WhenAll(uploadScreenshotTasks);
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Successfully finished uploading screenshots");

            RGDebug.LogVerbose("Data uploaded to Regression Games");
            
        }

        private string GetSessionDirectory(string path = "")
        {
            var fullPath = Path.Combine(rootPath, "RGData",  _sessionName, path);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            return fullPath;
        }

        public void Cleanup()
        {
            // Delete everything in the session folder
            var sessionPath = GetSessionDirectory();
            Directory.Delete(sessionPath, true);
            Debug.Log("Deleted all local temp data collected for RG bots");
        }

    }
    
    // Extension code borrowed from StackOverflow to allow creating a zip of directory
    // while filtering out certain contents
    public static class ZipHelper {
        public static void CreateFromDirectory(string sourceDirectoryName,
            string destinationArchiveFileName,
            System.IO.Compression.CompressionLevel compressionLevel = CompressionLevel.Fastest,
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
    
}