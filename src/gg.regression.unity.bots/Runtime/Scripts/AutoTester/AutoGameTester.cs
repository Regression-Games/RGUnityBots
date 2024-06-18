using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;

namespace RegressionGames.AutoTester
{
    public class AutoGameTester : MonoBehaviour
    {
        // Maybe expose this as a field.. maybe ??
        private float actionIntervalSeconds = 0.3f;

        private bool _running;

        private AutoTestMetadata _autoTestMetadata;

        public void Start()
        {
            LoadAutoTestMetadata();
        }

        private void LoadAutoTestMetadata()
        {
            var path = $"{Application.persistentDataPath}/autotest_metadata.json";
            if (File.Exists(path))
            {
                var data = File.ReadAllText(path);
                _autoTestMetadata = JsonConvert.DeserializeObject<AutoTestMetadata>(data);
            }
            else
            {
                _autoTestMetadata = new AutoTestMetadata();
            }
        }

        public void AutoTestGame()
        {
            _running = !_running;
        }

        private TransformStatus _lastObjectClicked = null;

        private MouseAction _lastMouseAction = null;

        private float _lastTimeStateChanged = -1;

        public void Update()
        {
            if (_running)
            {
                if (_lastObjectClicked != null)
                {
                    // do this here, so that hopefully if we exited the program, we don't record that as a 'good' action to take
                    var uiTransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
                    var gameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();

                    //Compute the delta values we need to record / evaluate to know if we need to record a key frame
                    var uiDeltas = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(uiTransforms.Item1, uiTransforms.Item2, out var hasUIDelta);
                    var gameObjectDeltas = InGameObjectFinder.GetInstance().ComputeNormalizedPathBasedDeltaCounts(gameObjectTransforms.Item1, gameObjectTransforms.Item2, out var hasGameObjectDelta);

                    // AND.. check that a keyframe condition happened based on that action
                    var keyFrameTypes = ScreenRecorder.GetKeyFrameType(false, hasUIDelta, hasGameObjectDelta, null);
                    if (keyFrameTypes.Count > 0)
                    {
                        _lastTimeStateChanged = Time.unscaledTime;
                        _autoTestMetadata.actionableObjectPaths[_lastObjectClicked.NormalizedPath] = _lastMouseAction;
                    }
                }

                if (Time.unscaledTime - _lastTimeStateChanged > 60f)
                {
                    // if we went 1 minute without a state update
                    // reload the application FORCEFULLY
                    RGDebug.LogWarning("AutoTester - Went 1 minute without an action resulting in a state change... stopping game");
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }

                var currentUITransforms = InGameObjectFinder.GetInstance().GetUITransformsForCurrentFrame();
                var currentGameObjectTransforms = InGameObjectFinder.GetInstance().GetGameObjectTransformsForCurrentFrame();
                _lastObjectClicked = ClickOnRandomObject(currentUITransforms.Item2, currentGameObjectTransforms.Item2, out _lastMouseAction);
            }
        }

        private float LastActionTime = float.MinValue;

