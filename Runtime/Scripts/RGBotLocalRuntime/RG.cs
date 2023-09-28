using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateActionTypes;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RG
    {
        private RGTickInfoData _tickInfo; 
        
        private readonly ConcurrentQueue<RGActionRequest> _actionQueue = new();
        
        private readonly ConcurrentQueue<RGValidationResult> _validationResults = new();

        public string CharacterConfig { get; private set; } = null;

        public Dictionary<string,object> GetState(string entityId = null)
        {
            if (entityId == null)
            {
                return _tickInfo.gameState;
            }

            if (_tickInfo.gameState.TryGetValue(entityId, out var result))
            {
                return result as Dictionary<string,object>;
            }
            return null;
        }

        public List<Dictionary<string, object>> FindEntities(string objectType = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<Dictionary<string, object>>();
            }
            
            // filter down to objectType Matches
            var result = gameState.Values.Where(value =>
            {
                if (objectType != null && value is Dictionary<string, object> entity && entity.TryGetValue("type", out var objType))
                {
                    return objectType.Equals(objType);
                }
                return true;
            });

            return result.Select(value => value as Dictionary<string, object>).ToList();
        }

        public List<Dictionary<string, object>> FindPlayers(uint? clientId = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<Dictionary<string, object>>();
            }

            // filter down to isPlayer
            var result = gameState.Values.Where(value =>
            {
                if (value is Dictionary<string, object> entity && entity.TryGetValue("isPlayer", out var isPlayer))
                {
                    return (bool)isPlayer;
                }
                return false;
            });
            
            // filter down to clientId Matches
            if (clientId != null)
            {
                result = result.Where(value =>
                {
                    if (value is Dictionary<string, object> entity && entity.TryGetValue("clientId", out var cliId))
                    {
                        return clientId == (uint)cliId;
                    }
                    return false;
                });
            }

            return result.Select(value => value as Dictionary<string, object>).ToList();
        }

        public void PerformAction(RGActionRequest rgAction)
        {
            _actionQueue.Enqueue(rgAction);
        }
        
        public void RecordValidation(RGValidationResult rgValidation)
        {
            _validationResults.Enqueue(rgValidation);
        }
        
        
        internal void SetCharacterConfig(string characterConfig)
        {
            this.CharacterConfig = characterConfig;
        }
        
        internal void SetTickInfo(RGTickInfoData tickInfo)
        {
            this._tickInfo = tickInfo;
        }
        
        internal List<RGActionRequest> FlushActions()
        {
            List<RGActionRequest> result = new();
            while (_actionQueue.TryDequeue(out RGActionRequest action))
            {
                result.Add(action);
            }
            return result;
        }

        internal List<RGValidationResult> FlushValidations()
        {
            List<RGValidationResult> result = new();
            while (_validationResults.TryDequeue(out RGValidationResult validation))
            {
                result.Add(validation);
            }
            return result;
        }
        
        
    }
}