﻿using System;
using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on a random renderable object in the frame</summary>
     */
    [Serializable]
    public class RandomMouseObjectActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = BotSegment.SDK_API_VERSION_2;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.RandomMouse_ClickObject;

        /**
         * <summary>The minimum time gap between clicks, 0 means click once per frame.</summary>
         */
        public float timeBetweenClicks;
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

        public void ProcessAction()
        {
            throw new NotImplementedException();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
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