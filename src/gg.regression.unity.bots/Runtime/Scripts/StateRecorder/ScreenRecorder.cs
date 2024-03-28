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
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Media;
#endif


namespace RegressionGames.StateRecorder
{
    public class ScreenRecorder : MonoBehaviour
    {
        [Tooltip("Minimum FPS at which to capture frames.  Key frames will potentially be recorded more frequently than this.")]
        public int recordingMinFPS = 5;

        [Tooltip("Directory to save state recordings in.  This directory will be created if it does not exist.  If not specific, this will default to 'unity_videos' in your user profile path for your operating system.")]
        public string stateRecordingsDirectory = "";

        private double _lastCvFrameTime = -1.0;

        private int _frameCountSinceLastTick;

        private string _currentGameplaySessionDirectoryPrefix;

        private CancellationTokenSource _tokenSource;

        private static ScreenRecorder _this;
        
        private static RGServiceManager _rgServiceManager = new();

        private bool _isRecording;

        private readonly ConcurrentQueue<Texture2D> _texture2Ds = new();

        private long _videoNumber;
        private long _tickNumber;
        private DateTime _startTime;

        private BlockingCollection<((string, long), (byte[], int, int, GraphicsFormat, NativeArray<byte>, Action))>
            _frameQueue;

#if UNITY_EDITOR
        private MediaEncoder _encoder;
#endif

        [NonSerialized]
        private FrameStateData _priorFrame;

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


        private void OnDestroy()
        {
            _frameQueue?.CompleteAdding();
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
            _tokenSource = null;
#if UNITY_EDITOR
            _encoder?.Dispose();
            _encoder = null;
#endif
            if (_isRecording)
            {
                RGBotManager.GetInstance().ShowUploadingIndicator(true);
                HandleRecordingSaving();
            }
        }

        /**
         * We separate out the async saving code since OnDestroy can't be async, and we need to wait for saving
         * before doing API calls
         */
        private async void HandleRecordingSaving()
        {
            _isRecording = false;
            var endTime = DateTime.Now;
            KeyboardInputActionObserver.GetInstance()?.StopRecording();
            MouseInputActionObserver.GetInstance()?.StopRecording();
            var gameplaySessionDataDirectory = _currentGameplaySessionDirectoryPrefix + "_data";
            var gameplaySessionScreenshotDirectory = _currentGameplaySessionDirectoryPrefix + "_screenshots";
            var thumbnailPath = _currentGameplaySessionDirectoryPrefix + "_thumbnail.png";
            await Task.Run(() =>
            {
                // First, save the gameplay session data
                RGDebug.LogInfo($"Zipping state recording replay to file: {gameplaySessionDataDirectory}.zip");
                ZipFile.CreateFromDirectory(gameplaySessionDataDirectory, gameplaySessionDataDirectory + ".zip");
                Directory.Delete(gameplaySessionDataDirectory, true);
                RGDebug.LogInfo($"Finished zipping replay to file: {gameplaySessionDataDirectory}.zip");
                
                // Then save the screenshots separately
                RGDebug.LogInfo($"Zipping screenshot recording replay to file: {gameplaySessionScreenshotDirectory}.zip");
                ZipFile.CreateFromDirectory(gameplaySessionScreenshotDirectory, gameplaySessionScreenshotDirectory + ".zip");
                RGDebug.LogInfo($"Finished zipping replay to file: {gameplaySessionScreenshotDirectory}.zip");
                
                // Finally, we also save a thumbnail, by choosing the middle file in the screenshots
                var screenshotFiles = Directory.GetFiles(gameplaySessionScreenshotDirectory);
                var middleFile = screenshotFiles[screenshotFiles.Length / 2]; // this gets floored automatically
                File.Copy(middleFile, thumbnailPath);
                Directory.Delete(gameplaySessionScreenshotDirectory, true);
            });
            CreateAndUploadGameplaySession(endTime);
        }

        private async void CreateAndUploadGameplaySession(DateTime endTime)
        {
            RGDebug.LogInfo($"Creating and uploading GameplaySession on the backend, from {_startTime} to {endTime} with {_tickNumber} ticks");
            try
            {
                
                // First, create the gameplay session
                long gameplaySessionId = -1;
                await _rgServiceManager.CreateGameplaySession(_startTime, endTime, _tickNumber,
                    (response) =>
                    {
                        gameplaySessionId = response.id;
                        RGDebug.LogInfo($"Created gameplay session with id: {response.id}");
                    },
                    () => { RGDebug.LogError($"Failed to create gameplay session"); });
                
                // If the gameplay session was not created, return
                if (gameplaySessionId == -1)
                {
                    return;
                }
                
                // Upload the gameplay session data
                await _rgServiceManager.UploadGameplaySessionData(gameplaySessionId,
                    _currentGameplaySessionDirectoryPrefix + "_data.zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session data"); },
                    () => { RGDebug.LogError($"Failed to upload gameplay session data"); });
                
