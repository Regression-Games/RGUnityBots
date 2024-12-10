using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using JetBrains.Annotations;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.RemoteOrchestration.Types;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEditor;
using UnityEngine;

namespace RegressionGames.ClientDashboard
{
    /// <summary>
    /// Starts and stops RGTcpServer, handles and sends messages to and from connected clients
    /// </summary>
    [ExecuteAlways]
    public class RGTcpManager : MonoBehaviour
    {
        // the last active sequence that was sent to the client
        private BotSegmentsPlaybackController m_botSegmentsPlaybackController;
        private BotSequence m_startPlayingSequence = null;
        private ActiveSequence m_activeSequence = null;
        private ReplayToolbarManager m_replayToolbarManager;
        private List<AvailableBotSequence> m_availableBotSequences = new ();

        /// <summary>
        /// Create a menu item to open the client dashboard, which will connect to this server
        /// </summary>
        [MenuItem("Regression Games/Open Dashboard")]
        public static void OpenRGDashboard()
        {
            // Configure the process
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.GetFullPath("Packages/gg.regression.unity.bots/Runtime/Resources/RegressionGames.exe"),
                // TODO: Arguments,
                CreateNoWindow = false
            };
            Process.Start(startInfo);
        }
        
        public void Start()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (!Application.isPlaying)
            {
                // I don't know why Unity insists on instantiating this object to default fields
                // but we really do want null...
                m_activeSequence = null;
            }
            
            RGTcpServer.Start();
            RGTcpServer.OnClientHandshake -= OnClientHandshake;
            RGTcpServer.OnClientHandshake += OnClientHandshake;
            RGTcpServer.ProcessClientMessage -= ProcessClientMessage;
            RGTcpServer.ProcessClientMessage += ProcessClientMessage;
            StartCoroutine(RGSequenceManager.ResolveSequenceFiles(ProcessResolvedSequences));
        }
        
        /// <summary>
        /// Assign or reset variables when the editor transitions to or from play mode
        /// </summary>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                {
                    m_startPlayingSequence = null;
                    m_activeSequence = null;
                    RGTcpServer.Stop();
                    break;  
                }
                case PlayModeStateChange.ExitingPlayMode:
                {
                    m_botSegmentsPlaybackController = null;
                    m_replayToolbarManager = null;
                    m_startPlayingSequence = null;
                    m_activeSequence = null;
                    RGTcpServer.Stop();
                    break;
                }
                case PlayModeStateChange.EnteredEditMode:
                {
                    RGTcpServer.Stop();
                    break;
                }
                case PlayModeStateChange.EnteredPlayMode:
                {
                    m_botSegmentsPlaybackController = FindObjectOfType<BotSegmentsPlaybackController>();
                    m_replayToolbarManager = FindObjectOfType<ReplayToolbarManager>();
                    break;
                }
            }
        }

        /// <summary>
        /// Watch server-side resources to see if there are any updates we need to send to connected clients
        /// </summary>
        private void Update()
        {
            if (!RGTcpServer.IsRunning || !Application.isPlaying)
            {
                return;
            }

            // check if the active sequence has changed
            var activeSequence = GetActiveBotSequence();
            if (activeSequence?.resourcePath != m_activeSequence?.resourcePath)
            {
                m_activeSequence = activeSequence;
                SendActiveSequence();
            }
        
            // check if we need to start playing a sequence
            if (m_startPlayingSequence != null)
            {
                var botManager = RGBotManager.GetInstance();
                if (botManager != null)
                {
                    botManager.OnBeginPlaying();
                }
                m_replayToolbarManager.selectedReplayFilePath = null;
                m_startPlayingSequence.Play();
                m_startPlayingSequence = null;
            }
        }

        /// <summary>
        /// Send some information to the client immediately after successful handshake
        /// </summary>
        private void OnClientHandshake(TcpClient client)
        {
            SendActiveSequence();
            SendAvailableSequences();
        }
                
        /// <summary>
        /// Called after sequence files are resolved
        /// Updates the dashboard with the available sequences
        /// </summary>
        private void ProcessResolvedSequences(IDictionary<string, (string, BotSequence)> sequences)
        {
            m_availableBotSequences = sequences.Select(kvp => new AvailableBotSequence(kvp.Key, kvp.Value.Item2)).ToList();
            SendAvailableSequences();
        }
        
        /// <summary>
        /// Handle a new message from a client based on its type
        /// </summary>
        private void ProcessClientMessage(TcpClient client, TcpMessage message)
        {
            switch (message.type)
            {
                case TcpMessageType.Ping:
                {
                    RGTcpServer.QueueMessage(new TcpMessage { type = TcpMessageType.Pong }, client); break;
                }
                case TcpMessageType.PlaySequence:
                {
                    var playSequenceData = (PlaySequenceTcpMessageData) message.payload;
                    var botSequence = BotSequence.LoadSequenceJsonFromPath(playSequenceData.resourcePath);
                    m_startPlayingSequence = botSequence.Item3;
                    break;
                }
            }
        }
        
        /// <summary>
        /// Get the active bot sequence, if there is one.
        /// Update won't call this in edit mode.
        /// </summary>
        private ActiveSequence GetActiveBotSequence()
        {
            if (m_botSegmentsPlaybackController == null || m_botSegmentsPlaybackController.GetState() == PlayState.NotLoaded)
            {
                return null;
            }

            var playState = m_botSegmentsPlaybackController.GetState();
            if (playState == PlayState.Playing || playState == PlayState.Starting || (playState == PlayState.Stopped && m_botSegmentsPlaybackController.ReplayCompletedSuccessfully() == null && m_botSegmentsPlaybackController.GetLastSegmentPlaybackWarning() == null))
            {
                // a group of segments is playing.. let's see if we can figure out more details or not
                if (BotSequence.ActiveBotSequence != null)
                {
                    // this is a bot sequence, give them the name and path
                    return new ActiveSequence
                    {
                        name = BotSequence.ActiveBotSequence.name,
                        description = BotSequence.ActiveBotSequence.description,
                        resourcePath = BotSequence.ActiveBotSequence.resourcePath
                    };
                }

                // else - a zip file or other bot segments are running outside of a sequence
                return new ActiveSequence
                {
                    name = "BotSegments are active outside of a BotSequence",
                    description = "BotSegment(s) are active outside of a BotSequence.  This happens when a user is testing individual BotSegments or BotSegmentLists from the overlay, or when a replay is running from a .zip file.",
                    resourcePath = ""
                };
            }

            return null;
        }

        #region Send Messages

        private void SendAvailableSequences([CanBeNull] TcpClient client = null)
        {
            var message = new TcpMessage 
            {
                type = TcpMessageType.AvailableSequences,
                payload = new AvailableSequencesTcpMessageData
                {
                    availableSequences = m_availableBotSequences
                }
            };
            RGTcpServer.QueueMessage(message, client);
        }
        
        private void SendActiveSequence([CanBeNull] TcpClient client = null)
        {
            var message = new TcpMessage
            {
                type = TcpMessageType.ActiveSequence,
                payload = new ActiveSequenceTcpMessageData
                {
                    activeSequence = m_activeSequence
                }
            };
            RGTcpServer.QueueMessage(message, client);
        }

        #endregion
    }
}

