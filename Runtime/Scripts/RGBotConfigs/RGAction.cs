
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
     /**
     * Represents an action that a bot can construct the game object to take.
     * This RGAction class should be inherited in a sub class, and developers
     * will need to implement the `StartAction` and `GetActionName` function.
     *
     * TODO: At any time, the action should be able to report that it failed
     *       or succeeded. Need error handling rules.
     * TODO: Maybe we can connect this to RGState, so actions can report their state?
     *
     */
     public abstract class RGAction : MonoBehaviour
     {

      /**
         * The name of this action, which is used by the bot to request this specific action
         */
      public abstract string GetActionName();

      /**
         * The action to kick off, given some arguments. Usually this will set up some state
         * variables inside of this component, and then most of the logic will happen in an
         * update function.
         */
      public abstract void StartAction(Dictionary<string, object> input);

     }
}

