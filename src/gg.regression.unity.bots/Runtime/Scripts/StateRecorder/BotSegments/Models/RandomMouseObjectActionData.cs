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
         * <summary>Allow mouse events that 'move' with a button held down</summary>
         */
        public bool allowDrag;

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

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // no-op
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            var now = Time.unscaledTime;
            if (now - timeBetweenClicks > Replay_LastClickTime)
            {
                var screenHeight = Screen.height;
                var screenWidth = Screen.width;

                List<ObjectStatus> possibleTransformsToClick;
                // pick randomly either UI or gameObject
                var uiOrEntityObject = Random.Range(0, 2) > 0;
                var preconditionsMet = preconditionNormalizedPaths.Count == 0;
                if (!preconditionsMet)
                {
                    preconditionsMet = currentTransforms.Any(a => a.Value.screenSpaceBounds != null && StateRecorderUtils.OptimizedContainsStringInList(preconditionNormalizedPaths, a.Value.NormalizedPath));
                }

                if (!preconditionsMet)
                {
                    preconditionsMet = currentEntities.Any(a => a.Value.screenSpaceBounds != null && StateRecorderUtils.OptimizedContainsStringInList(preconditionNormalizedPaths, a.Value.NormalizedPath));
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

                if (currentEntities.Count == 0 || uiOrEntityObject && currentTransforms.Count > 0)
                {
                    possibleTransformsToClick = currentTransforms.Values.Where(a => a.screenSpaceBounds != null).ToList();
                }
                else
                {
                    possibleTransformsToClick = currentEntities.Values.Where(a => a.screenSpaceBounds != null).ToList();
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

                        var ssb = transformOption.screenSpaceBounds.Value;
                        var ssbCenter = ssb.center;
                        var x = (int)ssbCenter.x;
                        var y = (int)ssbCenter.y;

                        bool valid;
                        var count = excludedAreas.Count;
                        var reScan = false;
                        do
                        {
                            valid = true;
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
                                    xMin = (int)(xMin * ratio);
                                    xMax = (int)(xMax * ratio);
                                }

                                if (screenHeight != screenSize.y)
                                {
                                    var ratio = screenHeight / (float)screenSize.y;
                                    yMin = (int)(yMin * ratio);
                                    yMax = (int)(yMax * ratio);
                                }

                                if (x >= xMin && x <= xMax)
                                {
                                    valid = false;
                                    if (reScan)
                                    {
                                        // violation on reScan, abandon this object
                                        reScan = false;
                                        break; // for
                                    }

                                    // see if we can pick an X that is valid within our object
                                    if (ssb.min.x < xMin)
                                    {
                                        // pick a new X within the box as close to the center as possible to try to still hit collider
                                        x = xMin - 1;
                                        // need to rescan again to make sure we didn't violate other areas if multiple overlap my object
                                        reScan = true;
                                    }
                                    else if (ssb.max.x > xMax)
                                    {
                                        // pick a new X within the box as close to the center as possible to try to still hit collider
                                        x = xMax + 1;
                                        // need to rescan again to make sure we didn't violate other areas if multiple overlap my object
                                        reScan = true;
                                    }
                                    else
                                    {
                                        break; // for
                                    }
                                }

                                if (y >= yMin && y <= yMax)
                                {
                                    valid = false;
                                    if (reScan)
                                    {
                                        // violation on reScan, abandon this object
                                        reScan = false;
                                        break; // for
                                    }

                                    // see if we can pick a Y that is valid within our object
                                    if (ssb.min.y < yMin)
                                    {
                                        // pick a new Y within sthe box as close to the center as possible to try to still hit collider
                                        y = yMin - 1;
                                        // need to rescan again to make sure we didn't violate other areas if multiple overlap my object
                                        reScan = true;
                                    }
                                    else if (ssb.max.y > yMax)
                                    {
                                        // pick a new Y within the box as close to the center as possible to try to still hit collider
                                        y = yMax + 1;
                                        // need to rescan again to make sure we didn't violate other areas if multiple overlap my object
                                        reScan = true;
                                    }
                                    else
                                    {
                                        break; // for
                                    }
                                }
                            }

                            // if valid on the rescan.. stop the outer loop
                            if (valid && reScan)
                            {
                                reScan = false;
                            }
                        } while (reScan);

                        if (valid)
                        {
                            var drag = allowDrag && Random.Range(0, 2) == 0;
                            var lb = Random.Range(0, 2) == 0;
                            var mb = Random.Range(0, 2) == 0;
                            var rb = Random.Range(0, 2) == 0;
                            var fb = Random.Range(0, 2) == 0;
                            var bb = Random.Range(0, 2) == 0;
                            RGDebug.LogInfo($"({segmentNumber}) - Bot Segment - RandomMouseObjectClicker - {{x:{x}, y:{y}, lb:{(lb?1:0)}, mb:{(mb?1:0)}, rb:{(rb?1:0)}, fb:{(fb?1:0)}, bb:{(bb?1:0)}}} on object with NormalizedPath: {transformOption.NormalizedPath}");
                            MouseEventSender.SendRawPositionMouseEvent(
                                segmentNumber,
                                new Vector2(x, y),
                                lb,
                                mb,
                                rb,
                                fb,
                                bb,
                                Vector2.zero // don't support random scrolling yet...
                            );

                            if (!drag)
                            {
                                RGDebug.LogInfo($"({segmentNumber}) - Bot Segment - RandomMouseObjectClicker - unclick - {{x:{x}, y:{y}}}");
                                // send the un-click event
                                MouseEventSender.SendRawPositionMouseEvent(
                                    segmentNumber,
                                    new Vector2(x, y),
                                    false,
                                    false,
                                    false,
                                    false,
                                    false,
                                    Vector2.zero // don't support random scrolling yet...
                                );
                            }

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

        private GUIStyle _guiStyle = null;

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
            OnGUIDrawExcludedAreas();
            OnGUIDrawExcludedObjects(currentTransforms, currentEntities);
        }

        private void OnGUIDrawExcludedObjects(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (excludedNormalizedPaths.Count > 0)
            {
                var screenHeight = Screen.height;
                var bgColor = (Color.yellow + Color.red) / 2;
                bgColor.a = 0.4f;
                foreach (var currentUITransform in currentTransforms)
                {
                    var uiValue = currentUITransform.Value;
                    if (uiValue.screenSpaceBounds.HasValue)
                    {
                        if (StateRecorderUtils.OptimizedContainsStringInList(excludedNormalizedPaths, uiValue.NormalizedPath))
                        {
                            var ssb = uiValue.screenSpaceBounds.Value;
                            GUI.backgroundColor = bgColor;
                            /*
                             * Screen coordinates are 2D, measured in pixels and start in the lower left corner at (0,0) and go to (Screen.width, Screen.height). Screen coordinates change with the resolution of the device, and even the orientation (if you app allows it) on mobile devices.
                             * GUI coordinates are used by the IMGUI system. They are identical to Screen coordinates except that they start at (0,0) in the upper left and go to (Screen.width, Screen.height) in the lower right.
                             */
                            GUI.Box(new Rect(ssb.min.x, screenHeight-ssb.max.y, ssb.size.x, ssb.size.y), "Excluded\nObject", _guiStyle);
                        }
                    }
                }

                foreach (var currentGameObjectTransform in currentEntities)
                {
                    var uiValue = currentGameObjectTransform.Value;
                    if (uiValue.screenSpaceBounds.HasValue)
                    {
                        if (StateRecorderUtils.OptimizedContainsStringInList(excludedNormalizedPaths, uiValue.NormalizedPath))
                        {
                            var ssb = uiValue.screenSpaceBounds.Value;
                            GUI.backgroundColor = bgColor;
                            /*
                             * Screen coordinates are 2D, measured in pixels and start in the lower left corner at (0,0) and go to (Screen.width, Screen.height). Screen coordinates change with the resolution of the device, and even the orientation (if you app allows it) on mobile devices.
                             * GUI coordinates are used by the IMGUI system. They are identical to Screen coordinates except that they start at (0,0) in the upper left and go to (Screen.width, Screen.height) in the lower right.
                             */
                            GUI.Box(new Rect(ssb.min.x, screenHeight-ssb.max.y, ssb.size.x, ssb.size.y), "Excluded\nObject", _guiStyle);
                        }
                    }
                }
            }
        }

        private void OnGUIDrawExcludedAreas()
        {
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
            stringBuilder.Append(",\"allowDrag\":");
            stringBuilder.Append(allowDrag ? "true" : "false");
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
