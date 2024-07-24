using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RegressionGames.StateRecorder
{
    public static class ScreenshotCapture
    {
        private static RenderTexture _screenShotTexture;

        private static readonly ConcurrentDictionary<int, AsyncGPUReadbackRequest?> GPUReadbackRequests = new();

        // we use a lock on this object to control thread safety of updates and data reads for the lastN frames and completion actions
        private static readonly object SyncLock = new();

        // track the last N frames captured
        private const int MaxTrackedFrames = 5;
        // if a request comes in that is OLDER than one of those last MaxTrackedFrames.. give them the latest one captured, else wait for that next frame
        private static readonly List<(int, (Color32[], int, int, GraphicsFormat)?)> LastNFrames = new(MaxTrackedFrames);

        // if completion actions are requested for a given frame number.. complete them as soon as their frame number finishes, or complete immediately if a later frame has already finished
        private static readonly Dictionary<int, List<Action<(Color32[], int, int, GraphicsFormat)?>>> CompletionActions = new();

        private static (Color32[], int, int, GraphicsFormat)? GetDataForFrame(int frame)
        {
            lock (SyncLock)
            {
                // make sure this frame's request isn't outstanding
                if (!GPUReadbackRequests.ContainsKey(frame))
                {
                    //assumes these are kept numerically ordered by frame
                    foreach (var lastNFrame in LastNFrames)
                    {
                        var lnFrame = lastNFrame.Item1;
                        var lnData = lastNFrame.Item2;
                        if (lnFrame >= frame)
                        {
                            return lnData;
                        }
                    }
                }
            }

            return null;
        }

        private static void AddFrame(int frame, (Color32[], int, int, GraphicsFormat)? data)
        {
            lock (SyncLock)
            {
                var lastNFramesCount = LastNFrames.Count;
                var added = false;
                for (var i = 0; i < lastNFramesCount; i++)
                {
                    var lastNFrame = LastNFrames[i];
                    // keep the frames numerically ordered
                    if (lastNFrame.Item1 > frame)
                    {
                        added = true;
                        LastNFrames.Insert(i,(frame,data));
                        RGDebug.LogDebug($"ScreenshotCapture - Added tracked screenshot for frame # {frame} at index: {i}");
                        break;
                    }
                }

                if (!added)
                {
                    LastNFrames.Add((frame, data));
                    RGDebug.LogDebug($"ScreenshotCapture - Added tracked screenshot for frame # {frame} at index: {LastNFrames.Count-1}");
                }



                if (LastNFrames.Count > MaxTrackedFrames)
                {
                    var oldFrame = LastNFrames[0];
                    LastNFrames.RemoveAt(0);
                    RGDebug.LogDebug($"ScreenshotCapture - Removed old tracked frame # {oldFrame.Item1}");
                }
            }
        }

        private static void HandleCompletedActionCallbacks()
        {
            var gpuRemoveList = new List<int>();
            // wait for all the GPU data to come back
            foreach (var keyValuePair in GPUReadbackRequests)
            {
                if (keyValuePair.Value is { done: true })
                {
                    gpuRemoveList.Add(keyValuePair.Key);
                }
            }

            foreach (var i in gpuRemoveList)
            {
                GPUReadbackRequests.TryRemove(i, out _);
            }

            var toRemoveList = new List<int>();
            // flush and handle any remaining completion actions
            lock (SyncLock)
            {
                foreach (var keyValuePair in CompletionActions)
                {
                    var caFrame = keyValuePair.Key;
                    // make sure this frame's request isn't outstanding
                    if (!GPUReadbackRequests.ContainsKey(caFrame))
                    {
                        var caActions = keyValuePair.Value;
                        if (caActions != null)
                        {
                            //assumes these are kept numerically ordered by frame
                            foreach (var lastNFrame in LastNFrames)
                            {
                                var lnFrame = lastNFrame.Item1;
                                var lnData = lastNFrame.Item2;
                                if (lnFrame == caFrame)
                                {
                                    // found a frame > my frame
                                    foreach (var caAction in caActions)
                                    {
                                        caAction.Invoke(lnData);
                                    }

                                    caActions.Clear();
                                    toRemoveList.Add(caFrame);
                                    break;
                                }
                                else if (lnFrame > caFrame)
                                {
                                    // found a frame > my frame
                                    foreach (var caAction in caActions)
                                    {
                                        caAction.Invoke(lnData);
                                    }

                                    caActions.Clear();
                                    toRemoveList.Add(caFrame);
                                    break;
                                }
                            }
                        }
                    }
                }

                foreach (var i in toRemoveList)
                {
                    CompletionActions.Remove(i);
                }
            }
        }

        public static void WaitForCompletion()
        {
            if (GPUReadbackRequests.Count > 0)
            {
                RGDebug.LogInfo($"ScreenshotCapture - Waiting for " + GPUReadbackRequests.Count + " unfinished GPU Read back requests before stopping");
            }

            // wait for all the GPU data to come back
            foreach (var asyncGPUReadbackRequest in GPUReadbackRequests.Values)
            {
                asyncGPUReadbackRequest?.WaitForCompletion();
            }

            GPUReadbackRequests.Clear();
            lock (SyncLock)
            {
                // flush and handle any remaining completion actions
                foreach (var keyValuePair in CompletionActions)
                {
                    var caFrame = keyValuePair.Key;
                    var caActions = keyValuePair.Value;
                    if (caActions != null)
                    {
                        //assumes these are kept numerically ordered by frame
                        foreach (var lastNFrame in LastNFrames)
                        {
                            var lnFrame = lastNFrame.Item1;
                            var lnData = lastNFrame.Item2;
                            if (lnFrame >= caFrame)
                            {
                                // found a frame >= my frame
                                foreach (var caAction in caActions)
                                {
                                    caAction.Invoke(lnData);
                                }

                                caActions.Clear();
                                break;
                            }
                        }
                    }

                    // didn't find it .. use the last frame we had
                    if (caActions?.Count > 0 && LastNFrames.Count > 0)
                    {
                        foreach (var caAction in caActions)
                        {
                            caAction.Invoke(LastNFrames[^1].Item2);
                        }

                        caActions.Clear();
                    }
                }

                // do this after processing any dangling completion actions
                CompletionActions.Clear();

                // do this after waiting for the gpu buffer to flush
                LastNFrames.Clear();
            }
        }

        /**
         * <summary>Calls the onSuccess callback when the readback request finishes or if data is already available</summary>
         */
        public static void GetCurrentScreenshotWithCallback(long segmentNumber, Action<(Color32[], int, int, GraphicsFormat)?> onCompletion)
        {
            var frame = UnityEngine.Time.frameCount;
            var frameData = GetDataForFrame(frame);
            if (frameData == null)
            {
                UpdateGPUData(frame, onCompletion);
            }
        }

        private static void UpdateGPUData(int frame, Action<(Color32[], int, int, GraphicsFormat)?> onCompletion)
        {
            lock (SyncLock)
            {
                if (onCompletion != null)
                {
                    if (!CompletionActions.TryGetValue(frame, out var caList))
                    {
                        caList = new List<Action<(Color32[], int, int, GraphicsFormat)?>>() { onCompletion };
                        CompletionActions[frame] = caList;
                    }
                    else
                    {
                        caList.Add(onCompletion);
                    }
                }
            }

            // get this in there so we don't get duplicate requests out for the same frame
            if (GPUReadbackRequests.TryAdd(frame, null))
            {
                var screenWidth = Screen.width;
                var screenHeight = Screen.height;

                if (_screenShotTexture == null || _screenShotTexture.width != screenWidth || _screenShotTexture.height != screenHeight)
                {
                    if (_screenShotTexture != null)
                    {
                        Object.Destroy(_screenShotTexture);
                    }

                    _screenShotTexture = new RenderTexture(screenWidth, screenHeight, 0);
                }

                var theGraphicsFormat = _screenShotTexture.graphicsFormat;

                try
                {
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenShotTexture);
                    var readbackRequest = AsyncGPUReadback.Request(_screenShotTexture, 0, GraphicsFormat.R8G8B8A8_SRGB, request =>
                    {
                        if (!request.hasError)
                        {
                            var data = request.GetData<Color32>();
                            var pixels = new Color32[data.Length];
                            var copyBuffer = new Color32[screenWidth];
                            data.CopyTo(pixels);
                            if (SystemInfo.graphicsUVStartsAtTop)
                            {
                                // the pixels from the GPU are upside down, we need to reverse this for it to be right side up
                                var halfHeight = screenHeight / 2;
                                for (var i = 0; i <= halfHeight; i++)
                                {
                                    // swap rows
                                    // bottom row to buffer
                                    Array.Copy(pixels, i * screenWidth, copyBuffer, 0, screenWidth);
                                    // top row to bottom
                                    Array.Copy(pixels, (screenHeight - i - 1) * screenWidth, pixels, i * screenWidth, screenWidth);
                                    // buffer to top row
                                    Array.Copy(copyBuffer, 0, pixels, (screenHeight - i - 1) * screenWidth, screenWidth);
                                }
                            } //else.. we're fine

                            RGDebug.LogDebug($"ScreenshotCapture - Captured screenshot for frame # {frame}");
                            AddFrame(frame, (pixels, screenWidth, screenHeight, theGraphicsFormat));
                        }
                        else
                        {
                            RGDebug.LogWarning($"ScreenshotCapture - Error capturing screenshot for frame # {frame}");
                            AddFrame(frame, null);
                        }
                        HandleCompletedActionCallbacks();
                    });
                    // update from null to the real request
                    GPUReadbackRequests[frame] = readbackRequest;

                }
                catch (Exception e)
                {
                    RGDebug.LogWarning($"ScreenshotCapture - Exception starting to capture screenshot for frame # {frame} - {e.Message}");
                }
            }
        }


        /**
         * <summary>Returns Color32[] array of the pixels IF there is a valid screenshot buffered for this segment/frame.
         * If not buffered, starts a new readback request</summary>
         */
        public static Color32[] GetCurrentScreenshot(long segmentNumber, out int width, out int height, out GraphicsFormat graphicsFormat)
        {
            var frame = UnityEngine.Time.frameCount;

            HandleCompletedActionCallbacks();

            var frameData = GetDataForFrame(frame);
            if (frameData == null)
            {
                UpdateGPUData(frame, null);
            }
            else
            {
                width = frameData.Value.Item2;
                height = frameData.Value.Item3;
                graphicsFormat = frameData.Value.Item4;
                return frameData.Value.Item1;
            }

            width = -1;
            height = -1;
            graphicsFormat = GraphicsFormat.None;
            return null;

        }
    }
}
