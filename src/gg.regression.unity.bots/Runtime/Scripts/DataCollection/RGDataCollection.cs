using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.Types;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

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
        private static readonly ProfilerMarker LogCollectionMarker = new(ProfilerCategory.Ai, "RegressionGames.DataCollection.LogCollector");
        private static readonly ProfilerMarker SaveTickDataMarker = new(ProfilerCategory.Ai, "RegressionGames.DataCollection.SaveTickData");

        private readonly string _sessionName = Guid.NewGuid().ToString();
        private readonly ConcurrentDictionary<long, BotInstanceDataCollectionState> _clientIdToState = new();
        private readonly string _rootPath = Application.persistentDataPath;
        private readonly ConcurrentQueue<long> _screenshotTicksRequested = new();
        private readonly ConcurrentDictionary<long, long> _screenshotTicksCaptured = new();

        private class BotInstanceDataCollectionState
        {
            public long clientId;
            public RGBot bot;
            public ConcurrentQueue<LogDataPoint> logs = new();
            public DateTime startTime;
            public List<Task> replayDataTasks = new();
            public Task validationDataTask;
            public Task logFlushTask;
            public RGValidationSummary validationSummary = new(0, 0, 0);

            public BotInstanceDataCollectionState(RGBot bot, DateTime startTime)
            {
                this.bot = bot;
                this.startTime = startTime;
            }

            public void StartCapturingLogs()
            {
                Application.logMessageReceivedThreaded += LogMessageReceived;
            }

            public void StopCapturingLogs()
            {
                Application.logMessageReceivedThreaded -= LogMessageReceived;
            }

            private void LogMessageReceived(string message, string stacktrace, LogType type)
            {
                Profiler.BeginSample("RegressionGames.DataCollection.LogCollector");
                // PERF: DateTimeOffset.Now is suprisingly costly, use DateTimeOffset.UtcNow unless we have to know the local timezone.
                // It's still good to use a DateTimeOffset here though because it ensures that when we serialize the data, it is clearly marked as a UTC time (with the `Z` suffix)
                var dataPoint = new LogDataPoint(DateTimeOffset.UtcNow, type, message, stacktrace);
                logs.Enqueue(dataPoint);
                Profiler.EndSample();
            }
        }

        /**
         * Registers a bot instance under a specific client id
         * Called on the main thread.
         */
        public void RegisterBot(long clientId, RGBot bot)
        {
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Registering client for bot {bot.id}");
            var state = new BotInstanceDataCollectionState(bot, DateTime.Now);
            _clientIdToState[clientId] = state;
            state.StartCapturingLogs();
        }

        /**
         * Screenshots are requested from a non-main thread, so we need to queue them up and process them on the main
         * thread only.
         * Called on the main thread once per Update (frame).
         */
        public IEnumerator ProcessScreenshotRequests()
        {

            // wait for finality of frame rendering before capturing screen shot
            yield return new WaitForEndOfFrame();
            var done = false;
            while (!done && _screenshotTicksRequested.TryDequeue(out long tick))
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
                        // we let this use the default of 75% quality as 100% file sizes are large for not much gain in quality
                        byte[] bytes = texture.EncodeToJPG();

                        string path = GetSessionDirectory($"screenshots/{tick}.jpg");

                        // Save the byte array as a jpg file
                        // We should wait to make sure all these tasks finish before we upload
                        File.WriteAllBytesAsync(path, bytes);
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
                    // if we already took that screen shot, grab the next number from the queue
                    // until its empty or we find one we can screenshot for this frame
                    done = false;
                }
            }
        }

        /**
         * Saves the state, action, and validation information for a given tick
         */
