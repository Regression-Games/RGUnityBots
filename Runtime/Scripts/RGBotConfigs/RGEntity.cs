using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    public class RGEntity : MonoBehaviour
    {
        [Header("General Information")] [Tooltip("Does this object represent a human/bot player ?")]
        public bool isPlayer;

        [Tooltip("A type name for associating like objects in the state")]
        public string objectType;

        // this is used in our toolkit to understand which things would need dynamic models
        [Tooltip("Is this object spawned during runtime, or a fixed object in the scene?")]
        public bool isRuntimeObject = false;

        [Header("3D Positioning")] 
        public bool syncPosition = true;
        public bool syncRotation = true;
        
        // maps action names to RGAction components
        private Dictionary<string, RGAction> actionMap = new Dictionary<string, RGAction>();
            
        // The client Id that owns this entity
        // Used as a performance optimization for mapping the ClientId into the state payloads
        public long? ClientId = null;
        
        /**
         * Updates the registry with a game object that has RGAction scripts attached to it
         */
        void Start()
        {
            var actions = GetComponents<RGAction>();
            foreach (var action in actions)
            {
                actionMap[action.GetActionName()] = action;
            }

            if (actionMap.Count > 0)
            {
                RGDebug.LogDebug($"Entity registered with {actionMap.Count} actions");
            }
        }

        public RGAction GetActionHandler(string actionName)
        {
            if(actionMap.TryGetValue(actionName, out RGAction action))
            {
                return action;
            }
            return null;
        }

        /*
         * RGEntity holds an 'ObjectType' that is provided by the developer. We map this object type
         * to its actions and states
         */
        public Dictionary<Type, string> MapObjectType(Dictionary<Type, string> objectTypeMap)
        {
            Dictionary<Type, string> cloneDict = new Dictionary<Type, string>(objectTypeMap);
            KeyValuePair<Type, string>[] keyValuePairs = objectTypeMap.ToArray();

            for(int i= 0; i < keyValuePairs.Length; i++)
            {
                Type componentType = keyValuePairs[i].Key;
                
                // skip previously assigned object types
                if (!string.IsNullOrEmpty(keyValuePairs[i].Value))
                {
                    continue;
                }
                
                // map object type to components with 'objectName'
                var component = gameObject.GetComponent(componentType);
                if (component != null)
                {
                    cloneDict[componentType] = objectType;
                }
            }
            return cloneDict;
        }
    }
}