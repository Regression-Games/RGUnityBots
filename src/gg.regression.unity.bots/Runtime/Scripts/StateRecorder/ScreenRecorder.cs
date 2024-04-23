using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Media;
#endif


// ReSharper disable once ForCanBeConvertedToForeach - Better performance using indexing vs enumerators
// ReSharper disable once LoopCanBeConvertedToQuery - Better performance using indexing vs enumerators
namespace RegressionGames.StateRecorder
{
    public class ScreenRecorder : MonoBehaviour
    {
        [Tooltip("Minimum FPS at which to capture frames if you desire more granularity in recordings.  Key frames may still be recorded more frequently than this. <= 0 will only record key frames")]
        public int recordingMinFPS = 0;

        [Tooltip("Directory to save state recordings in.  This directory will be created if it does not exist.  If not specific, this will default to 'unity_videos' in your user profile path for your operating system.")]
        public string stateRecordingsDirectory = "";

        private double _lastCvFrameTime = -1.0;

        private int _frameCountSinceLastTick;

        private string _currentGameplaySessionDirectoryPrefix;
        private string _currentGameplaySessionScreenshotsDirectoryPrefix;
        private string _currentGameplaySessionDataDirectoryPrefix;
        private string _currentGameplaySessionThumbnailPath;

        private CancellationTokenSource _tokenSource;

        private static ScreenRecorder _this;

        private bool _isRecording;

        private readonly ConcurrentQueue<Texture2D> _texture2Ds = new();

        private long _videoNumber;
        private long _tickNumber;
        private DateTime _startTime;

        private BlockingCollection<((string, long), (byte[], int, int, GraphicsFormat, byte[], Action))>
            _frameQueue;

        private readonly List<(string, Task)> _fileWriteTasks = new();

#if UNITY_EDITOR
        private MediaEncoder _encoder;
#endif

        private MouseInputActionObserver _mouseObserver;


        public static ScreenRecorder GetInstance()
        {
            return _this;
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
        }

        private void OnDestroy()
        {
            _frameQueue?.CompleteAdding();
            if (_fileWriteTasks.Count > 0)
            {
                RGDebug.LogInfo($"Waiting for unfinished file write tasks before stopping ["+ string.Join(",", _fileWriteTasks.Select(a=>a.Item1).ToArray())+ "]");
            }
            Task.WaitAll(_fileWriteTasks.Select(a=>a.Item2).ToArray());
            _fileWriteTasks.Clear();
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = null;
#if UNITY_EDITOR
            _encoder?.Dispose();
            _encoder = null;
#endif
            if (_isRecording)
            {
                var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                if (gameFacePixelHashObserver != null)
                {
                    gameFacePixelHashObserver.SetActive(false);
                }
                KeyboardInputActionObserver.GetInstance()?.StopRecording();
                _mouseObserver.ClearBuffer();
                _isRecording = false;
                _ = HandleEndRecording(_tickNumber, _startTime, DateTime.Now, _currentGameplaySessionDataDirectoryPrefix, _currentGameplaySessionScreenshotsDirectoryPrefix, _currentGameplaySessionThumbnailPath);
            }
        }

        private async Task HandleEndRecording(long tickCount, DateTime startTime, DateTime endTime, string dataDirectoryPrefix, string screenshotsDirectoryPrefix, string thumbnailPath)
        {
            StartCoroutine(ShowUploadingIndicator(true));

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

            // Finally, we also save a thumbnail, by choosing the middle file in the screenshots
            var screenshotFiles = Directory.GetFiles(screenshotsDirectoryPrefix);
            var middleFile = screenshotFiles[screenshotFiles.Length / 2]; // this gets floored automatically
            File.Copy(middleFile, thumbnailPath);

            // wait for the zip tasks to finish
            Task.WaitAll(zipTask1, zipTask2);

            Directory.Delete(dataDirectoryPrefix, true);
            Directory.Delete(screenshotsDirectoryPrefix, true);

            await CreateAndUploadGameplaySession(tickCount, startTime, endTime, dataDirectoryPrefix, screenshotsDirectoryPrefix, thumbnailPath);
        }

