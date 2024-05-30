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
         * <summary>the ui elements visible must match this list exactly; (could be similar/duplicate paths in the list, need to match value + # of appearances)
         * First argument is the hashCode of the path to speed up dictionary evaluation of key match vs using strings</summary>
         */
        public Dictionary<int, (string,int)> uiElementsCounts;
        public Dictionary<int, StateElementDeltaType> uiElementsDeltas;


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
        public string[] clickedObjectNormalizedPaths;

        public bool IsDone;

        // gives the position relative to the current screen size
        public Vector2Int NormalizedPosition => new()
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
        private readonly List<ReplayKeyFrameEntry> _keyFrames = new();
        private int _keyFrameIndex = 0;
        private readonly List<ReplayKeyboardInputEntry> _keyboardData = new();
        private int _keyboardIndex = 0;
        private readonly List<ReplayMouseInputEntry> _mouseData = new();
        private int _mouseIndex = 0;

        public string SessionId { get; private set; }
        public bool IsShiftDown;

        private readonly Dictionary<string, ReplayKeyboardInputEntry> _pendingEndKeyboardInputs = new();


        public ReplayDataContainer(string zipFilePath)
        {
            ParseReplayZip(zipFilePath);
        }

        public void Reset()
        {
            // sets indexes back to 0
            _keyFrameIndex = 0;
            _keyboardIndex = 0;
            _mouseIndex = 0;

            IsShiftDown = false;
            _pendingEndKeyboardInputs.Clear();

            // reset all the tracking flags
            foreach (var replayKeyFrameEntry in _keyFrames)
            {
                replayKeyFrameEntry.uiMatched = false;
                replayKeyFrameEntry.gameMatched = false;
            }

            foreach (var replayKeyboardInputEntry in _keyboardData)
            {
                replayKeyboardInputEntry.startEndSentFlags[0] = false;
                replayKeyboardInputEntry.startEndSentFlags[1] = false;
            }

            foreach (var replayMouseInputEntry in _mouseData)
            {
                replayMouseInputEntry.IsDone = false;
            }
        }

        public List<ReplayMouseInputEntry> DequeueMouseInputsUpToTime(double? time = null)
        {
            List<ReplayMouseInputEntry> output = new();
            var mouseCount = _mouseData.Count;
            while (_mouseIndex < mouseCount)
            {
                var item = _mouseData[_mouseIndex];
                if (time == null || item.startTime < time)
                {
                    output.Add(item);
                    // 'remove' the one we just peeked by updating our index
                    ++_mouseIndex;
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
            var keyboardCount = _keyboardData.Count;
            while (_keyboardIndex < keyboardCount)
            {
                var item = _keyboardData[_keyboardIndex];
                if (time == null || item.startTime < time)
                {
                    output.Add(item);
                    // 'remove' the one we just peeked by updating our index
                    ++_keyboardIndex;
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
            if (_keyFrameIndex < _keyFrames.Count)
            {
                return _keyFrames[_keyFrameIndex++];
            }

            return null;
        }

        public ReplayKeyFrameEntry PeekKeyFrame()
        {
            if (_keyFrameIndex < _keyFrames.Count)
            {
                // do not updated index
                return _keyFrames[_keyFrameIndex];
            }

            return null;
        }

        public void ParseReplayZip(string zipFilePath)
        {
            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            ReplayFrameStateData firstFrame = null;
            ReplayFrameStateData priorFrame = null;

            ReplayKeyFrameEntry priorKeyFrame = null;
            foreach (var entry in entries)
            {
                using var sr = new StreamReader(entry.Open());
                var frameData = JsonConvert.DeserializeObject<ReplayFrameStateData>(sr.ReadToEnd());

                firstFrame ??= frameData;

                if (SessionId == null)
                {
                    SessionId = frameData.sessionId;
                }

                // process key frame info
                ReplayKeyFrameEntry keyFrame = null;
                if (frameData.keyFrame.Length > 0)
                {
                    var gameElements = new List<(string,int,int,int)>();
                    var uiScenes = new HashSet<string>();
                    var gameScenes = new HashSet<string>();

                    var uiElementsCounts = new Dictionary<int, (string,int)>();

                    foreach (var replayGameObjectState in frameData.state)
                    {
                        if (replayGameObjectState.worldSpaceBounds == null)
                        {
                            uiScenes.Add(replayGameObjectState.scene);

                            var hashCode = replayGameObjectState.path.GetHashCode();

                            if (uiElementsCounts.TryGetValue(hashCode, out var val))
                            {
                                uiElementsCounts[hashCode] = (val.Item1, val.Item2 + 1);
                            }
                            else
                            {
                                uiElementsCounts[hashCode] = (replayGameObjectState.path, 1);
                            }
                        }
                        else
                        {
                            gameElements.Add((replayGameObjectState.path, replayGameObjectState.rendererCount, replayGameObjectState.colliders.Count, replayGameObjectState.rigidbodies.Count));
                            gameScenes.Add(replayGameObjectState.scene);
                        }
                    }

                    var uiElementsDeltas = new Dictionary<int, StateElementDeltaType>();

                    if (priorKeyFrame != null)
                    {
                        foreach (var elementsCount in uiElementsCounts)
                        {
                            var hashCode = elementsCount.Key.GetHashCode();
                            if (priorKeyFrame.uiElementsCounts.TryGetValue(hashCode, out var counts))
                            {
                                // this entry was in the prior frame
                                if (counts.Item2 > elementsCount.Value.Item2)
                                {
                                    uiElementsDeltas[hashCode] = StateElementDeltaType.Decreased;
                                }
                                else if (counts.Item2 < elementsCount.Value.Item2)
                                {
                                    uiElementsDeltas[hashCode] = StateElementDeltaType.Increased;
                                }
                                else
                                {
                                    uiElementsDeltas[hashCode] = StateElementDeltaType.NonZero;
                                }
                            }
                            else
                            {
                                // this entry wasn't in the prior frame
                                uiElementsDeltas[elementsCount.Key] = StateElementDeltaType.NonZero;
                            }
                            // now check for things that went away
                            foreach (var keyValuePair in priorKeyFrame.uiElementsCounts)
                            {
                                // went to zero
                                uiElementsDeltas.TryAdd(keyValuePair.Key, StateElementDeltaType.Zero);
                            }
                        }

                    }
                    else
                    {
                        foreach (var uiElementsCount in uiElementsCounts)
                        {
                            uiElementsDeltas[uiElementsCount.Key] = StateElementDeltaType.NonZero;
                        }
                    }

                    keyFrame = new ReplayKeyFrameEntry()
                    {
                        tickNumber = frameData.tickNumber,
                        pixelHash = frameData.pixelHash,
                        keyFrameTypes = frameData.keyFrame,
                        time = frameData.time - firstFrame.time,
                        uiScenes = uiScenes.ToArray(),
                        gameScenes = gameScenes.ToArray(),
                        gameElements = gameElements.ToArray(),
                        uiElementsCounts = uiElementsCounts,
                        uiElementsDeltas = uiElementsDeltas
                    };
                    _keyFrames.Add(keyFrame);
                    priorKeyFrame = keyFrame;
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
                            _keyboardData.Add(theData);

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
                        string[] specificGameObjectPaths = null;

                        // we also validate the object ids on mouse release to adjust click positions
                        if (keyFrame != null && mouseInputData.clickedObjectNormalizedPaths != null )
                        {
                            specificGameObjectPaths = mouseInputData.clickedObjectNormalizedPaths;
                        }

                        _mouseData.Add(new ReplayMouseInputEntry()
                        {
                            tickNumber = frameData.tickNumber,
                            screenSize = frameData.screenSize,
                            startTime = mouseInputData.startTime - firstFrame.time,
                            clickedObjectNormalizedPaths = specificGameObjectPaths ?? Array.Empty<string>(),
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

                priorFrame = frameData;
            }

            if (firstFrame == null)
            {
                // entries was empty
                throw new Exception("Error parsing replay .zip.  Must include at least 1 frame json/jpg pair.");
            }
        }

        private (List<string>,List<string>) FindObjectPathsWithIds(int[] objectIds, List<ReplayGameObjectState> priorState, List<ReplayGameObjectState> state)
        {
            // look in current state first, then fall back to prior state
            // copy me
            var objectIdsToFind = objectIds.ToList();
            List<string> objectPathsFound = new();
            List<string> objectPathsNormalizedFound = new();

            var stateCount = state.Count;
            for (var i = 0; i < stateCount; i++)
            {
                var so = state[i];
                if (StateRecorderUtils.OptimizedRemoveIntFromList(objectIdsToFind, so.id))
                {
                    objectPathsFound.Add(so.path);
                    objectPathsNormalizedFound.Add(so.normalizedPath);
                }
            }

            if (objectIdsToFind.Count > 0 && priorState != null)
            {
                stateCount = priorState.Count;
                for (var i = 0; i < stateCount; i++)
                {
                    var so = priorState[i];
                    if (StateRecorderUtils.OptimizedRemoveIntFromList(objectIdsToFind, so.id))
                    {
                        objectPathsFound.Add(so.path);
                        objectPathsNormalizedFound.Add(so.normalizedPath);
                    }
                }
            }
            return (objectPathsFound, objectPathsNormalizedFound);
        }
    }
}
