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
        /// Determines whether this action is valid for the object.
        /// </summary>
        /// <param name="obj">
        /// The target object for which to check validity.
        /// The object will be of type ObjectType.
        /// </param>
        /// <returns>Whether the action is valid for the object.</returns>
        public abstract bool IsValidForObject(UnityEngine.Object obj);
        
        /// <summary>
        /// Obtain an instance of this action for the target object.
        /// This method is only called if IsValidForObject is true for the given object.
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
        /// <summary>
        /// Get the action associated with this instance.
        /// </summary>
        public RGGameAction BaseAction { get; }
        
        /// <summary>
        /// Perform the action by simulating the applicable user inputs.
        /// </summary>
        /// <param name="param">Value from the action's ParameterRange</param>
        public void Perform(object param);
    }
    
    public abstract class RGGameActionInstance<TAction, TParam> : IRGGameActionInstance where TAction : RGGameAction
    {
        /// <summary>
        /// Same as BaseAction, but is already the appropriate type
        /// this action instance is associated with.
        /// </summary>
        public TAction Action { get; private set; }

        public RGGameAction BaseAction => Action;
        
        public UnityEngine.Object TargetObject { get; private set; }

        public RGGameActionInstance(TAction action, UnityEngine.Object targetObject)
        {
            Action = action;
            TargetObject = targetObject;
        }

        public void Perform(object param)
        {
            PerformAction((TParam)param);
        }
        
        protected abstract void PerformAction(TParam param);
    }
}