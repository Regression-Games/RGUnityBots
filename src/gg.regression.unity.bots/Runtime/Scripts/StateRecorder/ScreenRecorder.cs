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
using RegressionGames.ActionManager;
using RegressionGames.CodeCoverage;
using RegressionGames.RemoteOrchestration;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using StateRecorder.BotSegments;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
// ReSharper disable InconsistentNaming

// ReSharper disable once ForCanBeConvertedToForeach - Better performance using indexing vs enumerators
// ReSharper disable once LoopCanBeConvertedToQuery - Better performance using indexing vs enumerators
namespace RegressionGames.StateRecorder
{

    struct TickDataToWriteToDisk
    {
        public string directoryPrefix { get; }
        public long tickNumber { get; }
        public byte[] botSegmentJson { get; }
        public byte[] jsonData { get; }

        [CanBeNull] public byte[] screenshotData { get; }
        public string logs { get; }

        public TickDataToWriteToDisk(string directoryPrefix, long tickNumber, byte[] botSegmentJson, byte[] jsonData, byte[] screenshotData, string logs)
        {
            this.directoryPrefix = directoryPrefix;
            this.tickNumber = tickNumber;
            this.botSegmentJson = botSegmentJson;
            this.jsonData = jsonData;
            this.screenshotData = screenshotData;
            this.logs = logs;
        }
    }

    public class ScreenRecorder : MonoBehaviour
    {
        public static readonly string RecordingPathName = "Latest_Recording";

        [Tooltip("Minimum FPS at which to capture frames if you desire more granularity in recordings.  Key frames may still be recorded more frequently than this. <= 0 will only record key frames")]
        public int recordingMinFPS;

        [Tooltip("Directory to save state recordings in.  This directory will be created if it does not exist.  If not specific, this will default to 'unity_videos' in your user profile path for your operating system.")]
        public string stateRecordingsDirectory = "";

        [Tooltip("Minimize the amount of criteria in bot segment recordings.  This limits the endCriteria in recorded bot segments to only include objects with count deltas and objects that were under click locations.")]
        public bool minimizeRecordingCriteria = true;

        [Tooltip("RG-INTERNAL/EXPERIMENTAL: Minimize the amount of mouse movement data capture in bot segment recordings.  This currently breaks replay on any game where camera movement is locked to the mouse, like First Person Shooters.")]
        public bool minimizeRecordingMouseMovements = false;

        private double _lastCvFrameTime = -1;

        private int _frameCountSinceLastTick;

        private string _currentSessionId;
        private string _referenceSessionId;

        private string _currentGameplaySessionDirectoryPrefix;
        private string _currentGameplaySessionScreenshotsDirectoryPrefix;
        private string _currentGameplaySessionBotSegmentsDirectoryPrefix;
        private string _currentGameplaySessionDataDirectoryPrefix;
        private string _currentGameplaySessionCodeCoverageMetadataPath;
        private string _currentGameplaySessionActionCoverageMetadataPath;
        private string _currentGameplaySessionGameMetadataPath;
        private string _currentGameplaySessionThumbnailPath;
        private string _currentGameplaySessionLogsDirectoryPrefix;

        private CancellationTokenSource _tokenSource;

        private static ScreenRecorder _this;

        public bool IsRecording { get; private set; }

        private bool _isReplay;

        private readonly ConcurrentQueue<Texture2D> _texture2Ds = new();

        private long _tickNumber;
        private DateTime _startTime;

        // data to record for a given tick
        // and a "cleanup" callback to invoke after the data is written
        private BlockingCollection<(TickDataToWriteToDisk, Action)> _tickQueue;

        private readonly List<(string, Task)> _fileWriteTasks = new();

        private MouseInputActionObserver _mouseObserver;
        private ProfilerObserver _profilerObserver;
        private LoggingObserver _loggingObserver;

#if UNITY_EDITOR
        private bool _needToRefreshAssets;
#endif

        public static ScreenRecorder GetInstance()
        {
            return _this;
        }

        public string GetCurrentSaveDirectory()
        {
            return _currentGameplaySessionDataDirectoryPrefix;
        }

        public void Awake()
        {
            // only allow 1 of these to be alive
            if (_this != null && _this.gameObject != gameObject)
            {
                Destroy(gameObject);
                return;
            }

            if (string.IsNullOrEmpty(stateRecordingsDirectory))
            {
                stateRecordingsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/unity_videos";
            }

            // keep this thing alive across scenes
            DontDestroyOnLoad(gameObject);
            _this = this;
        }

        public void OnEnable()
        {
            _this._mouseObserver = GetComponent<MouseInputActionObserver>();
            _this._profilerObserver = GetComponent<ProfilerObserver>();
            _this._loggingObserver = GetComponent<LoggingObserver>();
        }

        private void OnApplicationQuit()
        {
            StopRecordingNoCoroutine();
        }

        private void Start()
        {
            var read_formats = Enum.GetValues( typeof( GraphicsFormat ) ).Cast<GraphicsFormat>()
                .Where( f => SystemInfo.IsFormatSupported( f, FormatUsage.ReadPixels ) )
                .ToArray();
            RGDebug.LogInfo( "Supported Formats for Readback\n" + string.Join( "\n", read_formats ) );
        }

