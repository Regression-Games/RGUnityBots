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
                .Where(type => typeof(IRGStateEntity).IsAssignableFrom(type)
                    // alternative: => type.IsSubclassOf(typeof(B))
                    // alternative: && type != typeof(B)
                    // alternative: && ! type.IsAbstract
                ).ToArray();
            
            BehaviourStateMappings.Clear();

            var behaviourTypeNames = new Dictionary<string, Type>();
            
            foreach (var stateEntityType in stateEntityTypes)
            {
                // assumes the generated classes have these in them
                var entityTypeName = (string)stateEntityType.GetField("EntityTypeName")?.GetValue(null);
                var behaviourType = (Type)stateEntityType.GetField("BehaviourType")?.GetValue(null);
                var isPlayer = true.Equals(stateEntityType.GetField("IsPlayer")?.GetValue(null));
                BehaviourStateMappings[behaviourType!] = new BehaviourStateMappingContainer(stateEntityType, entityTypeName, isPlayer);
                if (behaviourTypeNames.TryGetValue(entityTypeName!, out var otherBehaviourType))
                {
                    RGDebug.LogError($"Error: RGStateType name: {entityTypeName} is used to identify multiple MonoBehaviours.  One of these will NOT be visible in the state sent to Regression Games bots.  MonoBehaviours: {behaviourType.FullName} , {otherBehaviourType.FullName}");
                }
                behaviourTypeNames[entityTypeName] = behaviourType;
            }

            var actionTypes = AppDomain.CurrentDomain.GetAssemblies()
                // alternative: .GetExportedTypes()
                .SelectMany(domainAssembly => domainAssembly.GetTypes())
                .Where(type => typeof(IRGActions).IsAssignableFrom(type)
                    // alternative: => type.IsSubclassOf(typeof(B))
                    // alternative: && type != typeof(B)
                    // alternative: && ! type.IsAbstract
                ).ToArray();
            
            BehaviourActionMappings.Clear();
            foreach (var actionType in actionTypes)
            {
                // assumes the generated classes have these in them
                var behaviourType = (Type)actionType.GetField("BehaviourType")?.GetValue(null);
                var entityTypeName = BehaviourStateMappings[behaviourType!]?.EntityType;
                BehaviourActionMappings[behaviourType!] = new BehaviourActionsMappingContainer(
                    actionType,
                    entityTypeName,
                    (IDictionary<string, Delegate>)actionType.GetField("ActionRequestDelegates")?.GetValue(null)
                );
            }

        }
        
        private static readonly Dictionary<Type,BehaviourStateMappingContainer> BehaviourStateMappings = new ();
        
        private static readonly Dictionary<Type,BehaviourActionsMappingContainer> BehaviourActionMappings = new ();

        // ReSharper disable once InconsistentNaming
        public static BehaviourStateMappingContainer GetRGStateEntityMappingForBehaviour(MonoBehaviour behaviour)
        {
            BehaviourStateMappings.TryGetValue(behaviour.GetType(), out var result);
            return result;
        }
        
        // ReSharper disable once InconsistentNaming
        public static BehaviourActionsMappingContainer GetRGActionsMappingForBehaviour(MonoBehaviour behaviour)
        {
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
