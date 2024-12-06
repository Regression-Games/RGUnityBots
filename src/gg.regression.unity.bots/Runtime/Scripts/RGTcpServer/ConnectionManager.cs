using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using RegressionGames.RemoteOrchestration.Models;
using RegressionGames.RemoteOrchestration.Types;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using UnityEditor;
using UnityEngine;

namespace RegressionGames
{
    // ReSharper disable InconsistentNaming
    [ExecuteInEditMode]
    public class ConnectionManager : MonoBehaviour
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
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Path.GetFullPath("Packages/gg.regression.unity.bots/Runtime/Resources/RegressionGames.exe"),
                Arguments = "Extra args to pass to the program",
                UseShellExecute = false, 
                CreateNoWindow = false
            };
            Process.Start(startInfo);
        }
        
        /// <summary>
        /// Starts a TCP listener and starts a background thread to listen for client connections
        /// </summary>
        public void Start()
        {
            EditorApplication.playModeStateChanged += OnExitPlayMode;

            RGTcpServer.OnClientHandshake += OnClientHandshake;
            RGTcpServer.ProcessClientMessage += ProcessClientMessage;
            
            // this will load Bot Sequences to send to the client
            // TODO: load Bot Sequences again whenever we edit them
            // this could be through the client or from reloading resources...
            StartCoroutine(RGSequenceManager.ResolveSequenceFiles(ProcessResolvedSequences));
        }
        
        private void ProcessResolvedSequences(IDictionary<string, (string, BotSequence)> sequences)
        {
            m_availableBotSequences = sequences.Select(kvp => new AvailableBotSequence(kvp.Key, kvp.Value.Item2)).ToList();
        }

        /// <summary>
        /// Watch server-side resources to see if there are any updates we need to send to connected clients
        /// </summary>
        private void Update()
        {
            var server = RGTcpServer.GetInstance();
            if (!server.IsRunning)
            {
                return;
            }

            if (Application.isPlaying)
            {
                // check if the active sequence has changed
                var activeSequence = GetActiveBotSequence();
                if (activeSequence?.resourcePath != m_activeSequence?.resourcePath)
                {
                    m_activeSequence = activeSequence;
                    var message = new TcpMessage
                    {
                        type = TcpMessageType.ACTIVE_SEQUENCE,
                        payload = new ActiveSequenceTcpMessageData
                        {
                            activeSequence = m_activeSequence
                        }
                    };
                    server.QueueMessage(message);
                }
            
                // check if we need to start playing a sequence
                if (m_startPlayingSequence != null)
                {
                    if (Application.isPlaying)
                    {
                        var botManager = RGBotManager.GetInstance();
                        if (botManager != null)
                        {
                            botManager.OnBeginPlaying();
                        }
                        m_replayToolbarManager.selectedReplayFilePath = null;
                        m_startPlayingSequence.Play();
                    }
                    m_startPlayingSequence = null;
                }
            }
        }

        /// <summary>
        /// Send some information to the client immediately after successful handshake
        /// </summary>
        private void OnClientHandshake(TcpClient client)
        {
            var server = RGTcpServer.GetInstance();
            
            // send the currently active sequence to the client
            var message = new TcpMessage
            {
                type = TcpMessageType.ACTIVE_SEQUENCE,
                payload = new ActiveSequenceTcpMessageData
                {
                    activeSequence = m_activeSequence
                }
            };
            server.QueueMessage(client, message);
                            
            // then send the complete list of available bot sequences
            message = new TcpMessage
            {
                type = TcpMessageType.AVAILABLE_SEQUENCES,
                payload = new AvailableSequencesTcpMessageData
                {
                    availableSequences = m_availableBotSequences
                }
            };
            server.QueueMessage(client, message);
        }

        private void ProcessClientMessage(TcpClient client, TcpMessage message)
        {
            var server = RGTcpServer.GetInstance();
            switch (message.type)
            {
                case TcpMessageType.PING:
                {
                    server.QueueMessage(client, new TcpMessage { type = TcpMessageType.PONG }); break;
                }
                case TcpMessageType.PLAY_SEQUENCE:
                {
                    var playSequenceData = (PlaySequenceTcpMessageData) message.payload;
                    var botSequence = BotSequence.LoadSequenceJsonFromPath(playSequenceData.resourcePath);
                    m_startPlayingSequence = botSequence.Item3;
                    break;
                }
            }
        }
        
        private ActiveSequence GetActiveBotSequence()
        {
            if (m_botSegmentsPlaybackController != null && m_botSegmentsPlaybackController.GetState() != PlayState.NotLoaded)
            {
                if (m_botSegmentsPlaybackController.GetState() == PlayState.Playing || m_botSegmentsPlaybackController.GetState() == PlayState.Starting || (m_botSegmentsPlaybackController.GetState() == PlayState.Stopped && m_botSegmentsPlaybackController.ReplayCompletedSuccessfully() == null && m_botSegmentsPlaybackController.GetLastSegmentPlaybackWarning() == null))
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
                        };
                    }

                    // else - a zip file or other bot segments are running outside of a sequence
                    return new ActiveSequence()
                    {
                        name = "BotSegments are active outside of a BotSequence",
                        description = "BotSegment(s) are active outside of a BotSequence.  This happens when a user is testing individual BotSegments or BotSegmentLists from the overlay, or when a replay is running from a .zip file.",
                        resourcePath = "",
                    };
                }
            }

            return null;
        }
        
        private void OnApplicationQuit()
        {
            // m_server.Stop();
        }
        
        /// <summary>
        /// Assign or reset variables when the editor transitions to or from play mode
        /// </summary>
        private void OnExitPlayMode(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                {
                    m_startPlayingSequence = null;
                    m_activeSequence = null;
                    break;
                }
                case PlayModeStateChange.EnteredPlayMode:
                {
                    m_botSegmentsPlaybackController = FindObjectOfType<BotSegmentsPlaybackController>();
                    m_replayToolbarManager = FindObjectOfType<ReplayToolbarManager>();
                    break;
                }
                case PlayModeStateChange.ExitingPlayMode:
                {
                    m_botSegmentsPlaybackController = null;
                    m_replayToolbarManager = null;
                    m_startPlayingSequence = null;
                    m_activeSequence = null;
                    break;
                }
            }
        }
    }
}