        private TransformStatus ClickOnRandomObject(Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms, out MouseAction mouseAction)
        {
            var now = Time.unscaledTime;
            if (now - actionIntervalSeconds > LastActionTime)
            {
                var screenHeight = Screen.height;
                var screenWidth = Screen.width;

                List<TransformStatus> possibleTransformsToClick;

                // 67% chance of choosing a known actionable path, 33% chance of exploration with random action
                var randomAction = Random.Range(0,3) == 0;

                if (Time.unscaledTime - _lastTimeStateChanged > 10f)
                {
                    // if more than 10 seconds since a good action, go all random
                    randomAction = true;
                }

                var excludedNormalizedPaths = new List<string>();
                excludedNormalizedPaths.Add("RGOverlayCanvasV2");
                excludedNormalizedPaths.AddRange(_autoTestMetadata.exitGameObjectPaths);

                // normalized to 1024x768 for excluding the RGOverlayCanvasV2
                var excludedAreas = new List<RectInt>() { new RectInt(920, 0, 104, 34) };
                var screenSize = new Vector2Int(1024, 768);

                if (randomAction)
                {
                    // pick randomly either UI or gameObject
                    var uiOrGameObject = Random.Range(0, 2) > 0;

                    // pick a random action
                    if (currentGameObjectTransforms.Count == 0 || (uiOrGameObject && currentUITransforms.Count > 0))
                    {
                        possibleTransformsToClick = currentUITransforms.Values.Where(a => a.Transform != null && a.screenSpaceBounds != null).ToList();
                    }
                    else if (currentGameObjectTransforms.Count > 0)
                    {
                        possibleTransformsToClick = currentGameObjectTransforms.Values.Where(a => a.Transform != null && a.screenSpaceBounds != null).ToList();
                    }
                    else
                    {
                        possibleTransformsToClick = new();
                    }
                }
                else
                {
                    // pick from the known good actions
                    if (currentUITransforms.Count > 0)
                    {
                        possibleTransformsToClick = currentUITransforms.Values.Where(a => a.Transform != null && a.screenSpaceBounds != null && _autoTestMetadata.actionableObjectPaths.Keys.Contains(a.NormalizedPath)).ToList();
                    }
                    else
                    {
                        possibleTransformsToClick = new();
                    }

                    if (currentGameObjectTransforms.Count > 0)
                    {
                        possibleTransformsToClick.AddRange(currentGameObjectTransforms.Values.Where(a => a.Transform != null && a.screenSpaceBounds != null && _autoTestMetadata.actionableObjectPaths.Keys.Contains(a.NormalizedPath)));
                    }
                }

                var possibleTransformsCount = possibleTransformsToClick.Count;
                if (possibleTransformsCount > 0)
                {
                    var RESTART_LIMIT = 20;
                    var restartCount = 0;
                    // try up to 20 times per frame to find a valid click object, but after that, give up till the next frame as none may be available
                    // helps prevent long searches, or cases where the user excludes the whole screen
                    do
                    {
                        var transformOption = possibleTransformsToClick[Random.Range(0, possibleTransformsCount)];
                        if (StateRecorderUtils.OptimizedStringStartsWithStringInList(excludedNormalizedPaths, transformOption.NormalizedPath))
                        {
                            // not allowed object .. pick another
                            continue; // while
                        }

                        var ssbCenter = transformOption.screenSpaceBounds.Value.center;
                        var x = (int)ssbCenter.x;
                        var y = (int)ssbCenter.y;

                        var valid = true;
                        var count = excludedAreas.Count;
                        for (var i = 0; i < count; i++)
                        {
                            var excludedArea = excludedAreas[i];
                            var xMin = excludedArea.xMin;
                            var xMax = excludedArea.xMax;
                            var yMin = excludedArea.yMin;
                            var yMax = excludedArea.yMax;

                            // normalize the location to the current resolution
                            if (screenWidth != screenSize.x)
                            {
                                var ratio = screenWidth / (float)screenSize.x;
                                xMin = (int) (xMin * ratio);
                                xMax = (int) (xMax * ratio);
                            }

                            if (screenHeight != screenSize.y)
                            {
                                var ratio = screenHeight / (float)screenSize.y;
                                yMin = (int) (yMin * ratio);
                                yMax = (int) (yMax * ratio);
                            }

                            if (x >= xMin && x <= xMax)
                            {
                                valid = false;
                                break; // for
                            }

                            if (y >= yMin && y <= yMax)
                            {
                                valid = false;
                                break; // for
                            }
                        }

                        if (valid)
                        {
                            // 20% of random mouse action, 80% of common mouse action
                            var useRandomMouseAction = Random.Range(0, 5) == 0;
                            if (useRandomMouseAction)
                            {
                                var lb = Random.Range(0, 2) == 0;
                                var mb = Random.Range(0, 2) == 0;
                                var rb = Random.Range(0, 2) == 0;
                                var fb = Random.Range(0, 2) == 0;
                                var bb = Random.Range(0, 2) == 0;
                                mouseAction = new MouseAction(lb, mb, rb, fb, bb);
                            }
                            else
                            {
                                var index = Random.Range(0, MouseAction.CommonGameMouseActions.Count);
                                mouseAction = MouseAction.CommonGameMouseActions[index];
                            }

                            RGDebug.LogInfo($"AutoTester - {{x:{x}, y:{y}, lb:{(mouseAction.leftButton ? 1 : 0)}, mb:{(mouseAction.middleButton ? 1 : 0)}, rb:{(mouseAction.rightButton ? 1 : 0)}, fb:{(mouseAction.forwardButton ? 1 : 0)}, bb:{(mouseAction.backButton ? 1 : 0)}}} on object with NormalizedPath: {transformOption.NormalizedPath}", transformOption.Transform.gameObject);

                            MouseEventSender.SendRawPositionMouseEvent(
                                0,
                                new Vector2(x, y),
                                mouseAction.leftButton,
                                mouseAction.middleButton,
                                mouseAction.rightButton,
                                mouseAction.forwardButton,
                                mouseAction.backButton,
                                Vector2.zero // don't support random scrolling yet...
                            );

                            //var shouldUnclick = Random.Range(0, 2) == 0; // TODO: Implement this later
                            var shouldUnclick = true;
                            if (shouldUnclick)
                            {
                                RGDebug.LogInfo($"AutoTester - unclick - {{x:{x}, y:{y}}}");
                                // send the un-click (no drags here)
                                MouseEventSender.SendRawPositionMouseEvent(
                                    0,
                                    new Vector2(x, y),
                                    false,
                                    false,
                                    false,
                                    false,
                                    false,
                                    Vector2.zero // don't support random scrolling yet...
                                );
                            }

                            LastActionTime = now;
                            return transformOption;
                        }

                    } while (++restartCount < RESTART_LIMIT);
                }
            }

            mouseAction = null;
            return null;
        }

