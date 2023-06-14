using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RegressionGames.StateActionTypes;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEngine;

namespace RegressionGames.Editor
{
    public class RGTickInfoActionManager
    {
        private readonly Dictionary<long, RGAgentReplayData> playerInfo = new();

        public void Reset()
        {
            playerInfo.Clear();
        }

        public List<long> GetAllPlayerIds()
        {
            return playerInfo.Keys.ToList();
        }

        public List<RGAgentReplayData> GetAllPlayers()
        {
            var result = playerInfo.Values.ToList();
            return result;
        }

        [CanBeNull]
        public RGAgentDataForTick GetPlayerInfoForTick(int tickNumber, long playerId)
        {
            if (tickNumber > 0 && playerInfo.TryGetValue(playerId, out var replayData))
                return RGAgentDataForTick.FromReplayData(replayData, tickNumber);
            return null;
        }

        public Vector3[] GetPathForPlayerId(int tickNumber, long playerId)
        {
            if (playerInfo.TryGetValue(playerId, out var replayData))
                if (replayData.tickInfo[tickNumber - 1] != null)
                {
                    var linePoints = new Stack<Vector3>();
                    // rewind the tickInfo until we hit a null;
                    // then give them back in the correct order
                    for (var i = tickNumber - 1; i >= 0; i--)
                    {
                        var ti = replayData.tickInfo[i];
                        if (ti == null || ti.position == null) break;

                        linePoints.Push(ti.position.Value);
                    }

                    if (linePoints.Count > 0) return linePoints.ToArray();
                }

            return Array.Empty<Vector3>();
        }

        public void processTick(int tickNumber, RGTickInfoData tickData)
        {
            foreach (var gameStateObject in tickData.gameState)
            {
                dynamic jsonObject = (JObject)gameStateObject.Value;
/* TODO 
                long playerId = jsonObject.id;

                var tickInfo = populateTickInfoDataForPlayer(tickNumber, playerId);

                tickInfo.state = jsonObject;
                populateReplayDataForPlayer(playerId, (string?)jsonObject.type);
                if (jsonObject.position != null)
                    tickInfo.position = new Vector3((float)jsonObject.position.x,
                        (float)jsonObject.position.y,
                        (float)jsonObject.position.z);
                        */
            }
        }

        public void processActions(int tickNumber, long playerId, RGActionRequest[] actions)
        {
            var tickInfo = populateTickInfoDataForPlayer(tickNumber, playerId);
            tickInfo.actions = actions;
            if (actions.Length > 0)
                // show path and actions by default only for bot players with actions
                populateReplayDataForPlayer(playerId, null, true,
                    true, true);
        }

        private RGAgentReplayData populateReplayDataForPlayer(long playerId, [CanBeNull] string type = null,
            bool? showPath = null,
            bool? showActions = null, bool? highlight = null)
        {
            playerInfo.TryAdd(playerId, new RGAgentReplayData());
            playerInfo[playerId].id = playerId;
            if (type != null) playerInfo[playerId].type = type;
            if (showPath != null) playerInfo[playerId].showPath = showPath.Value;
            if (showActions != null) playerInfo[playerId].showActions = showActions.Value;

            if (highlight != null) playerInfo[playerId].showHighlight = highlight.Value;

            return playerInfo[playerId];
        }

        private RGAgentTickInfo populateTickInfoDataForPlayer(int tickNumber, long playerId)
        {
            // make sure this player is in the dictionary
            populateReplayDataForPlayer(playerId);

            if (playerInfo[playerId].tickInfo == null) playerInfo[playerId].tickInfo = new List<RGAgentTickInfo>();

            // make sure the list is big enough
            while (playerInfo[playerId].tickInfo.Count < tickNumber) playerInfo[playerId].tickInfo.Add(null);

            if (playerInfo[playerId].tickInfo[tickNumber - 1] == null)
                playerInfo[playerId].tickInfo[tickNumber - 1] = new RGAgentTickInfo();

            return playerInfo[playerId].tickInfo[tickNumber - 1];
        }
    }

    [Serializable]
    public class RGAgentReplayData
    {
        public List<RGAgentTickInfo?> tickInfo;
        public long id;
        public string type;
        public bool enabled = true;
        public bool showPath;
        public bool showActions;
        public bool showHighlight;
        public string objectName => $"{type}_{id}";
    }

    [Serializable]
    public class RGAgentTickInfo
    {
        public RGActionRequest[] actions = Array.Empty<RGActionRequest>();
        [CanBeNull] public Vector3? position;
        public dynamic state;
    }

    [Serializable]
    public class RGAgentDataForTick
    {
        public RGAgentReplayData data { get; private set; }
        [CanBeNull] public RGAgentTickInfo? tickInfo { get; private set; }
        public bool justSpawned { get; private set; }
        public bool justDespawned { get; private set; }

        [CanBeNull]
        public static RGAgentDataForTick FromReplayData(RGAgentReplayData data, int tickNumber)
        {
            var ti = tickNumber <= data.tickInfo.Count ? data.tickInfo[tickNumber - 1] : null;
            var result = new RGAgentDataForTick();
            result.data = data;
            result.tickInfo = ti;
            // if prior tick's info was null, we just spawn
            result.justSpawned = ti != null && (tickNumber < 2 || data.tickInfo[tickNumber - 2] == null);
            result.justDespawned = ti == null && tickNumber > 1 &&
                                   tickNumber - 2 < data.tickInfo.Count && data.tickInfo[tickNumber - 2] != null;
            return result;
        }
    }
}