                // Next, upload the gameplay session screenshots
                await _rgServiceManager.UploadGameplaySessionScreenshots(gameplaySessionId,
                    _currentGameplaySessionDirectoryPrefix + "_screenshots.zip",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session screenshots"); },
                    () => { RGDebug.LogError($"Failed to upload gameplay session screenshots"); });
                
                // Finally, upload the thumbnail
                await _rgServiceManager.UploadGameplaySessionThumbnail(gameplaySessionId,
                    _currentGameplaySessionDirectoryPrefix + "_thumbnail.png",
                    () => { RGDebug.LogInfo($"Uploaded gameplay session thumbnail"); },
                    () => { RGDebug.LogError($"Failed to upload gameplay session thumbnail"); });
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            RGBotManager.GetInstance().ShowUploadingIndicator(false);
            
        }

        // Update is called once per frame
        private void LateUpdate()
        {
            while (_texture2Ds.TryDequeue(out var text))
            {
                // have to destroy the textures on the main thread
                Destroy(text);
            }

            if (_isRecording)
            {
                StartCoroutine(RecordFrame());
            }
        }

        public void StartRecording()
        {
            if (!_isRecording)
            {
                KeyboardInputActionObserver.GetInstance()?.StartRecording();
                MouseInputActionObserver.GetInstance()?.StartRecording();
                _isRecording = true;
                _tickNumber = 0;
                _startTime = DateTime.Now;
                _frameQueue =
                    new BlockingCollection<((string, long), (byte[], int, int, GraphicsFormat, NativeArray<byte>, Action))>(
                        new ConcurrentQueue<((string, long), (byte[], int, int, GraphicsFormat, NativeArray<byte>,
                            Action)
                            )>());

                _tokenSource = new CancellationTokenSource();

                Directory.CreateDirectory(stateRecordingsDirectory);

                // find the first index number we haven't used yet
                do
                {
                    _currentGameplaySessionDirectoryPrefix =
                        $"{stateRecordingsDirectory}/{Application.productName}/run_{_videoNumber++}";
                } while (Directory.Exists(_currentGameplaySessionDirectoryPrefix + "_data") || File.Exists(_currentGameplaySessionDirectoryPrefix + "_data.zip"));

                if (!Directory.Exists(_currentGameplaySessionDirectoryPrefix))
                {
                    Directory.CreateDirectory(_currentGameplaySessionDirectoryPrefix + "_data");
                    Directory.CreateDirectory(_currentGameplaySessionDirectoryPrefix + "_screenshots");
                }

                // run the frame processor in the background
                Task.Run(ProcessFrames, _tokenSource.Token);
                RGDebug.LogInfo($"Recording replay screenshots to directory: {_currentGameplaySessionDirectoryPrefix}_screenshots");
            }
        }

        private bool IsKeyFrame(List<RecordedGameObjectState> priorState, List<RecordedGameObjectState> currentState)
        {
            if (priorState != null)
            {
                // we have to treat

                var scenesInPriorFrame = priorState.Select(s => s.scene).Distinct().ToList();
                var scenesInCurrentFrame = currentState.Select(s => s.scene).Distinct().ToList();
                if (scenesInPriorFrame.Count != scenesInCurrentFrame.Count ||
                    !scenesInPriorFrame.All(scenesInCurrentFrame.Contains))
                {
                    // elements from scenes changed this frame
                    return true;
                }

                // visible UI elements changed
                var uiElementsInPriorFrame = priorState.Where(s => s.worldSpaceBounds == null).Select(s => s.id).Distinct().ToList();
                var uiElementsInCurrentFrame = currentState.Where(s => s.worldSpaceBounds == null).Select(s => s.id).Distinct().ToList();
                if (uiElementsInPriorFrame.Count != uiElementsInCurrentFrame.Count ||
                    !uiElementsInPriorFrame.All(uiElementsInCurrentFrame.Contains))
                {
                    // visible UI elements changed this frame
                    return true;
                }
            }

            return false;
        }

        public void StopRecording()
        {
            OnDestroy();
        }

