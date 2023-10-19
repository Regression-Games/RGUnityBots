using System;
using System.Collections.Generic;
using System.Reflection;
using RegressionGames.StateActionTypes;
using UnityEngine;

/*
 * A component that can be inherited to relay game state information to
 * Regression Games. Includes a few default pieces of information that can
 * be enabled from the editor when attached to an object.
 */
namespace RegressionGames.RGBotConfigs
{
    [DisallowMultipleComponent]    
    [RequireComponent(typeof(RGEntity))]
    // ReSharper disable once InconsistentNaming
    public class RGState : MonoBehaviour, IRGState
    {
        
        // ReSharper disable once InconsistentNaming
        // we require each state to have an 'RGEntity' component
        protected RGEntity rgEntity
        {
            get { return GetComponent<RGEntity>(); }
        }

        /**
         * A function that is overriden to provide the custom state of this specific GameObject.
         * For example, you may want to retrieve and set the health of a player on the returned
         * object, or their inventory information
         */
        protected virtual Dictionary<string, object> GetState()
        {
            return new Dictionary<string, object>();
        }

        /**
         * Returns the entire internal state for this object, which consists of the default
         * states tracked by RG, and the result of any overridden GetState implementation.
         */
        public RGStateEntity GetGameObjectState()
        {
            var theTransform = rgEntity.transform;
            
            var state = CreateStateEntity();
            state["id"] = theTransform.GetInstanceID();
            state["type"] = rgEntity.objectType;
            state["isPlayer"] = rgEntity.isPlayer;
            state["isRuntimeObject"] = rgEntity.isRuntimeObject;
            state["position"] = theTransform.position;
            state["rotation"] = theTransform.rotation;
            
            var dict = GetState();
            foreach (var entry in dict)
            {
                // allow overriding default state fields like position
                state[entry.Key] = entry.Value;
            }

            return state;
        }

        protected virtual RGStateEntity CreateStateEntity()
        {
            return new RGStateEntity();
        }
    }

}