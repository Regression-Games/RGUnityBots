using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RegressionGames.StateActionTypes;
using Vector3 = UnityEngine.Vector3;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RG
    {
        private RGTickInfoData _tickInfo; 
        
        private readonly ConcurrentQueue<RGActionRequest> _actionQueue = new();
        
        private readonly ConcurrentQueue<RGValidationResult> _validationResults = new();

        public string CharacterConfig { get; private set; } = null;

        public readonly uint ClientId;

        public RG(uint clientId)
        {
            this.ClientId = clientId;
        }

        /**
         * <summary>Retrieve the current game state.</summary>
         * <returns>{RGStateEntity} The current game state.</returns>
         */
        public Dictionary<string, RGStateEntity> GetState()
        {
            return _tickInfo.gameState;
        }

        /**
         * <summary>Retrieve list of all player/bot entities controlled by this clientId.</summary>
         * <returns>{List&lt;RGStateEntity&gt;} List of the entities from the state.</returns>
         */
        public List<RGStateEntity> GetMyPlayers()
        {
            return this.FindPlayers(ClientId);
        }

        /**
         * <summary>Used to find the closest Entity to the given position.</summary>
         * <param name="objectType">{string | null} Search for entities of a specific type</param>
         * * <param name="position">{Vector3 | null} Position to search from.  If not passed, attempts to use the client's bot position in index 0.</param>
         * <returns>{RGStateEntity} The closest Entity matching the search criteria, or null if none match.</returns>
         */
        [CanBeNull]
        public RGStateEntity FindNearestEntity(string objectType = null, Vector3? position = null)
        {
            var result = this.FindEntities(objectType);

            var pos = position ?? this.GetMyPlayers()[0].position ?? Vector3.zero;
            
            result.Sort((e1, e2) =>
            {
                var val = MathFunctions.DistanceSq(pos, e1.position ?? Vector3.zero) -
                             MathFunctions.DistanceSq(pos, e1.position ?? Vector3.zero);
                if (val < 0)
                {
                    return -1;
                }
                else if (val > 0)
                {
                    return 1;
                }

                return 0;
            });

            return result.Count>0 ? result[0] : null;
        }

        /**
         * <summary>Used to find a list of entities from the game state.</summary>
         * <param name="objectType">{string | null} Search for entities of a specific type</param>
         * <returns>{List&lt;RGStateEntity&gt;} All entities with the given objectType, or all entities in the state if objectType is null.</returns>
         */
        public List<RGStateEntity> FindEntities(string objectType = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity>();
            }
            
            // filter down to objectType Matches
            var result = gameState.Values.Where(value =>
            {
                if (objectType != null && value.type != null)
                {
                    return objectType.Equals(value.type);
                }
                return true;
            });

            return result.ToList();
        }

        /**
         * <summary>Used to find a list of players from the game state.</summary>
         * <param name="clientId">{uint | null} Search for players owned by a specific clientId</param>
         * <returns>{List&lt;RGStateEntity&gt;} All players owned by the given clientId, or all players in the state if client is null.</returns>
         */
        public List<RGStateEntity> FindPlayers(uint? clientId = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity>();
            }

            // filter down to isPlayer
            var result = gameState.Values.Where(value => value.isPlayer);
            
            // filter down to clientId Matches
            if (clientId != null)
            {
                result = result.Where(value => clientId == value.clientId);
            }

            return result.ToList();
        }

        /**
         * <summary>Queue an action to perform.  Multiple actions can be queued per tick</summary>
         * <param name="rgAction">{RGActionRequest} action request to queue</param>
         */
        public void PerformAction(RGActionRequest rgAction)
        {
            _actionQueue.Enqueue(rgAction);
        }
        
        /**
         * <summary>Record a validation result.  Multiple validation results can be recorded per tick</summary>
         * <param name="rgValidation">{RGValidationResult} validation result to queue</param>
         */
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
    
    internal static class MathFunctions 
    {

        /**
         * <returns>{double} The square distance between two positions</returns>
         */
        public static double DistanceSq (Vector3 position1, Vector3 position2) {
            return Math.Pow(position2.x - position1.x, 2) + Math.Pow(position2.y - position1.y, 2) + Math.Pow(position2.z - position1.z, 2);
        }
    }
}