        public void OnApplicationQuit()
        {
            // Record the last action taken so we know that is 'bad' to click on
            if (_lastObjectClicked != null)
            {
                RecordExitGameObject(_lastObjectClicked);
            }
        }

        private void RecordExitGameObject(TransformStatus exitGameObject)
        {
            RGDebug.LogWarning("AutoTester - Exiting game because of click on object path: " + exitGameObject.NormalizedPath);
            _autoTestMetadata.exitGameObjectPaths.Add(exitGameObject.NormalizedPath);
            var jsonData = Encoding.UTF8.GetBytes(
                _autoTestMetadata.ToJsonString()
            );
            RecordJson(Application.persistentDataPath, jsonData);
        }

        public void OnDestroy()
        {
            if (_autoTestMetadata != null)
            {
                var jsonData = Encoding.UTF8.GetBytes(
                    _autoTestMetadata.ToJsonString()
                );
                RecordJson(Application.persistentDataPath, jsonData);
            }
        }

        private void RecordJson(string directoryPath, byte[] jsonData)
        {
            try
            {
                // write out the json to file
                var path = $"{directoryPath}/autotest_metadata.json";
                if (jsonData.Length == 0)
                {
                    RGDebug.LogError($"AutoTester - ERROR: Empty autotest_metadata JSON");
                }

                // Save the byte array as a file
                File.WriteAllBytes(path, jsonData);
                RGDebug.LogInfo("AutoTester - Wrote autotest_metadata JSON to path: " + path);
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"AutoTester - ERROR: Unable to record autotest_metadata JSON");
            }
        }
    }

    public class AutoTestMetadataJsonConverter: Newtonsoft.Json.JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            AutoTestMetadata data = new AutoTestMetadata();
            if (jObject.ContainsKey("exitGameObjectPaths"))
            {
                var exitGameObjectPaths = jObject["exitGameObjectPaths"].ToObject<List<string>>();
                foreach (var exitGameObjectPath in exitGameObjectPaths)
                {
                    data.exitGameObjectPaths.Add(exitGameObjectPath);
                }
            }

            if (jObject.ContainsKey("actionableObjectPaths"))
            {
                var actionableObjectPaths = jObject["actionableObjectPaths"].ToObject<List<JObject>>();
                foreach (var actionableObjectPath in actionableObjectPaths)
                {
                    var path = actionableObjectPath["path"].ToObject<String>();
                    var mouseAction = actionableObjectPath["mouseAction"].ToObject<MouseAction>();
                    data.actionableObjectPaths.Add(path, mouseAction);
                }
            }

            return data;
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(AutoTestMetadata) == objectType;
        }

        public override bool CanWrite => false;

        public override bool CanRead => true;
    }



    public class MouseAction
    {
        public static readonly List<MouseAction> CommonGameMouseActions = new()
        {
            // singular buttons
            new MouseAction(true,false,false,false,false),
            new MouseAction(false,true,false,false,false),
            new MouseAction(false,false,true,false,false),
            new MouseAction(false,false,false,true,false),
            new MouseAction(false,false,false,false,true),
            // common combos
            new MouseAction(true,false,true,false,false),
            new MouseAction(true,true,true,false,false),
            new MouseAction(false,true,true,false,false),
            new MouseAction(true,true,false,false,false)
        };

        public MouseAction(bool leftButton, bool middleButton, bool rightButton, bool forwardButton, bool backButton)
        {
            this.leftButton = leftButton;
            this.middleButton = middleButton;
            this.rightButton = rightButton;
            this.forwardButton = forwardButton;
            this.backButton = backButton;
        }

        public bool leftButton;
        public bool middleButton;
        public bool rightButton;
        public bool forwardButton;
        public bool backButton;
        public Vector2 scroll = Vector2.zero;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"leftButton\":").Append(leftButton ? "true" : "false");
            stringBuilder.Append(",\"middleButton\":").Append(middleButton ? "true" : "false");
            stringBuilder.Append(",\"rightButton\":").Append(rightButton ? "true" : "false");
            stringBuilder.Append(",\"forwardButton\":").Append(forwardButton ? "true" : "false");
            stringBuilder.Append(",\"backButton\":").Append(backButton ? "true" : "false");
            stringBuilder.Append("}");
        }
    }

    [JsonConverter(typeof(AutoTestMetadataJsonConverter))]
    public class AutoTestMetadata
    {
        public HashSet<string> exitGameObjectPaths = new();

        public Dictionary<string, MouseAction> actionableObjectPaths = new();

        private static readonly StringBuilder _stringBuilder = new(1000);

        public string ToJsonString()
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"exitGameObjectPaths\":[");
            var count = exitGameObjectPaths.Count;
            var i = 0;
            foreach (var exitGameObjectPath in exitGameObjectPaths)
            {
                StringJsonConverter.WriteToStringBuilder(_stringBuilder, exitGameObjectPath);
                if (++i < count)
                {
                    _stringBuilder.Append(",");
                }
            }

            i = 0;
            count = actionableObjectPaths.Count;
            _stringBuilder.Append("],\"actionableObjectPaths\":[\r\n");
            foreach (var actionableObjectPath in actionableObjectPaths)
            {
                _stringBuilder.Append("{\"path\":");
                StringJsonConverter.WriteToStringBuilder(_stringBuilder, actionableObjectPath.Key);
                _stringBuilder.Append(",\"mouseAction\":");
                actionableObjectPath.Value.WriteToStringBuilder(_stringBuilder);
                _stringBuilder.Append("}");
                if (++i < count)
                {
                    _stringBuilder.Append(",\r\n");
                }
            }
            _stringBuilder.Append("\r\n]}");
            return _stringBuilder.ToString();
        }
    }
}
