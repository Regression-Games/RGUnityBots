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

        public List<Dictionary<string, object>> FindPlayers(string clientId = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<Dictionary<string, object>>();
            }

            // filter down to isPlayer
            var result = gameState.Values.Where(value =>
            {
                var entity = (value as Dictionary<string, object>);
                if (entity != null && entity.TryGetValue("isPlayer", out var isPlayer) == true)
                {
                    if (Boolean.TryParse(isPlayer.ToString(), out var result))
                    {
                        return result;
                    }
                }

                return false;
            });
            
            // filter down to clientId Matches
            /*if (clientId != null)
            {
                result = result.Where(value =>
                {
                    if ((value as Dictionary<string, object>)?.TryGetValue("clientId", out var cliId) == true)
                    {
                        return clientId.Equals(cliId?.ToString());
                    }

                    return false;
                });
            }*/

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