        private async Task HandleEndRecording(long tickCount,
            DateTime startTime,
            DateTime endTime,
            bool wasReplay,
            long loggedWarnings,
            long loggedErrors,
            string dataDirectoryPrefix,
            string botSegmentsDirectoryPrefix,
            string screenshotsDirectoryPrefix,
            string codeCoverageMetadataPath,
            string actionCoverageMetadataPath,
            string thumbnailPath,
            string logsDirectoryPrefix,
            string gameMetadataPath,
            bool onDestroy = false)
        {
            if (!onDestroy)
            {
                StartCoroutine(ShowUploadingIndicator(true));
            }

            // Save code coverage metadata if code coverage is enabled
            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            if (rgSettings.GetFeatureCodeCoverage())
            {
                var metadata = RGCodeCoverage.GetMetadata();
                if (metadata != null)
                {
                    RGDebug.LogInfo($"Saving code coverage metadata to file: {codeCoverageMetadataPath}");
                    string codeCoverageMetadataJson = ((IStringBuilderWriteable)metadata).ToJsonString();
                    // ReSharper disable once UseAwaitUsing
                    using StreamWriter swc = new StreamWriter(codeCoverageMetadataPath);
                    // ReSharper disable once MethodHasAsyncOverload
                    swc.Write(codeCoverageMetadataJson);
                }
            }

            // Save action coverage metadata if action analysis was done
            var actionUsageSummary = RGActionRuntimeCoverageAnalysis.BuildSummary();
            if (actionUsageSummary != null)
            {
                RGDebug.LogInfo($"Saving action coverage metadata to file: {actionCoverageMetadataPath}");
                string actionCoverageMetadataJson = ((IStringBuilderWriteable)actionUsageSummary).ToJsonString();
                // ReSharper disable once UseAwaitUsing
                using StreamWriter swa = new StreamWriter(actionCoverageMetadataPath);
                // ReSharper disable once MethodHasAsyncOverload
                swa.Write(actionCoverageMetadataJson);
            }

            // Save game metadata
            var gameMetadata = RGGameMetadata.GetMetadata();
            RGDebug.LogInfo($"Saving game metadata to file: {gameMetadataPath}");
            string gameMetadataJson = ((IStringBuilderWriteable)gameMetadata).ToJsonString();
            // ReSharper disable once UseAwaitUsing
            using StreamWriter swg = new StreamWriter(gameMetadataPath);
            // ReSharper disable once MethodHasAsyncOverload
            swg.Write(gameMetadataJson);

            var zipTask1 = Task.Run(() =>
            {
                // First, save the gameplay session data
                RGDebug.LogInfo($"Zipping state recording replay to file: {dataDirectoryPrefix}.zip");
                ZipFile.CreateFromDirectory(dataDirectoryPrefix, dataDirectoryPrefix + ".zip");
                RGDebug.LogInfo($"Finished zipping replay to file: {dataDirectoryPrefix}.zip");
            });

            var zipTask2 = Task.Run(() =>
            {
                // Then save the screenshots separately
                RGDebug.LogInfo($"Zipping screenshot recording replay to file: {screenshotsDirectoryPrefix}.zip");
                ZipFile.CreateFromDirectory(screenshotsDirectoryPrefix, screenshotsDirectoryPrefix + ".zip");
                RGDebug.LogInfo($"Finished zipping replay to file: {screenshotsDirectoryPrefix}.zip");
            });

            var zipTask3 = Task.Run(() =>
            {
                // Then save the bot segments separately
                RGDebug.LogInfo($"Zipping bot_segments recording replay to file: {botSegmentsDirectoryPrefix}.zip");
                ZipFile.CreateFromDirectory(botSegmentsDirectoryPrefix, botSegmentsDirectoryPrefix + ".zip");
                RGDebug.LogInfo($"Finished zipping replay to file: {botSegmentsDirectoryPrefix}.zip");
            });

            var zipTask4 = Task.Run(() =>
            {
                // Then save the logs separately
                RGDebug.LogInfo($"Zipping logs recording replay to file: {logsDirectoryPrefix}.zip");
                ZipFile.CreateFromDirectory(logsDirectoryPrefix, logsDirectoryPrefix + ".zip");
                RGDebug.LogInfo($"Finished zipping replay to file: {logsDirectoryPrefix}.zip");
            });

            // Finally, we also save a thumbnail, by choosing the middle file in the screenshots
            var screenshotFiles = Directory.GetFiles(screenshotsDirectoryPrefix);
            if (screenshotFiles.Length > 0)
            {
                var middleFile = screenshotFiles[screenshotFiles.Length / 2];
                File.Copy(middleFile, thumbnailPath);
            }

            // reset the action analysis now that we're done with it
            RGActionRuntimeCoverageAnalysis.Reset();

            // wait for the zip tasks to finish
            Task.WaitAll(zipTask1, zipTask2, zipTask3, zipTask4);

            if (!wasReplay)
            {
                // Copy the most recent recording into the user's project if running in the editor , or their persistent data path if running in production runtime
                // do NOT copy if this was a replay
                await MoveSegmentsToProject(botSegmentsDirectoryPrefix);
            }

#if UNITY_EDITOR
            _needToRefreshAssets = true;
#endif

            //TODO (REG-2181): Add upload logic for action analysis metadata

            var uploadTask = CreateAndUploadGameplaySession(
                tickCount,
                startTime,
                endTime,
                loggedWarnings,
                loggedErrors,
                dataDirectoryPrefix,
                botSegmentsDirectoryPrefix,
                screenshotsDirectoryPrefix,
                thumbnailPath,
                logsDirectoryPrefix,
                gameMetadataPath,
                onDestroy
            );

            if (Directory.Exists(dataDirectoryPrefix))
            {
                Directory.Delete(dataDirectoryPrefix, true);
            }
            if (Directory.Exists(botSegmentsDirectoryPrefix))
            {
                Directory.Delete(botSegmentsDirectoryPrefix, true);
            }
            if (Directory.Exists(screenshotsDirectoryPrefix))
            {
                Directory.Delete(screenshotsDirectoryPrefix, true);
            }
            if (Directory.Exists(logsDirectoryPrefix))
            {
                Directory.Delete(logsDirectoryPrefix, true);
            }

            await uploadTask;

            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = null;
        }

