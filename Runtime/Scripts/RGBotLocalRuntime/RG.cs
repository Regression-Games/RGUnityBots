using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RG
    {
        public bool Completed { get; private set; } = false;
        
        private RGTickInfoData _tickInfo; 
        
        private readonly ConcurrentQueue<RGActionRequest> _actionQueue = new();
        
        private readonly ConcurrentQueue<RGValidationResult> _validationResults = new();

        public Dictionary<string, object> CharacterConfig = new();

        public readonly long ClientId;

        public RG(long clientId)
        {
            ClientId = clientId;
        }

        /**
         * <summary>{string} The current game scene name.</summary>
         */
        public string SceneName => _tickInfo.sceneName;

        /**
         * <summary>Mark this bot complete and ready for teardown.</summary>
         */
        public void Complete()
        {
            Completed = true;
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
            return FindPlayers(ClientId);
        }

        /**
         * <summary>Used to find the closest Entity to the given position.</summary>
         * <param name="objectType">{string | null} Search for entities of a specific type</param>
         * <param name="position">
         *     {Vector3 | null} Position to search from.  If not passed, attempts to use the client's bot
         *     position in index 0.
         * </param>
         * <param name="filterFunction">{Func&lt;RGStateEntity, bool&gt; | null} Function to filter entities.</param>
         * <returns>{RGStateEntity} The closest Entity matching the search criteria, or null if none match.</returns>
         */
        [CanBeNull]
        public RGStateEntity FindNearestEntity(string objectType = null, Vector3? position = null,
            Func<RGStateEntity, bool> filterFunction = null)
        {
            var result = FindEntities(objectType);

            if (filterFunction != null)
                // filter entities
                result = result.Where(filterFunction).ToList();

            if (result.Count > 1)
            {
                var pos = position ?? GetMyPlayers()[0].position;
                
                // sort by distance
                result.Sort((e1, e2) =>
                {
                    var val = MathFunctions.DistanceSq(pos, e1.position) -
                              MathFunctions.DistanceSq(pos, e1.position);
                    if (val < 0)
                        return -1;
                    if (val > 0) return 1;

                    return 0;
                });
            }

            return result.Count > 0 ? result[0] : null;
        }

        /**
         * <summary>Used to find a button Entity with the specific type name.</summary>
         * <param name="buttonName">{string | null} Search for button entities with a specific type name.</param>
         * <returns>{RGStateEntity} The Entity for a button matching the search criteria, or null if none match.</returns>
         */
        public RGStateEntity GetInteractableButton(string buttonName)
        {
            RGStateEntity button = FindEntities(buttonName).FirstOrDefault();
            if (button != null && EntityHasAttribute(button, "interactable", true))
            {
                return button;
            }
            return null;
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
         * <param name="clientId">{long | null} Search for players owned by a specific clientId</param>
         * <returns>{List&lt;RGStateEntity&gt;} All players owned by the given clientId, or all players in the state if client is null.</returns>
         */
        public List<RGStateEntity> FindPlayers(long? clientId = null)
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
         * <summary>Used to find if an entity has the specific attribute and expectedValue.</summary>
         * <param name="entity">{RGStateEntity} Search for attributes of the specified entity</param>
         * * <param name="attribute">{string} The name of the attribute to evaluate.</param>
         * * <param name="expectedValue">{object | null} Expected value of the attribute. `null` means that the attribute's value is not evaluated</param>
         * <returns>{bool} True if the provided entity has the specified attribute and matches the expectedValue if provided.</returns>
         */
        public bool EntityHasAttribute(RGStateEntity entity, string attribute, object expectedValue = null)
        {
            if (entity.TryGetValue(attribute, out var attributeValue))
            {
                if (expectedValue != null)
                {
                    return attributeValue.Equals(expectedValue);
                }
                
                return true;
            }

            return false;
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
        
        
        internal void SetCharacterConfig(Dictionary<string, object> characterConfig)
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
        
        // Debug actions for bots

        public void DrawLineTo(Vector3 position, Color color, string name)
        {
            PerformAction(new RGActionRequest("DrawLineTo", new Dictionary<string, object>()
            {
                { "position", position},
                { "color", color},
                { "name", name},
            }));
        }

        public void RemoveLineTo(string name)
        {
            PerformAction(new RGActionRequest("DrawLineTo", new Dictionary<string, object>()
            {
                { "remove", true},
                { "name", name},
            }));
        }
        
        public void RemoveAllLinesTo()
        {
            PerformAction(new RGActionRequest("DrawLineTo", new Dictionary<string, object>()
            {
                { "removeAll", true}
            }));
        }

        public void DrawText(string content)
        {
            PerformAction(new RGActionRequest("DrawText", new Dictionary<string, object>()
            {
                { "content", content },
            }));
        }

        public void RemoveText()
        {
            PerformAction(new RGActionRequest("DrawText", new Dictionary<string, object>()
            {
                { "remove", true }
            }));
        }

        public void DrawIndicator(Vector3 position, float size, Color color, string name)
        {
            PerformAction(new RGActionRequest("DrawIndicator", new Dictionary<string, object>()
            {
                { "position", position},
                { "size", size },
                { "color", color},
                { "name", name},
            }));
        }

        public void RemoveIndicator(string name)
        {
            PerformAction(new RGActionRequest("DrawIndicator", new Dictionary<string, object>()
            {
                { "remove", true },
                { "name", name}
            }));
        }
        
        public void RemoveAllIndicators()
        {
            PerformAction(new RGActionRequest("DrawIndicator", new Dictionary<string, object>()
            {
                { "removeAll", true }
            }));
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