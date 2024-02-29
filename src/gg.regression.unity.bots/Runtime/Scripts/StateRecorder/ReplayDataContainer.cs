using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace RegressionGames.StateRecorder
{
    [Serializable]
    public class ReplayKeyFrameEntry
    {
        public long tickNumber;
        public double time;

        /**
         * <summary>the scenes for objects set must match this list (no duplicates allowed in the list)</summary>
         */
        public string[] scenes;
        /**
         * <summary>the ui elements visible must match this list exactly; (could be similar/duplicate paths in the list, need to match value + # of appearances)</summary>
         */
        public string[] uiElements;
        /**
         * <summary>used for mouse input events to confirm that what they clicked on path wise exists; (could be similar/duplicate paths in the list, need to match value + >= # of appearances)</summary>
         */
        public string[] specificObjectPaths;
    }

    [Serializable]
    public abstract class ReplayInputEntry
    {
        public long tickNumber;
        public double startTime;
    }

    [Serializable]
    public class ReplayMouseInputEntry :ReplayInputEntry
    {
        public Vector2Int screenSize;
        public Vector2Int position;

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
    public class ReplayKeyboardInputEntry :ReplayInputEntry
    {
        public string binding;
        public Key key => KeyboardInputActionObserver.AllKeyboardKeys[binding.Substring(binding.LastIndexOf('/')+1)];

        public double? endTime;
        // used to track if we have sent the start and end events for this entry yet
        public bool[] startEndSentFlags = new bool[] {false, false};
        // have we finished processing this input
        public bool IsDone => startEndSentFlags[0] && startEndSentFlags[1];

    }

    public class ReplayDataContainer
    {

        private readonly Queue<ReplayKeyFrameEntry> _keyFrames = new();

        private readonly Queue<ReplayKeyboardInputEntry> _keyboardData = new();

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

        private void ParseReplayZip(string zipFilePath)
        {
            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            ReplayFrameStateData firstFrame = null;
            ReplayFrameStateData priorFrame = null;
            foreach (var entry in entries)
            {

                using var sr = new StreamReader(entry.Open());
                var frameData = JsonConvert.DeserializeObject<ReplayFrameStateData>(sr.ReadToEnd());

                if (firstFrame == null)
                {
                    firstFrame = frameData;
                }

                // process key frame info
                ReplayKeyFrameEntry keyFrame = null;
                if (frameData.keyFrame)
                {
                    keyFrame = new ReplayKeyFrameEntry()
                    {
                        tickNumber = frameData.tickNumber,
                        time = frameData.time - firstFrame.time,
                        scenes = frameData.state.Select(a => a.scene).Distinct().ToArray(),
                        uiElements = frameData.state.Where(a => a.worldSpaceBounds == null).Select(a => a.path).ToArray(),
                    };
                    _keyFrames.Enqueue(keyFrame);
                }

                foreach (var inputData in frameData.inputs.keyboard)
                {
                    if (inputData is KeyboardInputActionData keyboardInputData)
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

                // go through the mouse input data and setup the different entries
                // if they are a new button, add that to the key frame data
                List<string> specificGameObjectPaths = new();

                ReplayMouseInputEntry priorMouseInput = null;

                foreach (var inputData in frameData.inputs.mouse)
                {
                    if (inputData is MouseInputActionData mouseInputData)
                    {
                        List<String> clickedOnObjectPaths = null;
                        if (keyFrame != null && mouseInputData.newButtonPress)
                        {
                            clickedOnObjectPaths = FindObjectPathsAtPosition(mouseInputData.position, priorFrame?.state, frameData.state);

                            // we need the mouse data to have the prior and next frame objects as possibilities
                            // BUT.. we need to do enforcement on only the new frame data
                            var clickedPathsInCurrentFrame = FindObjectPathsAtPosition(mouseInputData.position, null, frameData.state);
                            foreach (var clickedOnObject in clickedPathsInCurrentFrame)
                            {
                                if (clickedOnObject.StartsWith("RGOverlay"))
                                {
                                    // this was a click on RG.. ignore it in the replay
                                    specificGameObjectPaths.Clear();
                                    break;
                                }
                                specificGameObjectPaths.Add(clickedOnObject);
                            }
                        }

                        priorMouseInput = new ReplayMouseInputEntry()
                        {
                            tickNumber = frameData.tickNumber,
                            screenSize = frameData.screenSize,
                            startTime = mouseInputData.startTime - firstFrame.time,
                            clickedObjectPaths = clickedOnObjectPaths != null ? clickedOnObjectPaths.ToArray() : Array.Empty<string>(),
                            position = mouseInputData.position,
                            leftButton = mouseInputData.leftButton,
                            middleButton = mouseInputData.middleButton,
                            rightButton = mouseInputData.rightButton,
                            forwardButton = mouseInputData.forwardButton,
                            backButton = mouseInputData.backButton,
                            scroll = mouseInputData.scroll
                        };

                        _mouseData.Enqueue(priorMouseInput);
                    }
                }

                if (keyFrame != null)
                {
                    keyFrame.specificObjectPaths = specificGameObjectPaths.ToArray();
                }

                priorFrame = frameData;
            }

            if (firstFrame == null)
            {
                // entries was empty
                throw new Exception("Error parsing replay .zip.  Must include at least 1 frame json/jpg pair.");
            }



        }

        private List<string> FindObjectPathsAtPosition(Vector2 position, IEnumerable<ReplayGameObjectState> priorState, IEnumerable<ReplayGameObjectState> state)
        {
            // collect the set of possible matches from the current and prior frame
            // why both.. well.. the click action will be on the current frame... that it caused
            // but the object it clicked on may no longer exist because it caused the very state change we're recording
            // so we get the entries from the current frame, and if those are in the prior frame also.. we prioritize their current frame bounds/location
            // CONSIDER: Another way to do this is to capture the paths clicked in the state at each click based on the prior frame.. but that saves a ton more state an affects their runtime
            // by having to process the state on every frame and click

            // make sure screen space position Z is around 0
            var currentStateEntries = state.ToDictionary(a=>a.id, a=> a.screenSpaceBounds.Contains(new Vector3(position.x, position.y, 0))?a:null);
            var priorStateEntries = priorState?.Where(a => !currentStateEntries.ContainsKey(a.id) && a.screenSpaceBounds.Contains(new Vector3(position.x, position.y, 0))).ToList();
            if (priorStateEntries != null)
            {
                priorStateEntries.AddRange(currentStateEntries.Values.Where(a => a != null));
                return priorStateEntries.Select(a=>a.path).ToList();
            }
            //else
            return currentStateEntries.Values.Where(a => a != null).Select(a => a.path).ToList();
        }
    }
}
