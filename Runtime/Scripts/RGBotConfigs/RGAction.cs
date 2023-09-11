
using System;
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
         /*
          * Stores a map of labels to delegates. Used to link RGAction attributes back to
          * their original methods
          */
         private Dictionary<string, Delegate> methodMap = new Dictionary<string, Delegate>();

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

         /*
          * Add a method delegate to our map, with the given label. Labels are unique identifiers
          * to the delegates. Used to map RGAction attributes back to their original methods
          */
         protected void AddMethod(string label, Delegate method)
         {
             if (!methodMap.ContainsKey(label))
             {
                 methodMap[label] = method;
             }
             else
             {
                 RGDebug.LogError($"Method with label '{label}' already exists.");
             }
         }

         /*
          * Invokes the delegate with the given label, with the given args. Args are a generic
          * list of objects, to accommodate any number of parameters of any type
          */
         protected void Invoke(string label, params object[] args)
         {
             if (methodMap.TryGetValue(label, out Delegate method))
             {
                 method.DynamicInvoke(args);
             }
             else
             {
                 RGDebug.LogError($"Method with label '{label}' not found.");
             }
         }
     }
}

