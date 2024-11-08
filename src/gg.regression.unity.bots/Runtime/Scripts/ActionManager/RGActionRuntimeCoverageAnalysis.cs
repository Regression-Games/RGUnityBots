using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming
namespace RegressionGames.ActionManager
{
    public static class RGActionRuntimeCoverageAnalysis
    {
        private static readonly Dictionary<int, Dictionary<RGGameAction, RGActionUsageMetrics>> _analysisData = new ();

        private static readonly HashSet<RGGameAction> _allActions = new();

        private static bool _inProgress;

        private static int _currentSegment = -1;

        public static void Reset()
        {
            _allActions.Clear();
            _analysisData.Clear();
            _currentSegment = -1;
        }

        public static void StartRecording(int segmentNumber)
        {
            if (!_inProgress)
            {
                // do not Reset here as they could record multiple times in a single bot sequence

                if (_allActions.Count == 0)
                {
                    var allActions = RGActionManager.Actions;
                    foreach (var rgGameAction in allActions)
                    {
                        _allActions.Add(rgGameAction);
                    }
                }

                _inProgress = true;

                SetCurrentSegmentNumber(segmentNumber);
            }

        }

        public static void SetCurrentSegmentNumber(int segmentNumber)
        {
            if (_inProgress)
            {
                _currentSegment = segmentNumber;
                if (!_analysisData.ContainsKey(_currentSegment))
                {
                    _analysisData[_currentSegment] = new();
                }
            }
        }

        public static void RecordActionUsage(RGGameAction action, Object targetObject)
        {
            if (_inProgress)
            {
                if (!_analysisData[_currentSegment].TryGetValue(action, out var metrics))
                {
                    metrics = new RGActionUsageMetrics();
                    _analysisData[_currentSegment][action] = metrics;
                }

                ++metrics.invocations;
                metrics.objectsActedOn.Add(CreateLabelForTargetObject(targetObject));
            }
        }

        private static string CreateLabelForTargetObject(Object targetObject)
        {
            if (targetObject is GameObject gameObject)
            {
                // get the path to this game object
                return TransformStatus.GetOrCreateTransformStatus(gameObject.transform).Path;
            }

            if (targetObject is Component component)
            {
                // everything else attached to a game object (including the transform) - this is Intentionally a // instead of / so we can differentiate components vs transforms in the path easily
                return TransformStatus.GetOrCreateTransformStatus(component.transform).Path + "//" + targetObject.GetType().Name;
            }

            return targetObject.ToString();
        }

        public static void StopRecording()
        {
            _inProgress = false;
        }

        public static RGActionUsageSummary BuildSummary()
        {
            if (_currentSegment >= 0)
            {
                var summary = new RGActionUsageSummary();
                foreach (var (_, data) in _analysisData)
                {
                    foreach (var (action, metrics) in data)
                    {
                        if (!summary.usedActionMetrics.TryGetValue(action, out var existingMetric))
                        {
                            existingMetric = new RGActionUsageMetrics();
                            summary.usedActionMetrics[action] = existingMetric;
                        }

                        existingMetric.invocations += metrics.invocations;
                        foreach (var s in metrics.objectsActedOn)
                        {
                            existingMetric.objectsActedOn.Add(s);
                        }

                    }
                }

                foreach (var rgGameAction in _allActions)
                {
                    if (!summary.usedActionMetrics.ContainsKey(rgGameAction))
                    {
                        summary.unusedActions.Add(rgGameAction);
                    }
                }

                return summary;
            }

            // we didn't actually record anything
            return null;
        }

    }

    [Serializable]
    public class RGActionUsageMetrics : IStringBuilderWriteable
    {

        public int apiVersion = SdkApiVersion.VERSION_26;

        public long invocations = 0;
        public readonly HashSet<string> objectsActedOn = new();
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"invocations\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, invocations);
            stringBuilder.Append(",\n\"objectsActedOn\":[");
            var objectsActedOnCount = objectsActedOn.Count;
            var counter = 1;
            foreach (var objectName in objectsActedOn)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, objectName);
                if (counter++ < objectsActedOnCount) // we start the counter at 1 (not 0) so this is a post increment as we want to compare the value this pass before incrementing
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("]\n}");
        }
    }

    [Serializable]
    public class RGActionUsageSummary : IStringBuilderWriteable
    {

        public int apiVersion = SdkApiVersion.VERSION_26;

        public readonly HashSet<RGGameAction> unusedActions = new();

        public readonly Dictionary<RGGameAction, RGActionUsageMetrics> usedActionMetrics = new();
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"unusedActions\":[\n");
            var unusedActionsCount = unusedActions.Count;
            var counter = 1;
            foreach (var rgGameAction in unusedActions)
            {
                rgGameAction.WriteToStringBuilder(stringBuilder);
                if (counter++ < unusedActionsCount) // we start the counter at 1 (not 0) so this is a post increment as we want to compare the value this pass before incrementing
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("],\n");
            stringBuilder.Append("\"usedActionMetrics\":[\n");
            var usedActionMetricsCount = usedActionMetrics.Count;
            counter = 1;
            // sort by number of invocations descending
            var sortedUsedActionMetrics = usedActionMetrics.ToList().OrderBy(kvp => -1 * kvp.Value.invocations).ToList();
            foreach (var (action, metrics) in sortedUsedActionMetrics)
            {
                stringBuilder.Append("{\n\"metrics\":");
                metrics.WriteToStringBuilder(stringBuilder);
                stringBuilder.Append("\n,\"action\":");
                action.WriteToStringBuilder(stringBuilder);
                stringBuilder.Append("\n}");
                if (counter++ < usedActionMetricsCount) // we start the counter at 1 (not 0) so this is a post increment as we want to compare the value this pass before incrementing
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("]\n}");
        }
    }
}