//        public async Task SaveReplayDataInfo(long clientId, RGStateActionReplayData replayData)
//        {
//            RGDebug.LogVerbose($"DataCollection[{clientId}] - Saving replay data for tick {replayData.tickInfo.tick}");
//
//            var state = _clientIdToState.TryGetValue(clientId, out var s) ? s : null;
//            if (state == null)
//            {
//                // Shouldn't really happen
//                RGDebug.LogError("Attempted to save replay data for a stopped bot");
//                return;
//            }
//
//            using var _ = SaveTickDataMarker.Auto();
//
//            // Add the new replay data to a new or existing mapping in our client replay data dictionary
//            var filePath =
//                GetSessionDirectory($"replayData/{clientId}/rgbot_replay_data_{replayData.tickInfo.tick}.txt");
//            var task = File.WriteAllTextAsync(filePath, replayData.ToSerialized());
//            state.replayDataTasks.Add(task);
//
//            // If the replay data has a validation, queue up a screenshot
//            if (replayData.validationResults?.Length > 0)
//            {
//                RGDebug.LogVerbose($"DataCollection[{clientId}] - Also saving a screenshot for tick {replayData.tickInfo.tick}");
//                _screenshotTicksRequested.Enqueue(replayData.tickInfo.tick);
//
//                var validations = replayData.validationResults;
//
//                // update the validation summary data
//                state.validationSummary.passed += validations.Count(rd => rd.result == RGValidationResultType.PASS);
//                state.validationSummary.failed += validations.Count(rd => rd.result == RGValidationResultType.FAIL);
//                state.validationSummary.warnings += validations.Count(rd => rd.result == RGValidationResultType.WARNING);
//
//                var validationsLinesString = SerializeJsonLines(validations);
//
//                // Wait for any prior validation write task to finish
//                if (state.validationDataTask != null)
//                {
//                    await state.validationDataTask;
//                    state.validationDataTask = null;
//                }
//
//                // Write out the validations to the JSONL file; note.. the prior write mush finish before the next tick
//                // otherwise we can't write to the file safely
//                var validationFilePath =
//                    GetSessionDirectory($"validationData/{clientId}/rgbot_validations.jsonl");
//                var validationTask = File.AppendAllTextAsync(validationFilePath, validationsLinesString);
//                state.validationDataTask = validationTask;
//            }
//
//            // Atomically swap the logs queue with an empty queue.
//            // Then the log collector can upload this queue's logs while logs continue to accumulate in the new queue
//            var logs = Interlocked.Exchange(ref state.logs, new());
//            if (!logs.IsEmpty)
//            {
//                state.logFlushTask = FlushLogs(clientId, logs);
//            }
//
//            // remove any already completed tasks from the tracker
//            state.replayDataTasks.RemoveAll(v => v.IsCompleted);
//
//        }

        class BotDoesNotExistOnServerException : Exception
        {
            public BotDoesNotExistOnServerException(string message) : base(message)
            {
            }
        }

        public async Task SaveBotInstanceHistory(long clientId)
        {
            // Removing the state from the dictionary will prevent any further data from being saved
            var state = _clientIdToState.TryRemove(clientId, out var s) ? s : null;
            if (state == null)
            {
                // Shouldn't really happen
                RGDebug.LogError("Attempted to save replay data for a stopped bot");
                return;
            }
            state.StopCapturingLogs();

            try
            {
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Starting to save bot instance history");

                var serviceManager = RGServiceManager.GetInstance();
                if (serviceManager == null)
                {
                    // No point in continuing if there's no service manager
                    return;
                }
                Exception createBotInstanceException = null;
                var botInstanceId = state.clientId;
                try
                {
                    // Before we do anything, we need to verify that this bot actually exists in Regression Games
                    await serviceManager.GetBotCodeDetails(state.bot.id,
                            rgBot =>
                            {
                                RGDebug.LogVerbose(
                                    $"DataCollection[{clientId}] - Found a bot on Regression Games with id {state.bot.id}");
                            },
                            () => throw new BotDoesNotExistOnServerException(
                                $"Bot id {state.bot.id} does not exist on the server. Please use the Regression Games > Synchronize Bots with RG menu option to register your bot."));

                    // Always create a bot instance id, since this is a local bot and doesn't exist on the servers yet
                    RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance...");


                    await serviceManager.CreateBotInstance(
                        state.bot.id,
                        state.startTime,
                        (result) => { botInstanceId = result.id; },
                        () => { });
                }
                catch (Exception e)
                {
                    // we save this to throw later after zipping up the files locally, otherwise developers can't validate things locally
                    createBotInstanceException = e;
                }

                RGDebug.LogVerbose(
                    $"DataCollection[{clientId}] - Creating the record for bot instance, with id {botInstanceId}");

                // Save text files for each replay tick, zip it up, and then upload
                RGDebug.LogVerbose(
                    $"DataCollection[{clientId}] - Zipping the replay data for botInstanceId: {botInstanceId}...");

                //Wait for any outstanding file write tasks to finish
                await Task.WhenAll(state.replayDataTasks);

                ZipHelper.CreateFromDirectory(
                    GetSessionDirectory($"replayData/{clientId}/"),
                    GetSessionDirectory($"replayData/rg_bot_replay_data-{botInstanceId}.zip")
                );
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Zipped data, now uploading...");

                // Flush any outstanding logs.
                // We don't need to do the atomic-swap trick here
                // because we've been removed from the state dictionary so no more logs will be written to this queue.
                await FlushLogs(clientId, state.logs);
                var logsFilePath =
                    GetSessionDirectory($"validationData/{clientId}/rgbot_logs.jsonl");

                // Save all of the validation data (i.e. the validation summary and validations file overall)
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading validation data...");

                // Wait for any prior validation write task to finish
                if (state.validationDataTask != null)
                {
                    await state.validationDataTask;
                }

                var validationFilePath =
                    GetSessionDirectory($"validationData/{clientId}/rgbot_validations.jsonl");


                // ==== now that we've saved everything ... do all the uploading if we can
                if (createBotInstanceException != null)
                {
                    throw createBotInstanceException;
                }

                // Create a bot history record for this bot
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Creating the record for bot instance history...");
                await serviceManager.CreateBotInstanceHistory(
                    botInstanceId,
                    (result) => { },
                    () => { });
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Created the record for bot instance history");

                await serviceManager.UploadReplayData(
                    botInstanceId, GetSessionDirectory($"replayData/rg_bot_replay_data-{botInstanceId}.zip"),
                    () => { }, () => { });
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Successfully uploaded replay data");

                // Upload validation data
                await serviceManager
                    .UploadValidations(botInstanceId, validationFilePath, () => { }, () => { });

                RGDebug.LogVerbose(
                     $"DataCollection[{clientId}] - Saved validations, now uploading validation summary...");
                await serviceManager
                    .UploadValidationSummary(botInstanceId, state.validationSummary, _ => { }, () => { });
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Validation summary uploaded successfully");

                RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading logs...");

                await serviceManager
                    .UploadLogs(botInstanceId, logsFilePath, () => { }, () => { });
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploaded logs");

                // Upload all of the screenshots for this client. Only upload the screenshots for ticks that had
                // validation results. Also, only upload 5 at a time.
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Uploading screenshots...");
                var uploadSemaphore = new SemaphoreSlim(5);

                var screenShotsDirectory = GetSessionDirectory("screenshots");
                var screenshotFiles = Directory.GetFiles(screenShotsDirectory);

                List<Task> uploadScreenshotTasks = new();
                foreach (var screenshotFilePath in screenshotFiles)
                {
                    if (File.Exists(screenshotFilePath))
                    {
                        // [...]/screenshots/<tick>.jpg
                        // grab the tick part of the path
                        var tick = long.Parse(screenshotFilePath
                            .Substring(screenshotFilePath.LastIndexOf(Path.DirectorySeparatorChar) + 1).Split('.')[0]);
                        await uploadSemaphore.WaitAsync();
                        var task = serviceManager.UploadScreenshot(
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
                            });
                        uploadScreenshotTasks.Add(task);
                    }
                    else
                    {
                        RGDebug.LogWarning(
                            $"DataCollection[{clientId}] - Screenshot file not found - {screenshotFilePath}");
                    }
                }

                // Wait for all screenshots to upload
                await Task.WhenAll(uploadScreenshotTasks);
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Finished uploading screenshots");
                RGDebug.LogVerbose($"DataCollection[{clientId}] - Data uploaded to Regression Games");
            }
            catch (Exception e)
            {
                if (e is BotDoesNotExistOnServerException)
                {
                    RGDebug.LogWarning($"DataCollection[{clientId}] - Error uploading data - {e.Message}");
                }
                else
                {
                    RGDebug.LogException(e, $"DataCollection[{clientId}] - Error uploading data");
                }

                throw;
            }
            finally
            {
                // DEV NOTE : Comment out these cleanup calls to test extracts / replays locally
                Cleanup(clientId);
                // If there are no more bots, cleanup everything else
                if (_clientIdToState.IsEmpty)
                {
                    Cleanup();
                }
            }

        }

        private Task FlushLogs(long clientId, ConcurrentQueue<LogDataPoint> logs)
        {
            var lines = SerializeJsonLines(logs);
            var logsFilePath =
                GetSessionDirectory($"validationData/{clientId}/rgbot_logs.jsonl");
            return File.AppendAllTextAsync(logsFilePath, lines);
        }

        private string GetSessionDirectory(string path = "")
        {
            //TODO: (REG-1422) Make this deterministic in a way that we can sync data later if the connection was down
            var fullPath = Path.Combine(_rootPath, "RGData", _sessionName, path);
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
            var replayPath = GetSessionDirectory($"replayData/{clientId}");
            Directory.Delete(replayPath, true);
            RGDebug.LogVerbose($"DataCollection[{clientId}] - Cleaned up all data for client");
        }

        private static string SerializeJsonLines(IEnumerable<object> items)
        {
            // Convert the list into JSONL format
            var builder = new StringBuilder();
            foreach (var item in items)
            {
                builder.AppendLine(JsonConvert.SerializeObject(item));
            }

            // Combine the JSON strings with newline characters
            return builder.ToString();
        }
    }

    // Extension code borrowed from StackOverflow to allow creating a zip of directory
    // while filtering out certain contents
    public static class ZipHelper
    {
        public static void CreateFromDirectory(string sourceDirectoryName,
            string destinationArchiveFileName,
            CompressionLevel compressionLevel = CompressionLevel.Fastest,
            bool includeBaseDirectory = false,
            Predicate<string> exclusionFilter = null
        )
        {
            if (string.IsNullOrEmpty(sourceDirectoryName))
            {
                throw new ArgumentNullException("sourceDirectoryName");
            }
            if (string.IsNullOrEmpty(destinationArchiveFileName))
            {
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
