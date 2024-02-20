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
using StateRecorder.JsonConverters;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

#if UNITY_EDITOR
using RegressionGames;
using UnityEditor;
using UnityEditor.Media;
#endif


namespace StateRecorder
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public abstract class InputActionData
    {
        public double startTime;
    }
    
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class FrameStateData
    {
        public long tickNumber;
        public bool keyFrame;
        public float time;
        public int[] screenSize;
        public PerformanceMetricData performance;
        public List<RenderableGameObjectState> state;
        public InputData inputs;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        public List<InputActionData> keyboard;
        public List<InputActionData> mouse;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class PerformanceMetricData
    {
        public float previousTickTime;
        public int framesSincePreviousTick;
        public int fps;
        public EngineStatsData engineStats;
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class EngineStatsData
    {
        public float frameTime;
        public float renderTime;

        public int triangles;
        public int vertices;

        public int setPassCalls;

        public int drawCalls;
        public int dynamicBatchedDrawCalls;
        public int staticBatchedDrawCalls;
        public int instancedBatchedDrawCalls;

        public int batches;
        public int dynamicBatches;
        public int staticBatches;
        public int instancedBatches;
    }

    public class ScreenRecorder : MonoBehaviour
    {

        //TODO: ? Move these values to RGSettings ?
        [Tooltip("FPS at which to capture frames")]
        public int recordingFPS = 5;

        [Tooltip("Forces image (non video) mode even in the Editor")]
        public bool forceImageMode;

        public string stateRecordingsDirectory = "/Users/zack/unity_videos";
        
        private bool _useImageMode;

        private float _lastCvFrameTime = -1f;

        private int _frameCountSinceLastTick;

        private string _currentVideoDirectory;
        
        private CancellationTokenSource _tokenSource;

        private static ScreenRecorder _this;

        private bool _isRecording;

        private readonly ConcurrentQueue<Texture2D> _texture2Ds = new ();
        
        private long _videoNumber;
        private long _tickNumber;

        private FrameStateData _priorFrame;

        public static ScreenRecorder GetInstance()
        {
            return _this;
        }

        public void Awake()
        {
#if UNITY_EDITOR
            _useImageMode = forceImageMode;
#else
        _useImageMode = true;
#endif
            // only allow 1 of these to be alive
            if (_this != null && _this.gameObject != gameObject)
            {
                Destroy(gameObject);
                return;
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
            _encoder?.Dispose();
            _encoder = null;
            KeyboardInputActionObserver.GetInstance()?.StopRecording();
            MouseInputActionObserver.GetInstance()?.StopRecording();
            if (_isRecording)
            {
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
        void LateUpdate()
        {
            while (_texture2Ds.TryDequeue(out var text))
            {
                // have to destroy the textures on the main thread
                Destroy(text);
            }

            if (_isRecording && recordingFPS > 0)
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
                    new(
                        new ConcurrentQueue<((string, long), (byte[], int, int, GraphicsFormat, NativeArray<byte>,
                            Action)
                            )>());

                _tokenSource = new();

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

                if (!_useImageMode)
                {
                    RGDebug.LogInfo($"Recording replay video to directory: {_currentVideoDirectory}");
                }
                else
                {
                    // run the frame processor in the background
                    Task.Run(ProcessFrames, _tokenSource.Token);
                    RGDebug.LogInfo($"Recording replay screenshots to directory: {_currentVideoDirectory}");
                }
            }
        }

        private bool IsKeyFrame(FrameStateData priorFrame, FrameStateData currentFrame)
        {
            if (currentFrame.inputs.keyboard.FirstOrDefault(i => i.startTime > currentFrame.performance.previousTickTime) != null)
            {
                // we had a keyboard input that started on this frame
                return true;
            }
            
            if (currentFrame.inputs.mouse.FirstOrDefault(i => ((MouseInputActionData)i).NewButtonPress) != null)
            {
                // we had a mouse input that started on this frame
                return true;
            }
            
            var scenesInPriorFrame = priorFrame.state.Select(s => s.scene).Distinct().ToList();
            var scenesInCurrentFrame = currentFrame.state.Select(s => s.scene).Distinct().ToList();
            if (scenesInPriorFrame.Count != scenesInCurrentFrame.Count ||
                !scenesInPriorFrame.All(scenesInCurrentFrame.Contains))
            {
                // elements from scenes changed this frame
                return true;
            }
            
            // visible UI elements changed
            var uiElementsInPriorFrame = priorFrame.state.Where(s => s.worldSpaceBounds == null).Select(s => s.id).Distinct().ToList();
            var uiElementsInCurrentFrame = currentFrame.state.Where(s => s.worldSpaceBounds == null).Select(s => s.id).Distinct().ToList();
            if (uiElementsInPriorFrame.Count != uiElementsInCurrentFrame.Count ||
                !uiElementsInPriorFrame.All(uiElementsInCurrentFrame.Contains))
            {
                // visible UI elements changed this frame
                return true;
            }
            
            //TODO: Future - What other things should 'automatically' be a key frame.
            //TODO: Can we make it so that developers can add their own definitions for what is a key frame ??? Do we need to ?

            return false;
        }

        public void StopRecording()
        {
            OnDestroy();
        }

        IEnumerator RecordFrame()
        {
            yield return new WaitForEndOfFrame();
            if (!_frameQueue.IsCompleted)
            {
                ++_frameCountSinceLastTick;
                // handle recording
                var time = Time.unscaledTime;

                // estimating the time in int milliseconds .. won't exactly match target FPS.. but will be close
                if ((int)(1000 * (time - _lastCvFrameTime)) >= (int)(1000.0f / recordingFPS))
                {

                    var performanceMetrics = new PerformanceMetricData()
                    {
                        framesSincePreviousTick = _frameCountSinceLastTick,
                        previousTickTime = _lastCvFrameTime,
                        fps = (int)(_frameCountSinceLastTick / (time - _lastCvFrameTime)),
                        engineStats = new EngineStatsData()
                        {
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
                            instancedBatches = UnityStats.instancedBatches,
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

                        if (!_useImageMode)
                        {
#if UNITY_EDITOR
                            if (_encoder == null)
                            {
                                try
                                {
                                    // encode the frames as video
                                    var vidAttr = new VideoTrackAttributes
                                    {
                                        bitRateMode = VideoBitrateMode.Medium,
                                        frameRate = new MediaRational(25),
                                        width = (uint)screenShot.width,
                                        height = (uint)screenShot.height,
                                        includeAlpha = false
                                    };

                                    var audAttr = new AudioTrackAttributes
                                    {
                                        sampleRate = new MediaRational(48000),
                                        channelCount = 2
                                    };

                                    _encoder = new MediaEncoder($"{_currentVideoDirectory}/gameVideo.mp4", vidAttr,
                                        audAttr);
                                    RGDebug.LogDebug(
                                        $"Created mp4 encoder for file: {_currentVideoDirectory}/gameVideo.mp4");
                                }
                                catch (Exception e)
                                {
                                    RGDebug.LogException(e);
                                }
                            }

                            if (_encoder != null)
                            {
                                try
                                {
                                    _encoder.AddFrame(screenShot);
                                }
                                catch (Exception e)
                                {
                                    RGDebug.LogException(e);
                                }
                            }
#endif
                        }
                        else
                        {
                            ++_tickNumber;
                            var statefulObjects = InGameObjectFinder.GetInstance()?.GetStateForCurrentFrame();
                            var keyboardInputData = KeyboardInputActionObserver.GetInstance()?.FlushInputDataBuffer();

                            var mouseInputData = MouseInputActionObserver.GetInstance()?.FlushInputDataBuffer();
                            
                            var deviceInputs = new InputData
                            {
                                keyboard = keyboardInputData,
                                mouse = mouseInputData
                            };

                            var frameState = new FrameStateData()
                            {
                                tickNumber = _tickNumber,
                                time = time,
                                screenSize = new[] { screenWidth, screenHeight },
                                performance = performanceMetrics,
                                state = statefulObjects,
                                inputs = deviceInputs
                            };
                            
                            // tell if the new frame is a key frame
                            if (_priorFrame == null || IsKeyFrame(_priorFrame, frameState))
                            {
                                // first frame in a recording is always a key frame
                                frameState.keyFrame = true;
                                RGDebug.LogDebug("Tick " + _tickNumber + " had " + keyboardInputData?.Count + " keyboard inputs , "+ mouseInputData?.Count+ " mouse inputs - isKeyFrame: " + frameState.keyFrame);
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



        private BlockingCollection<((string, long), (byte[], int, int, GraphicsFormat, NativeArray<byte>, Action))>
            _frameQueue;

        private MediaEncoder _encoder;

        void ProcessFrames()
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
                string path = $"{directoryPath}/{frameNumber}".PadLeft(9, '0') + ".jpg";
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
                string path = $"{directoryPath}/{frameNumber}".PadLeft(9, '0') + ".json";
                // Save the byte array as a file
                File.WriteAllBytesAsync(path, jsonData);
            }
            catch (Exception e)
            {
                RGDebug.LogWarning($"WARNING: Unable to record JSON for frame # {frameNumber} - {e}");
            }

        }


        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

            Converters = new List<JsonConverter>
            {
                new ColorJsonConverter(),
                new BoundsJsonConverter(),
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
            Error = delegate(object _, ErrorEventArgs args)
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

