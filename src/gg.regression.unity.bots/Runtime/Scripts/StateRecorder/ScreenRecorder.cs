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
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif


// ReSharper disable once ForCanBeConvertedToForeach - Better performance using indexing vs enumerators
// ReSharper disable once LoopCanBeConvertedToQuery - Better performance using indexing vs enumerators
namespace RegressionGames.StateRecorder
{
    public class ScreenRecorder : MonoBehaviour
    {
        [Tooltip("Minimum FPS at which to capture frames if you desire more granularity in recordings.  Key frames may still be recorded more frequently than this. <= 0 will only record key frames")]
        public int recordingMinFPS;

        [Tooltip("Directory to save state recordings in.  This directory will be created if it does not exist.  If not specific, this will default to 'unity_videos' in your user profile path for your operating system.")]
        public string stateRecordingsDirectory = "";

        private double _lastCvFrameTime = -1;

        private int _frameCountSinceLastTick;

        private string _currentSessionId;
        private string _referenceSessionId;

        private string _currentGameplaySessionDirectoryPrefix;
        private string _currentGameplaySessionScreenshotsDirectoryPrefix;
        private string _currentGameplaySessionBotSegmentsDirectoryPrefix;
        private string _currentGameplaySessionDataDirectoryPrefix;
        private string _currentGameplaySessionThumbnailPath;

        private CancellationTokenSource _tokenSource;

        private static ScreenRecorder _this;

        private bool _isRecording;

        private bool _usingIOSMetalGraphics = false;

        private readonly ConcurrentQueue<Texture2D> _texture2Ds = new();

        private long _tickNumber;
        private DateTime _startTime;

        private BlockingCollection<((string, long), (byte[], byte[], int, int, byte[], Action))>
            _tickQueue;

        private readonly List<(string, Task)> _fileWriteTasks = new();

