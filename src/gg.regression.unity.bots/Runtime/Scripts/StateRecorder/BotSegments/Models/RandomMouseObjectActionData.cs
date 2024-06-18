using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on a random renderable object in the frame</summary>
     */
    [Serializable]
    [JsonConverter(typeof(RandomMouseObjectActionDataJsonConverter))]
    public class RandomMouseObjectActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = BotSegment.SDK_API_VERSION_2;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.RandomMouse_ClickObject;

        [NonSerialized]
        private float Replay_LastClickTime = float.MinValue;

        /**
         * <summary>The minimum time gap between clicks, 0 means click once per frame.</summary>
         */
        public float timeBetweenClicks;

        /**
         * <summary>Used to normalize the excluded areas values to the current screen size</summary>
         */
        public Vector2Int screenSize;

        /**
         * <summary>The screen pixel rects to avoid clicking in</summary>
         */
        public List<RectInt> excludedAreas = new();

        /**
         * <summary>The object paths to avoid clicking on</summary>
         */
        public List<string> excludedNormalizedPaths = new();

        /**
         * <summary>The object paths that must be visible for this bot to keep clicking.</summary>
         */
        public List<string> preconditionNormalizedPaths = new();

        public bool? IsCompleted()
        {
            return null;
        }

        public void ReplayReset()
        {
        }

        public void StartAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms)
        {
            // no-op
        }

        public bool ProcessAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms, out string error)
        {
            var now = Time.unscaledTime;
            if (now - timeBetweenClicks > Replay_LastClickTime)
            {
                var screenHeight = Screen.height;
                var screenWidth = Screen.width;

                List<TransformStatus> possibleTransformsToClick;
                // pick randomly either UI or gameObject
                var uiOrGameObject = Random.Range(0, 2) > 0;
                var preconditionsMet = preconditionNormalizedPaths.Count == 0;
                if (!preconditionsMet)
                {
                    preconditionsMet = currentUITransforms.Any(a => a.Value.screenSpaceBounds != null && StateRecorderUtils.OptimizedContainsStringInList(preconditionNormalizedPaths, a.Value.NormalizedPath));
                }

                if (!preconditionsMet)
                {
                    preconditionsMet = currentGameObjectTransforms.Any(a => a.Value.screenSpaceBounds != null && StateRecorderUtils.OptimizedContainsStringInList(preconditionNormalizedPaths, a.Value.NormalizedPath));
                }

                if (!preconditionsMet)
                {
                    //TODO: Someday make this only log the ones that weren't matched instead of all the preconditions
                    StringBuilder theError = new StringBuilder(500);
                    theError.Append("RandomMouseClicker - Missing one or more precondition normalized paths\r\n");
                    var preconditionNormalizedPathsCount = preconditionNormalizedPaths.Count;
                    for (var i = 0; i < preconditionNormalizedPathsCount; i++)
                    {
                        var pc = preconditionNormalizedPaths[i];
                        theError.Append(pc);
                        if (i + 1 < preconditionNormalizedPathsCount)
                        {
                            theError.Append("\r\n");
                        }
                    }

                    error = theError.ToString();
                    return false;
                }

                if (currentGameObjectTransforms.Count == 0 || uiOrGameObject && currentUITransforms.Count > 0)
                {
                    possibleTransformsToClick = currentUITransforms.Values.Where(a => a.screenSpaceBounds != null).ToList();
                }
                else
                {
                    possibleTransformsToClick = currentGameObjectTransforms.Values.Where(a => a.screenSpaceBounds != null).ToList();
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
                            RGDebug.LogInfo($"({segmentNumber}) - Bot Segment - RandomMouseObjectClicker - {{x:{x}, y:{y}, lb:{(lb?1:0)}, mb:{(mb?1:0)}, rb:{(rb?1:0)}, fb:{(fb?1:0)}, bb:{(bb?1:0)}}} on object with NormalizedPath: {transformOption.NormalizedPath}", transformOption.Transform.gameObject);
                            MouseEventSender.SendRawPositionMouseEvent(
                                segmentNumber,
                                new Vector2(x, y),
                                lb,
                                mb,
                                rb,
                                fb,
                                bb,
                                new Vector2(0,0) // don't support random scrolling yet...
                            );
                            Replay_LastClickTime = now;
                            error = null;
                            return true;
                        }

                    } while (++restartCount < RESTART_LIMIT);
                }
            }

            error = null;
            return false;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"screenSize\":");
            VectorIntJsonConverter.WriteToStringBuilder(stringBuilder, screenSize);
            stringBuilder.Append(",\"timeBetweenClicks\":");
            FloatJsonConverter.WriteToStringBuilder(stringBuilder, timeBetweenClicks);
            stringBuilder.Append(",\"excludedAreas\":[");
            var excludedAreasCount = excludedAreas.Count;
            for (var i = 0; i < excludedAreasCount; i++)
            {
                var excludedArea = excludedAreas[i];
                RectIntJsonConverter.WriteToStringBuilder(stringBuilder, excludedArea);
                if (i + 1 < excludedAreasCount)
                {
                    stringBuilder.Append(",");
                }
            }

            stringBuilder.Append("],\"excludedNormalizedPaths\":[");
            var excludedNormalizedPathsCount = excludedNormalizedPaths.Count;
            for (var i = 0; i < excludedNormalizedPathsCount; i++)
            {
                var excludedNormalizedPath = excludedNormalizedPaths[i];
                StringJsonConverter.WriteToStringBuilder(stringBuilder, excludedNormalizedPath);
                if (i + 1 < excludedAreasCount)
                {
                    stringBuilder.Append(",");
                }
            }

            stringBuilder.Append("],\"preconditionNormalizedPaths\":[");
            var preconditionNormalizedPathsCount = preconditionNormalizedPaths.Count;
            for (var i = 0; i < preconditionNormalizedPathsCount; i++)
            {
                var preconditionNormalizedPath = preconditionNormalizedPaths[i];
                StringJsonConverter.WriteToStringBuilder(stringBuilder, preconditionNormalizedPath);
                if (i + 1 < preconditionNormalizedPathsCount)
                {
                    stringBuilder.Append(",");
                }
            }

            stringBuilder.Append("]}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
