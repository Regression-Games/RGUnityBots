using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public abstract class RGGameAction
    {
        /// The path of the action, typically derived from
        /// the location where the associated input handling logic takes place.
        /// 
        /// This serves as an identifier for the action. It is expected that
        /// upon changes to the code, the path will remain the same if the
        /// location of the associated input-handling logic has not changed.
        public string Path { get; private set; }
        
        /// The type of object that the action is associated with.
        /// The object must be derived from UnityEngine.Object, and
        /// all instances of the object should be retrievable via
        /// UnityEngine.Object.FindObjectsOfType(ObjectType).
        public Type ObjectType { get; private set; }
        
        /// User-specified data for this action.
        public Dictionary<string, object> UserData { get; }
        
         /// The range of parameter values accepted by this action's
         /// Perform() method.
        public abstract IRGValueRange ParameterRange { get; }
        
        public RGGameAction(string path, Type objectType)
        {
            Path = path;
            ObjectType = objectType;
        }

        /// <summary>
        /// Obtain an instance of this action for the target object.
        /// </summary>
        /// <param name="obj">
        /// The object for which to obtain an action instance.
        /// This should be of type ObjectType.
        /// </param>
        /// <returns>An instance of this action for the target object.</returns>
        public abstract IRGGameActionInstance GetInstance(UnityEngine.Object obj);
    }

    public interface IRGGameActionInstance
    {
        /// <returns>Returns whether this action instance is valid in the current game state</returns>
        public bool IsValid();

        /// <summary>
        /// Perform the action by simulating the applicable user inputs.
        /// </summary>
        /// <param name="param">Value from the action's ParameterRange</param>
        public void Perform(object param);
    }
    
    public abstract class RGGameActionInstance<TAction, TParam> : IRGGameActionInstance where TAction : RGGameAction
    {
        public TAction Action { get; private set; }
        
        public UnityEngine.Object Instance { get; private set; }

        public RGGameActionInstance(TAction action, UnityEngine.Object instance)
        {
            Action = action;
            Instance = instance;
        }

        public bool IsValid()
        {
            return true;
        }

        public void Perform(object param)
        {
            Perform((TParam)param);
        }
        
        public abstract void Perform(TParam param);
    }
}