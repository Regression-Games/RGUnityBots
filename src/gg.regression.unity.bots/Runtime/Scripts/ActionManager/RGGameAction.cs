using System;
using System.Collections.Generic;
using System.Linq;

namespace RegressionGames.ActionManager
{
    public abstract class RGGameAction
    {
        /// The path of the action, typically derived from
        /// the location where the associated input handling logic takes place.
        /// An action may have multiple paths if multiple code locations were inferred to
        /// have equivalent actions by IsEquivalentTo and were grouped together.
        public List<string[]> Paths { get; private set; }
        
        /// The type of object that the action is associated with.
        /// The object must be derived from UnityEngine.Object, and
        /// all instances of the object should be retrievable via
        /// UnityEngine.Object.FindObjectsOfType(ObjectType).
        public Type ObjectType { get; private set; }
        
         /// The range of parameter values accepted by this action's
         /// Perform() method.
        public abstract IRGValueRange ParameterRange { get; }

         /// The name of this action as it should be displayed when presented to the user.
        public abstract string DisplayName { get; }

        public RGGameAction(string[] path, Type objectType)
        {
            Paths = new List<string[]> { path };
            ObjectType = objectType;
        }

        public RGGameAction(RGSerializedAction serializedAction)
        {
            Paths = new List<string[]>(serializedAction.paths.Select(path => path.Split("/")));
            ObjectType = Type.GetType(serializedAction.objectTypeName);
            if (ObjectType == null)
            {
                throw new Exception($"Object type {ObjectType} does not exist");
            }
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

        /// <summary>
        /// Serializes this action into a storable data structure.
        /// </summary>
        /// <returns>The serialized action data.</returns>
        public RGSerializedAction Serialize()
        {
            RGSerializedAction result = new RGSerializedAction();
            result.actionTypeName = GetType().AssemblyQualifiedName;
            result.paths = new List<string>(Paths.Select(path => string.Join("/", path)));
            result.objectTypeName = ObjectType.AssemblyQualifiedName;
            Serialize(result);
            return result;
        }

        /// <summary>
        /// Serialize the action-specific parameters (such as key code, button name, etc.).
        /// This is stored to the actionParameters field of RGSerializedAction.
        /// </summary>
        /// <param name="actionParametersOut">List where the action-specific data should be placed.</param>
        protected abstract void Serialize(RGSerializedAction serializedAction);
    }
    
    [Serializable]
    public class RGSerializedAction
    {
        public string actionTypeName;
        public List<string> paths;
        public string objectTypeName;

        // Used by actions that have a serializable function parameter (e.g. key function, axis name function, etc.)
        public ActionParamFuncType actionFuncType;
        public string actionFuncData;
        
        // Field where arbitrary string data can be stored
        public string actionStringData;

        public RGGameAction Deserialize()
        {
            Type actionType = Type.GetType(actionTypeName);
            var constructor = actionType.GetConstructor(new[] { typeof(RGSerializedAction) });
            if (constructor == null)
            {
                throw new Exception($"Missing deserialization constructor for {actionType.FullName}");
            }
            return (RGGameAction)constructor.Invoke(new object[] { this });
        }
    }

    public interface IRGGameActionInstance
    {
        /// <summary>
        /// Get the action associated with this instance.
        /// </summary>
        public RGGameAction BaseAction { get; }

        /// <summary>
        /// Get the device inputs needed to perform this action instance.
        /// </summary>
        public IEnumerable<RGActionInput> GetInputs(object param);
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

        public IEnumerable<RGActionInput> GetInputs(object param)
        {
            return GetActionInputs((TParam)param);
        }
        
        protected abstract IEnumerable<RGActionInput> GetActionInputs(TParam param);
    }
}