        private async Task CreateAndUploadGameplaySession(long tickCount, DateTime startTime, DateTime endTime, string dataDirectoryPrefix, string screenshotsDirectoryPrefix, string thumbnailPath)
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

                // Upload the gameplay session data
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
                StartCoroutine(ShowUploadingIndicator(false));
            }
        }

        private void LateUpdate()
        {
            _fileWriteTasks.RemoveAll(a => a.Item2.IsCompleted);

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
            RGBotManager.GetInstance().ShowUploadingIndicator(shouldShow);
        }

        private IEnumerator StartRecordingCoroutine()
        {
            // Do this 1 frame after the request so that the click action of starting the recording itself isn't captured
            yield return null;
            if (!_isRecording)
            {
                ReplayDataPlaybackController.SendMouseEvent(0, new ReplayMouseInputEntry()
                {
                    // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure, but on new recording, we want this gone
                    position = new Vector2Int(Screen.width +20, -20)
                }, null, new List<RecordedGameObjectState>());

                KeyboardInputActionObserver.GetInstance()?.StartRecording();
                _mouseObserver.ClearBuffer();
                _priorStates?.Clear();
                _newStates.Clear();
                _isRecording = true;
                _tickNumber = 0;
                _startTime = DateTime.Now;
                _frameQueue =
                    new BlockingCollection<((string, long), (byte[], int, int, GraphicsFormat, byte[], Action))>(
                        new ConcurrentQueue<((string, long), (byte[], int, int, GraphicsFormat, byte[],
                            Action)
                            )>());

                _tokenSource = new CancellationTokenSource();

                Directory.CreateDirectory(stateRecordingsDirectory);

                // find the first index number we haven't used yet
                do
                {
                    _currentGameplaySessionDirectoryPrefix =
                        $"{stateRecordingsDirectory}/{Application.productName}/run_{_videoNumber++}";
                } while (Directory.Exists(_currentGameplaySessionDirectoryPrefix));

                _currentGameplaySessionDataDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/data";
                _currentGameplaySessionScreenshotsDirectoryPrefix = _currentGameplaySessionDirectoryPrefix + "/screenshots";
                _currentGameplaySessionThumbnailPath = _currentGameplaySessionDirectoryPrefix + "/thumbnail.jpg";
                Directory.CreateDirectory(_currentGameplaySessionDataDirectoryPrefix);
                Directory.CreateDirectory(_currentGameplaySessionScreenshotsDirectoryPrefix);

                // run the frame processor in the background
                Task.Run(ProcessFrames, _tokenSource.Token);
                RGDebug.LogInfo($"Recording replay screenshots and data to directories inside {_currentGameplaySessionDirectoryPrefix}");
            }
        }

        public void StartRecording()
        {
            var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
            if (gameFacePixelHashObserver != null)
            {
                gameFacePixelHashObserver.SetActive(true);
            }
            StartCoroutine(StartRecordingCoroutine());

        }

        /**
         * <summary>Modifies the lists with removal leaving only the unique entries in each list.  Assumes lists have unique entries as though created from a hashset</summary>
         */
        private bool IntListsMatch(List<int> priorScenes, List<int> currentScenes)
        {
            if (priorScenes.Count != currentScenes.Count)
            {
                return false;
            }
            for (var i = 0; i < priorScenes.Count;)
            {
                var priorScene = priorScenes[i];
                var found = false;
                var currentScenesCount = currentScenes.Count;
                for (var j = 0; j < currentScenesCount; j++)
                {
                    var currentScene = currentScenes[j];
                    if (priorScene == currentScene)
                    {
                        priorScenes.RemoveAt(i);
                        currentScenes.RemoveAt(j);
                        found = true;
                        break; // inner for loop
                    }
                }

                if (!found)
                {
                    i++;
                }
            }

            return priorScenes.Count == 0 && currentScenes.Count == 0;
        }

        // cache this to avoid re-alloc on every frame
        private readonly List<KeyFrameType> _keyFrameTypeList = new(10);
        private readonly List<int> _sceneHandlesInPriorFrame = new(10);
        private readonly List<int> _sceneHandlesInCurrentFrame = new(10);
        private readonly List<int> _uiElementsInCurrentFrame = new(1000);
        private readonly Dictionary<int, RecordedGameObjectState> _worldElementsInCurrentFrame = new(1000);

        private void GetKeyFrameType(List<RecordedGameObjectState> priorState, List<RecordedGameObjectState> currentState, string pixelHash)
        {
            _keyFrameTypeList.Clear();
            if (priorState == null)
            {
                _keyFrameTypeList.Add(KeyFrameType.FIRST_FRAME );
                return;
            }

            if (pixelHash != null)
            {
                _keyFrameTypeList.Add(KeyFrameType.UI_PIXELHASH);
            }

            // avoid dynamic resizing of structures
            _sceneHandlesInPriorFrame.Clear();
            _sceneHandlesInCurrentFrame.Clear();
            _uiElementsInCurrentFrame.Clear();
            _worldElementsInCurrentFrame.Clear();

            // need to do current before previous due to the world element renderers comparisons

            var currentStateCount = currentState.Count;
            for (var i = 0; i < currentStateCount; i++)
            {
                var recordedGameObjectState = currentState[i];

                var sceneHandle = recordedGameObjectState.scene.handle;
                // scenes list is normally 1, and almost never beyond single digits.. this check is faster than hashing
                if (!StateRecorderUtils.OptimizedContainsIntInList(_sceneHandlesInCurrentFrame, sceneHandle))
                {
                    _sceneHandlesInCurrentFrame.Add(sceneHandle);
                }

                if (recordedGameObjectState.worldSpaceBounds == null)
                {
                    _uiElementsInCurrentFrame.Add(recordedGameObjectState.id);
                }
                else
                {
                    _worldElementsInCurrentFrame[recordedGameObjectState.id] = recordedGameObjectState;
                }
            }

            // performance optimization to avoid using hashing checks on every element
            bool addedRendererCount = false, addedGameElement = false;
            var worldElementsCount = 0;
            var hadDifferentUIElements = false;
            var priorStateCount = priorState.Count;
            for (var i = 0; i < priorStateCount; i++)
            {
                var recordedGameObjectState = priorState[i];

                var sceneHandle = recordedGameObjectState.scene.handle;
                // scenes list is normally 1, and almost never beyond single digits.. this check is faster than hashing
                if (!StateRecorderUtils.OptimizedContainsIntInList(_sceneHandlesInPriorFrame, sceneHandle))
                {
                    _sceneHandlesInPriorFrame.Add(sceneHandle);
                }

                if (recordedGameObjectState.worldSpaceBounds == null)
                {
                    if (!hadDifferentUIElements)
                    {
                        hadDifferentUIElements |= !StateRecorderUtils.OptimizedRemoveIntFromList(_uiElementsInCurrentFrame, recordedGameObjectState.id);
                    }
                }
                else
                {
                    ++worldElementsCount;
                    // once we've added both of these.. skip some checks
                    if (!addedRendererCount || !addedGameElement)
                    {
                        if (_worldElementsInCurrentFrame.TryGetValue(recordedGameObjectState.id, out var elementInCurrentFrame))
                        {
                            if (!addedRendererCount && elementInCurrentFrame.rendererCount != recordedGameObjectState.rendererCount)
                            {
                                // if an element changed its renderer count
                                _keyFrameTypeList.Add(KeyFrameType.GAME_ELEMENT_RENDERER_COUNT);
                                addedRendererCount = true;
                            }
                        }
                        else
                        {
                            if (!addedGameElement)
                            {
                                // if we had an element disappear
                                _keyFrameTypeList.Add(KeyFrameType.GAME_ELEMENT);
                                addedGameElement = true;
                            }
                        }
                    }
                }
            }

            // scene loaded or changed
            if (!IntListsMatch(_sceneHandlesInPriorFrame, _sceneHandlesInCurrentFrame))
            {
                // elements from scenes changed this frame
                _keyFrameTypeList.Add(KeyFrameType.SCENE);
            }

            // visible UI elements changed
            if (hadDifferentUIElements || _uiElementsInCurrentFrame.Count != 0)
            {
                // visible UI elements changed this frame
                _keyFrameTypeList.Add(KeyFrameType.UI_ELEMENT);
            }

            // visible game elements changed
            if (worldElementsCount != _worldElementsInCurrentFrame.Count && !addedGameElement)
            {
                // if we had a new element appear
                _keyFrameTypeList.Add(KeyFrameType.GAME_ELEMENT);
            }
        }

        public void StopRecording()
        {
            OnDestroy();
        }

        // pre-allocate a big list we can re-use
        private List<RecordedGameObjectState> _priorStates = null;
        private List<RecordedGameObjectState> _newStates = new(1000);

        private IEnumerator RecordFrame()
        {
            if (!_frameQueue.IsCompleted)
            {
                ++_frameCountSinceLastTick;
                // handle recording ... uses unscaled time for real framerate calculations
                var time = Time.unscaledTimeAsDouble;

                byte[] jsonData = null;

                _newStates.Clear();
                InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame(_priorStates, _newStates);

                // generally speaking, you want to observe the mouse relative to the prior state as the mouse input generally causes the 'newState' and thus
                // what it clicked on normally isn't in the new state (button was in the old state)
                if (_priorStates != null)
                {
                    _mouseObserver.ObserveMouse(_priorStates);
                }
                else
                {
                    _mouseObserver.ObserveMouse(_newStates);
                }

                var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                var pixelHash = gameFacePixelHashObserver != null ? gameFacePixelHashObserver.GetPixelHash(true) : null;

                // tell if the new frame is a key frame or the first frame (always a key frame)
                GetKeyFrameType(_priorStates, _newStates, pixelHash);

                // estimating the time in int milliseconds .. won't exactly match target FPS.. but will be close
                if (_keyFrameTypeList.Count > 0
                    || (recordingMinFPS > 0 && (int)(1000 * (time - _lastCvFrameTime)) >= (int)(1000.0f / (recordingMinFPS)))
                   )
                {

                    _lastCvFrameTime = time;

                    _frameCountSinceLastTick = 0;

                    var screenWidth = Screen.width;
                    var screenHeight = Screen.height;

                    try
                    {
                        ++_tickNumber;

                        var performanceMetrics = new PerformanceMetricData()
                        {
                            framesSincePreviousTick = _frameCountSinceLastTick,
                            previousTickTime = _lastCvFrameTime,
                            fps = (int)(_frameCountSinceLastTick / (time - _lastCvFrameTime)),
                            engineStats = new EngineStatsData()
                            {
#if UNITY_EDITOR
                                frameTime = UnityStats.frameTime,
                                renderTime = UnityStats.renderTime,
                                triangles = UnityStats.triangles,
                                vertices = UnityStats.vertices,
                                setPassCalls = UnityStats.setPassCalls,
                                drawCalls = UnityStats.drawCalls,
                                dynamicBatchedDrawCalls = UnityStats.dynamicBatchedDrawCalls,
                                staticBatchedDrawCalls = UnityStats.staticBatchedDrawCalls,
                                instancedBatchedDrawCalls = UnityStats.instancedBatchedDrawCalls,
                                batches = UnityStats.batches,
                                dynamicBatches = UnityStats.dynamicBatches,
                                staticBatches = UnityStats.staticBatches,
                                instancedBatches = UnityStats.instancedBatches
#endif
                            }
                        };

                        var keyboardInputData = KeyboardInputActionObserver.GetInstance()?.FlushInputDataBuffer();
                        var mouseInputData = _mouseObserver.FlushInputDataBuffer(true);

                        // we often get events in the buffer with input times fractions of a ms after the current frame time for this update, but actually related to causing this update
                        // update the frame time to be latest of 'now' or the last device event in it
                        // otherwise replay gets messed up trying to read the inputs by time
                        var mostRecentKeyboardTime = keyboardInputData == null || keyboardInputData.Count == 0 ? 0.0 : keyboardInputData.Max(a => a.startTime);
                        var mostRecentMouseTime = mouseInputData == null || mouseInputData.Count == 0 ? 0.0 : mouseInputData.Max(a => a.startTime);
                        var mostRecentDeviceEventTime = Math.Max(mostRecentKeyboardTime, mostRecentMouseTime);
                        var frameTime = Math.Max(time, mostRecentDeviceEventTime);

                        var frameState = new FrameStateData()
                        {
                            keyFrame = _keyFrameTypeList.ToArray(),
                            tickNumber = _tickNumber,
                            time = frameTime,
                            timeScale = Time.timeScale,
                            screenSize = new Vector2Int() { x = screenWidth, y = screenHeight },
                            performance = performanceMetrics,
                            pixelHash = pixelHash,
                            state = _newStates,
                            inputs = new InputData()
                            {
                                keyboard = keyboardInputData,
                                mouse = mouseInputData
                            }
                        };

                        if (frameState.keyFrame != null)
                        {
                            RGDebug.LogDebug("Tick " + _tickNumber + " had " + keyboardInputData?.Count + " keyboard inputs , " + mouseInputData?.Count + " mouse inputs - KeyFrame: [" + string.Join(',', frameState.keyFrame) + "]");
                        }

                        // serialize to json byte[]
                        jsonData = Encoding.UTF8.GetBytes(
                            frameState.ToJson()
                        );

                        if (_priorStates == null)
                        {
                            // after the first pass, we need to allocate the prior.. we use null of this to indicate the first frame though so have to do it here at end of first tick
                            _priorStates = _newStates;
                            _newStates = new(1000);
                        }
                        else
                        {
                            // switch the list references
                            (_priorStates, _newStates) = (_newStates, _priorStates);
                        }

                    }
                    catch (Exception e)
                    {
                        RGDebug.LogException(e, "Exception capturing state for frame");
                    }

                    if (jsonData != null)
                    {

                        // wait for all frame rendering/etc to finish before taking the screenshot
                        yield return new WaitForEndOfFrame();

                        var screenShot = new Texture2D(screenWidth, screenHeight);

                        try
                        {
                            screenShot.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
                            screenShot.Apply();

                            // queue up writing the frame data to disk async
                            _frameQueue.Add((
                                (_currentGameplaySessionDirectoryPrefix, _tickNumber),
                                (
                                    jsonData,
                                    screenShot.width,
                                    screenShot.height,
                                    screenShot.graphicsFormat,
                                    screenShot.GetRawTextureData(),
                                    () =>
                                    {
                                        // MUST happen on main thread
                                        // but, we can't cleanup the texture until we've finished processing or unity goes BOOM/poof/dead
                                        _texture2Ds.Enqueue(screenShot);
                                    }
                                )
                            ));
                            // null this out so the queue can clean it up, not this code...
                            screenShot = null;
                        }
                        catch (Exception e)
                        {
                            RGDebug.LogException(e, "Exception capturing screenshot for frame");
                        }
                        finally
                        {
                            if (screenShot != null)
                            {
                                // Destroy the texture to free up memory
                                _texture2Ds.Enqueue(screenShot);
                            }
                        }
                    }
                }
            }
        }

        private void ProcessFrames()
        {
            while (!_frameQueue.IsCompleted && _tokenSource is { IsCancellationRequested: false })
            {
                try
                {
                    var tuple = _frameQueue.Take(_tokenSource.Token);
                    ProcessFrame(tuple.Item1.Item1, tuple.Item1.Item2, tuple.Item2.Item1, tuple.Item2.Item2,
                        tuple.Item2.Item3, tuple.Item2.Item4, tuple.Item2.Item5);
                    // invoke the cleanup callback function
                    tuple.Item2.Item6();
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException or InvalidOperationException)
                    {
                        RGDebug.LogException(e, "Error Processing Frames");
                    }
                }
            }
        }

        private void ProcessFrame(string directoryPath, long frameNumber, byte[] jsonData, int width, int height,
            GraphicsFormat graphicsFormat, byte[] frameData)
        {
            RecordJSON(directoryPath, frameNumber, jsonData);
            RecordJPG(directoryPath, frameNumber, width, height, graphicsFormat, frameData);
        }

        private void RecordJPG(string directoryPath, long frameNumber,int width, int height,
            GraphicsFormat graphicsFormat, byte[] frameData)
        {
            try
            {
                var imageOutput =
                    ImageConversion.EncodeArrayToJPG(frameData, graphicsFormat, (uint)width, (uint)height);

                // write out the image to file
                var path = $"{directoryPath}/screenshots/{frameNumber}".PadLeft(9, '0') + ".jpg";
                // Save the byte array as a file
                var fileWriteTask = File.WriteAllBytesAsync(path, imageOutput.ToArray(), _tokenSource.Token);
                fileWriteTask.ContinueWith((nextTask) =>
                {
                    if (nextTask.Exception != null)
                    {
                        RGDebug.LogException(nextTask.Exception, $"ERROR: Unable to record JPG for frame # {frameNumber}");
                    }

                });
                _fileWriteTasks.Add((path,fileWriteTask));
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"WARNING: Unable to record JPG for frame # {frameNumber}");
            }
        }

        private void RecordJSON(string directoryPath, long frameNumber, byte[] jsonData)
        {
            try
            {
                // write out the json to file
                var path = $"{directoryPath}/data/{frameNumber}".PadLeft(9, '0') + ".json";
                if (jsonData.Length == 0)
                {
                    RGDebug.LogError($"ERROR: Empty JSON data for frame # {frameNumber}");
                }

                // Save the byte array as a file
                var fileWriteTask = File.WriteAllBytesAsync(path, jsonData, _tokenSource.Token);
                fileWriteTask.ContinueWith((nextTask) =>
                {
                    if (nextTask.Exception != null)
                    {
                        RGDebug.LogException(nextTask.Exception, $"ERROR: Unable to record JSON for frame # {frameNumber}");
                    }

                });
                _fileWriteTasks.Add((path,fileWriteTask));
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"ERROR: Unable to record JSON for frame # {frameNumber}");
            }
        }

        public static readonly JsonSerializerSettings JsonSerializerSettings = new()
        {
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = JsonConverterContractResolver.Instance,
            Error = delegate(object _, ErrorEventArgs args)
            {
                // just eat certain errors
                if (args.ErrorContext.Error is MissingComponentException || args.ErrorContext.Error.InnerException is UnityException or NotSupportedException or MissingComponentException)
                {
                    args.ErrorContext.Handled = true;
                }
                else
                {
                    // do nothing anyway.. but useful for debugging which errors happened
                    args.ErrorContext.Handled = true;
                }
            },
            // state, behaviours, state, field, child field ... so we can basically see the vector values of a field on a behaviour on an object, but stop there
            //MaxDepth = 5
        };
    }

    public class JsonConverterContractResolver : DefaultContractResolver
    {
        public static readonly JsonConverterContractResolver Instance = new();

        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = base.CreateContract(objectType);
            // this will only be called once and then cached

            if (objectType == typeof(float) || objectType == typeof(Single))
            {
                contract.Converter = new FloatJsonConverter();
            }
            else if (objectType == typeof(double) || objectType == typeof(Double))
            {
                contract.Converter = new DoubleJsonConverter();
            }
            else if (objectType == typeof(decimal) || objectType == typeof(Decimal))
            {
                contract.Converter = new DecimalJsonConverter();
            }
            else if (objectType == typeof(Color))
            {
                contract.Converter = new ColorJsonConverter();
            }
            else if (objectType == typeof(Bounds))
            {
                contract.Converter = new BoundsJsonConverter();
            }
            else if (objectType == typeof(Vector2Int) || objectType == typeof(Vector3Int))
            {
                contract.Converter = new VectorIntJsonConverter();
            }
            else if (objectType == typeof(Vector2) || objectType == typeof(Vector3) || objectType == typeof(Vector4))
            {
                contract.Converter = new VectorJsonConverter();
            }
            else if (objectType == typeof(Quaternion))
            {
                contract.Converter = new QuaternionJsonConverter();
            }
            else if (objectType == typeof(Image))
            {
                contract.Converter = new ImageJsonConverter();
            }
            else if (objectType == typeof(Button))
            {
                contract.Converter = new ButtonJsonConverter();
            }
            else if (objectType == typeof(TextMeshPro))
            {
                contract.Converter = new TextMeshProJsonConverter();
            }
            else if (objectType == typeof(TextMeshProUGUI))
            {
                contract.Converter = new TextMeshProUGUIJsonConverter();
            }
            else if (objectType == typeof(Text))
            {
                contract.Converter = new TextJsonConverter();
            }
            else if (objectType == typeof(Rect))
            {
                contract.Converter = new RectJsonConverter();
            }
            else if (objectType == typeof(RawImage))
            {
                contract.Converter = new RawImageJsonConverter();
            }
            else if (objectType == typeof(Mask))
            {
                contract.Converter = new MaskJsonConverter();
            }
            else if (objectType == typeof(Animator))
            {
                contract.Converter = new AnimatorJsonConverter();
            }
            else if (objectType == typeof(Rigidbody))
            {
                contract.Converter = new RigidbodyJsonConverter();
            }
            else if (objectType == typeof(Rigidbody2D))
            {
                contract.Converter = new Rigidbody2DJsonConverter();
            }
            else if (objectType == typeof(Collider))
            {
                contract.Converter = new ColliderJsonConverter();
            }
            else if (objectType == typeof(Collider2D))
            {
                contract.Converter = new Collider2DJsonConverter();
            }
            else if (objectType == typeof(ParticleSystem))
            {
                contract.Converter = new ParticleSystemJsonConverter();
            }
            else if (objectType == typeof(MeshFilter))
            {
                contract.Converter = new MeshFilterJsonConverter();
            }
            else if (objectType == typeof(MeshRenderer))
            {
                contract.Converter = new MeshRendererJsonConverter();
            }
            else if (objectType == typeof(SkinnedMeshRenderer))
            {
                contract.Converter = new SkinnedMeshRendererJsonConverter();
            }
            else if (objectType == typeof(NavMeshAgent))
            {
                contract.Converter = new NavMeshAgentJsonConverter();
            }
            else if (IsUnityType(objectType) && InGameObjectFinder.GetInstance().collectStateFromBehaviours)
            {
                if (NetworkVariableJsonConverter.Convertable(objectType))
                {
                    // only support when netcode is in the project
                    contract.Converter = new NetworkVariableJsonConverter();
                }
                else
                {
                    contract.Converter = new UnityObjectFallbackJsonConverter();
                }
            }
            else if (typeof(Behaviour).IsAssignableFrom(objectType))
            {
                contract.Converter = new UnityObjectFallbackJsonConverter();
            }

            return contract;
        }

        // leave out bossroom types as that is our main test project
        // (isUnity, isBossRoom)
        private readonly Dictionary<Assembly, (bool, bool)> _unityAssemblies = new();

        private bool IsUnityType(Type objectType)
        {
            var assembly = objectType.Assembly;
            if (!_unityAssemblies.TryGetValue(assembly, out var isUnityType))
            {
                var isUnity = assembly.FullName.StartsWith("Unity");
                var isBossRoom = false;
                if (isUnity)
                {
                    isBossRoom = assembly.FullName.StartsWith("Unity.BossRoom");
                }

                isUnityType = (isUnity, isBossRoom);
                _unityAssemblies[assembly] = isUnityType;
            }

            return isUnityType is { Item1: true, Item2: false };
        }
    }
}
