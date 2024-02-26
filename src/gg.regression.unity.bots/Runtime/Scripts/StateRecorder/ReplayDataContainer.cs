using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{
    [Serializable]
    public class ReplayKeyFrameEntry
    {
        public double time;

        // the scenes for objects set must match this list (no duplicates allowed in the list)
        public string[] scenes;
        // the ui elements visible must match this list exactly (could be similar/duplicate paths in the list, need to match value + # of appearances)
        public string[] uiElements;
        // used for mouse input events to confirm that what they clicked on path wise exists
        public string[] specificObjectPaths;
    }

    public abstract class ReplayInputEntry
    {
        public double startTime;
        public double? endTime;
    }

    [Serializable]
    public class ReplayMouseInputEntry :ReplayInputEntry
    {
        // non-fractional pixel accuracy
        public int[] position;
        public bool[] leftMiddleRightForwardBackButton;
        // scroll wheel
        public bool[] scrollDownUpLeftRight;
        public string[] clickedObjectPaths;
    }

    [Serializable]
    public class ReplayKeyboardInputEntry :ReplayInputEntry
    {
        public string binding;
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

        public List<ReplayMouseInputEntry> DequeueMouseInputsUpToTime(double time)
        {
            List<ReplayMouseInputEntry> output = new();
            while (_mouseData.TryPeek(out var item))
            {
                if (item.startTime < time)
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

        public List<ReplayKeyboardInputEntry> DequeueKeyboardInputsUpToTime(double time)
        {
            List<ReplayKeyboardInputEntry> output = new();
            while (_keyboardData.TryPeek(out var item))
            {
                if (item.startTime < time)
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


        private void ParseReplayZip(string zipFilePath)
        {
            using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Read);

            // sort by numeric value of entries (not string comparison of filenames)
            var entries = zipArchive.Entries.Where(e => e.Name.EndsWith(".json")).OrderBy(e => int.Parse(e.Name.Substring(0, e.Name.IndexOf('.'))));

            FrameStateData firstFrame = null;
            foreach (var entry in entries)
            {

                using var sr = new StreamReader(entry.Open());
                var frameData = JsonConvert.DeserializeObject<FrameStateData>(sr.ReadToEnd());

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
                        time = firstFrame.time - frameData.time,
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
                                theData.endTime = keyboardInputData.endTime;
                                _pendingEndKeyboardInputs.Remove(theData.binding);
                            }
                        }
                        else
                        {
                            theData = new ReplayKeyboardInputEntry()
                            {
                                startTime = keyboardInputData.startTime,
                                endTime = keyboardInputData.endTime,
                                binding = keyboardInputData.binding
                            };

                            // we put this in the queue by its encounter position/start time
                            // we track pending ones if endtime not encountered yet
                            this._keyboardData.Enqueue(theData);

                            if (theData.endTime == null)
                            {
                                _pendingEndKeyboardInputs[theData.binding] = theData;
                            }
                        }

                    }
                }

                // go through the mouse input data and setup the different entries
                // if they are a new button, add that to the key frame data
                HashSet<string> specificGameObjects = new();

                ReplayMouseInputEntry priorMouseInput = null;

                foreach (var inputData in frameData.inputs.mouse)
                {
                    if (inputData is MouseInputActionData mouseInputData)
                    {
                        List<String> clickedOnObjects = null;
                        if (keyFrame != null && mouseInputData.newButtonPress)
                        {
                            clickedOnObjects = FindObjectsAtPosition(mouseInputData.position, frameData.state);
                            foreach (var clickedOnObject in clickedOnObjects)
                            {
                                specificGameObjects.Add(clickedOnObject);
                            }
                        }

                        if (priorMouseInput != null && (mouseInputData.newButtonPress || !mouseInputData.IsButtonHeld))
                        {
                            priorMouseInput.endTime = mouseInputData.startTime;
                        }

                        priorMouseInput = new ReplayMouseInputEntry()
                        {
                            startTime = mouseInputData.startTime,
                            clickedObjectPaths = clickedOnObjects != null ? clickedOnObjects.ToArray() : Array.Empty<string>(),
                            leftMiddleRightForwardBackButton = mouseInputData.leftMiddleRightForwardBackButton,
                            scrollDownUpLeftRight = mouseInputData.scrollDownUpLeftRight
                        };

                        _mouseData.Enqueue(priorMouseInput);
                    }
                }

                if (keyFrame != null)
                {
                    keyFrame.specificObjectPaths = specificGameObjects.ToArray();
                }
            }

            if (firstFrame == null)
            {
                // entries was empty
                throw new Exception("Error parsing replay .zip.  Must include at least 1 frame json/jpg pair.");
            }

        }

        private List<string> FindObjectsAtPosition(IReadOnlyList<int> position, IEnumerable<RenderableGameObjectState> state)
        {
            var point = new Vector3(position[0], position[1]);
            return state.Where(a => a.screenSpaceBounds.Contains(point)).Select(a => a.path).ToList();
        }
    }
}
