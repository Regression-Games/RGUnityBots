using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RegressionGames.StateActionTypes;
using RegressionGames.StateRecorder.JsonConverters;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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

        private string _currentVideoDirectory;

        private CancellationTokenSource _tokenSource;

        private static ScreenRecorder _this;

        private bool _isRecording;

        private readonly ConcurrentQueue<Texture2D> _texture2Ds = new();

        private long _videoNumber;
        private long _tickNumber;

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
                KeyboardInputActionObserver.GetInstance()?.StopRecording();
                MouseInputActionObserver.GetInstance()?.StopRecording();
                var theVideoDirectory = _currentVideoDirectory;
                Task.Run(() =>
                {
                    RGDebug.LogInfo($"Zipping recording replay to file: {_currentVideoDirectory}.zip");
                    ZipFile.CreateFromDirectory(theVideoDirectory, theVideoDirectory + ".zip");
                    Directory.Delete(theVideoDirectory, true);
                    RGDebug.LogInfo($"Finished zipping replay to file: {_currentVideoDirectory}");
                });
                _isRecording = false;
            }
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
                    _currentVideoDirectory =
                        $"{stateRecordingsDirectory}/{Application.productName}/run_{_videoNumber++}";
                } while (Directory.Exists(_currentVideoDirectory) || File.Exists(_currentVideoDirectory + ".zip"));

                if (!Directory.Exists(_currentVideoDirectory))
                {
                    Directory.CreateDirectory(_currentVideoDirectory);
                }

                // run the frame processor in the background
                Task.Run(ProcessFrames, _tokenSource.Token);
                RGDebug.LogInfo($"Recording replay screenshots to directory: {_currentVideoDirectory}");
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
                    || (int)(1000 * (time - _lastCvFrameTime)) >= (int)(1000.0f / (recordingMinFPS>0?recordingMinFPS:1))
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
                            timeScale =  Time.timeScale,
                            screenSize = new Vector2Int() { x= screenWidth, y=screenHeight },
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
                            RGDebug.LogDebug("Tick " + _tickNumber + " had " + keyboardInputData?.Count + " keyboard inputs , " + mouseInputData?.Count + " mouse inputs - KeyFrame: [" + string.Join(',',frameState.keyFrame) + "]");
                        }

                        _priorFrame = frameState;

                        // serialize to json byte[]
                        var jsonData = Encoding.UTF8.GetBytes(
                            JsonConvert.SerializeObject(frameState, Formatting.Indented, _serializerSettings)
                        );

                        var theScreenshot = screenShot;

                        // queue up writing the frame data to disk async
                        _frameQueue.Add((
                            (_currentVideoDirectory, _tickNumber),
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
                var path = $"{directoryPath}/{frameNumber}".PadLeft(9, '0') + ".jpg";
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
                var path = $"{directoryPath}/{frameNumber}".PadLeft(9, '0') + ".json";
                // Save the byte array as a file
                File.WriteAllBytesAsync(path, jsonData);
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"WARNING: Unable to record JSON for frame # {frameNumber} - {e}");
            }
        }

        private readonly JsonSerializerSettings _serializerSettings = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

            Converters = new List<JsonConverter>
            {
                new ColorJsonConverter(),
                new BoundsJsonConverter(),
                new VectorIntJsonConverter(),
                new VectorJsonConverter(),
                new QuaternionJsonConverter(),
                new ImageJsonConverter(),
                new ButtonJsonConverter(),
                new TextMeshProJsonConverter(),
                new TextMeshProUGUIJsonConverter(),
                new TextJsonConverter(),
                new RectJsonConverter(),
                new RawImageJsonConverter(),
                new MaskJsonConverter(),
                new AnimatorJsonConverter(),
                new RigidbodyJsonConverter(),
                new ColliderJsonConverter(),
                new Collider2DJsonConverter(),
                new ParticleSystemJsonConverter(),
                new MeshFilterJsonConverter(),
                new MeshRendererJsonConverter(),
                new SkinnedMeshRendererJsonConverter(),
                new NavMeshAgentJsonConverter(),
                // KEEP THIS UnityObjectJsonConverter AT THE END OF THE LIST AS A FALLBACK TO PREVENT PERFORMANCE EXPLOSION
                new UnityObjectFallbackJsonConverter()
            },
            Error = delegate (object _, ErrorEventArgs args)
            {
                // just eat certain errors
                if (args.ErrorContext.Error.InnerException is UnityException)
                {
                    args.ErrorContext.Handled = true;
                }

                args.ErrorContext.Handled = true;
            }
        };
    }
}