        private MouseInputActionObserver _mouseObserver;
        private ProfilerObserver _profilerObserver;

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
            _usingIOSMetalGraphics = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal);
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
        }

        private void OnDestroy()
        {
            StopRecording();
        }

        private void Start()
        {
            var read_formats = System.Enum.GetValues( typeof( GraphicsFormat ) ).Cast<GraphicsFormat>()
                .Where( f => SystemInfo.IsFormatSupported( f, FormatUsage.ReadPixels ) )
                .ToArray();
            RGDebug.LogInfo( "Supported Formats for Readback\n" + string.Join( "\n", read_formats ) );
        }

        private async Task HandleEndRecording(long tickCount, DateTime startTime, DateTime endTime, string dataDirectoryPrefix, string botSegmentsDirectoryPrefix, string screenshotsDirectoryPrefix, string thumbnailPath, bool onDestroy = false)
        {
            if (!onDestroy)
            {
                StartCoroutine(ShowUploadingIndicator(true));
            }

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

            // Finally, we also save a thumbnail, by choosing the middle file in the screenshots
            var screenshotFiles = Directory.GetFiles(screenshotsDirectoryPrefix);
            var middleFile = screenshotFiles[screenshotFiles.Length / 2]; // this gets floored automatically
            File.Copy(middleFile, thumbnailPath);

            // wait for the zip tasks to finish
            Task.WaitAll(zipTask1, zipTask2);

            Directory.Delete(dataDirectoryPrefix, true);
            Directory.Delete(botSegmentsDirectoryPrefix, true);
            Directory.Delete(screenshotsDirectoryPrefix, true);

            await CreateAndUploadGameplaySession(tickCount, startTime, endTime, dataDirectoryPrefix, botSegmentsDirectoryPrefix,screenshotsDirectoryPrefix, thumbnailPath, onDestroy);
        }

        private async Task CreateAndUploadGameplaySession(long tickCount, DateTime startTime, DateTime endTime, string dataDirectoryPrefix, string botSegmentsDirectoryPrefix, string screenshotsDirectoryPrefix, string thumbnailPath, bool onDestroy = false)
        {

            try
            {
                RGDebug.LogInfo($"Creating and uploading GameplaySession on the backend, from {startTime} to {endTime} with {tickCount} ticks");

                // First, create the gameplay session
                long gameplaySessionId = -1;
                await RGServiceManager.GetInstance().CreateGameplaySession(startTime, endTime, tickCount,
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

                // Next, upload the gameplay session screenshots
                await RGServiceManager.GetInstance().UploadGameplaySessionScreenshots(gameplaySessionId,
                    screenshotsDirectoryPrefix + ".zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session screenshots from {screenshotsDirectoryPrefix}.zip"); },
                    () => { });

                // Finally, upload the thumbnail
                await RGServiceManager.GetInstance().UploadGameplaySessionThumbnail(gameplaySessionId,
                    thumbnailPath,
                    () => { RGDebug.LogInfo($"Uploaded gameplay session thumbnail from {thumbnailPath}"); },
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

        private void LateUpdate()
        {
            _fileWriteTasks.RemoveAll(a => a.Item2 == null || a.Item2.IsCompleted);

            while (_texture2Ds.TryDequeue(out var tex))
            {
                // have to destroy the textures on the main thread
                Destroy(tex);
            }

            if (_isRecording)
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
            if (!_isRecording)
            {
                _lastCvFrameTime = -1;
                KeyboardInputActionObserver.GetInstance()?.StartRecording();
                _profilerObserver.StartProfiling();
                _mouseObserver.ClearBuffer();
                var objectFinders = FindObjectsByType<ObjectFinder>(FindObjectsSortMode.None);
                foreach (var objectFinder in objectFinders)
                {
                    objectFinder.Cleanup();
                }
                _isRecording = true;
                _tickNumber = 0;
                _currentSessionId = Guid.NewGuid().ToString("n");
                _referenceSessionId = referenceSessionId;
                _startTime = DateTime.Now;
                _tickQueue =
                    new BlockingCollection<((string, long), (byte[], byte[], int, int, byte[], Action))>(
                        new ConcurrentQueue<((string, long), (byte[], byte[], int, int, byte[],
                            Action)
                            )>());

                _tokenSource = new CancellationTokenSource();

                Directory.CreateDirectory(stateRecordingsDirectory);

                var prefix = referenceSessionId != null ? "replay" : "recording";
                var pf = _currentSessionId.Substring(Math.Max(0, _currentSessionId.Length - 6));
                var postfix = referenceSessionId != null ? pf + "_" + referenceSessionId.Substring(Math.Max(0, referenceSessionId.Length - 6)) : pf;
                var dateTimeString =  System.DateTime.Now.ToString("MM-dd-yyyy_HH.mm");

                // find the first index number we haven't used yet
                do
                {
                    _currentGameplaySessionDirectoryPrefix =
                        $"{stateRecordingsDirectory}/{Application.productName}/{prefix}_{dateTimeString}_{postfix}";
                } while (Directory.Exists(_currentGameplaySessionDirectoryPrefix));

                _currentGameplaySessionDataDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/data";
                _currentGameplaySessionScreenshotsDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/screenshots";
                _currentGameplaySessionBotSegmentsDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/bot_segments";
                _currentGameplaySessionThumbnailPath = _currentGameplaySessionDirectoryPrefix + "/thumbnail.jpg";
                Directory.CreateDirectory(_currentGameplaySessionDataDirectoryPrefix);
                Directory.CreateDirectory(_currentGameplaySessionBotSegmentsDirectoryPrefix);
                Directory.CreateDirectory(_currentGameplaySessionScreenshotsDirectoryPrefix);

                // run the tick processor in the background
                Task.Run(ProcessTicks, _tokenSource.Token);
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
            StartCoroutine(StartRecordingCoroutine(referenceSessionId));

        }

        // cache this to avoid re-alloc on every frame
        private readonly List<KeyFrameType> _keyFrameTypeList = new(10);

        private void GetKeyFrameType(bool firstFrame, bool hasDeltas, bool pixelHashChanged)
        {
            _keyFrameTypeList.Clear();
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

        public void StopRecording()
        {
            var wasRecording = _isRecording;
            _isRecording = false;
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
            }

            ScreenshotCapture.WaitForCompletion();

            _tickQueue?.CompleteAdding();

            // wait for all the tick writes to be queued up
            while (_tickQueue != null && _tickQueue.Count() != 0)
            {
                Thread.Sleep(5);
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
                _ = HandleEndRecording(_tickNumber, _startTime, DateTime.Now, _currentGameplaySessionDataDirectoryPrefix, _currentGameplaySessionBotSegmentsDirectoryPrefix,  _currentGameplaySessionScreenshotsDirectoryPrefix, _currentGameplaySessionThumbnailPath, true);
            }

            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = null;
        }

        private IEnumerator RecordFrame()
        {
            if (!_tickQueue.IsCompleted)
            {
                // wait for end of frame before capturing, otherwise .isVisible is wrong and GPU data won't be accurate for screenshot
                // also impacts ordering for clearing the pixel hash
                yield return new WaitForEndOfFrame();

                ++_frameCountSinceLastTick;
                // handle recording ... uses unscaled time for real framerate calculations
                var now = Time.unscaledTimeAsDouble;

                var transformStatuses = (new Dictionary<long, ObjectStatus>(),new Dictionary<long, ObjectStatus>());
                var entityStatuses = (new Dictionary<long, ObjectStatus>(),new Dictionary<long, ObjectStatus>());

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
                GetKeyFrameType(_tickNumber ==0, hasDeltas, pixelHashChanged);

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
                        var mouseInputData = _mouseObserver.FlushInputDataBuffer();

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

                        double inputTime = -1;
                        // note to future devs: it may be tempting get the earliest input time for every segment so we can playback with minimal delay
                        // but.. because a mouse or keyboard button can be held down across many frames and across bot segment boundaries
                        // if you replay the release action too soon in the N+1 bot segment, then you have wrongfully altered the original action
                        // thus you can only get the earliest time on the 'first' segment
                        inputTime = _lastCvFrameTime;

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
                            keyFrameCriteria = keyFrameCriteria,
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

                        var frameState = new RecordingFrameStateData()
                        {
                            sessionId = _currentSessionId,
                            referenceSessionId = _referenceSessionId,
                            keyFrame = _keyFrameTypeList.ToArray(),
                            tickNumber = _tickNumber,
                            time = frameTime,
                            timeScale = Time.timeScale,
                            screenSize = new Vector2Int() { x = screenWidth, y = screenHeight },
                            performance = performanceMetrics,
                            pixelHash = pixelHash,
                            state = currentStates.Values,
                            inputs = inputData
                        };

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

                        ScreenshotCapture.GetCurrentScreenshotWithCallback(
                            currentTickNumber,
                            (result) =>
                            {
                                if (result.HasValue)
                                {
                                    if (Interlocked.CompareExchange(ref didQueue, 1, 0) == 0)
                                    {
                                        // queue up writing the tick data to disk async
                                        _tickQueue.Add((
                                            (_currentGameplaySessionDirectoryPrefix, currentTickNumber),
                                            (
                                                botSegmentJson,
                                                jsonData,
                                                result.Value.Item2,
                                                result.Value.Item3,
                                                result.Value.Item1,
                                                () => { }
                                            )
                                        ));
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
                                            (_currentGameplaySessionDirectoryPrefix, currentTickNumber),
                                            (
                                                botSegmentJson,
                                                jsonData,
                                                screenWidth,
                                                screenHeight,
                                                null,
                                                () => { }
                                            )
                                        ));
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
        }

        private void ProcessTicks()
        {
            while (!_tickQueue.IsCompleted && _tokenSource is { IsCancellationRequested: false })
            {
                try
                {
                    var tuple = _tickQueue.Take(_tokenSource.Token);
                    ProcessTick(tuple.Item1.Item1, tuple.Item1.Item2, tuple.Item2.Item1, tuple.Item2.Item2,
                        tuple.Item2.Item3, tuple.Item2.Item4, tuple.Item2.Item5);
                    // invoke the cleanup callback function
                    tuple.Item2.Item6();
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException && e is not InvalidOperationException)
                    {
                        RGDebug.LogException(e, "Error Processing Ticks");
                    }
                }
            }
        }

        private void ProcessTick(string directoryPath, long tickNumber, byte[] botSegmentJson, byte[] jsonData, int tickScreenshotWidth, int tickScreenshotHeight, byte[] tickScreenshotData)
        {
            RecordBotSegment(directoryPath, tickNumber, botSegmentJson);
            RecordJson(directoryPath, tickNumber, jsonData);
            RecordJPG(directoryPath, tickNumber, tickScreenshotWidth, tickScreenshotHeight, tickScreenshotData);
        }

        private void RecordJPG(string directoryPath, long tickNumber, int tickScreenshotWidth, int tickScreenshotHeight, byte[] tickScreenshotData)
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
    }
}
