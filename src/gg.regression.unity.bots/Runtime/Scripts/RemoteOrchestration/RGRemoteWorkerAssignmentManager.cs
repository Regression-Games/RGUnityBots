using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.RemoteOrchestration.Types;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.Types;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming
namespace RegressionGames.RemoteOrchestration
{
    public static class RGRemoteWorkerAssignmentManager
    {

        internal static Guid Guid = new();

        internal static readonly int Rc_RemoteWorkerPlusSequenceConflict = 8;

        internal static readonly string RemoteWorkerPathArgument = "-rgremoteworker";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        public static void FirstLoadChecks()
        {
            // do this here to fail fast on bad args
            ParseArgument();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            var isRemoteWorker = ParseArgument();
            if (isRemoteWorker)
            {
                BotSegmentsPlaybackController playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();
                if (playbackController == null)
                {
                    RGDebug.LogError($"RGOverlay must be in the first scene loaded for your game to utilize {RemoteWorkerPathArgument} command line argument");
                    Application.Quit(RGHeadlessSequenceRunner.Rc_OverlayNotInScene);
                }

                playbackController.gameObject.AddComponent<RGRemoteWorkerBehaviour>();
            }
        }

        internal static bool ParseArgument()
        {
            var args = Environment.GetCommandLineArgs();
            var argsLength = args.Length;

            var isRemoteWorker = false;
            var hasSequenceArg = false;
            for (var i = 0; i < argsLength; i++)
            {
                var arg = args[i];
                if (arg == RemoteWorkerPathArgument)
                {
                    isRemoteWorker = true;
                }
                else if (arg == RGHeadlessSequenceRunner.SequencePathArgument)
                {
                    hasSequenceArg = true;
                }
            }

            if (isRemoteWorker && hasSequenceArg)
            {
                // else
                RGDebug.LogError($"{RemoteWorkerPathArgument} command line argument cannot be used in combination with argument {RGHeadlessSequenceRunner.SequencePathArgument}");
                Application.Quit(Rc_RemoteWorkerPlusSequenceConflict);
            }

            return isRemoteWorker;
        }

    }

    public class RGRemoteWorkerBehaviour : MonoBehaviour
    {

        private static readonly int HeartbeatInterval = 10; // seconds

        private float _lastHeartbeatTime = float.MinValue;
        private float _lastRegistrationTime = float.MinValue;

        private bool _firstRun = true;
        private volatile bool _registrationInProgress = false;
        private volatile bool _registrationComplete = false;

        private long ClientId = long.MinValue;

        private List<AvailableBotSequence> _availableBotSequences = null;

        public volatile WorkAssignment ActiveWorkAssignment = null;


        private void Update()
        {
            if ( _firstRun)
            {
                _firstRun = false;
                // start a co-routine to resolve the sequences available on this SDK client
                StartCoroutine(RGSequenceManager.ResolveSequenceFiles(ProcessResolvedSequences));
                return;
            }

            if (!_registrationComplete && !_registrationInProgress && _availableBotSequences != null)
            {
                var timeNow = Time.unscaledTime;
                // check to send registration if past the interval - rate limited to avoid request spam if server offline
                if (_lastRegistrationTime + HeartbeatInterval < timeNow)
                {
                    _lastRegistrationTime = timeNow;
                    var registrationRequest = new SDKClientRegistrationRequest(RGRemoteWorkerAssignmentManager.Guid)
                    {
                        availableSequences = _availableBotSequences
                    };
                    _registrationInProgress = true;
                    _ = RGServiceManager.GetInstance().SendRemoteWorkerRegistration(
                        request: registrationRequest,
                        onSuccess: RegistrationResponseHandler,
                        onFailure: () => { _registrationInProgress = false; }
                    );
                    return;
                }
            }

            if (_registrationComplete)
            {
                // see if we need to start/update/etc the workAssignment
                if (ActiveWorkAssignment != null)
                {
                    ActiveWorkAssignment.Update();
                }

                var now = Time.unscaledTime;
                // check to send heartbeat if past the interval
                if (_lastHeartbeatTime + HeartbeatInterval < now)
                {
                    SendHeartbeatForCurrentState(null);
                }
            }

        }

