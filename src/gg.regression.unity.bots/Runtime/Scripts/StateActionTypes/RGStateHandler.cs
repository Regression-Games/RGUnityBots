using System;
using System.Linq;
using UnityEngine;

/*
 * A component that provides state information to Regression Games.
 */
namespace RegressionGames.StateActionTypes
{
    [DisallowMultipleComponent]
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    // ReSharper disable once InconsistentNaming
    public sealed class RGStateHandler : MonoBehaviour
    {
        // used to track if this game object was spawned and owned by a bot
        internal long? ClientId;
        
        // don't need to be re-creating my state entity every tick, just updating its values
        private RGStateEntity_Core _myStateEntity;
        
        public static RGStateHandler EnsureCoreRGStateOnGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }
            var rgState = gameObject.GetComponent<RGStateHandler>();
            if (rgState == null)
            {
                // attach one.. yes on the first tick at runtime..
                // this is where we cache state entities and track clientIds
                rgState = gameObject.AddComponent<RGStateHandler>();
            }
            return rgState;
        }
        
        // Used to fill in the core state for any RGEntity that does NOT have an
        // RGState implementation on its game object
        public static RGStateEntity_Core GetCoreStateForGameObject(GameObject gameObject)
        {
            var rgState = EnsureCoreRGStateOnGameObject(gameObject);
            
            RGStateEntity_Core state = rgState._myStateEntity;

            var theTransform = gameObject.transform;
            
            // only create this thing once, not every tick
            if (state == null)
            {
                // if button.. include whether it is interactable
                var button = gameObject.GetComponent<UnityEngine.UI.Button>();
                state = button != null ? new RGStateEntity_Button() : new RGStateEntity_Core();

                rgState._myStateEntity = state;
                
                //only need to set these once
                state["id"] = theTransform.GetInstanceID();
                state["name"] = gameObject.name;
                state["isPlayer"] = false; // core state sets this false.. other things may override it later
            }

            // these can update on any tick
            state["position"] = theTransform.position;
            state["rotation"] = theTransform.rotation;
            state["clientId"] = rgState.ClientId;

            if (state is RGStateEntity_Button)
            {
                var button = gameObject.GetComponent<UnityEngine.UI.Button>();
                CanvasGroup cg = gameObject.GetComponentInParent<CanvasGroup>();
                state["interactable"] = (cg == null || cg.enabled && cg.interactable) && button.enabled &&
                                        button.interactable;
            }
            
            return state;
        }

        internal static void PopulateStateEntityForStatefulObject(RGStateEntity_Core gameObjectCoreState, MonoBehaviour monoBehaviour, out bool isPlayer)
        {
            var type = monoBehaviour.GetType();

            var entityTypeForBehaviour =
                BehavioursWithStateOrActions.GetRGStateEntityMappingForBehaviour(monoBehaviour);
            
            var typeName = entityTypeForBehaviour.EntityType ?? type.Name;
            isPlayer = entityTypeForBehaviour.IsPlayer;
            
            // try to avoid re-allocating this on every tick as much as possible
            RGStateEntityBase state = null;
            if (gameObjectCoreState.TryGetValue(typeName, out var stateObject))
            {
                state = (RGStateEntityBase)stateObject;
            }
            else
            {
                // load the correct type to match this behaviour for nice '.' lookup in C# code
                var stateType = entityTypeForBehaviour.RGStateEntityType;
                if (stateType != null)
                {
                    state = (RGStateEntityBase)Activator.CreateInstance(stateType);
                }
                // save this whether it is null or stateful
                gameObjectCoreState[typeName] = state;
            }

            // ignore any behaviour that doesn't have states
            state?.PopulateFromMonoBehaviour(monoBehaviour);
        }
        
    }

}