        private async Task MoveSegmentsToProject(string botSegmentsDirectoryPrefix)
        {
            // Get all the file paths normalized to /
            // Note: Ensure that these files aren't loaded lazily (don't use Directory.EnumerateFiles)
            // as the folder gets moved later down this function and lazy loading will not find the files.
            var segmentFiles = Directory.GetFiles(botSegmentsDirectoryPrefix)
                .Where(a=>a.EndsWith(".json"))
                .Select(a=>a.Replace('\\','/'))
                .Select(a=>a.Substring(a.LastIndexOf('/')+1));

            // Order numerically instead of alphanumerically to ensure 2.json is before 10.json.
            segmentFiles = BotSegmentDirectoryParser.OrderJsonFiles(segmentFiles);

            string segmentResourceDirectory = null;
            string sequenceJsonPath = null;
#if UNITY_EDITOR
            segmentResourceDirectory = "Assets/RegressionGames/Resources/BotSegments/" + RecordingPathName;
            sequenceJsonPath = "Assets/RegressionGames/Resources/BotSequences/" + RecordingPathName + ".json";
#else
            // Production runtime should write to persistent data path
            segmentResourceDirectory = Application.persistentDataPath + "/RegressionGames/Resources/BotSegments/" + RecordingPathName;
            sequenceJsonPath = Application.persistentDataPath + "/RegressionGames/Resources/BotSequences/" + RecordingPathName + ".json";
#endif
            try
            {
                // delete the existing directory if it exists and re-create it
                if (Directory.Exists(segmentResourceDirectory))
                {
                    Directory.Delete(segmentResourceDirectory, true);
                }

                // Path.GetDirectoryName returns the parent directory (e.g. "Assets/RegressionGames/Resources/BotSegments")
                // This is because our current path does not end with a trailing /
                // So we can safely create this without interfering with the .Move command right below.
                Directory.CreateDirectory(Path.GetDirectoryName(segmentResourceDirectory));

                // If on a Mac, for some weird reason, .Move will fail for project paths that have spaces. We predict
                // that downstream, something is happening where the space is not properly handled in Unix commands.
                // Unfortunately, we could only reproduce this once, but this change should fix it. On Mac, we instead
                // use the File.Copy function.
                if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                {
                    Directory.CreateDirectory(segmentResourceDirectory); // Since we aren't moving, we need to create the directory
                    // ReSharper disable once PossibleMultipleEnumeration
                    foreach (var segmentFile in segmentFiles)
                    {
                        var sourcePath = botSegmentsDirectoryPrefix + "/" + segmentFile;
                        var destinationPath = segmentResourceDirectory + "/" + segmentFile;
                        File.Copy(sourcePath, destinationPath);
                    }
                    // Then delete the original directory
                    Directory.Delete(botSegmentsDirectoryPrefix, true);
                }
                else
                {
                    // move the directory (this also deletes the source directory)
                    Directory.Move(botSegmentsDirectoryPrefix, segmentResourceDirectory);
                }

            }
            catch (Exception ex)
            {
                // this is here for dev debugging
                RGDebug.LogException(ex, "Failed to copy bot segments into game resources");
                throw;
            }

            // Write a README.txt into the directory explaining it is auto generated by recording
            var segmentsReadmePath = segmentResourceDirectory + "/README.txt";
            await File.WriteAllBytesAsync(segmentsReadmePath, Encoding.UTF8.GetBytes("The Bot Segment json files in this directory are auto generated when recording a gameplay session and should not be modified.  Creating a new recording will overwrite the files in this directory."));

            // Create the bot_sequence json for this directory
            // ReSharper disable once PossibleMultipleEnumeration - if the mac specific code path already enumerated this list.. oh well, so be it
            var sequenceEntries = segmentFiles.Select(a => new BotSequenceEntry()
            {
                path = segmentResourceDirectory + "/" + a
            }).ToList();


            var botSequence = new BotSequence()
            {
                name = "Latest Recording",
                description = "Note: This sequence was generated when recording a gameplay session and should not be modified.  Creating a new recording will overwrite this sequence.",
                segments = sequenceEntries
            };

            Directory.CreateDirectory(Path.GetDirectoryName(sequenceJsonPath));
            await File.WriteAllBytesAsync(sequenceJsonPath, Encoding.UTF8.GetBytes(botSequence.ToJsonString()));

            // refresh the sequences list
            RGSequenceManager.GetInstance()?.LoadSequences();

        }

        private async Task CreateAndUploadGameplaySession(long tickCount,
            DateTime startTime,
            DateTime endTime,
            long loggedWarnings,
            long loggedErrors,
            string dataDirectoryPrefix,
            string botSegmentsDirectoryPrefix,
            string screenshotsDirectoryPrefix,
            string thumbnailPath,
            string logsPathPrefix,
            string gameMetadataPath,
            bool onDestroy = false
        )
        {

            try
            {
                RGDebug.LogInfo($"Creating and uploading GameplaySession on the backend, from {startTime} to {endTime} with {tickCount} ticks");

                // First, create the gameplay session
                long gameplaySessionId = -1;
                await RGServiceManager.GetInstance().CreateGameplaySession(startTime, endTime, tickCount, loggedWarnings, loggedErrors,
                    (response) =>
                    {
                        gameplaySessionId = response.id;
                        RGDebug.LogInfo($"Created gameplay session with id: {response.id}");
                    },
                    () => { });

                // If the gameplay session was not created, return
                if (gameplaySessionId == -1)
                {
                    RGDebug.LogWarning("Unable to upload gameplay session data.  Check your authorization credentials and network connection");
                    return;
                }

                // Upload the gameplay bot_segments
                await RGServiceManager.GetInstance().UploadGameplaySessionBotSegments(gameplaySessionId,
                    botSegmentsDirectoryPrefix + ".zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session bot_segments from {botSegmentsDirectoryPrefix}.zip"); },
                    () => { });

