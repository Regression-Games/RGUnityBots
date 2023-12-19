using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames
{
    public static class BehavioursWithStateOrActions
    {
        public static void Initialize()
        {
            var stateEntityTypes = AppDomain.CurrentDomain.GetAssemblies()
                // alternative: .GetExportedTypes()
                .SelectMany(domainAssembly => domainAssembly.GetTypes())
                .Where(t => typeof(IRGStateEntity).IsAssignableFrom(t) || t.IsSubclassOf(typeof(RGStateEntityBase))
                    // alternative: => type.IsSubclassOf(typeof(B))
                    // alternative: && type != typeof(B)
                    // alternative: && ! type.IsAbstract
                )
                .Where(t =>
                    !t.IsAbstract && !t.IsInterface && t != typeof(RGStateEntity_Empty) && t != typeof(RGStateEntity_Core) && !t.IsSubclassOf(typeof(RGStateEntity_Core))
                ).ToArray();
            
            BehaviourStateMappings.Clear();

            var behaviourTypeNames = new Dictionary<string, Type>();
            
            foreach (var stateEntityType in stateEntityTypes)
            {
                // assumes the generated classes have these in them
                var entityTypeName = (string)stateEntityType.GetField("EntityTypeName")?.GetValue(null);
                var behaviourType = (Type)stateEntityType.GetField("BehaviourType")?.GetValue(null);
                var isPlayer = stateEntityType.GetField("IsPlayer")?.GetValue(null);
                if (entityTypeName == null || behaviourType == null || isPlayer == null)
                {
                    RGDebug.LogError($"Error: {stateEntityType.FullName} must define fields\r\n" +
                                     $"'public static readonly string EntityTypeName = \"<EntityTypeName>\";' where '<EntityTypeName>' is either a custom RG State type or is the name of the MonoBehaviour with which this state is related.\r\n" +
                                     $"'public static readonly Type BehaviourType = \"typeof(<BehaviourName>)\";' where '<BehaviourName>' is the class of the Behaviour this state represents.\r\n" +
                                     $"'public static readonly bool IsPlayer = \"<IsPlayer>\";' where '<IsPlayer>' is true if this state or its Behaviour represent a player controlled object.");
                }
                else
                {
                    BehaviourStateMappings[behaviourType!] =
                        new BehaviourStateMappingContainer(stateEntityType, entityTypeName, true.Equals(isPlayer));
                    if (behaviourTypeNames.TryGetValue(entityTypeName!, out var otherBehaviourType))
                    {
                        RGDebug.LogError(
                            $"Error: RGStateEntity type name: {entityTypeName} is used to identify multiple MonoBehaviours.  One of these will NOT be visible in the state sent to Regression Games bots.  MonoBehaviours: {behaviourType.FullName} , {otherBehaviourType.FullName}");
                    }

                    behaviourTypeNames[entityTypeName] = behaviourType;
                }
            }

            var actionTypes = AppDomain.CurrentDomain.GetAssemblies()
                // alternative: .GetExportedTypes()
                .SelectMany(domainAssembly => domainAssembly.GetTypes())
                .Where(type => typeof(IRGActions).IsAssignableFrom(type) && type != typeof(IRGActions)
                    // alternative: => type.IsSubclassOf(typeof(B))
                    // alternative: && type != typeof(B)
                    // alternative: && ! type.IsAbstract
                ).ToArray();
            
            BehaviourActionMappings.Clear();
            foreach (var actionType in actionTypes)
            {
                // assumes the generated classes have these in them
                var etn = (string)actionType.GetField("EntityTypeName")?.GetValue(null);
                var behaviourType = (Type)actionType.GetField("BehaviourType")?.GetValue(null);
                var delegates =
                    (IDictionary<string, Delegate>)actionType.GetField("ActionRequestDelegates")?.GetValue(null);
                if (etn == null || behaviourType == null || delegates == null)
                {
                    RGDebug.LogError($"Error: {actionType.FullName} must define fields\r\n" +
                                     $"'public static readonly string EntityTypeName = \"<EntityTypeName>\";' where '<EntityTypeName>' is either a custom RG State type or is the name of the MonoBehaviour this action invokable against.\r\n" +
                                     $"'public static readonly Type BehaviourType = \"typeof(<BehaviourName>)\";' where '<BehaviourName>' is the class of the Behaviour this action is invokable against.\r\n" +
                                     $"'public static readonly IDictionary<string, Delegate> ActionRequestDelegates = ...;' where ... is a Dictionary with the actionName to delegate method bindings.");
                }
                else
                {
                    BehaviourActionMappings[behaviourType!] = new BehaviourActionsMappingContainer(
                        actionType,
                        etn,
                        delegates
                    );
                }
            }

        }
        
        private static readonly Dictionary<Type,BehaviourStateMappingContainer> BehaviourStateMappings = new ();
        
        private static readonly Dictionary<Type,BehaviourActionsMappingContainer> BehaviourActionMappings = new ();

        // ReSharper disable once InconsistentNaming
        public static BehaviourStateMappingContainer GetRGStateEntityMappingForBehaviour(MonoBehaviour behaviour)
        {
            // protect against missing scripts on game objects
            if (behaviour == null)
            {
                return null;
            }
            BehaviourStateMappings.TryGetValue(behaviour.GetType(), out var result);
            return result;
        }
        
        // ReSharper disable once InconsistentNaming
        public static BehaviourActionsMappingContainer GetRGActionsMappingForBehaviour(MonoBehaviour behaviour)
        {
            // protect against missing scripts on game objects
            if (behaviour == null)
            {
                return null;
            }
            BehaviourActionMappings.TryGetValue(behaviour.GetType(), out var result);
            return result;
        }

    }
    
    public sealed class BehaviourStateMappingContainer
    {
        // ReSharper disable once InconsistentNaming
        public readonly Type RGStateEntityType;
        public readonly string EntityType;
        public readonly bool IsPlayer;

        public BehaviourStateMappingContainer(Type rgStateEntityType, string entityType, bool isPlayer)
        {
            this.RGStateEntityType = rgStateEntityType;
            this.EntityType = entityType;
            this.IsPlayer = isPlayer;
        }
    }
    
    public sealed class BehaviourActionsMappingContainer
    {
        // ReSharper disable once InconsistentNaming
        public readonly Type ActionsType;
        public readonly string EntityType;
        public readonly IDictionary<string, Delegate> ActionRequestDelegates;

        public BehaviourActionsMappingContainer(Type actionsType, string entityType, IDictionary<string, Delegate> actionRequestDelegates)
        {
            this.EntityType = entityType;
            this.ActionsType = actionsType;
            this.ActionRequestDelegates = actionRequestDelegates;
        }
    }

}
