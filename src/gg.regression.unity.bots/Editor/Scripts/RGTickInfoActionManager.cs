using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using RegressionGames.StateActionTypes;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace RegressionGames.Editor
{
#if UNITY_EDITOR
    public class RGTickInfoActionManager
    {
        private readonly Dictionary<long, RGEntityReplayData> _entityInfo = new();

        public void Reset()
        {
            _entityInfo.Clear();
        }

        public List<long> GetAllEntityIds()
        {
            return _entityInfo.Keys.ToList();
        }

        public List<RGEntityReplayData> GetAllEntities()
        {
            var result = _entityInfo.Values.ToList();
            return result;
        }

        [CanBeNull]
        public RGEntityDataForTick GetEntityInfoForTick(int tickNumber, long entityId)
        {
            if (tickNumber > 0 && _entityInfo.TryGetValue(entityId, out var replayData))
                return RGEntityDataForTick.FromReplayData(replayData, tickNumber);
            return null;
        }

        public Vector3[] GetPathForEntityId(int tickNumber, long entityId)
        {
            if (_entityInfo.TryGetValue(entityId, out var replayData))
                if (replayData.tickInfo[tickNumber - 1] != null)
                {
                    var linePoints = new Stack<Vector3>();
                    // rewind the tickInfo until we hit a null;
                    // then give them back in the correct order
                    for (var i = tickNumber - 1; i >= 0; i--)
                    {
                        var ti = replayData.tickInfo[i];
                        if (ti == null) break;

                        linePoints.Push(ti.state.position);
                    }

                    if (linePoints.Count > 0) return linePoints.ToArray();
                }

            return Array.Empty<Vector3>();
        }

        public void ProcessTick(int tickNumber, RGTickInfoData tickData)
        {
            foreach (var gameStateObject in tickData.gameState)
            {
                RGStateEntity_Core entity = gameStateObject.Value;
                var entityId = entity.id;

                var tickInfo = PopulateTickInfoDataForEntity(tickNumber, entityId);

                tickInfo.state = entity;

                // support old 'type' field to be able to load extracts created pre Jan 2024
                PopulateReplayDataForEntity(entityId, entity.name, (string)entity.GetValueOrDefault("type", null), entity.types, entity.isPlayer);

                // handle strong typing on position / rotation accessors
                if (entity.ContainsKey("position") && entity["position"] is JObject)
                {
                    var jObj = (JObject)entity["position"];
                    entity["position"] = new Vector3(jObj["x"].Value<float>(),
                        jObj["y"].Value<float>(),
                        jObj["z"].Value<float>());
                }

                if (entity.ContainsKey("rotation") && entity["rotation"] is JObject)
                {
                    var jObj = (JObject)entity["rotation"];
                    entity["rotation"] = new Quaternion(jObj["x"].Value<float>(),
                        jObj["y"].Value<float>(),
                        jObj["z"].Value<float>(),
                        jObj["w"].Value<float>());
                }
            }
        }

        public void ProcessReplayData(int tickNumber, long entityId, RGStateActionReplayData data)
        {
            var tickInfo = PopulateTickInfoDataForEntity(tickNumber, entityId);
            tickInfo.actions = data.actions ?? Array.Empty<RGActionRequest>();
            tickInfo.validationResults = data.validationResults ?? Array.Empty<RGValidationResult>();
            // show path and actions by default only for bot players with actions
            PopulateReplayDataForEntity(entityId, null, null, Array.Empty<string>(), true, true,
                true, true);
        }

        private RGEntityReplayData PopulateReplayDataForEntity(long entityId, string name, string type, string[] types, bool isPlayer = false,

            bool? showPath = null,
            bool? showActions = null, bool? highlight = null)
        {
            _entityInfo.TryAdd(entityId, new RGEntityReplayData());
            _entityInfo[entityId].id = entityId;
            _entityInfo[entityId].isPlayer = isPlayer;
            _entityInfo[entityId].name = name;
            _entityInfo[entityId].types = types;

            // It's ok for _us_ to call the obsolete 'type' property, because we're supporting old replays.
#pragma warning disable CS0612 // Type or member is obsolete
            _entityInfo[entityId].type = type;
#pragma warning restore CS0612 // Type or member is obsolete

            if (showPath != null) _entityInfo[entityId].showPath = showPath.Value;
            if (showActions != null) _entityInfo[entityId].showActions = showActions.Value;
            if (highlight != null) _entityInfo[entityId].showHighlight = highlight.Value;

            return _entityInfo[entityId];
        }

        private RGEntityTickInfo PopulateTickInfoDataForEntity(int tickNumber, long entityId)
        {
            // make sure this player is in the dictionary
            PopulateReplayDataForEntity(entityId, null, null, Array.Empty<string>());

            _entityInfo[entityId].tickInfo ??= new List<RGEntityTickInfo>();

            // make sure the list is big enough
            while (_entityInfo[entityId].tickInfo.Count < tickNumber) _entityInfo[entityId].tickInfo.Add(null);

            return _entityInfo[entityId].tickInfo[tickNumber - 1] ??
                   (_entityInfo[entityId].tickInfo[tickNumber - 1] = new RGEntityTickInfo());
        }
    }

    [Serializable]
    public class RGEntityReplayData
    {
        public List<RGEntityTickInfo> tickInfo;
        public long id;
        public string name;
        // support the 'type' field of old replays
        [Obsolete]
        public string type;
        public string[] types;
        public bool isPlayer = false;
        public bool enabled = true;
        public bool showPath;
        public bool showActions;
        public bool showHighlight;

        // It's ok for _us_ to call the obsolete 'type' property, because we're supporting old replays.
#pragma warning disable CS0612 // Type or member is obsolete
        public string objectName => $"{name ?? (type ?? "[" + string.Join(',', types) + "]")}_{id}";
#pragma warning restore CS0612 // Type or member is obsolete
    }

    [Serializable]
    public class RGEntityTickInfo
    {
        public RGActionRequest[] actions = Array.Empty<RGActionRequest>();
        public RGValidationResult[] validationResults = Array.Empty<RGValidationResult>();
        public RGStateEntity_Core state;
    }

    [Serializable]
    public class RGEntityDataForTick
    {
        public RGEntityReplayData Data { get; private set; }
        [CanBeNull] public RGEntityTickInfo TickInfo { get; private set; }
        public bool JustSpawned { get; private set; }
        public bool JustDespawned { get; private set; }

        [CanBeNull]
        public static RGEntityDataForTick FromReplayData(RGEntityReplayData data, int tickNumber)
        {
            var ti = tickNumber > 0 && tickNumber <= data.tickInfo.Count ? data.tickInfo[tickNumber - 1] : null;
            var result = new RGEntityDataForTick
            {
                Data = data,
                TickInfo = ti,
                // if prior tick's info was null, we just spawn
                JustSpawned = ti != null && (tickNumber < 2 || data.tickInfo[tickNumber - 2] == null),
                JustDespawned = ti == null && tickNumber > 1 &&
                                tickNumber - 2 < data.tickInfo.Count && data.tickInfo[tickNumber - 2] != null
            };
            return result;
        }
    }
#endif
}
