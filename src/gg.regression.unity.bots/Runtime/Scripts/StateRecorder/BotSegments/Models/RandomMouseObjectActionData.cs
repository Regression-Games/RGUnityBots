using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using Unity.Plastic.Newtonsoft.Json;
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

        public bool? IsCompleted()
        {
            return null;
        }

        public void ReplayReset()
        {
        }

        public void ProcessAction(int segmentNumber, IEnumerable<TransformStatus> currentTransformStatus)
        {
            var now = Time.unscaledTime;
            if (now - timeBetweenClicks > Replay_LastClickTime)
            {
                var screenHeight = Screen.height;
                var screenWidth = Screen.width;

                var possibleTransformsToClick = currentTransformStatus.Where(a => a.screenSpaceBounds != null).ToList();
                var possibleTransformsCount = possibleTransformsToClick.Count;
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
                            var ratio = screenWidth / screenSize.x;
                            xMin *= ratio;
                            xMax *= ratio;
                        }

                        if (screenHeight != screenSize.y)
                        {
                            var ratio = screenHeight / screenSize.y;
                            yMin *= ratio;
                            yMax *= ratio;
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
                        MouseEventSender.SendMouseEvent(segmentNumber, new MouseInputActionData()
                        {
                            position = new Vector2Int(x, y),
                            leftButton = Random.Range(0, 2) == 0,
                            middleButton = Random.Range(0, 2) == 0,
                            rightButton = Random.Range(0, 2) == 0,
                        }, null, null, null, null);
                        Replay_LastClickTime = now;
                        break; // while
                    }

                } while (++restartCount < RESTART_LIMIT);
            }
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

            stringBuilder.Append("]}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
