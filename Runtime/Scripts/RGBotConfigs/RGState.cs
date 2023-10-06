using System;
using System.Collections.Generic;
using System.Reflection;
using RegressionGames.StateActionTypes;
using UnityEngine;

/*
 * A component that can be inherited to relay game state information to
 * Regression Games. Includes a few default pieces of information that can
 * be enabled from the editor when attached to an object.
 *
 * TODO (REG-1300): Can we use a generic type instead of a dictionary? That way users can
 *       debug and use the states within their own code?
 */
namespace RegressionGames.RGBotConfigs
{
    public class RGState : MonoBehaviour
    {
        [Header("General Information")] [Tooltip("Does this object represent a human/bot player ?")]
        public bool isPlayer;

        [Tooltip("A type name for associating like objects in the state")]
        public string objectType;

        // this is used in our toolkit to understand which things would need dynamic models
        [Tooltip("Is this object spawned during runtime, or a fixed object in the scene?")]
        public bool isRuntimeObject = false;

        /**
         * A function that is overriden to provide the custom state of this specific GameObject.
         * For example, you may want to retrieve and set the health of a player on the returned
         * object, or their inventory information
         */
        public virtual Dictionary<string, object> GetState()
        {
            return new Dictionary<string, object>();
        }

        /**
         * Returns the entire internal state for this object, which consists of the default
         * states tracked by RG, and the result of any overridden GetState implementation.
         */
        public RGStateEntity GetGameObjectState()
        {
            var theTransform = this.transform;
            
            var state = new RGStateEntity()
            {
                ["id"] = theTransform.GetInstanceID(),
                ["type"] = objectType,
                ["isPlayer"] = isPlayer,
                ["isRuntimeObject"] = isRuntimeObject,
            };

            state["position"] = theTransform.position;
            state["rotation"] = theTransform.rotation;
            var dict = GetState();
            foreach (var entry in dict)
            {
                state.Add(entry.Key, entry.Value);
            }
            
            
            // find all RGStateProvider behaviors and get their values
            var stateProviders = this.gameObject.GetComponents<RGStateProvider>();
            foreach (var rgStateProvider in stateProviders)
            {
                var type = rgStateProvider.GetType();
                var dictionary = new Dictionary<string, object>();
                state[type.Name] = dictionary;
                foreach (PropertyInfo prop in type.GetProperties())
                {
                    try
                    {
                        if (prop.CanRead &&
                            prop.PropertyType.IsPublic || prop.PropertyType.IsSerializable)
                        {
                            if (prop.PropertyType.IsPrimitive ||
                                prop.PropertyType == typeof(Vector3) ||
                                prop.PropertyType == typeof(Vector2) || 
                                prop.PropertyType == typeof(Vector4) ||
                                prop.PropertyType == typeof(Quaternion))
                            {
                                dictionary[prop.Name] = prop.GetValue(rgStateProvider);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // some properties' values aren't accessible
                    }
                }
            }

            return state;
        }
    }
}