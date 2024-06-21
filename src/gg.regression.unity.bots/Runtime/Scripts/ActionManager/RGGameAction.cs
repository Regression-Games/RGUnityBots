using System;
using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public abstract class RGGameAction
    {
        /// The path of the action, typically derived from
        /// the location where the associated input handling logic takes place.
        /// An action may have multiple paths if multiple code locations were inferred to
        /// have equivalent actions by IsEquivalentTo and were grouped together.
        public List<string> Paths { get; private set; }
        
        /// The type of object that the action is associated with.
        /// The object must be derived from UnityEngine.Object, and
        /// all instances of the object should be retrievable via
        /// UnityEngine.Object.FindObjectsOfType(ObjectType).
        public Type ObjectType { get; private set; }
        
         /// The range of parameter values accepted by this action's
         /// Perform() method.
        public abstract IRGValueRange ParameterRange { get; }
        
        /// User-specified data for this action.
        public Dictionary<string, object> UserData { get; }
        
        public RGGameAction(string path, Type objectType)
        {
            Paths = new List<string> { path };
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

        /// <summary>
        /// Indicates whether this action is equivalent as another action.
        /// This is used for deciding whether an action should be grouped together with another.
        /// </summary>
        /// <param name="other">The action to compare against.</param>
        /// <returns>Whether the actions are equivalent.</returns>
        public virtual bool IsEquivalentTo(RGGameAction other)
        {
            return ObjectType == other.ObjectType && ParameterRange.RangeEquals(other.ParameterRange);
        }
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