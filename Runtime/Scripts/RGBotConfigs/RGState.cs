using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.Tilemaps;

/*
 * A component that can be inherited to relay game state information to
 * Regression Games. Includes a few default pieces of information that can
 * be enabled from the editor when attached to an object.
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

            var obsoleteAttributeType = typeof(ObsoleteAttribute);
            // find all Components and get their values
            var components = this.gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                // skip 'expensive' components
                if (component is Collider or Collider2D or MonoBehaviour and not RGState and not RGAgent)
                {
                    var type = component.GetType();
                    var dictionary = new Dictionary<string, object>();
                    foreach (PropertyInfo prop in type.GetProperties())
                    {
                        try
                        {
                            if (prop.CanRead &&
                                (prop.PropertyType.IsPublic || prop.PropertyType.IsSerializable) &&
                                !prop.IsDefined(obsoleteAttributeType, false)) ;
                            {
                                if (prop.PropertyType.IsPrimitive ||
                                    prop.PropertyType == typeof(Vector3) ||
                                    prop.PropertyType == typeof(Vector2) ||
                                    prop.PropertyType == typeof(Vector4) ||
                                    prop.PropertyType == typeof(Quaternion))
                                {
                                    dictionary[prop.Name] = prop.GetValue(component);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // some properties' values aren't accessible
                        }
                    }

                    state[type.Name] = dictionary;
                }
            }

            return state;
        }
    }

}