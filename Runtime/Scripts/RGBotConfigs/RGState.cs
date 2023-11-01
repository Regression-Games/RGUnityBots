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
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    [RequireComponent(typeof(RGEntity))]
    // ReSharper disable once InconsistentNaming
    public abstract class RGState : MonoBehaviour, IRGState
    {
        // ReSharper disable once InconsistentNaming
        // we require each state to have an 'RGEntity' component on the same GameObject
        protected RGEntity rgEntity
        {
            get { return GetComponent<RGEntity>(); }
        }

        /**
         * <summary>A function that is overriden to provide the custom state of this specific GameObject.
         * For example, you may want to retrieve and set the health of a player on the returned
         * object, or their inventory information</summary>
         */
        protected virtual Dictionary<string, object> GetState()
        {
            return new Dictionary<string, object>();
        }

        /**
         * <summary>Returns the entire internal state for this object, which consists of the default
         * states tracked by RG, and the result of any overridden GetState implementation.</summary>
         */
        public RGStateEntity GetGameObjectState()
        {
            var state = CreateStateEntity();
            var dict = GetState();
            foreach (var entry in dict)
            {
                // allow overriding default or everything state fields like position, etc
                state[entry.Key] = entry.Value;
            }

            return state;
        }

        /**
         * <summary>A function that is overridden to supply a custom implementation of RGStateEntity.
         * This allows more natural coding when working with the state for local C# Unity bots vs accessing entries in a Dictionary.</summary>
         * <example>RGStatePlatformer2DPlayer</example>
         */
        protected virtual RGStateEntity CreateStateEntity()
        {
            return new RGStateEntity();
        }
        
        
        // Used to fill in the core state for any RGEntity that does NOT have an
        // RGState implementation on its game object
        public static RGStateEntity GenerateCoreStateForRGEntity(RGEntity rgEntity)
        {
            var state = new RGStateEntity();
            PopulateCoreStateForEntity(state, rgEntity);
            if (rgEntity.includeStateForAllBehaviours)
            {
                PopulateEverythingStateForEntity(state, rgEntity);
            }
            return state;
        }

        private static void PopulateCoreStateForEntity(RGStateEntity state, RGEntity entity)
        {
            var theTransform = entity.transform;
            
            state["id"] = theTransform.GetInstanceID();
            state["type"] = entity.objectType;
            state["isPlayer"] = entity.isPlayer;
            state["isRuntimeObject"] = entity.isRuntimeObject;
            state["position"] = theTransform.position;
            state["rotation"] = theTransform.rotation;
        }

        private static void PopulateEverythingStateForEntity(RGStateEntity state, RGEntity entity)
        {
            var obsoleteAttributeType = typeof(ObsoleteAttribute);
            // find all Components and get their values
            var components = entity.gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                // skip 'expensive' components, only get colliders and MonoBehaviours
                if (component is Collider or Collider2D or MonoBehaviour and not RGState and not RGAction and not RGEntity and not RGAgent)
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
        }
    }

}