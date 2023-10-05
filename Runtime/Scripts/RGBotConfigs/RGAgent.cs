
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    /**
     * This class keeps track of the actions that an agent can take within the game.
     * If an action is requested that doesn't exist at startup, an error is thrown.
     */
    public class RGAgent : MonoBehaviour
    {

        private Dictionary<string, RGAction> actionMap;

        // The client Id that owns this agent
        // Used as a performance optimization for mapping the ClientId into the state payloads
        public long? ClientId = null;

        public RGAgent()
        {
            actionMap = new Dictionary<string, RGAction>();
        }

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

            RGDebug.LogDebug($"Agent registered with {actionMap.Count} actions");
        }

        public RGAction GetActionHandler(string actionName)
        {
            if(actionMap.TryGetValue(actionName, out RGAction action))
            {
                return action;
            }
            return null;
        }

    }
}
