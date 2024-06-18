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
        private float actionIntervalSeconds = 0.1f;

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
                        _autoTestMetadata.actionableObjectPaths.Add(_lastObjectClicked.NormalizedPath);
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
                _lastObjectClicked = ClickOnRandomObject(currentUITransforms.Item2, currentGameObjectTransforms.Item2);
            }
        }

        private float LastActionTime = float.MinValue;

        private TransformStatus ClickOnRandomObject(Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms)
        {
            var now = Time.unscaledTime;
            if (now - actionIntervalSeconds > LastActionTime)
            {
                var screenHeight = Screen.height;
                var screenWidth = Screen.width;

                List<TransformStatus> possibleTransformsToClick;

                // 50% chance of choosing a known actionable path, 50% chance of exploration with random action
                var randomAction = Random.Range(0,2) == 0;

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
                        possibleTransformsToClick = currentUITransforms.Values.Where(a => a.Transform != null && a.screenSpaceBounds != null && _autoTestMetadata.actionableObjectPaths.Contains(a.NormalizedPath)).ToList();
                    }
                    else
                    {
                        possibleTransformsToClick = new();
                    }

                    if (currentGameObjectTransforms.Count > 0)
                    {
                        possibleTransformsToClick.AddRange(currentGameObjectTransforms.Values.Where(a => a.Transform != null && a.screenSpaceBounds != null && _autoTestMetadata.actionableObjectPaths.Contains(a.NormalizedPath)));
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
                            var lb = Random.Range(0, 2) == 0;
                            var mb = Random.Range(0, 2) == 0;
                            var rb = Random.Range(0, 2) == 0;
                            var fb = Random.Range(0, 2) == 0;
                            var bb = Random.Range(0, 2) == 0;
                            RGDebug.LogInfo($"AutoTester - {{x:{x}, y:{y}, lb:{(lb?1:0)}, mb:{(mb?1:0)}, rb:{(rb?1:0)}, fb:{(fb?1:0)}, bb:{(bb?1:0)}}} on object with NormalizedPath: {transformOption.NormalizedPath}", transformOption.Transform.gameObject);
                            MouseEventSender.SendRawPositionMouseEvent(
                                0,
                                new Vector2(x, y),
                                lb,
                                mb,
                                rb,
                                fb,
                                bb,
                                Vector2.zero // don't support random scrolling yet...
                            );

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
                            LastActionTime = now;
                            return transformOption;
                        }

                    } while (++restartCount < RESTART_LIMIT);
                }
            }
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
                var actionableObjectPaths = jObject["actionableObjectPaths"].ToObject<List<string>>();
                foreach (var actionableObjectPath in actionableObjectPaths)
                {
                    data.actionableObjectPaths.Add(actionableObjectPath);
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

    [JsonConverter(typeof(AutoTestMetadataJsonConverter))]
    public class AutoTestMetadata
    {
        public HashSet<string> exitGameObjectPaths = new();

        public HashSet<string> actionableObjectPaths = new();

        private static readonly StringBuilder _stringBuilder = new(1000);

        public string ToJsonString()
        {
            _stringBuilder.Clear();
            _stringBuilder.Append("{\"exitGameObjectPaths\":[");
            var exitGameSceneObjectPathsCount = exitGameObjectPaths.Count;
            var i = 0;
            foreach (var exitGameObjectPath in exitGameObjectPaths)
            {
                StringJsonConverter.WriteToStringBuilder(_stringBuilder, exitGameObjectPath);
                if (++i < exitGameSceneObjectPathsCount)
                {
                    _stringBuilder.Append(",");
                }
            }

            i = 0;
            _stringBuilder.Append("],\"actionableObjectPaths\":[");
            foreach (var actionableObjectPath in actionableObjectPaths)
            {
                StringJsonConverter.WriteToStringBuilder(_stringBuilder, actionableObjectPath);
                if (++i < exitGameSceneObjectPathsCount)
                {
                    _stringBuilder.Append(",");
                }
            }
            _stringBuilder.Append("]}");
            return _stringBuilder.ToString();
        }
    }
}
