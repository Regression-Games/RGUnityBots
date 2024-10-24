using System;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace RegressionGames.RemoteOrchestration.Models
{
    [Serializable]
    public class WorkAssignment
    {
        public long id;
        public string resourcePath;
        public DateTime startTime;
        public int? timeout;
        public WorkAssignmentStatus status;
        public IStringBuilderWriteable details;

        [NonSerialized]
        private BotSequence _botSequence;

        [NonSerialized]
        private float _realStartTime;

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"id\":");
            LongJsonConverter.WriteToStringBuilder(stringBuilder, id);
            stringBuilder.Append(",\"status\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, status.ToString());
            stringBuilder.Append(",\"details\":");
            if (details == null)
            {
                stringBuilder.Append("null");
            }
            else
            {
                details.WriteToStringBuilder(stringBuilder);
            }
            stringBuilder.Append("}");
        }

        private void StartHelper()
        {
            resourcePath = BotSequence.ToResourcePath(resourcePath);
            try
            {
                var botSequence = BotSequence.LoadSequenceJsonFromPath(resourcePath);
                if (botSequence.Item3 == null)
                {
                    status = WorkAssignmentStatus.COMPLETE_ERROR;
                    Stop();
                    details = new ErrorDetails($"Failed to load BotSequence from path: {resourcePath}");
                }
                else
                {
                    status = WorkAssignmentStatus.IN_PROGRESS;
                    _botSequence = botSequence.Item3;
                    _realStartTime = Time.unscaledTime;
                    _botSequence.Play();
                }
            }
            catch (Exception e)
            {
                status = WorkAssignmentStatus.COMPLETE_ERROR;
                Stop();
                details = new ErrorDetails($"Failed to load BotSequence from path: {resourcePath}, exception: {e}");
            }
        }

        public void Update()
        {
            if (status == WorkAssignmentStatus.IN_PROGRESS)
            {
                if (timeout is > 0)
                {
                    var now = Time.unscaledTime;
                    var timeoutTime = _realStartTime + timeout.Value;
                    if (now > timeoutTime)
                    {
                        status = WorkAssignmentStatus.COMPLETE_TIMEOUT;
                        Stop();
                        return;
                    }
                }

                // check if the sequence is done
                var activeBotSequence = RGRemoteWorkerBehaviour.GetActiveBotSequence();
                if (activeBotSequence == null)
                {
                    var playbackController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
                    var lastWarning = playbackController.GetLastSegmentPlaybackWarning();
                    // ended
                    if (lastWarning != null)
                    {
                        status = WorkAssignmentStatus.COMPLETE_ERROR;
                        Stop();
                        details = new ErrorDetails(lastWarning);
                    }
                    else
                    {
                        // completed cleanly
                        status = WorkAssignmentStatus.COMPLETE_SUCCESS;
                        Stop();
                        details = null;
                    }
                }
                else
                {
                    if (string.CompareOrdinal(activeBotSequence.resourcePath, resourcePath) != 0)
                    {
                        // some other sequence is now running, we must have completed and we missed it
                        status = WorkAssignmentStatus.COMPLETE_SUCCESS;
                        Stop();
                        details = null;
                    }
                    // else - still running... leave it alone
                }
            }

            if (status == WorkAssignmentStatus.WAITING_TO_START)
            {
                var dateNow = DateTime.UtcNow;
                if (dateNow > startTime)
                {
                    StartHelper();
                }
            }

        }

        public void Stop()
        {
            if (_botSequence != null)
            {
                _botSequence.Stop();
            }
            _botSequence = null;
        }

        private class ErrorDetails : IStringBuilderWriteable
        {
            private readonly string _error;
            public ErrorDetails(string error)
            {
                this._error = error;
            }
            public void WriteToStringBuilder(StringBuilder stringBuilder)
            {
                stringBuilder.Append("{\"error\":");
                StringJsonConverter.WriteToStringBuilder(stringBuilder, _error);
                stringBuilder.Append("}");
            }
        }


    }
}
