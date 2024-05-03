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
using StateRecorder;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

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
        public int recordingMinFPS;

        [Tooltip("Directory to save state recordings in.  This directory will be created if it does not exist.  If not specific, this will default to 'unity_videos' in your user profile path for your operating system.")]
        public string stateRecordingsDirectory = "";

        private double _lastCvFrameTime = -1.0;

        private int _frameCountSinceLastTick;

        private string _currentSessionId;
        private string _referenceSessionId;

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


        public static readonly Dictionary<int, RecordedGameObjectState> _emptyStateDictionary = new(0);


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

        private IEnumerator StartRecordingCoroutine(string referenceSessionId)
        {
            // Do this 1 frame after the request so that the click action of starting the recording itself isn't captured
            yield return null;
            if (!_isRecording)
            {
                ReplayDataPlaybackController.SendMouseEvent(0, new ReplayMouseInputEntry()
                {
                    // get the mouse off the screen, when replay fails, we leave the virtual mouse cursor alone so they can see its location at time of failure, but on new recording, we want this gone
                    position = new Vector2Int(Screen.width +20, -20)
                }, null, _emptyStateDictionary);

                KeyboardInputActionObserver.GetInstance()?.StartRecording();
                _mouseObserver.ClearBuffer();
                InGameObjectFinder.GetInstance()?.Cleanup();
                _isRecording = true;
                _tickNumber = 0;
                _currentSessionId = Guid.NewGuid().ToString("n");
                _referenceSessionId = referenceSessionId;
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

        public void StartRecording(string referenceSessionId)
        {
            var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
            if (gameFacePixelHashObserver != null)
            {
                gameFacePixelHashObserver.SetActive(true);
            }
            StartCoroutine(StartRecordingCoroutine(referenceSessionId));

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

        private void GetKeyFrameType(bool firstFrame, Dictionary<int,RecordedGameObjectState> priorState, Dictionary<int,RecordedGameObjectState> currentState, string pixelHash)
        {
            _keyFrameTypeList.Clear();
            if (firstFrame)
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

            foreach(var recordedGameObjectState in currentState.Values)
            {
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
            foreach(var recordedGameObjectState in priorState.Values)
            {
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

        private RenderTexture _screenShotTexture = null;

        private IEnumerator RecordFrame()
        {
            if (!_frameQueue.IsCompleted)
            {
                ++_frameCountSinceLastTick;
                // handle recording ... uses unscaled time for real framerate calculations
                var time = Time.unscaledTimeAsDouble;

                byte[] jsonData = null;

                var states = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame();

                // generally speaking, you want to observe the mouse relative to the prior state as the mouse input generally causes the 'newState' and thus
                // what it clicked on normally isn't in the new state (button was in the old state)
                var priorStates = states?.Item1;
                var currentStates = states?.Item2;
                if (priorStates?.Count > 0)
                {
                    _mouseObserver.ObserveMouse(priorStates);
                }
                else
                {
                    _mouseObserver.ObserveMouse(currentStates);
                }

                var gameFacePixelHashObserver = GameFacePixelHashObserver.GetInstance();
                var pixelHash = gameFacePixelHashObserver != null ? gameFacePixelHashObserver.GetPixelHash(true) : null;

                // tell if the new frame is a key frame or the first frame (always a key frame)
                GetKeyFrameType(_tickNumber ==0, priorStates, currentStates, pixelHash);

                // estimating the time in int milliseconds .. won't exactly match target FPS.. but will be close
                if (_keyFrameTypeList.Count > 0
                    || (recordingMinFPS > 0 && (int)(1000 * (time - _lastCvFrameTime)) >= (int)(1000.0f / (recordingMinFPS)))
                   )
                {

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

                        _lastCvFrameTime = time;
                        _frameCountSinceLastTick = 0;

                        var keyboardInputData = KeyboardInputActionObserver.GetInstance()?.FlushInputDataBuffer();
                        var mouseInputData = _mouseObserver.FlushInputDataBuffer(true);

                        // we often get events in the buffer with input times fractions of a ms after the current frame time for this update, but actually related to causing this update
                        // update the frame time to be latest of 'now' or the last device event in it
                        // otherwise replay gets messed up trying to read the inputs by time
                        var mostRecentKeyboardTime = keyboardInputData == null || keyboardInputData.Count == 0 ? 0.0 : keyboardInputData.Max(a => a.startTime);
                        var mostRecentMouseTime = mouseInputData == null || mouseInputData.Count == 0 ? 0.0 : mouseInputData.Max(a => a.startTime);
                        var mostRecentDeviceEventTime = Math.Max(mostRecentKeyboardTime, mostRecentMouseTime);
                        var frameTime = Math.Max(time, mostRecentDeviceEventTime);

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
                            inputs = new InputData()
                            {
                                keyboard = keyboardInputData,
                                mouse = mouseInputData
                            }
                        };

                        if (frameState.keyFrame != null)
                        {
                            if (RGDebug.IsDebugEnabled)
                            {
                                RGDebug.LogDebug("Tick " + _tickNumber + " had " + keyboardInputData?.Count + " keyboard inputs , " + mouseInputData?.Count + " mouse inputs - KeyFrame: [" + string.Join(',', frameState.keyFrame) + "]");
                            }
                        }

                        // serialize to json byte[]
                        jsonData = Encoding.UTF8.GetBytes(
                            frameState.ToJsonString()
                        );
                    }
                    catch (Exception e)
                    {
                        RGDebug.LogException(e, "Exception capturing state for frame");
                    }

                    if (jsonData != null)
                    {
                        if (_screenShotTexture == null || _screenShotTexture.width != screenWidth || _screenShotTexture.height != screenHeight)
                        {
                            if (_screenShotTexture != null)
                            {
                                Object.Destroy(_screenShotTexture);
                            }

                            _screenShotTexture = new RenderTexture(screenWidth, screenHeight, 0);
                        }

                        // wait for end of frame before capturing screenshot
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenShotTexture);
                        AsyncGPUReadback.Request(_screenShotTexture, 0, TextureFormat.RGBA32, request =>
                        {
                            if (!request.hasError)
                            {
                                var data = request.GetData<byte>();
                                var pixels = new byte[data.Length];
                                data.CopyTo(pixels);

                                // queue up writing the frame data to disk async
                                _frameQueue.Add((
                                    (_currentGameplaySessionDirectoryPrefix, _tickNumber),
                                    (
                                        jsonData,
                                        screenWidth,
                                        screenHeight,
                                        _screenShotTexture.graphicsFormat,
                                        pixels,
                                        () =>
                                        { }
                                    )
                                ));
                            }
                            else
                            {
                                RGDebug.LogError("Error capturing screenshot for frame");
                            }

                        });
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
                    if (e is not OperationCanceledException && e is not InvalidOperationException)
                    {
                        RGDebug.LogException(e, "Error Processing Frames");
                    }
                }
            }
        }

        private void ProcessFrame(string directoryPath, long frameNumber, byte[] jsonData, int width, int height, GraphicsFormat graphicsFormat, byte[] frameData)
        {
            RecordJson(directoryPath, frameNumber, jsonData);
            RecordJPG(directoryPath, frameNumber, width, height, graphicsFormat, frameData);
        }

        private void RecordJPG(string directoryPath, long frameNumber, int width, int height, GraphicsFormat graphicsFormat,
            byte[] frameData)
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

        private void RecordJson(string directoryPath, long frameNumber, byte[] jsonData)
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
    }
}
