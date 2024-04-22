using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using StateRecorder;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{
    [Serializable]
    public class ReplayKeyFrameEntry
    {
        public long tickNumber;
        public double time;

        public KeyFrameType[] keyFrameTypes;

        /**
         * <summary>Have the UI element/scene related conditions for this key frame been met</summary>
         */
        public bool uiMatched;

        /**
         * <summary>Have the game element/scene related conditions for this key frame been met</summary>
         */
        public bool gameMatched;

        public bool IsMatched => uiMatched && gameMatched;

        /**
         * <summary>the scenes for ui elements must match this list (no duplicates allowed in the list)</summary>
         */
        public string[] uiScenes;

        /**
         * <summary>the ui elements visible must match this list exactly; (could be similar/duplicate paths in the list, need to match value + # of appearances)</summary>
         */
        public string[] uiElements;


        /**
         * <summary>the scenes for game elements must match this list (no duplicates allowed in the list)</summary>
         */
        public string[] gameScenes;

        /**
         * <summary>the in game elements visible must include everything in this list; (could be similar/duplicate paths in the list, need to match value + >= # of appearances)
         * This will also try to match the number of renderers and colliders for each object</summary>
         */
        // (path, #renderers, #colliders, #rigidbodies)
        public (string, int,int,int)[] gameElements;

        /**
         * <summary>Hash value of the pixels on screen. (Used for GameFace or other objectless UI systems)</summary>
         */
        public string pixelHash;
    }

    [Serializable]
    public abstract class ReplayInputEntry
    {
        public long tickNumber;
        public double startTime;
    }

    [Serializable]
    public class ReplayMouseInputEntry : ReplayInputEntry
    {
        public Vector2Int screenSize;
        public Vector2Int position;

        public Vector3? worldPosition;

        //main 5 buttons
        public bool leftButton;
        public bool middleButton;
        public bool rightButton;
        public bool forwardButton;
        public bool backButton;

        // scroll wheel
        public Vector2 scroll;
        public string[] clickedObjectPaths;
        public bool IsDone;

        // gives the position relative to the current screen size
        public Vector2 NormalizedPosition => new()
        {
            x = (int)(position.x * (Screen.width / (float)screenSize.x)),
            y = (int)(position.y * (Screen.height / (float)screenSize.y))
        };
    }

    [Serializable]
    public class ReplayKeyboardInputEntry : ReplayInputEntry
    {
        public string binding;
        public Key key => KeyboardInputActionObserver.AllKeyboardKeys[binding.Substring(binding.LastIndexOf('/') + 1)];

        public double? endTime;

        // used to track if we have sent the start and end events for this entry yet
        public bool[] startEndSentFlags = new bool[] { false, false };

        // have we finished processing this input
        public bool IsDone => startEndSentFlags[0] && startEndSentFlags[1];
    }

    public class ReplayDataContainer
    {
        private readonly Queue<ReplayKeyFrameEntry> _keyFrames = new();

        private readonly Queue<ReplayKeyboardInputEntry> _keyboardData = new();

        public bool IsShiftDown = false;

        private readonly Dictionary<string, ReplayKeyboardInputEntry> _pendingEndKeyboardInputs = new();

        private readonly Queue<ReplayMouseInputEntry> _mouseData = new();

        public ReplayDataContainer(string zipFilePath)
        {
            ParseReplayZip(zipFilePath);
        }

        public List<ReplayMouseInputEntry> DequeueMouseInputsUpToTime(double? time = null)
        {
            List<ReplayMouseInputEntry> output = new();
            while (_mouseData.TryPeek(out var item))
            {
                if (time == null || item.startTime < time)
                {
                    output.Add(item);
                    // remove the one we just peeked
                    _mouseData.Dequeue();
                }
                else
                {
                    // hit the end of times before the limit
                    break;
                }
            }

            return output;
        }

        public List<ReplayKeyboardInputEntry> DequeueKeyboardInputsUpToTime(double? time = null)
        {
            List<ReplayKeyboardInputEntry> output = new();
            while (_keyboardData.TryPeek(out var item))
            {
                if (time == null || item.startTime < time)
                {
                    output.Add(item);
                    // remove the one we just peeked
                    _keyboardData.Dequeue();
                }
                else
                {
                    // hit the end of times before the limit
                    break;
                }
            }

            return output;
        }

        public ReplayKeyFrameEntry DequeueKeyFrame()
        {
            if (_keyFrames.TryDequeue(out var result))
            {
                return result;
            }

            return null;
        }

        public ReplayKeyFrameEntry PeekKeyFrame()
        {
            if (_keyFrames.TryPeek(out var result))
            {
                return result;
            }

            return null;
        }

        public void ParseReplayZip(string zipFilePath)
        {
            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            ReplayFrameStateData firstFrame = null;
            foreach (var entry in entries)
            {
                using var sr = new StreamReader(entry.Open());
                var frameData = JsonConvert.DeserializeObject<ReplayFrameStateData>(sr.ReadToEnd());

                firstFrame ??= frameData;

                // process key frame info
                ReplayKeyFrameEntry keyFrame = null;
                if (frameData.keyFrame.Length > 0)
                {
                    var uiElements = new List<string>();
                    var gameElements = new List<(string,int,int,int)>();
                    var uiScenes = new HashSet<string>();
                    var gameScenes = new HashSet<string>();
                    foreach (var replayGameObjectState in frameData.state)
                    {
                        if (replayGameObjectState.worldSpaceBounds == null)
                        {
                            uiElements.Add(replayGameObjectState.path);
                            uiScenes.Add(replayGameObjectState.scene);
                        }
                        else
                        {
                            gameElements.Add((replayGameObjectState.path, replayGameObjectState.rendererCount, replayGameObjectState.colliders.Count, replayGameObjectState.rigidbodies.Count));
                            gameScenes.Add(replayGameObjectState.scene);
                        }
                    }
                    keyFrame = new ReplayKeyFrameEntry()
                    {
                        tickNumber = frameData.tickNumber,
                        pixelHash = frameData.pixelHash,
                        keyFrameTypes = frameData.keyFrame,
                        time = frameData.time - firstFrame.time,
                        uiScenes = uiScenes.ToArray(),
                        uiElements = uiElements.ToArray(),
                        gameScenes = gameScenes.ToArray(),
                        gameElements = gameElements.ToArray()
                    };
                    _keyFrames.Enqueue(keyFrame);
                }

                foreach (var inputData in frameData.inputs.keyboard)
                {
                    if (inputData is { } keyboardInputData)
                    {
                        if (_pendingEndKeyboardInputs.TryGetValue(keyboardInputData.binding, out var theData))
                        {
                            if (keyboardInputData.endTime != null)
                            {
                                theData.endTime = keyboardInputData.endTime - firstFrame.time;
                                _pendingEndKeyboardInputs.Remove(theData.binding);
                            }
                        }
                        else
                        {
                            theData = new ReplayKeyboardInputEntry()
                            {
                                tickNumber = frameData.tickNumber,
                                startTime = keyboardInputData.startTime - firstFrame.time,
                                endTime = keyboardInputData.endTime - firstFrame.time,
                                binding = keyboardInputData.binding
                            };

                            // we put this in the queue by its encounter position/start time
                            // we track pending ones if endtime not encountered yet
                            _keyboardData.Enqueue(theData);

                            if (theData.endTime == null)
                            {
                                _pendingEndKeyboardInputs[theData.binding] = theData;
                            }
                        }
                    }
                }


                foreach (var inputData in frameData.inputs.mouse)
                {
                    if (inputData is { } mouseInputData)
                    {
                        // go through the mouse input data and setup the different entries
                        string[] specificGameObjectPaths = Array.Empty<string>();

                        // we also validate the object ids on mouse release to adjust click positions
                        if (keyFrame != null && mouseInputData.clickedObjectIds != null )
                        {
                            specificGameObjectPaths = FindObjectsWithIds(mouseInputData.clickedObjectIds, frameData.state).ToArray();
                        }

                        _mouseData.Enqueue(new ReplayMouseInputEntry()
                        {
                            tickNumber = frameData.tickNumber,
                            screenSize = frameData.screenSize,
                            startTime = mouseInputData.startTime - firstFrame.time,
                            clickedObjectPaths = specificGameObjectPaths,
                            position = mouseInputData.position,
                            worldPosition = mouseInputData.worldPosition,
                            leftButton = mouseInputData.leftButton,
                            middleButton = mouseInputData.middleButton,
                            rightButton = mouseInputData.rightButton,
                            forwardButton = mouseInputData.forwardButton,
                            backButton = mouseInputData.backButton,
                            scroll = mouseInputData.scroll
                        });
                    }
                }
            }

            if (firstFrame == null)
            {
                // entries was empty
                throw new Exception("Error parsing replay .zip.  Must include at least 1 frame json/jpg pair.");
            }
        }

        private IEnumerable<string> FindObjectsWithIds(int[] objectIds, List<ReplayGameObjectState> state)
        {
            var currentStateEntries = state.Where(a => objectIds.Contains(a.id)).Select(a => a.path);
            return currentStateEntries;
        }
    }
}
