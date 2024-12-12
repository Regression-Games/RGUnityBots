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
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RegressionGames.ClientDashboard
{
    /// <summary>
    /// Starts and stops RGTcpServer, handles and sends messages to and from connected clients
    /// </summary>
    [ExecuteAlways]
    public class RGTcpManager : MonoBehaviour
    {
        // the last active sequence that was sent to the client
        private static BotSegmentsPlaybackController m_botSegmentsPlaybackController;
        private static ReplayToolbarManager m_replayToolbarManager;

        private static List<AvailableBotSequence> m_availableBotSequences = new ();
        private static List<BotSequenceEntry> m_availableBotSegments = new();
        
        private static ActiveSequence m_activeSequence = null;
        private static BotSequence m_startPlayingSequence = null;
        private static BotSegmentList m_startPlayingSegment = null;
        private static bool m_shouldStopReplay = false;
        

        public void Start()
        {
            
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

            if (!Application.isPlaying)
            {
                // I don't know why Unity insists on instantiating this object to default fields
                // but we really do want null...
                m_activeSequence = null;
            }
            
            RGTcpServer.OnClientHandshake -= OnClientHandshake;
            RGTcpServer.OnClientHandshake += OnClientHandshake;
            RGTcpServer.ProcessClientMessage -= ProcessClientMessage;
            RGTcpServer.ProcessClientMessage += ProcessClientMessage;
            RGTcpServer.Start();
            StartCoroutine(RGSequenceManager.ResolveSequenceFiles(ProcessAndSendSequences));
            ProcessAndSendSegments();
        }

        /// <summary>
        /// Watch server-side resources to see if there are any updates we need to send to connected clients.
        /// Also perform any actions requested by a client which must be executed on the main thread. 
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
        
            // check if we need to start playing a sequence or segment
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
                m_startPlayingSegment = null;
                m_shouldStopReplay = false;
            } 
            else if (m_startPlayingSegment != null)
            {
                var botManager = RGBotManager.GetInstance();
                if (botManager != null)
                {
                    botManager.OnBeginPlaying();
                }
                m_botSegmentsPlaybackController.SetDataContainer(new BotSegmentsPlaybackContainer(m_startPlayingSegment.segments));
                m_botSegmentsPlaybackController.Play();
                m_startPlayingSequence = null;
                m_startPlayingSegment = null;
                m_shouldStopReplay = false;
            } 
            else if (m_shouldStopReplay)
            {
                m_replayToolbarManager.StopReplay();
                m_shouldStopReplay = false;
            }
        }
        
        #region Editor Only  
        
#if UNITY_EDITOR
        /// <summary>
        /// Create a menu item to open the client dashboard, which will connect to this server
        /// </summary>
        [MenuItem("Regression Games/Open Dashboard")]
        public static void OpenDashboard()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.GetFullPath("Packages/gg.regression.unity.bots/Runtime/Resources/RegressionGames.exe"),
                CreateNoWindow = false
            };
            Process.Start(startInfo);
        }
        
        /// <summary>
        /// This and Start are both necessary in editor because:
        /// * Script reload may occur after Start is called, wiping values like available sequences
        /// * Script reload may not trigger on events like entering play mode, but Start always will
        /// </summary>
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void DidReloadScripts()
        {
            RGTcpServer.OnClientHandshake -= OnClientHandshake;
            RGTcpServer.OnClientHandshake += OnClientHandshake;
            RGTcpServer.ProcessClientMessage -= ProcessClientMessage;
            RGTcpServer.ProcessClientMessage += ProcessClientMessage;
            RGTcpServer.Start();
            
            var it  = RGSequenceManager.ResolveSequenceFiles(ProcessAndSendSequences);
            while(it.MoveNext()){}
            ProcessAndSendSegments();
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
#endif
        #endregion
        
        #region TCPServer Callbacks

        /// <summary>
        /// Send some information to the client immediately after successful handshake
        /// </summary>
        private static void OnClientHandshake(TcpClient client)
        {
            SendActiveSequence();
            SendAvailableSequences();
            SendAvailableSegments();
        }
        
        /// <summary>
        /// Handle a new message from a client based on its type
        /// </summary>
        private static void ProcessClientMessage(TcpClient client, TcpMessage message)
        {
            switch (message.type)
            {
                case TcpMessageType.Ping:
                {
                    RGTcpServer.QueueMessage(new TcpMessage { type = TcpMessageType.Pong }, client); break;
                }
                case TcpMessageType.PlaySequence:
                {
                    var playSequenceData = (PlayResourceTcpMessageData) message.payload;
                    var botSequence = BotSequence.LoadSequenceJsonFromPath(playSequenceData.resourcePath);
                    m_startPlayingSequence = botSequence.Item3;
                    break;
                }
                case TcpMessageType.PlaySegment:
                {
                    var playSegmentData = (PlayResourceTcpMessageData) message.payload;
                    var segmentList = BotSequence.CreateBotSegmentListForPath(playSegmentData.resourcePath, out var sessId);
                    m_startPlayingSegment = segmentList;
                    break;
                }
                case TcpMessageType.StopReplay:
                {
                    m_shouldStopReplay = true;
                    break;
                }
            }
        }
        
        #endregion
        
        #region Bot Sequences
                  
        /// <summary>
        /// Called after sequence files are resolved
        /// Updates the dashboard with the available sequences
        /// </summary>
        private static void ProcessAndSendSequences(IDictionary<string, (string, BotSequence)> sequences)
        {
            m_availableBotSequences = sequences.Select(kvp => new AvailableBotSequence(kvp.Key, kvp.Value.Item2)).ToList();
            SendAvailableSequences();
        }
        
        private static void ProcessAndSendSegments()
        {
            m_availableBotSegments = BotSegment.LoadAllSegments().Values.Select(seg => seg.Item2).ToList();
            SendAvailableSegments();
        }
        
        /// <summary>
        /// Returns the active bot sequence, if there is one
        /// </summary>
        private static ActiveSequence GetActiveBotSequence()
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
        
        #endregion

        #region Send Messages

        private static void SendAvailableSequences([CanBeNull] TcpClient client = null)
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
        
        private static void SendAvailableSegments([CanBeNull] TcpClient client = null)
        {
            var message = new TcpMessage
            {
                type = TcpMessageType.AvailableSegments,
                payload = new AvailableSegmentsTcpMessageData
                {
                    availableSegments = m_availableBotSegments
                }
            };
            RGTcpServer.QueueMessage(message, client);
        }
        
        private static void SendActiveSequence([CanBeNull] TcpClient client = null)
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