        private void SendHeartbeatForCurrentState(WorkAssignment currentWorkAssignment)
        {
            var now = Time.unscaledTime;
            _lastHeartbeatTime = now;
            currentWorkAssignment ??= ActiveWorkAssignment;
            var heartbeatRequest = new SDKClientHeartbeatRequest()
            {
                clientId = this.ClientId,
                activeSequence = GetActiveBotSequence(),
                activeWorkAssignment = currentWorkAssignment
            };
            _ = RGServiceManager.GetInstance().SendRemoteWorkerHeartbeat(heartbeatRequest, HeartbeatResponseHandler, () => { });
        }

        private void ProcessResolvedSequences(IDictionary<string, (string, BotSequence)> sequences)
        {
            _availableBotSequences = sequences.Select(kvp => new AvailableBotSequence(kvp.Key, kvp.Value.Item2)).ToList();
        }

        private void HeartbeatResponseHandler(SDKClientHeartbeatResponse response)
        {
            var incomingWorkAssignment = response.workAssignment;
            if (incomingWorkAssignment == null)
            {
                if (ActiveWorkAssignment != null)
                {
                    // remote side has either acknowledged our complete work assignment heartbeat, or was cancelled and we missed the cancellation update
                    // either way.. our work assignment is OVER... stop it / end it, get it out of here
                    var currentWorkAssignment = ActiveWorkAssignment;
                    ActiveWorkAssignment = null;
                    currentWorkAssignment.Stop();
                    // immediately send another heartbeat in case there is a new assignment
                    SendHeartbeatForCurrentState(null);
                }
            }
            else
            {
                if (ActiveWorkAssignment != null)
                {
                    // we should be updating the existing work assignment.. but 'might' have a conflict.. if we do we'll send 2 updates with the conflict first, then the real one
                    if (ActiveWorkAssignment.id != incomingWorkAssignment.id)
                    {
                        // conflict - send a heartbeat for that conflict (don't update the time though so the regular ones keep flowing)
                        incomingWorkAssignment.status = WorkAssignmentStatus.CONFLICT;
                        incomingWorkAssignment.details = new ConflictDetails(ActiveWorkAssignment.id, incomingWorkAssignment.id);
                        var heartbeatRequest = new SDKClientHeartbeatRequest()
                        {
                            clientId = this.ClientId,
                            activeSequence = GetActiveBotSequence(),
                            activeWorkAssignment = incomingWorkAssignment
                        };
                        // keep a reference to this in case a response changes it somehow before we send
                        var currentWorkAssignment = ActiveWorkAssignment;
                        // send report of CONFLICT immediately - if we have a running sequence this will tell them that
                        _ = RGServiceManager.GetInstance().SendRemoteWorkerHeartbeat(heartbeatRequest, HeartbeatResponseHandler, () => { });
                        SendHeartbeatForCurrentState(currentWorkAssignment);

                    }
                    else
                    {
                        // update the ActiveWorkAssignment if need be
                        if (incomingWorkAssignment.status == WorkAssignmentStatus.CANCELLED)
                        {
                            ActiveWorkAssignment.status = WorkAssignmentStatus.CANCELLED;
                            var currentWorkAssignment = ActiveWorkAssignment;
                            ActiveWorkAssignment = null;
                            currentWorkAssignment.Stop();
                            var heartbeatRequest = new SDKClientHeartbeatRequest()
                            {
                                clientId = this.ClientId,
                                activeSequence = GetActiveBotSequence(),
                                activeWorkAssignment = currentWorkAssignment
                            };
                            // send ACK of CANCEL immediately
                            _ = RGServiceManager.GetInstance().SendRemoteWorkerHeartbeat(heartbeatRequest, HeartbeatResponseHandler, () => { });
                        }
                        else if (incomingWorkAssignment.status == WorkAssignmentStatus.COMPLETE_ERROR || incomingWorkAssignment.status == WorkAssignmentStatus.COMPLETE_SUCCESS || incomingWorkAssignment.status == WorkAssignmentStatus.COMPLETE_TIMEOUT)
                        {
                            // server ack'd our completion.. clear out the assignment
                            var currentWorkAssignment = ActiveWorkAssignment;
                            ActiveWorkAssignment = null;
                            currentWorkAssignment.Stop();
                            // immediately send another heartbeat in case there is a new assignment
                            SendHeartbeatForCurrentState(null);
                        }
                        // else - we're still on the right assignment, just keep going if necessary
                    }
                }
                else // ActiveWorkAssignment == null
                {
                    // check to make sure no other segment is running right now
                    var activeSequence = GetActiveBotSequence();
                    if (activeSequence != null)
                    {
                        // conflict - send a heartbeat for that conflict (don't update the time though so the regular ones keep flowing)
                        incomingWorkAssignment.status = WorkAssignmentStatus.CONFLICT;
                        incomingWorkAssignment.details = new ConflictDetails(null, incomingWorkAssignment.id);
                        var heartbeatRequest = new SDKClientHeartbeatRequest()
                        {
                            clientId = this.ClientId,
                            activeSequence = GetActiveBotSequence(),
                            activeWorkAssignment = incomingWorkAssignment
                        };
                        // send report of CONFLICT immediately, don't mess with the active work assignment
                        _ = RGServiceManager.GetInstance().SendRemoteWorkerHeartbeat(heartbeatRequest, HeartbeatResponseHandler, () => { });
                    }
                    else
                    {
                        // nothing else is running currently... GOOD... we mark this work assignment as ready to start on next update
                        ActiveWorkAssignment = incomingWorkAssignment;
                        ActiveWorkAssignment.status = WorkAssignmentStatus.WAITING_TO_START;
                    }
                }
            }
        }

