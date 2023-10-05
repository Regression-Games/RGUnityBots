using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    public class RGTickInfoActionManager
    {
        private readonly Dictionary<long, RGAgentReplayData> entityInfo = new();

        public void Reset()
        {
            entityInfo.Clear();
        }

        public List<long> GetAllEntityIds()
        {
            return entityInfo.Keys.ToList();
        }

        public List<RGAgentReplayData> GetAllEntities()
        {
            var result = entityInfo.Values.ToList();
            return result;
        }

        [CanBeNull]
        public RGAgentDataForTick GetEntityInfoForTick(int tickNumber, long entityId)
        {
            if (tickNumber > 0 && entityInfo.TryGetValue(entityId, out var replayData))
                return RGAgentDataForTick.FromReplayData(replayData, tickNumber);
            return null;
        }

        public Vector3[] GetPathForEntityId(int tickNumber, long entityId)
        {
            if (entityInfo.TryGetValue(entityId, out var replayData))
                if (replayData.tickInfo[tickNumber - 1] != null)
                {
                    var linePoints = new Stack<Vector3>();
                    // rewind the tickInfo until we hit a null;
                    // then give them back in the correct order
                    for (var i = tickNumber - 1; i >= 0; i--)
                    {
                        var ti = replayData.tickInfo[i];
                        if (ti == null || ti.state.position == null) break;

                        linePoints.Push(ti.state.position.Value);
                    }

                    if (linePoints.Count > 0) return linePoints.ToArray();
                }

            return Array.Empty<Vector3>();
        }

        public void processTick(int tickNumber, RGTickInfoData tickData)
        {
            foreach (var gameStateObject in tickData.gameState)
            {
                RGStateEntity entity = gameStateObject.Value;
                long entityId = entity.id;

                var tickInfo = populateTickInfoDataForEntity(tickNumber, entityId);

                tickInfo.state = entity;
                bool isPlayer = entity.isPlayer;

                bool isRuntimeObject = entity.isRuntimeObject;
                populateReplayDataForEntity(entityId, isPlayer, isRuntimeObject, entity.type);
                
                // handle strong typing on position / rotation accessors
                if (entity.ContainsKey("position") && entity["position"] != null)
                {
                    if (entity["position"] is JObject)
                    {
                        var jObj = (JObject)entity["position"];
                        entity["position"] = new Vector3(jObj["x"].Value<float>(),
                            jObj["y"].Value<float>(),
                            jObj["z"].Value<float>());
                    }
                }
                
                if (entity.ContainsKey("rotation") && entity["rotation"] != null)
                {
                    if (entity["rotation"] is JObject)
                    {
                        var jObj = (JObject)entity["rotation"];
                        entity["rotation"] = new Quaternion(jObj["x"].Value<float>(),
                            jObj["y"].Value<float>(),
                            jObj["z"].Value<float>(),
                            jObj["w"].Value<float>());
                    }
                }
            }
        }

        public void processReplayData(int tickNumber, long entityId, RGStateActionReplayData data)
        {
            var tickInfo = populateTickInfoDataForEntity(tickNumber, entityId);
            tickInfo.actions = data.actions == null ? Array.Empty<RGActionRequest>() : data.actions;
            tickInfo.validationResults = data.validationResults == null ? Array.Empty<RGValidationResult>() : data.validationResults;
            // show path and actions by default only for bot players with actions
            populateReplayDataForEntity(entityId, true,false, null, true,
                true, true);
        }

        private RGAgentReplayData populateReplayDataForEntity(long entityId, bool isPlayer = false, bool isRuntimeObject = false, [CanBeNull] string type = null,
            bool? showPath = null,
            bool? showActions = null, bool? highlight = null)
        {
            entityInfo.TryAdd(entityId, new RGAgentReplayData());
            entityInfo[entityId].id = entityId;
            entityInfo[entityId].isPlayer = isPlayer;
            entityInfo[entityId].isRuntimeObject = isRuntimeObject;
            if (type != null)
            {
                entityInfo[entityId].type = type;
                if (isRuntimeObject)
                {
                    // makes sure this type is registered if this is non static entity
                    ReplayModelManager.GetInstance().AddObjectType(type);
                }
            }
            if (showPath != null) entityInfo[entityId].showPath = showPath.Value;
            if (showActions != null) entityInfo[entityId].showActions = showActions.Value;
            if (highlight != null) entityInfo[entityId].showHighlight = highlight.Value;

            return entityInfo[entityId];
        }

        private RGAgentTickInfo populateTickInfoDataForEntity(int tickNumber, long entityId)
        {
            // make sure this player is in the dictionary
            populateReplayDataForEntity(entityId);

            if (entityInfo[entityId].tickInfo == null) entityInfo[entityId].tickInfo = new List<RGAgentTickInfo>();

            // make sure the list is big enough
            while (entityInfo[entityId].tickInfo.Count < tickNumber) entityInfo[entityId].tickInfo.Add(null);

            if (entityInfo[entityId].tickInfo[tickNumber - 1] == null)
                entityInfo[entityId].tickInfo[tickNumber - 1] = new RGAgentTickInfo();

            return entityInfo[entityId].tickInfo[tickNumber - 1];
        }
    }

    [Serializable]
    public class RGAgentReplayData
    {
        public List<RGAgentTickInfo> tickInfo;
        public long id;
        public string type;
        public bool isPlayer = false;
        public bool isRuntimeObject = false;
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
        public RGValidationResult[] validationResults = Array.Empty<RGValidationResult>();
        public RGStateEntity state;
    }

    [Serializable]
    public class RGAgentDataForTick
    {
        public RGAgentReplayData data { get; private set; }
        [CanBeNull] public RGAgentTickInfo tickInfo { get; private set; }
        public bool justSpawned { get; private set; }
        public bool justDespawned { get; private set; }

        [CanBeNull]
        public static RGAgentDataForTick FromReplayData(RGAgentReplayData data, int tickNumber)
        {
            var ti = tickNumber > 0 && tickNumber <= data.tickInfo.Count ? data.tickInfo[tickNumber - 1] : null;
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
#endif
}
