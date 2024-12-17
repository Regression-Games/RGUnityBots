using System;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace RegressionGames.StateRecorder.BotSegments
{

    [Serializable]
    public class ActiveSequenceInfo : IStringBuilderWriteable
    {
        public ActiveSequenceInfo(BotSequence botSequence)
        {
            this.name = botSequence.name;
            this.description = botSequence.description;
            this.resourcePath = botSequence.resourcePath;
            this.segmentCount = botSequence.segments.Count;
        }

        public readonly string name;
        public readonly string description;
        public readonly string resourcePath;
        public readonly int segmentCount;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"segmentCount\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, segmentCount);
            stringBuilder.Append(",\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"description\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, description);
            stringBuilder.Append(",\"resourcePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, resourcePath);
            stringBuilder.Append("}");
        }
    }

    [Serializable]
    public class ActiveSegmentInfo : IStringBuilderWriteable
    {
        public ActiveSegmentInfo(BotSegment botSegment)
        {
            this.name = botSegment.name;
            this.description = botSegment.description;
            this.segmentNumber = botSegment.Replay_SegmentNumber;
            this.resourcePath = botSegment.resourcePath;
            UpdateStatus(botSegment);
        }

        public void UpdateStatus(BotSegment botSegment)
        {
            this.actionStarted = botSegment.Replay_ActionStarted;
            this.actionCompleted = botSegment.Replay_ActionCompleted;
            this.endCriteriaMatched = botSegment.Replay_Matched;
        }

        public readonly string name;
        public readonly string description;
        public readonly string resourcePath;

        public readonly int segmentNumber;

        public bool actionStarted;
        public bool actionCompleted;
        public bool endCriteriaMatched;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"segmentNumber\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, segmentNumber);
            stringBuilder.Append(",\"resourcePath\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, resourcePath);
            stringBuilder.Append(",\"actionStarted\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, actionStarted);
            stringBuilder.Append(",\"actionCompleted\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, actionCompleted);
            stringBuilder.Append(",\"endCriteriaMatched\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, endCriteriaMatched);
            stringBuilder.Append(",\"name\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, name);
            stringBuilder.Append(",\"description\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, description);

            stringBuilder.Append("}");
        }
    }

    [Serializable]
    public class BotSegmentPlaybackStatus : IStringBuilderWriteable
    {
        public ActiveSequenceInfo activeSequence;
        public ActiveSegmentInfo activeSegment;

        public string activeSegmentErrorStatus;

        public bool isExplorationActive;
        public string explorationErrorStatus;

        public void Reset()
        {
            activeSequence = null;
            activeSegment = null;

            activeSegmentErrorStatus = null;

            isExplorationActive = false;
            explorationErrorStatus = null;
        }

        public void UpdateActiveSequence(BotSequence sequence)
        {
            this.activeSequence = new ActiveSequenceInfo(sequence);
        }

        public void UpdateActiveSegment(BotSegment segment, string errorStatus)
        {
            if (this.activeSegment != null && this.activeSegment.segmentNumber == segment.Replay_SegmentNumber && this.activeSegment.resourcePath == segment.resourcePath)
            {
                // try to avoid allocating/gc one of these on every update
                this.activeSegment.UpdateStatus(segment);
            }
            else
            {
                this.activeSegment = new ActiveSegmentInfo(segment);
            }

            this.activeSegmentErrorStatus = errorStatus;
        }

        // ReSharper disable once ParameterHidesMember
        public void UpdateExplorationStatus(bool isExplorationActive, string errorStatus)
        {
            this.isExplorationActive = isExplorationActive;
            this.explorationErrorStatus = errorStatus;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"activeSegmentErrorStatus\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, activeSegmentErrorStatus);
            stringBuilder.Append(",\"isExplorationActive\":");
            BooleanJsonConverter.WriteToStringBuilder(stringBuilder, isExplorationActive);
            stringBuilder.Append(",\"explorationErrorStatus\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, explorationErrorStatus);
            stringBuilder.Append(",\"activeSequence\":");
            activeSequence.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\"activeSegment\":");
            activeSegment.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }

    public class BotSegmentPlaybackStatusManager : MonoBehaviour
    {

        public readonly BotSegmentPlaybackStatus PlaybackStatus = new();

    }
}
