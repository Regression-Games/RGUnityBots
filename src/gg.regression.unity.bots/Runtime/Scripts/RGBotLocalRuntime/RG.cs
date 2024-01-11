using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    public class RG
    {
        public bool Completed { get; private set; }

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
         * <summary>{long} The current tick since the start of the first bot that was run this session.</summary>
         */
        public long Tick => _tickInfo.tick;

        /**
         * <summary>Mark this bot complete and ready for teardown.</summary>
         */
        public void Complete()
        {
            Completed = true;
        }

        /**
         * <summary>Retrieve the current game state.</summary>
         * <returns>{Dictionary&lt;string, RGStateEntity_Core&gt;} The current game state.</returns>
         */
        public Dictionary<string, RGStateEntity_Core> GetState()
        {
            return _tickInfo.gameState;
        }

        /**
         * <summary>Returns the first player for my client id.
         * <br/><br/>
         * WARNING: When controlling multiple player bots from a single client the result of this method may change from one tick to the next.</summary>
         * <returns>{RGStateEntity_Core} The Entity for the player owned by my clientId at index 0.</returns>
         */
        public RGStateEntity_Core GetMyPlayer()
        {
            var players = GetMyPlayers();
            if (players.Count > 0)
            {
                return players[0];
            }
            return null;
        }

        /**
         * <summary>Retrieve list of all player/bot entities controlled by this clientId.</summary>
         * <returns>{List&lt;RGStateEntity_Core&gt;} List of the entities for all players owned by my clientId from the state.</returns>
         */
        public List<RGStateEntity_Core> GetMyPlayers()
        {
            return FindPlayers(ClientId);
        }

        /**
         * <summary>Used to find a button Entity with the specific type name.</summary>
         * <param name="scenePath">{string | null} Search for button entities with a specific scenePath.</param>
         * <returns>{RGStateEntity_Button} The Entity for a button matching the search criteria, or null if none match.</returns>
         */
        public RGStateEntity_Button GetInteractableButtonByScenePath(string scenePath)
        {
            // prefer the button where the name is closest to the front
            RGStateEntity_Core button = FindEntitiesByScenePath(scenePath).FirstOrDefault();
            if (button != null && EntityHasAttribute(button, "interactable", true))
            {
                return (RGStateEntity_Button)button;
            }
            return null;
        }

        /**
         * <summary>Used to find a button Entity with the specific type name.</summary>
         * <param name="buttonName">{string | null} Search for button entities with a specific name.</param>
         * <returns>{RGStateEntity_Button} The Entity for a button matching the search criteria, or null if none match.</returns>
         */
        public RGStateEntity_Button GetInteractableButtonByName(string buttonName)
        {
            RGStateEntity_Core button = FindEntitiesByName(buttonName, true).FirstOrDefault();
            if (button != null && EntityHasAttribute(button, "interactable", true))
            {
                return (RGStateEntity_Button)button;
            }
            return null;
        }
        
        /**
         * <summary>Used to find the closest Entity to the given position.</summary>
         * <param name="name">{string | null} Search for entities with a specific name</param>
         * <param name="partial">{bool} Search for entities partially matching a specific name</param>
         * <param name="position">
         *     {Vector3 | null} Position to search from.  If not passed, attempts to use the client's bot
         *     position in index 0.
         * </param>
         * <param name="filterFunction">{Func&lt;RGStateEntity, bool&gt; | null} Function to filter entities.</param>
         * <returns>{RGStateEntity_Core} The closest Entity matching the search criteria, or null if none match.</returns>
         */
        [CanBeNull]
        public RGStateEntity_Core FindNearestEntityByName(string name = null, bool partial = true, Vector3? position = null,
            Func<RGStateEntity_Core, bool> filterFunction = null)
        {
            var result = FindEntitiesByName(name, partial);

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
                              MathFunctions.DistanceSq(pos, e2.position);
                    if (val < 0)
                        return -1;
                    if (val > 0) return 1;

                    return 0;
                });
            }

            return result.Count > 0 ? result[0] : null;
        }

        /**
         * <summary>Used to find the closest Entity to the given position.</summary>
         * <param name="typeName">{string | null} Search for entities of a specific type name</param>
         * <param name="position">
         *     {Vector3 | null} Position to search from.  If not passed, attempts to use the client's bot
         *     position in index 0.
         * </param>
         * <param name="filterFunction">{Func&lt;RGStateEntity, bool&gt; | null} Function to filter entities.</param>
         * <returns>{RGStateEntity_Core} The closest Entity matching the search criteria, or null if none match.</returns>
         */
        [CanBeNull]
        public RGStateEntity_Core FindNearestEntityWithTypeName(string typeName = null, Vector3? position = null,
            Func<RGStateEntity_Core, bool> filterFunction = null)
        {
            var result = FindEntitiesWithTypeName(typeName);

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
                              MathFunctions.DistanceSq(pos, e2.position);
                    if (val < 0)
                        return -1;
                    if (val > 0) return 1;

                    return 0;
                });
            }

            return result.Count > 0 ? result[0] : null;
        }
        
        /**
         * <summary>Returns all buttons from the game state.</summary>
         * <returns>{List&lt;RGStateEntity_Button&gt;} All buttons in the state.</returns>
         */
        public List<RGStateEntity_Button> AllButtons()
        {
            return _tickInfo.gameState.Values.Where(v => v is RGStateEntity_Button).Cast<RGStateEntity_Button>().ToList();
        }

        /**
         * <summary>Returns all entities from the game state.</summary>
         * <param name="includeButtons">Should buttons be included in results (default false)</param>
         * <returns>{List&lt;RGStateEntity_Core&gt;} All entities in the state.</returns>
         */
        public List<RGStateEntity_Core> AllEntities(bool includeButtons = false)
        {
            return _tickInfo.gameState.Values.Where(v => includeButtons || v is not RGStateEntity_Button).ToList();
        }
        
        /**
         * <summary>Used to find a list of entities from the game state.</summary>
         * <param name="scenePath">{string | null} Search for entities matching the given scenePath.  The path can be partial to match any gameObjects that start with the provided path. sorts those with the match closest to the front to the front</param>
         * <returns>{List&lt;RGStateEntity_Core&gt;} All entities with the given scenePath, or all entities in the state if name is null.</returns>
         */
        public List<RGStateEntity_Core> FindEntitiesByScenePath(string scenePath = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity_Core>();
            }

            // filter down to objectType Matches
            var result = gameState.Values.Where(value =>
            {
                if (scenePath != null && value.pathInScene != null)
                {
                    return value.pathInScene.Contains(scenePath);
                }
                return true;
            });

            if (scenePath != null)
            {
                return result.OrderBy(v => v.pathInScene.IndexOf(scenePath, StringComparison.Ordinal)).ToList();
            }

            return result.ToList();
        }
        
        
        /**
         * <summary>Used to find a list of entities from the game state.</summary>
         * <param name="name">{string | null} Search for entities of a specific name</param>
         * <param name="partial">{bool} Search for entities partially matching a specific name, sorts those with the match closest to the front to the front</param>
         * <returns>{List&lt;RGStateEntity_Core&gt;} All entities with the given name, or all entities in the state if name is null.</returns>
         */
        public List<RGStateEntity_Core> FindEntitiesByName(string name = null, bool partial = true)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity_Core>();
            }

            // filter down to objectType Matches
            var result = gameState.Values.Where(value =>
            {
                if (name != null && value.name != null)
                {
                    if (partial)
                    {
                        return value.name.Contains(name);    
                    }

                    return value.name == name;

                }
                return true;
            });

            if (partial && name != null)
            {
                var orderedResult= result.OrderBy(v => v.name.IndexOf(name, StringComparison.Ordinal)).ToList();
                return orderedResult;
            }

            return result.ToList();
        }

        [Obsolete("See RG.FindEntitiesWithTypeName(string) ...")]
        public List<RGStateEntity_Core> FindEntities(string typeName = null)
        {
            return FindEntitiesWithTypeName(typeName);
        }

        /**
         * <summary>Used to find a list of entities from the game state.</summary>
         * <param name="objectType">{string | null} Search for entities of a specific type</param>
         * <returns>{List&lt;RGStateEntity_Core&gt;} All entities with the given objectType, or all entities in the state if objectType is null.</returns>
         */
        public List<RGStateEntity_Core> FindEntitiesWithTypeName(string typeName = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity_Core>();
            }

            // filter down to objectType Matches
            var result = gameState.Values.Where(value =>
            {
                if (typeName != null && value.types != null)
                {
                    return value.types.Contains(typeName);
                }
                return true;
            });

            return result.ToList();
        }
        
        /**
         * <summary>Used to find a list of entities from the game state that have the specified type.</summary>
         * <returns>{List&lt;RGStateEntity_Core&gt;} All entities with the given objectType, or all entities in the state if objectType is null.</returns>
         */
        public List<RGStateEntity_Core> FindEntitiesWithType<T>() where T: IRGStateEntity
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity_Core>();
            }

            // filter down to entities that have at least 1 entry of this type
            var result = gameState.Values.Where(stateEntity =>
                stateEntity.Values.Any(v => v is IRGStateEntity && typeof(T) == v.GetType())
            );

            return result.ToList();
        }

        /**
         * <summary>Used to find a list of players from the game state.</summary>
         * <param name="clientId">{long | null} Search for players owned by a specific clientId</param>
         * <returns>{List&lt;RGStateEntity_Core&gt;} All players owned by the given clientId, or all players in the state if client is null.</returns>
         */
        public List<RGStateEntity_Core> FindPlayers(long? clientId = null)
        {
            var gameState = _tickInfo.gameState;
            if (gameState.Count == 0)
            {
                return new List<RGStateEntity_Core>();
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
        public bool EntityHasAttribute(IRGStateEntity entity, string attribute, object expectedValue = null)
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
         * <param name="rgAction"><see cref="RGActionRequest"/> action request to queue</param>
         */
        public void PerformAction(RGActionRequest rgAction)
        {
            _actionQueue.Enqueue(rgAction);
        }

        /**
         * <summary>Record a validation result.  Multiple validation results can be recorded per tick</summary>
         * <param name="rgValidation"><see cref="RGValidationResult"/> validation result to queue</param>
         */
        public void RecordValidation(RGValidationResult rgValidation)
        {
            _validationResults.Enqueue(rgValidation);
        }

        public void RecordValidation(string name, RGValidationResultType resultType, [CanBeNull] string icon = null)
        {
            _validationResults.Enqueue(new RGValidationResult(name, resultType, _tickInfo.tick, icon));
        }

        /**
         * <summary>Sets the <see cref="CharacterConfig"/> field by parsing the provided JSON string.</summary>
         * <param name="characterConfigJson">A JSON string representing the character config.</param>
         */
        // NOTE: This is called by the generated BotEntryPoint class in Agent Builder Bots.
        // Avoid changing the signature, name, or accessibility of this method!
        public void SetCharacterConfigFromJson(string characterConfigJson)
        {
            this.CharacterConfig = JsonConvert.DeserializeObject<Dictionary<string, object>>(characterConfigJson);
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

        public static class MathFunctions
        {
            /**
             * <returns>{double} The square distance between two positions</returns>
             */
            public static double DistanceSq (Vector3 position1, Vector3 position2) {
                return Math.Pow(position2.x - position1.x, 2) + Math.Pow(position2.y - position1.y, 2) + Math.Pow(position2.z - position1.z, 2);
            }
        }

    }

}