                // Upload the gameplay state data
                await RGServiceManager.GetInstance().UploadGameplaySessionData(gameplaySessionId,
                    dataDirectoryPrefix + ".zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session data from {dataDirectoryPrefix}.zip"); },
                    () => { });

                // Upload the gameplay session screenshots
                await RGServiceManager.GetInstance().UploadGameplaySessionScreenshots(gameplaySessionId,
                    screenshotsDirectoryPrefix + ".zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session screenshots from {screenshotsDirectoryPrefix}.zip"); },
                    () => { });

                // Upload the thumbnail
                await RGServiceManager.GetInstance().UploadGameplaySessionThumbnail(gameplaySessionId,
                    thumbnailPath,
                    () => { RGDebug.LogInfo($"Uploaded gameplay session thumbnail from {thumbnailPath}"); },
                    () => { });

                // Upload the metadata
                await RGServiceManager.GetInstance().UploadGameplaySessionMetadata(gameplaySessionId,
                    gameMetadataPath,
                    () => { RGDebug.LogInfo($"Uploaded gameplay session metadata from {gameMetadataPath}"); },
                    () => { });

                // Upload the logs
                await RGServiceManager.GetInstance().UploadGameplaySessionLogs(gameplaySessionId,
                    logsPathPrefix + ".zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session logs from {logsPathPrefix}.zip"); },
                    () => { });
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, "Exception uploading gameplay session recording data");
            }
            finally
            {
                if (!onDestroy)
                {
                    // only do this when the game object is still alive :D
                    StartCoroutine(ShowUploadingIndicator(false));
                }
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (_needToRefreshAssets)
            {
                _needToRefreshAssets = false;
                AssetDatabase.Refresh();
            }
#endif
        }

        private void LateUpdate()
        {
            _fileWriteTasks.RemoveAll(a => a.Item2 == null || a.Item2.IsCompleted);

            while (_texture2Ds.TryDequeue(out var tex))
            {
                // have to destroy the textures on the main thread
                Destroy(tex);
            }

            if (IsRecording)
            {
                StartCoroutine(RecordFrame());
            }
        }

        private IEnumerator ShowUploadingIndicator(bool shouldShow)
        {
            yield return null;
            FindObjectOfType<ReplayToolbarManager>()?.ShowUploadingIndicator(shouldShow);
        }

        private IEnumerator StartRecordingCoroutine(string referenceSessionId)
        {
            // Do this 1 frame after the request so that the click action of starting the recording itself isn't captured
            yield return null;
            if (!IsRecording)
            {
                _lastCvFrameTime = -1;
                KeyboardInputActionObserver.GetInstance()?.StartRecording();
                _profilerObserver.StartProfiling();
                _loggingObserver.StartCapturingLogs();
                _mouseObserver.ClearBuffer();
                var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
                foreach (var objectFinder in objectFinders)
                {
                    objectFinder.Cleanup();
                }
                IsRecording = true;
                _tickNumber = 0;
                _currentSessionId = Guid.NewGuid().ToString("n");
                _referenceSessionId = referenceSessionId;
                _startTime = DateTime.Now;
                _tickQueue = new BlockingCollection<(TickDataToWriteToDisk, Action)>(new ConcurrentQueue<(TickDataToWriteToDisk, Action)>());
                _tokenSource = new CancellationTokenSource();

                Directory.CreateDirectory(stateRecordingsDirectory);

                _isReplay = referenceSessionId != null;

                var prefix = _isReplay ? "replay" : "recording";
                var pf = _currentSessionId.Substring(Math.Max(0, _currentSessionId.Length - 6));
                var postfix = referenceSessionId != null ? pf + "_" + referenceSessionId.Substring(Math.Max(0, referenceSessionId.Length - 6)) : pf;
                var dateTimeString =  DateTime.Now.ToString("MM-dd-yyyy_HH.mm");

                // find the first index number we haven't used yet
                do
                {
                    _currentGameplaySessionDirectoryPrefix =
                        $"{stateRecordingsDirectory}/{Application.productName}/{prefix}_{dateTimeString}_{postfix}";
                } while (Directory.Exists(_currentGameplaySessionDirectoryPrefix));

                _currentGameplaySessionDataDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/data";
                _currentGameplaySessionScreenshotsDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/screenshots";
                _currentGameplaySessionBotSegmentsDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/bot_segments";
                _currentGameplaySessionCodeCoverageMetadataPath = _currentGameplaySessionDirectoryPrefix + "/code_coverage_metadata.json";
                _currentGameplaySessionActionCoverageMetadataPath = _currentGameplaySessionDirectoryPrefix + "/action_coverage_metadata.json";
                _currentGameplaySessionGameMetadataPath = _currentGameplaySessionDirectoryPrefix + "/game_metadata.json";
                _currentGameplaySessionThumbnailPath = _currentGameplaySessionDirectoryPrefix + "/thumbnail.jpg";
                _currentGameplaySessionLogsDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/logs";
                Directory.CreateDirectory(_currentGameplaySessionDataDirectoryPrefix);
                Directory.CreateDirectory(_currentGameplaySessionBotSegmentsDirectoryPrefix);
                Directory.CreateDirectory(_currentGameplaySessionScreenshotsDirectoryPrefix);
                Directory.CreateDirectory(_currentGameplaySessionLogsDirectoryPrefix);

                // run the tick processor in the background, but don't hook it to the token source.. we'll manage cancelling this on our own so we don't miss processing ticks
                Task.Run(ProcessTicks);
                RGDebug.LogInfo($"Recording replay screenshots and data to directories inside {_currentGameplaySessionDirectoryPrefix}");
            }
        }

        public void StartRecording(string referenceSessionId)
        {
            var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
            if (gameFacePixelHashObserver != null)
            {
                gameFacePixelHashObserver.SetActive(true);
            }

            // start/stop recording for this is called withing the monkey bot action itself; we just need to tell it to clear data and be ready for the next one
            RGActionRuntimeCoverageAnalysis.Reset();

            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            if (rgSettings.GetFeatureCodeCoverage())
            {
                RGCodeCoverage.StartRecording();
            }

            StartCoroutine(StartRecordingCoroutine(referenceSessionId));

        }

        // cache this to avoid re-alloc on every frame
        private readonly List<KeyFrameType> _keyFrameTypeList = new(10);

        private void GetKeyFrameType(bool firstFrame, bool hasDeltas, bool pixelHashChanged, bool endRecording)
        {
            _keyFrameTypeList.Clear();
            if (endRecording)
            {
                _keyFrameTypeList.Add(KeyFrameType.END_RECORDING );
                return;
            }

            if (firstFrame)
            {
                _keyFrameTypeList.Add(KeyFrameType.FIRST_FRAME );
                return;
            }

            if (pixelHashChanged)
            {
                _keyFrameTypeList.Add(KeyFrameType.UI_PIXELHASH);
            }

            if (hasDeltas)
            {
                _keyFrameTypeList.Add(KeyFrameType.GAME_ELEMENT);
            }
        }

        private void StopRecordingCleanupHelper(bool wasRecording, bool wasReplay)
        {
            long loggedWarnings = 0;
            long loggedErrors = 0;
            if (_loggingObserver != null)
            {
                loggedWarnings = _loggingObserver.LoggedWarnings;
                loggedErrors = _loggingObserver.LoggedErrors;
            }

            if (wasRecording)
            {
                var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                if (gameFacePixelHashObserver != null)
                {
                    gameFacePixelHashObserver.SetActive(false);
                }
                KeyboardInputActionObserver.GetInstance()?.StopRecording();
                _mouseObserver.ClearBuffer();
                _profilerObserver.StopProfiling();
                if (_loggingObserver != null)
                {
                    _loggingObserver.StopCapturingLogs();
                }

                ScreenshotCapture.GetInstance()?.WaitForCompletion();

                _tickQueue?.CompleteAdding();

                // wait for all the tick writes to be queued up.. this is an explicit check for null to avoid race condition between the last take marking completed and the file write tasks being created
                while (_tickQueue != null)
                {
                    // something short, but not so short that the cpu spikes
                    Thread.Sleep(5);
                }
            }

            _fileWriteTasks.RemoveAll(a => a.Item2.IsCompleted);

            // wait for all the write tasks to finish
            if (_fileWriteTasks.Count > 0)
            {
                RGDebug.LogInfo($"Waiting for unfinished file write tasks before stopping ["+ string.Join(",", _fileWriteTasks.Select(a=>a.Item1).ToArray())+ "]");
            }
            Task.WaitAll(_fileWriteTasks.Select(a=>a.Item2).ToArray());

            _fileWriteTasks.Clear();

            if (wasRecording)
            {
                _ = HandleEndRecording(
                    _tickNumber,
                    _startTime,
                    DateTime.Now,
                    wasReplay,
                    loggedWarnings,
                    loggedErrors,
                    _currentGameplaySessionDataDirectoryPrefix,
                    _currentGameplaySessionBotSegmentsDirectoryPrefix,
                    _currentGameplaySessionScreenshotsDirectoryPrefix,
                    _currentGameplaySessionCodeCoverageMetadataPath,
                    _currentGameplaySessionActionCoverageMetadataPath,
                    _currentGameplaySessionThumbnailPath,
                    _currentGameplaySessionLogsDirectoryPrefix,
                    _currentGameplaySessionGameMetadataPath,
                    true);
            }
            else
            {
                _tokenSource?.Cancel();
                _tokenSource?.Dispose();
                _tokenSource = null;
            }

            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
            if (rgSettings.GetFeatureCodeCoverage())
            {
                RGCodeCoverage.StopRecording();
            }
        }

        /**
         * Done as a coroutine so we can wait for record frame to finish one more time
         */
        private IEnumerator StopRecordingCoroutine(bool toolbarButtonTriggered)
        {
            var wasRecording = IsRecording;
            IsRecording = false;
            var wasReplay = _isReplay;
            _isReplay = false;


            if (wasRecording)
            {
                yield return RecordFrame(endRecording: true, endRecordingFromToolbarButton: toolbarButtonTriggered);
            }

            StopRecordingCleanupHelper(wasRecording, wasReplay);
        }

        /**
         * used on application quit/destroy... can't record the remaining data, but ends cleanly
         */
        private void StopRecordingNoCoroutine()
        {
            var wasRecording = IsRecording;
            IsRecording = false;
            var wasReplay = _isReplay;
            _isReplay = false;
            StopRecordingCleanupHelper(wasRecording, wasReplay);

        }

        /**
         * When triggered from the toolbar button, we need to exclude the last mouse click events so we don't capture the click on our own interface buttons
         */
        public void StopRecording(bool toolbarButtonTriggered = false)
        {
            StartCoroutine(StopRecordingCoroutine(toolbarButtonTriggered));
        }

        private IEnumerator RecordFrame(bool endRecording = false, bool endRecordingFromToolbarButton = false)
        {
            if (_tickQueue is { IsAddingCompleted: false })
            {
                // wait for end of frame before capturing, otherwise .isVisible on game objects is wrong and GPU data won't be accurate for screenshot
                // also impacts ordering for clearing the pixel hash
                yield return new WaitForEndOfFrame();

                ++_frameCountSinceLastTick;

                var remoteWorkerBehaviour = FindObjectOfType<RGRemoteWorkerBehaviour>();
                string logs = "";

                // when running remote work assignments, we only capture logs
                if (remoteWorkerBehaviour == null || !remoteWorkerBehaviour.HasActiveWorkAssignment)
                {
                    // handle recording ... uses unscaled time for real framerate calculations
                    var now = Time.unscaledTimeAsDouble;

                    var transformStatuses = (new Dictionary<long, ObjectStatus>(), new Dictionary<long, ObjectStatus>());
                    var entityStatuses = (new Dictionary<long, ObjectStatus>(), new Dictionary<long, ObjectStatus>());

                    var keyFrameCriteria = new List<KeyFrameCriteria>();

                    var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
                    var hasDeltas = false;
                    foreach (var objectFinder in objectFinders)
                    {
                        keyFrameCriteria.AddRange(objectFinder.GetKeyFrameCriteriaForCurrentFrame(out var hasObjectDeltas));
                        hasDeltas |= hasObjectDeltas;
                        if (objectFinder is TransformObjectFinder)
                        {
                            transformStatuses = objectFinder.GetObjectStatusForCurrentFrame();
                        }
                        else
                        {
                            entityStatuses = objectFinder.GetObjectStatusForCurrentFrame();
                        }
                    }

                    // generally speaking, you want to observe the mouse relative to the prior state as the mouse input generally causes the 'newState' and thus
                    // what it clicked on normally isn't in the new state (button was in the old state)
                    if (transformStatuses.Item1.Count > 0 || entityStatuses.Item1.Count > 0)
                    {
                        _mouseObserver.ObserveMouse(transformStatuses.Item1.Values.Concat(entityStatuses.Item1.Values));
                    }
                    else
                    {
                        _mouseObserver.ObserveMouse(transformStatuses.Item2.Values.Concat(entityStatuses.Item2.Values));
                    }

                    _profilerObserver.Observe();

                    var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                    string pixelHash = null;
                    var pixelHashChanged = gameFacePixelHashObserver != null && gameFacePixelHashObserver.HasPixelHashChanged();

                    // tell if the new frame is a key frame or the first frame (always a key frame)
                    GetKeyFrameType(_tickNumber == 0, hasDeltas, pixelHashChanged, endRecording);

                    // estimating the time in int milliseconds .. won't exactly match target FPS.. but will be close
                    if (_keyFrameTypeList.Count > 0
                        || (recordingMinFPS > 0 && (int)(1000 * (now - _lastCvFrameTime)) >= (int)(1000.0f / (recordingMinFPS)))
                       )
                    {
                        // record full frame state and screenshot
                        var screenWidth = Screen.width;
                        var screenHeight = Screen.height;

                        byte[] jsonData = null;
                        byte[] botSegmentJson = null;

                        try
                        {

                            if (_keyFrameTypeList.Count == 0)
                            {
                                _keyFrameTypeList.Add(KeyFrameType.TIMER);
                            }

                            var keyboardInputData = KeyboardInputActionObserver.GetInstance()?.FlushInputDataBuffer();
                            var mouseInputData = _mouseObserver.FlushInputDataBuffer(endRecordingFromToolbarButton, minimizeOutput: minimizeRecordingMouseMovements);

                            if (minimizeRecordingCriteria)
                            {
                                // minimize the keyFrameCriteria
                                keyFrameCriteria = keyFrameCriteria.Select(a =>
                                {
                                    if (a.type == KeyFrameCriteriaType.NormalizedPath)
                                    {
                                        var data = a.data as PathKeyFrameCriteriaData;
                                        if (data != null)
                                        {
                                            // include deltas
                                            if (data is { countRule: CountRule.GreaterThanEqual or CountRule.LessThanEqual })
                                            {
                                                a.transient = false;
                                                return a;
                                            }

                                            // also include any clicked on objects
                                            if (mouseInputData.FirstOrDefault(md => md.clickedObjectNormalizedPaths.Contains(data.path)) != null)
                                            {
                                                a.transient = false;
                                                return a;
                                            }
                                        }
                                    }
                                    return null;
                                }).Where(a=>a!=null).ToList();
                            }

                            // we often get events in the buffer with input times fractions of a ms after the current frame time for this update, but actually related to causing this update
                            // update the frame time to be latest of 'now' or the last device event in it
                            // otherwise replay gets messed up trying to read the inputs by time
                            var mostRecentKeyboardTime = keyboardInputData == null || keyboardInputData.Count == 0 ? 0.0 : keyboardInputData.Max(a => !a.endTime.HasValue ? a.startTime.Value : Math.Max(a.startTime.Value, a.endTime.Value));
                            var mostRecentMouseTime = mouseInputData == null || mouseInputData.Count == 0 ? 0.0 : mouseInputData.Max(a => a.startTime);
                            var mostRecentDeviceEventTime = Math.Max(mostRecentKeyboardTime, mostRecentMouseTime);
                            var frameTime = Math.Max(now, mostRecentDeviceEventTime);

                            // get the new state from all providers
                            var currentStates = new Dictionary<long, RecordedGameObjectState>();
                            foreach (var objectFinder in objectFinders)
                            {
                                var state = objectFinder.GetStateForCurrentFrame();
                                foreach (var recordedGameObjectState in state.Item2)
                                {
                                    currentStates[recordedGameObjectState.Key] = recordedGameObjectState.Value;
                                }
                            }

                            ++_tickNumber;

                            var inputData = new InputData()
                            {
                                keyboard = keyboardInputData,
                                mouse = mouseInputData
                            };

                            if (pixelHashChanged)
                            {
                                keyFrameCriteria.Add(new KeyFrameCriteria()
                                    {
                                        type = KeyFrameCriteriaType.UIPixelHash,
                                        transient = false,
                                        data = new UIPixelHashKeyFrameCriteriaData()
                                    }
                                );
                            }

                            // note to future devs: it may be tempting get the earliest input time for every segment so we can playback with minimal delay
                            // but.. because a mouse or keyboard button can be held down across many frames and across bot segment boundaries
                            // if you replay the release action too soon in the N+1 bot segment, then you have wrongfully altered the original action
                            // thus you can only get the earliest time on the 'first' segment
                            double inputTime = _lastCvFrameTime;

                            if (inputTime < 0)
                            {
                                // first frame, get the mouse input time for the first frame if it exists
                                if (mouseInputData.Count > 0)
                                {
                                    inputTime = mouseInputData[0].startTime;
                                }
                            }

                            if (inputTime < 0)
                            {
                                inputTime = 0;
                            }

                            //record bot segment data for action replay
                            var botSegment = new BotSegment()
                            {
                                sessionId = _currentSessionId,
                                endCriteria = keyFrameCriteria,
                                botAction = new BotAction()
                                {
                                    type = BotActionType.InputPlayback,
                                    data = new InputPlaybackActionData()
                                    {
                                        startTime = inputTime,
                                        inputData = inputData
                                    }
                                }
                            };

                            var performanceMetrics = new PerformanceMetricData()
                            {
                                framesSincePreviousTick = _frameCountSinceLastTick,
                                previousTickTime = _lastCvFrameTime > 0 ? _lastCvFrameTime : 0,
                                fps = _lastCvFrameTime > 0 ? (int)(_frameCountSinceLastTick / (now - _lastCvFrameTime)) : 0,
                                perFrameStatistics = _profilerObserver.DequeueAll()
                            };

                            _lastCvFrameTime = now;
                            _frameCountSinceLastTick = 0;

                            RecordingCodeCoverageState codeCoverageState = null;

                            RGSettings rgSettings = RGSettings.GetOrCreateSettings();
                            bool codeCovEnabled = rgSettings.GetFeatureCodeCoverage();
                            if (codeCovEnabled)
                            {
                                var codeCovMetadata = RGCodeCoverage.GetMetadata();
                                if (codeCovMetadata != null)
                                {
                                    codeCoverageState = new RecordingCodeCoverageState()
                                    {
                                        coverageSinceLastTick = RGCodeCoverage.CopyCodeCoverageState()
                                    };
                                }
                            }

                            GameObject esGameObject = (EventSystem.current != null ? EventSystem.current.gameObject : null);
                            List<string> eventSystemInputModules = new();
                            if (esGameObject != null)
                            {
                                eventSystemInputModules = esGameObject.gameObject.GetComponents<BaseInputModule>().Where(a => a.isActiveAndEnabled).Select(a => a.GetType().FullName).ToList();
                            }

                            var cameraInfo = new List<CameraInfo>();

                            // Future/Maybe: We may want to capture all cameras, but for now, we're getting main for user visuals
                            var mainCamera = Camera.main;
                            if (mainCamera != null)
                            {
                                cameraInfo.Add(new()
                                {
                                    name = mainCamera.name,
                                    farClipPlane = mainCamera.farClipPlane,
                                    nearClipPlane = mainCamera.nearClipPlane,
                                    fieldOfView = mainCamera.fieldOfView,
                                    orthographic = mainCamera.orthographic,
                                    orthographicSize = mainCamera.orthographicSize,
                                    position = mainCamera.transform.position
                                });
                            }

                            var frameState = new RecordingFrameStateData()
                            {
                                sessionId = _currentSessionId,
                                referenceSessionId = _referenceSessionId,
                                keyFrame = _keyFrameTypeList.ToArray(),
                                tickNumber = _tickNumber,
                                time = frameTime,
                                timeScale = Time.timeScale,
                                screenSize = new Vector2Int() { x = screenWidth, y = screenHeight },
                                cameraInfo = cameraInfo,
                                performance = performanceMetrics,
                                pixelHash = pixelHash,
                                // In Unity, if the GraphicsSettings.currentRenderPipeline property is null, the default render pipeline is the Built-in Render Pipeline.
                                currentRenderPipeline = GraphicsSettings.currentRenderPipeline == null ? null : GraphicsSettings.currentRenderPipeline.GetType().FullName,
                                activeEventSystemInputModules = eventSystemInputModules,
                                activeInputDevices = InputSystem.devices.Select(a => $"{a.name} - {a.path}").ToList(),
                                state = currentStates.Values,
                                codeCoverage = codeCoverageState,
                                inputs = inputData
                            };

                            if (codeCovEnabled)
                            {
                                RGCodeCoverage.Clear();
                            }

                            if (frameState.keyFrame != null)
                            {
                                if (RGDebug.IsDebugEnabled)
                                {
                                    RGDebug.LogDebug("Tick " + _tickNumber + " had " + keyboardInputData?.Count + " keyboard inputs , " + mouseInputData?.Count + " mouse inputs - KeyFrame: [" + string.Join(',', frameState.keyFrame) + "]");
                                }
                            }

                            // serialize to json byte[] - Maybe find a way to re-use the byte[] buffers here to avoid so much GC and alloc overhead on each write ?
                            jsonData = Encoding.UTF8.GetBytes(
                                frameState.ToJsonString()
                            );

                            botSegmentJson = Encoding.UTF8.GetBytes(
                                botSegment.ToJsonString()
                            );

                            logs = _loggingObserver.DequeueLogs();

                            inputData.MarkSent();
                        }
                        catch (Exception e)
                        {
                            RGDebug.LogException(e, $"Exception capturing state for tick # {_tickNumber}");
                        }


                        var didQueue = 0;

                        if (jsonData != null && botSegmentJson != null)
                        {
                            // save this off because we're about to operate on a different thread :)
                            var currentTickNumber = _tickNumber;

                            if (RGDebug.IsDebugEnabled)
                            {
                                RGDebug.LogDebug($"Capturing screenshot for tick # {currentTickNumber}");
                            }

                            ScreenshotCapture.GetInstance().GetCurrentScreenshotWithCallback(
                                currentTickNumber,
                                (result) =>
                                {
                                    if (result.HasValue)
                                    {
                                        if (Interlocked.CompareExchange(ref didQueue, 1, 0) == 0)
                                        {
                                            // queue up writing the tick data to disk async
                                            _tickQueue.Add((
                                                    new TickDataToWriteToDisk(
                                                        directoryPrefix: _currentGameplaySessionDirectoryPrefix,
                                                        tickNumber: currentTickNumber,
                                                        botSegmentJson: botSegmentJson,
                                                        jsonData: jsonData,
                                                        screenshotData: result.Value.Item1,
                                                        logs: logs
                                                    ),
                                                    () => { }
                                                )
                                            );
                                            if (RGDebug.IsDebugEnabled)
                                            {
                                                RGDebug.LogDebug($"Queued data to write for tick # {currentTickNumber}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        RGDebug.LogWarning($"Error capturing screenshot for tick # {currentTickNumber}");

                                        if (Interlocked.CompareExchange(ref didQueue, 1, 0) == 0)
                                        {
                                            // queue up writing the tick data to disk async
                                            _tickQueue.Add((
                                                    new TickDataToWriteToDisk(
                                                        directoryPrefix: _currentGameplaySessionDirectoryPrefix,
                                                        tickNumber: currentTickNumber,
                                                        botSegmentJson: botSegmentJson,
                                                        jsonData: jsonData,
                                                        screenshotData: null,
                                                        logs: logs
                                                    ),
                                                    () => { }
                                                )
                                            );
                                            if (RGDebug.IsDebugEnabled)
                                            {
                                                RGDebug.LogDebug($"Queued data to write without screenshot for tick # {currentTickNumber}");
                                            }
                                        }
                                    }
                                }
                            );
                        }
                    }
                }
                else
                {
                    // running a remote work assignment, only capture logs every 1000 lines
                    // also flush on the 'last' tick on stop recording
                    if (endRecording || _loggingObserver.QueueLength() > 1000)
                    {
                        logs = _loggingObserver.DequeueLogs();
                        RecordLogs(_currentGameplaySessionDirectoryPrefix, ++_tickNumber, logs);
                    }
                }
            }
        }

        private void ProcessTicks()
        {
            while (_tickQueue is {IsCompleted: false})
            {
                try
                {
                    bool canWrite;
                    (TickDataToWriteToDisk, Action) tuple;
                    // we may or may not have data.. make this a cancellable take
                    if (_tokenSource is { IsCancellationRequested: false })
                    {
                        tuple = _tickQueue.Take(_tokenSource.Token);
                        canWrite = true;
                    }
                    else
                    {
                        // _tokenSource was cancelled.. do this without blocking
                        canWrite = _tickQueue.TryTake(out tuple);
                    }

                    if (canWrite)
                    {
                        var tickData = tuple.Item1;
                        ProcessTick(tickData);

                        // invoke the cleanup callback function
                        tuple.Item2();
                    }
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException && e is not InvalidOperationException)
                    {
                        RGDebug.LogException(e, "Error Processing Ticks");
                    }
                }
            }
            // once we've got everything queued up.. nullify this so the end recording loop knows to continue
            _tickQueue?.Dispose();
            _tickQueue = null;
        }

        private void ProcessTick(TickDataToWriteToDisk tickData)
        {
            var directoryPrefix = tickData.directoryPrefix;
            var tickNumber = tickData.tickNumber;
            RecordBotSegment(directoryPrefix, tickNumber, tickData.botSegmentJson);
            RecordJson(directoryPrefix, tickNumber, tickData.jsonData);
            RecordJPG(directoryPrefix, tickNumber, tickData.screenshotData);
            RecordLogs(directoryPrefix, tickNumber, tickData.logs);
        }

        private void RecordJPG(string directoryPath, long tickNumber, byte[] tickScreenshotData)
        {
            if (tickScreenshotData != null)
            {
                try
                {
                    // write out the image to file
                    var path = $"{directoryPath}/screenshots/{tickNumber}".PadLeft(9, '0') + ".jpg";
                    // Save the byte array as a file
                    var fileWriteTask = File.WriteAllBytesAsync(path, tickScreenshotData.ToArray(), _tokenSource.Token);
                    fileWriteTask.ContinueWith((nextTask) =>
                    {
                        if (nextTask.Exception != null)
                        {
                            RGDebug.LogException(nextTask.Exception, $"ERROR: Unable to record JPG for tick # {tickNumber}");
                        }

                    });
                    _fileWriteTasks.Add((path, fileWriteTask));
                }
                catch (Exception e)
                {
                    RGDebug.LogException(e, $"WARNING: Unable to record JPG for tick # {tickNumber}");
                }
            }
        }

        private void RecordJson(string directoryPath, long tickNumber, byte[] jsonData)
        {
            try
            {
                // write out the json to file
                var path = $"{directoryPath}/data/{tickNumber}".PadLeft(9, '0') + ".json";
                if (jsonData.Length == 0)
                {
                    RGDebug.LogError($"ERROR: Empty JSON data for tick # {tickNumber}");
                }

                // Save the byte array as a file
                var fileWriteTask = File.WriteAllBytesAsync(path, jsonData, _tokenSource.Token);
                fileWriteTask.ContinueWith((nextTask) =>
                {
                    if (nextTask.Exception != null)
                    {
                        RGDebug.LogException(nextTask.Exception, $"ERROR: Unable to record JSON for tick # {tickNumber}");
                    }

                });
                _fileWriteTasks.Add((path,fileWriteTask));
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"ERROR: Unable to record JSON for tick # {tickNumber}");
            }
        }

        private void RecordBotSegment(string directoryPath, long tickNumber, byte[] jsonData)
        {
            try
            {
                // write out the json to file... while these numbers happen to align with the state data at recording time.. they don't mean the same thing
                var path = $"{directoryPath}/bot_segments/{tickNumber}".PadLeft(9, '0') + ".json";
                if (jsonData.Length == 0)
                {
                    RGDebug.LogError($"ERROR: Empty JSON bot_segment for tick # {tickNumber}");
                }

                // Save the byte array as a file
                var fileWriteTask = File.WriteAllBytesAsync(path, jsonData, _tokenSource.Token);
                fileWriteTask.ContinueWith((nextTask) =>
                {
                    if (nextTask.Exception != null)
                    {
                        RGDebug.LogException(nextTask.Exception, $"ERROR: Unable to record JSON bot_segment for tick # {tickNumber}");
                    }

                });
                _fileWriteTasks.Add((path,fileWriteTask));
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"ERROR: Unable to record JSON bot_segment for tick # {tickNumber}");
            }
        }

        private void RecordLogs(string directoryPath, long tickNumber, string logs)
        {
            try
            {
                // append logs to jsonl file
                var path = $"{directoryPath}/logs/{tickNumber}".PadLeft(9, '0') + ".jsonl";
                var fileWriteTask = File.WriteAllTextAsync(path, logs, _tokenSource.Token);
                fileWriteTask.ContinueWith((nextTask) =>
                {
                    if (nextTask.Exception != null)
                    {
                        RGDebug.LogException(nextTask.Exception,$"ERROR: Unable to record logs for tick # {tickNumber}");
                    }
                });
                _fileWriteTasks.Add((path, fileWriteTask));
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"ERROR: Unable to record logs for tick # {tickNumber}");
            }
        }
    }
}
