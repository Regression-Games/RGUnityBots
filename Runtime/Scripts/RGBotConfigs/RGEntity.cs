using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    [DisallowMultipleComponent]
    public class RGEntity : MonoBehaviour
    {
        [Tooltip("A type name for associating like objects in the state. If this is not specified, the system will use the name of the GameObject up to the first '(' as the objectType. This default attempts to group similar objects into the same objectType.")]
        public string objectType;
        
        [Tooltip("Does this object represent a human/bot player ?")]
        public bool isPlayer;

        // this is used in our toolkit to understand which things would need dynamic models
        [Tooltip("Is this object spawned during runtime, or a fixed object in the scene?")]
        public bool isRuntimeObject = false;
        
        [Tooltip("This option allows you to quickly include most public or serializable properties from all Colliders and MonoBehaviours attached to this GameObject." +
                 "\r\n\r\nWARNING:\r\nThis feature can negatively impact game performance and is best used during early development to quickly prototype your bots before optimizing later using [RGState] attributes to generate custom RGState classes." +
                 "\r\n\r\nSee https://docs.regression.gg/ for more information on optimizing state size and performance." +
                 "\r\r\r\n\r\nNOTE:\r\nThis feature utilizes reflection and may not work properly in environments where AOT is in use." +
                 "\r\n\r\nSee https://docs.unity3d.com/Manual/ScriptingRestrictions.html for more information on AOT and environment scripting restrictions")]
        public bool includeStateForAllBehaviours = false;
        
        // maps action names to RGAction components
        private Dictionary<string, RGAction> actionMap = new Dictionary<string, RGAction>();
            
        // The client Id that owns this entity
        // Used as a performance optimization for mapping the ClientId into the state payloads
        public long? ClientId = null;

        internal UnityEngine.UI.Button Button = null;
        
        /**
         * Updates the registry with a game object that has RGAction scripts attached to it
         */
        void Start()
        {
            Button = gameObject.GetComponent<UnityEngine.UI.Button>();
            if (Button != null)
            {
                // get the action from the overlay button click action, should be the only one in the scene.. but ignores others if it isn't
                var overlayMenu = GameObject.FindObjectOfType<RGOverlayMenu>();
                if (overlayMenu != null)
                {
                    var clickButtonAction = overlayMenu.GetComponent<RGAction_ClickButton>();
                    if (clickButtonAction != null)
                    {
                        actionMap[clickButtonAction.GetActionName()] = clickButtonAction;
                    }
                }
            }
            else
            {
                var overlayMenu = this.GetComponent<RGOverlayMenu>();
                
                var actions = this.GetComponents<RGAction>();
                foreach (var action in actions)
                {
                    // ignore button actions on anything but the OverlayMenu
                    if (action is RGAction_ClickButton)
                    {
                        if (overlayMenu != null)
                        {
                            actionMap[action.GetActionName()] = action;
                        }
                        // else ignore it
                    }
                    else
                    {
                        actionMap[action.GetActionName()] = action;
                    }
                }
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
         * to its states and actions
         */
        public (HashSet<string>, HashSet<string>) LookupStatesAndActions()
        {
            (HashSet<string>, HashSet<string>) result = new ();

            // don't use the class level one, because it is only populated on start
            var button = gameObject.GetComponent<UnityEngine.UI.Button>();
            
            result.Item1 = new HashSet<string>();
            result.Item2 = new HashSet<string>();
            // lookup states
            var stateComponents = gameObject.GetComponents<IRGState>();
            if (stateComponents.Length < 1)
            {
                // if none and is button, give default state
                if (button != null)
                {
                    result.Item1.Add(typeof(RGState_Button).FullName);
                }
            }
            else
            {
                foreach (var stateComponent in stateComponents)
                {
                    result.Item1.Add(stateComponent.GetType().FullName);
                }
            }
            
            // lookup actions
            var actionComponents = gameObject.GetComponents<RGAction>();
            foreach (var actionComponent in actionComponents)
            {
                result.Item2.Add(actionComponent.GetType().FullName);
            }
            
            // if button.. make sure it has click button action
            if (button != null)
            {
                var clickButtonAction = typeof(RGAction_ClickButton).FullName;
                result.Item2.Add(clickButtonAction);
            }
            
            return result;
        }
    }
}