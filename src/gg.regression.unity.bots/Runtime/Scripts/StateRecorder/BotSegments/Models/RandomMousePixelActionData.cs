using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on a random pixel in the frame</summary>
     */
    [Serializable]
    [JsonConverter(typeof(RandomMousePixelActionDataJsonConverter))]
    public class RandomMousePixelActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_2;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.RandomMouse_ClickPixel;

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


        private GUIStyle _guiStyle = null;

        private bool IsStopped;

        public bool IsCompleted()
        {
            return IsStopped;
        }

        public void ReplayReset()
        {
            IsStopped = false;
        }

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // no-op
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            if (!IsStopped)
            {
                var now = Time.unscaledTime;
                if (now - timeBetweenClicks > Replay_LastClickTime)
                {
                    var screenWidth = Screen.width;
                    var screenHeight = Screen.height;

                    var x = Random.Range(0, screenWidth);
                    var y = Random.Range(0, screenHeight);

                    var RESTART_LIMIT = 20;

                    var count = excludedAreas.Count;
                    var restartCount = 0;
                    // try up to 20 times per frame to find a valid click spot, but after that, give up till the next frame as none may be available
                    // helps prevent long searches, or cases where the user excludes the whole screen
                    for (var i = 0; i < count && restartCount < RESTART_LIMIT; i++)
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
                            x = Random.Range(0, screenWidth);
                            // restart the loop
                            i = -1;
                            restartCount++;
                        }

                        if (y >= yMin && y <= yMax)
                        {
                            y = Random.Range(0, screenHeight);
                            // restart the loop
                            i = -1;
                            restartCount++;
                        }
                    }

                    if (restartCount < RESTART_LIMIT)
                    {
                        error = null;
                        MouseEventSender.SendMouseEvent(segmentNumber, new MouseInputActionData()
                        {
                            position = new Vector2Int(x, y),
                            leftButton = Random.Range(0, 2) == 0,
                            middleButton = Random.Range(0, 2) == 0,
                            rightButton = Random.Range(0, 2) == 0,
                        }, null, null, currentTransforms, currentEntities);
                        return true;
                    }
                }

            }

            error = null;
            return false;
        }

        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            IsStopped = true;
        }

        public void OnGUI(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (_guiStyle == null)
            {
                _guiStyle = new ()
                {
                    alignment = TextAnchor.MiddleCenter,
                };
                _guiStyle.normal.background = Texture2D.whiteTexture; // must be white to tint properly
                var textColor = Color.white;
                textColor.a = 0.4f;
                _guiStyle.normal.textColor = textColor;
            }
            var screenHeight = Screen.height;
            var screenWidth = Screen.width;
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

                var bgColor = Color.red;
                bgColor.a = 0.4f;
                GUI.backgroundColor = bgColor;

                /*
                 * Screen coordinates are 2D, measured in pixels and start in the lower left corner at (0,0) and go to (Screen.width, Screen.height). Screen coordinates change with the resolution of the device, and even the orientation (if you app allows it) on mobile devices.
                 * GUI coordinates are used by the IMGUI system. They are identical to Screen coordinates except that they start at (0,0) in the upper left and go to (Screen.width, Screen.height) in the lower right.
                 */
                GUI.Box(new Rect(xMin, screenHeight-yMax, (xMax - xMin), (yMax - yMin)), "Excluded\nArea", _guiStyle);
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

            stringBuilder.Append("]}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
