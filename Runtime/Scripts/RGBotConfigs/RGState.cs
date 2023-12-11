using System;
using System.Collections.Generic;
using System.Reflection;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.UI;

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
        // we require each state to have an 'RGEntity' component
        protected RGEntity rgEntity => GetComponent<RGEntity>();

        private readonly Dictionary<string, object> _emptyDictionary = new();
        private IRGStateEntity _myStateEntity = null;
        
        /**
         * <summary>A function that is overriden to provide the custom state of this specific GameObject.
         * For example, you may want to retrieve and set the health of a player on the returned
         * object, or their inventory information</summary>
         */
        [Obsolete("Use PopulateRGEntityState(IRGStateEntity}")]
        protected virtual Dictionary<string, object> GetState()
        {
            return null;
        }

        // ReSharper disable once InconsistentNaming
        /**
         * <summary>A function that is overriden to provide the custom state of this specific GameObject.
         * For example, you may want to retrieve and set the health of a player on the returned
         * object, or their inventory information.
         * This implementation passes in the created IRGStateEntity to be populated.</summary>
         */
        protected virtual void PopulateRGEntityState(IRGStateEntity stateEntity)
        {
            // do nothing... override me
        }

        /**
         * <summary>Returns the entire internal state for this object, which consists of the default
         * states tracked by RG, and the result of any overridden GetState implementation.</summary>
         */
        public IRGStateEntity GetGameObjectState()
        {
            var state = CreateStateEntityClassInstance();
            PopulateRGEntityState(state);
            
            // support deprecated/obsolete way of populating state that caused too many GC allocations
            var oldWayToDoState = GetState();
            if (oldWayToDoState != null)
            {
                foreach (var (key,value) in oldWayToDoState)
                {
                    state[key] = value;
                }
            }
            return state;
        }
        
        protected virtual Type GetTypeForStateEntity()
        {
            return typeof(RGStateEntity<RGState>);
        }
        
        /**
         * <summary>A function that is overridden to supply a custom implementation of RGStateEntity.
         * This allows more natural coding when working with the state for local C# Unity bots vs accessing entries in a Dictionary.</summary>
         * <example>RGStateEntity_Platformer2DPlayer</example>
         */
        private IRGStateEntity CreateStateEntityClassInstance()
        {
            if (_myStateEntity == null)
            {
                var type = GetTypeForStateEntity();
                if (!typeof(IRGStateEntity).IsAssignableFrom(type))
                {
                    throw new Exception(
                        $"Invalid Type returned from GetTypeForStateEntity() in class {this.GetType().FullName}.  Type must implement IRGStateEntity.");
                }

                _myStateEntity = (IRGStateEntity)Activator.CreateInstance(type);
            }

            return _myStateEntity;
        }

        // Used to fill in the core state for any RGEntity that does NOT have an
        // RGState implementation on its game object
        public static IRGStateEntity GenerateCoreStateForRGEntity(RGEntity rgEntity)
        {
            IRGStateEntity state;
            // if button.. include whether it is interactable
            var button = rgEntity.Button;
            if (button != null)
            {
                state = new RGStateEntity_Button();
                CanvasGroup cg = rgEntity.gameObject.GetComponentInParent<CanvasGroup>();
                state["interactable"] = (cg == null || cg.enabled && cg.interactable) && button.enabled && button.interactable;
            }
            else
            {
                state = new RGStateEntity<RGState>();
            }
            var theTransform = rgEntity.transform;
            
            state["id"] = theTransform.GetInstanceID();
            state["type"] = rgEntity.objectType;
            state["isPlayer"] = rgEntity.isPlayer;
            state["isRuntimeObject"] = rgEntity.isRuntimeObject;
            state["position"] = theTransform.position;
            state["rotation"] = theTransform.rotation;
            state["clientId"] = rgEntity.ClientId;

            if (rgEntity.includeStateForAllBehaviours)
            {
                PopulateEverythingStateForEntity(state, rgEntity);
            }
            return state;
        }
        
        private static void PopulateEverythingStateForEntity(IRGStateEntity state, RGEntity entity)
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