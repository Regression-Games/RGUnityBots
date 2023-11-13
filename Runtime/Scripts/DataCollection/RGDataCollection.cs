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
        
        private readonly string _sessionName = Guid.NewGuid().ToString();
        private readonly Dictionary<long, RGBot> _clientIdToBots = new();
        private readonly Dictionary<long, DateTime> _clientIdStartTimes = new();
        private readonly ConcurrentDictionary<long, List<RGStateActionReplayData>> _clientIdToReplayData = new();
        private readonly string _rootPath = Application.persistentDataPath;
        private readonly ConcurrentQueue<long> _screenshotTicksRequested = new();
        private readonly ConcurrentDictionary<long, long> _screenshotTicksCaptured = new();
        

        /**
         * Registers a bot instance under a specific client id
         * Called on the main thread.
         */
        public void RegisterBot(long clientId, RGBot bot)
        {
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Registering client for bot {bot.id}");
            _clientIdToBots[clientId] = bot;
            _clientIdStartTimes[clientId] = DateTime.Now;
        }

        /**
         * Screenshots are requested from a non-main thread, so we need to queue them up and process them on the main
         * thread only.
         * Called on the main thread once per Update (frame).
         */
        public void ProcessScreenshotRequests()
        {
            var done = false;
            while(!done && _screenshotTicksRequested.TryDequeue(out long tick) )
            {
                // only allow one screenshot per tick #
                // if many bots are running locally, they may all ask for the same tick
                if (_screenshotTicksCaptured.TryAdd(tick, tick))
                {
                    RGDebug.LogVerbose($"Capturing screenshot for tick: {tick}");
                    var texture = ScreenCapture.CaptureScreenshotAsTexture(1);
                    try
                    {
                        // Encode the texture into a jpg byte array
                        byte[] bytes = texture.EncodeToJPG(100);

                        string path = GetSessionDirectory($"screenshots/{tick}.jpg");

                        // Save the byte array as a jpg file
                        File.WriteAllBytes(path, bytes);
                    }
                    finally
                    {
                        // Destroy the texture to free up memory
                        Object.Destroy(texture);
                    }
                    done = true;
                }
                else
                {
                    // if we already took that screen shot, grad the next number from the queue
                    // until its empty or we find one we can screenshot for this frame
                    done = false;
                }
            }
        }

        /**
         * Saves the state, action, and validation information for a given tick
         */
        public void SaveReplayDataInfo(long clientId, RGStateActionReplayData replayData)
        {
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Saving replay data for tick {replayData.tickInfo.tick}");
            
            // Add the new replay data to a new or existing mapping in our client replay data dictionary
            var replayDatas = _clientIdToReplayData.GetOrAdd(clientId, new List<RGStateActionReplayData>());
            replayDatas.Add(replayData);

            // If the replay data has a validation, queue up a screenshot
            if (replayData.validationResults?.Length > 0)
            {
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Also saving a screenshot for tick {replayData.tickInfo.tick}");
                _screenshotTicksRequested.Enqueue(replayData.tickInfo.tick);
            }
            
        }

        public async Task SaveBotInstanceHistory(long clientId)
        {
            try
            {
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Starting to save bot instance history");

                var bot = _clientIdToBots[clientId];

                // Before we do anything, we need to verify that this bot actually exists in Regression Games
                await RGServiceManager.GetInstance()?.GetBotCodeDetails(bot.id,
                        rgBot =>
                        {
                            RGDebug.LogVerbose(
                                $"DataCollection[{clientId}] - Found a bot on Regression Games with this ID");
                        },
                        () => throw new Exception(
                            "This bot does not exist on the server. Please use the Regression Games > Synchronize Bots with RG menu option to register your bot."))
                    !;

                // Always create a bot instance id, since this is a local bot and doesn't exist on the servers yet
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance...");
                var botInstanceId = 0l;
                await RGServiceManager.GetInstance()?.CreateBotInstance(
                    bot.id,
                    _clientIdStartTimes[clientId],
                    (result) =>
                    {
                        botInstanceId = result.id;
                    },
                    () => { })!;
                RGDebug.LogVerbose(
                    $"DataCollection[{clientId}] - Creating the record for bot instance, with id {botInstanceId}");

                // Create a bot history record for this bot
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance history...");
                await RGServiceManager.GetInstance()?.CreateBotInstanceHistory(
                    botInstanceId,
                    (result) => { },
                    () => { })!;
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Created the record for bot instance history");

                // Save text files for each replay tick, zip it up, and then upload
                RGDebug.LogVerbose(
                    $"DataCollection[{clientId}] - Zipping the replay data (total of {_clientIdToReplayData[clientId].Count} files)...");
                foreach (var replayData in _clientIdToReplayData[clientId])
                {
                    var filePath =
                        GetSessionDirectory($"replayData/{clientId}/rgbot_replay_data_{replayData.tickInfo.tick}.txt");
                    await File.WriteAllTextAsync(filePath, replayData.ToSerialized());
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

                // Save all of the validation data (i.e. the validation summary and validations file overall)
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading validation data...");
                var validations = _clientIdToReplayData[clientId]
                    .Where(rd => rd.validationResults?.Length > 0)
                    .SelectMany(rd => rd.validationResults)
                    .ToArray();
                var passed = validations.Count(rd => rd.result == RGValidationResultType.PASS);
                var failed = validations.Count(rd => rd.result == RGValidationResultType.FAIL);
                var warnings = validations.Count(rd => rd.result == RGValidationResultType.WARNING);
                var validationSummary = new RGValidationSummary(passed, failed, warnings);
                await RGServiceManager.GetInstance()
                    ?.UploadValidations(botInstanceId, validations, () => { }, () => { })!;
                RGDebug.LogVerbose(
                    $"DataCollection[{clientId}] - Saved validations, now uploading validation summary...");
                await RGServiceManager.GetInstance()
                    ?.UploadValidationSummary(botInstanceId, validationSummary, _ => { }, () => { })!;
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Validation summary uploaded successfully");

                // Upload all of the screenshots for this client. Only upload the screenshots for ticks that had
                // validation results. Also, only upload 5 at a time.
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading screenshots...");
                var uploadSemaphore = new SemaphoreSlim(5);
                var validationTicks = validations.Select(rd => rd.tick).Distinct().ToArray();
                List<Task> uploadScreenshotTasks = new();
                foreach (var tick in validationTicks)
                {
                    var screenshotFilePath = GetSessionDirectory($"screenshots/{tick}.jpg");
                    if (File.Exists(screenshotFilePath))
                    {
                        await uploadSemaphore.WaitAsync();
                        var task = RGServiceManager.GetInstance()?.UploadScreenshot(
                            botInstanceId, tick, screenshotFilePath,
                            () =>
                            {
                                uploadSemaphore.Release();
                                RGDebug.LogVerbose(
                                    $"DataCollection[{clientId}] - Successfully uploaded screenshot {tick}");
                            },
                            () =>
                            {
                                uploadSemaphore.Release();
                            })!;
                        uploadScreenshotTasks.Add(task);
                    }
                    else
                    {
                        RGDebug.LogWarning(
                            $"DataCollection[{clientId}] - Expected to find screenshot for tick {tick}, but it was not found");
                    }
                }

                // Wait for all screenshots to upload
                await Task.WhenAll(uploadScreenshotTasks);
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Finished uploading screenshots");
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Data uploaded to Regression Games");
            }
            catch (Exception e)
            {
                RGDebug.LogError($"DataCollection[{clientId}] - Error uploading data: {e}");
                throw;
            }
            finally
            {
                Cleanup(clientId);
                // If there are no more bots, cleanup everything else
                if (_clientIdToBots.Count == 0)
                {
                    Cleanup();
                }
            }

        }

        private string GetSessionDirectory(string path = "")
        {
            //TODO: (REG-1422) Make this deterministic in a way that we can sync data later if the connection was down
            var fullPath = Path.Combine(_rootPath, "RGData",  _sessionName, path);
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
            RGDebug.LogVerbose($"DataCollection - Cleaned up all data for session {_sessionName}");
        }

        /**
         * Cleanup data for a single clientId. Note that this does not cleanup screenshot since other bots
         * may rely on those screenshots.
         */
        private void Cleanup(long clientId)
        {
            _clientIdToReplayData.TryRemove(clientId, out _);
            _clientIdToBots.Remove(clientId, out _);
            _clientIdStartTimes.Remove(clientId, out _);
            var replayPath = GetSessionDirectory($"replayData/{clientId}");
            Directory.Delete(replayPath, true);
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Cleaned up all data for client");
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
    
}