        private class ConflictDetails : IStringBuilderWriteable
        {
            private readonly long? _existingId;
            private readonly long _newId;
            public ConflictDetails(long? existingId, long newId)
            {
                this._existingId = existingId;
                this._newId = newId;
            }
            public void WriteToStringBuilder(StringBuilder stringBuilder)
            {
                stringBuilder.Append("{\"conflict\":");
                if (_existingId.HasValue)
                {
                    StringJsonConverter.WriteToStringBuilder(stringBuilder, $"CONFLICT starting WorkAssignment id: {_newId}. Another WorkAssignment id: {_existingId.Value} is active on this system.");
                }
                else
                {
                    StringJsonConverter.WriteToStringBuilder(stringBuilder, $"CONFLICT starting WorkAssignment id: {_newId}. Another BotSequence or BotSegment is active outside of a WorkAssignment on this system");
                }
                stringBuilder.Append("}");
            }
        }

        private void RegistrationResponseHandler(SDKClientRegistrationResponse response)
        {
            _registrationComplete = true;
            _registrationInProgress = false;
            ClientId = response.id;
        }

        public static ActiveSequence GetActiveBotSequence()
        {
            var controller = FindObjectOfType<BotSegmentsPlaybackController>();
            if (controller != null && controller.GetState() != PlayState.NotLoaded)
            {
                // a group of segments is playing.. let's see if we can figure out more details or not
                if (BotSequence.ActiveBotSequence != null)
                {
                    // this is a bot sequence, give them the name and path
                    return new ActiveSequence()
                    {
                        name = BotSequence.ActiveBotSequence.name,
                        description = BotSequence.ActiveBotSequence.description,
                        resourcePath = BotSequence.ActiveBotSequence.resourcePath,
                        sequence = BotSequence.ActiveBotSequence
                    };
                }
                // else - a zip file or other bot segments are running outside of a sequence
                return new ActiveSequence()
                {
                    name = "BotSegments are active outside of a BotSequence",
                    description = "BotSegment(s) are active outside of a BotSequence.  This happens when a user is testing individual BotSegments or BotSegmentLists from the overlay, or when a replay is running from a .zip file.",
                    resourcePath = "",
                    sequence = null
                };
            }

            return null;
        }

        /// <summary>
        /// Plays back a bot sequence, and then returns the save location of the recording.
        /// </summary>
        /// <param name="botSequenceInfo">(filePath,resourcePath,BotSequence) tuple of the bot sequence</param>
        /// <param name="setPlaybackResult">A callback that will be called with the results of this playback</param>
        /// <param name="timeout">How long in seconds to wait for this sequence to complete, less than or == 0 means wait forever (default=0)</param>
        internal static IEnumerator StartBotSequence((string,string, BotSequence) botSequenceInfo, Action<PlaybackResult> setPlaybackResult, int timeout = 0)
        {
            var startTime = Time.unscaledTime;

            botSequenceInfo.Item3.Play();

            var playbackController = Object.FindObjectOfType<BotSegmentsPlaybackController>();

            yield return null; // Allow the recording to start playing
            var didTimeout = false;
            while (playbackController.GetState() == PlayState.Playing || playbackController.GetState() == PlayState.Paused)
            {
                if (timeout > 0 && Time.unscaledTime > startTime + timeout )
                {
                    didTimeout = true;
                    break;
                }
                yield return null;
            }
            yield return null;
            var result = new PlaybackResult
            {
                saveLocation = playbackController.SaveLocation() + ".zip",
                success = !didTimeout
            };
            setPlaybackResult(result);
        }


    }
}
