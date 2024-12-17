using System;
using System.Text;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using StateRecorder.BotSegments;
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
        public Exception activeSegmentExceptionStatus;

        public ExplorationState explorationState;

        public string explorationErrorStatus;
        public Exception explorationExceptionStatus;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"activeSegmentErrorStatus\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, activeSegmentErrorStatus);
            stringBuilder.Append(",\"explorationState\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, explorationState.ToString());
            stringBuilder.Append(",\"explorationErrorStatus\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, explorationErrorStatus);
            stringBuilder.Append(",\"activeSequence\":");
            activeSequence.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append(",\"activeSegment\":");
            activeSegment.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
    }

    /**
     * Manages the current status of bot sequence / segment playback.
     * Also handles updating the on screen status and writing to the log.
     */
    public class BotSegmentPlaybackStatusManager : MonoBehaviour
    {
        public bool pauseEditorOnPlaybackError = false;

        private float _lastTimeErrorClearedOrLogged;

        private string _lastSegmentMessage;
        private string _lastExplorationMessage;
        private string _lastLoggedMessage;

        // ReSharper disable once InconsistentNaming
        private const int ERROR_LOG_WAIT_INTERVAL = 3; // seconds before we log an error.. this is to avoid spamming the log every update

        private readonly BotSegmentPlaybackStatus PlaybackStatus = new();

        public string LastError()
        {
            return _lastLoggedMessage;
        }

        public void Reset()
        {
            var now = Time.unscaledTime;

            _lastTimeErrorClearedOrLogged = now;
            _lastSegmentMessage = null;
            _lastExplorationMessage = null;
            _lastLoggedMessage = null;

            PlaybackStatus.activeSequence = null;
            PlaybackStatus.activeSegment = null;

            PlaybackStatus.activeSegmentErrorStatus = null;

            PlaybackStatus.explorationState = ExplorationState.STOPPED;
            PlaybackStatus.explorationErrorStatus = null;
        }

        public void UpdateActiveSequence(BotSequence sequence)
        {
            PlaybackStatus.activeSequence = sequence == null ? null : new ActiveSequenceInfo(sequence);
        }

        /**
         * Leaves the current error status unchanged
         */
        public void UpdateActiveSegment(BotSegment segment)
        {
            if (segment == null)
            {
                PlaybackStatus.activeSegment = null;
            }
            else
            {
                if (PlaybackStatus.activeSegment != null && this.PlaybackStatus.activeSegment.segmentNumber == segment.Replay_SegmentNumber && this.PlaybackStatus.activeSegment.resourcePath == segment.resourcePath)
                {
                    // try to avoid allocating/gc one of these on every update
                    PlaybackStatus.activeSegment.UpdateStatus(segment);
                }
                else
                {
                    PlaybackStatus.activeSegment = new ActiveSegmentInfo(segment);
                }
            }
        }

        public void UpdateActiveSegmentAndErrorStatus(BotSegment segment, string errorStatus, Exception exceptionStatus = null)
        {
            UpdateActiveSegment(segment);
            PlaybackStatus.activeSegmentErrorStatus = errorStatus;
            PlaybackStatus.activeSegmentExceptionStatus = exceptionStatus;
        }

        // ReSharper disable once ParameterHidesMember
        public void UpdateExplorationStatus(ExplorationState explorationState, string errorStatus, Exception exceptionStatus = null)
        {
            PlaybackStatus.explorationState = explorationState;
            PlaybackStatus.explorationErrorStatus = errorStatus;
            PlaybackStatus.explorationExceptionStatus = exceptionStatus;
        }

        private void LogPlaybackStatus()
        {
            var now = Time.unscaledTime;

            var logPrefix = $"({"" + PlaybackStatus.activeSegment?.segmentNumber ?? "?"}) - Bot Segment - ";
            // we want to update the UI for every exploration error, but we only want to log every X often to avoid spamming the log
            // this should be set anytime the exploration error status is set.. but just in case
            if (!string.IsNullOrEmpty(PlaybackStatus.activeSegmentErrorStatus))
            {
                _lastSegmentMessage = "Error processing BotAction\n\n" + PlaybackStatus.activeSegmentErrorStatus;
            }
            else
            {
                _lastSegmentMessage = null;
            }

            var loggedMessage = _lastSegmentMessage;


            var forceLogOnFirstOrLastExploration = false;

            if (!string.IsNullOrEmpty(PlaybackStatus.explorationErrorStatus))
            {
                if (_lastExplorationMessage == null )
                {
                    forceLogOnFirstOrLastExploration = true;
                }
                _lastExplorationMessage = PlaybackStatus.explorationErrorStatus;
                loggedMessage = "Error processing exploratory BotAction\n\n" + PlaybackStatus.explorationErrorStatus + "\n\n\nPre-Exploration Error - " + _lastSegmentMessage;
            }
            else
            {
                if (_lastExplorationMessage != null)
                {
                    forceLogOnFirstOrLastExploration = true;
                }

                _lastExplorationMessage = null;
            }

            if (loggedMessage == null)
            {
                // clear ui status instantly once we have no errors
                FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(null);
                _lastTimeErrorClearedOrLogged = now;
                _lastLoggedMessage = null;
            }
            else
            {
                // don't be a spammer
                if (forceLogOnFirstOrLastExploration || _lastLoggedMessage != logPrefix + loggedMessage)
                {
                    if (forceLogOnFirstOrLastExploration || _lastTimeErrorClearedOrLogged + ERROR_LOG_WAIT_INTERVAL < now)
                    {
                        _lastLoggedMessage = logPrefix + loggedMessage;
                        if (PlaybackStatus.explorationExceptionStatus != null)
                        {
                            RGDebug.LogException(PlaybackStatus.explorationExceptionStatus, _lastLoggedMessage);
                        }
                        else if (PlaybackStatus.activeSegmentExceptionStatus != null)
                        {
                            RGDebug.LogException(PlaybackStatus.activeSegmentExceptionStatus, _lastLoggedMessage);
                        }
                        else
                        {
                            RGDebug.LogWarning(_lastLoggedMessage);
                        }

                        _lastTimeErrorClearedOrLogged = now;
                        FindObjectOfType<ReplayToolbarManager>()?.SetKeyFrameWarningText(_lastLoggedMessage);
                        if (pauseEditorOnPlaybackError)
                        {
                            Debug.Break();
                        }
                    }
                }

            }

        }

        public void Update()
        {
            LogPlaybackStatus();
        }

    }
}