        private IEnumerator RecordFrame()
        {
            yield return new WaitForEndOfFrame();
            if (!_frameQueue.IsCompleted)
            {
                ++_frameCountSinceLastTick;
                // handle recording ... uses unscaled time for real framerate calculations
                var time = Time.unscaledTimeAsDouble;

                var statefulObjects = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame();

                // tell if the new frame is a key frame or the first frame (always a key frame)
                var isKeyFrame = (_priorFrame == null) || IsKeyFrame(_priorFrame.state, statefulObjects);

                // estimating the time in int milliseconds .. won't exactly match target FPS.. but will be close
                if (isKeyFrame
                    || (int)(1000 * (time - _lastCvFrameTime)) >= (int)(1000.0f / (recordingMinFPS > 0 ? recordingMinFPS : 1))
                   )
                {
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

                    var screenWidth = Screen.width;
                    var screenHeight = Screen.height;

                    var screenShot = new Texture2D(screenWidth, screenHeight);
                    try
                    {
                        screenShot.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
                        screenShot.Apply();

                        ++_tickNumber;

                        var keyboardInputData = KeyboardInputActionObserver.GetInstance()?.FlushInputDataBuffer();
                        var mouseInputData = MouseInputActionObserver.GetInstance()?.FlushInputDataBuffer();

                        // we often get events in the buffer with input times fractions of a ms after the current frame time for this update, but actually related to causing this update
                        // update the frame time to be latest of 'now' or the last device event in it
                        // otherwise replay gets messed up trying to read the inputs by time
                        var mostRecentKeyboardTime = keyboardInputData == null || keyboardInputData.Count == 0 ? 0.0 : keyboardInputData.Max(a => a.startTime);
                        var mostRecentMouseTime = mouseInputData == null || mouseInputData.Count == 0 ? 0.0 : mouseInputData.Max(a => a.startTime);
                        var mostRecentDeviceEventTime = Math.Max(mostRecentKeyboardTime, mostRecentMouseTime);
                        var frameTime = Math.Max(time, mostRecentDeviceEventTime);

                        var frameState = new FrameStateData()
                        {
                            keyFrame = isKeyFrame,
                            tickNumber = _tickNumber,
                            time = frameTime,
                            timeScale = Time.timeScale,
                            screenSize = new Vector2Int() { x = screenWidth, y = screenHeight },
                            performance = performanceMetrics,
                            state = statefulObjects,
                            inputs = new InputData()
                            {
                                keyboard = keyboardInputData,
                                mouse = mouseInputData
                            }
                        };

                        if (frameState.keyFrame)
                        {
                            RGDebug.LogDebug("Tick " + _tickNumber + " had " + keyboardInputData?.Count + " keyboard inputs , " + mouseInputData?.Count + " mouse inputs - KeyFrame: [" + string.Join(',', frameState.keyFrame) + "]");
                        }

                        _priorFrame = frameState;

                        // serialize to json byte[]
                        var jsonData = Encoding.UTF8.GetBytes(
                            frameState.ToJson()
                        );

                        var theScreenshot = screenShot;

                        // queue up writing the frame data to disk async
                        _frameQueue.Add((
                            (_currentGameplaySessionDirectoryPrefix, _tickNumber),
                            (
                                jsonData,
                                screenShot.width,
                                screenShot.height,
                                screenShot.graphicsFormat,
                                screenShot.GetRawTextureData<byte>(),
                                () =>
                                {
                                    // MUST happen on main thread
                                    // but, we can't cleanup the texture until we've finished processing or unity goes BOOM/poof/dead
                                    _texture2Ds.Enqueue(theScreenshot);
                                }
                            )
                        ));
                        // null this out so the queue can clean it up, not this do
                        screenShot = null;
                    }
                    catch (Exception e)
                    {
                        RGDebug.LogException(e, "Exception capturing state or screenshot for frame");
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
                    if (e is not OperationCanceledException)
                    {
                        RGDebug.LogException(e, "Error Processing Frames");
                    }
                }
            }
        }

        private void ProcessFrame(string directoryPath, long frameNumber, byte[] jsonData, int width, int height,
            GraphicsFormat graphicsFormat, NativeArray<byte> frameData)
        {
            try
            {
                var imageOutput =
                    ImageConversion.EncodeNativeArrayToJPG(frameData, graphicsFormat, (uint)width, (uint)height);

                // write out the image to file
                var path = $"{directoryPath}_screenshots/{frameNumber}".PadLeft(9, '0') + ".jpg";
                // Save the byte array as a file
                File.WriteAllBytesAsync(path, imageOutput.ToArray());
                RecordFrameState(directoryPath, _tickNumber, jsonData);
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"WARNING: Unable to record JPG for frame # {frameNumber} - {e}");
            }
        }

        private void RecordFrameState(string directoryPath, long frameNumber, byte[] jsonData)
        {
            try
            {
                // write out the json to file
                var path = $"{directoryPath}_data/{frameNumber}".PadLeft(9, '0') + ".json";
                // Save the byte array as a file
                File.WriteAllBytesAsync(path, jsonData);
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"WARNING: Unable to record JSON for frame # {frameNumber} - {e}");
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
            else if (IsUnityType(objectType))
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
