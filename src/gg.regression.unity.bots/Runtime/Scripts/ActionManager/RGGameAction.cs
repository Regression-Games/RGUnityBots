using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RegressionGames.ActionManager.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.ActionManager
{
    [JsonConverter(typeof(RGGameActionJsonConverter))]
    public abstract class RGGameAction : ICloneable
    {
        
        /// The path of the action, typically derived from
        /// the location where the associated input handling logic takes place.
        /// An action may have multiple paths if multiple code locations were inferred to
        /// have equivalent actions by IsEquivalentTo and were grouped together.
        public List<string[]> Paths { get; set; }
        
        /// The type of object that the action is associated with.
        /// The object must be derived from UnityEngine.Object, and
        /// all instances of the object should be retrievable via
        /// UnityEngine.Object.FindObjectsOfType(ObjectType).
        public Type ObjectType { get; set; }
        
        /// The range of parameter values accepted by this action's
        /// Perform() method.
        [RGActionProperty("Parameter Range", true)]
        public IRGValueRange ParameterRange { get; set; }

        /// The name of this action as it should be displayed when presented to the user.
        public abstract string DisplayName { get; }

        public RGGameAction(string[] path, Type objectType, IRGValueRange paramRange)
        {
            Paths = new List<string[]> { path };
            ObjectType = objectType;
            ParameterRange = paramRange;
        }

        public RGGameAction(JObject serializedAction)
        {
            JArray paths = (JArray)serializedAction["paths"];
            Paths = new List<string[]>(paths.Select(path => path.ToString().Split("/")));

            var objectTypeName = serializedAction["objectTypeName"];
            if (objectTypeName.Type != JTokenType.Null)
            {
                ObjectType = Type.GetType(objectTypeName.ToString(), true);
            }

            ParameterRange = serializedAction["parameterRange"].ToObject<IRGValueRange>();
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
        /// Serializes this action to the specified StringBuilder.
        /// </summary>
        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"actionTypeName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, GetType().AssemblyQualifiedName);
            stringBuilder.Append(",\n\"paths\":[");
            int pathsCount = Paths.Count;
            for (int i = 0; i < pathsCount; ++i)
            {
                StringJsonConverter.WriteToStringBuilder(stringBuilder, string.Join("/", Paths[i]));
                if (i + 1 < pathsCount)
                {
                    stringBuilder.Append(",");
                }
            }
            stringBuilder.Append("],\n\"objectTypeName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, ObjectType?.AssemblyQualifiedName);
            stringBuilder.Append(",\n\"parameterRange\":");
            ParameterRange.WriteToStringBuilder(stringBuilder);
            WriteParametersToStringBuilder(stringBuilder);
            stringBuilder.Append("\n}");
        }

        /// <summary>
        /// Serialize the action-specific parameters (such as key code, button name, etc.).
        /// </summary>
        protected abstract void WriteParametersToStringBuilder(StringBuilder stringBuilder);

        public object Clone()
        {
            // Clone via serialization/deserialization
            StringBuilder stringBuilder = new StringBuilder();
            WriteToStringBuilder(stringBuilder);
            return JsonConvert.DeserializeObject<RGGameAction>(stringBuilder.ToString(), RGActionProvider.JSON_CONVERTERS);
        }
    }

    public interface IRGGameActionInstance
    {
        /// <summary>
        /// Get the action associated with this instance.
        /// </summary>
        public RGGameAction BaseAction { get; }

        /// <summary>
        /// Get the object associated with this action instance.
        /// </summary>
        public UnityEngine.Object TargetObject { get; }

        /// <summary>
        /// Determines whether the given parameter is valid for this action in the current state
        /// </summary>
        /// <param name="param">The parameter to check, which should be from the action's ParameterRange.</param>
        /// <returns>Whether the parameter is valid for this action in the current state</returns>
        public bool IsValidParameter(object param);

        /// <summary>
        /// Get the device inputs needed to perform this action instance.
        /// </summary>
        /// <param name="param">
        /// The action parameter, which should be from the action's ParameterRange.
        /// The caller should first check that the parameter is valid via IsValidParameter.
        /// </param>
        /// <returns>The set of inputs needed to perform the action in the current state.</returns>
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

        public bool IsValidParameter(object param)
        {
            return IsValidActionParameter((TParam)param);
        }

        public IEnumerable<RGActionInput> GetInputs(object param)
        {
            return GetActionInputs((TParam)param);
        }

        protected abstract bool IsValidActionParameter(TParam param);
        
        protected abstract IEnumerable<RGActionInput> GetActionInputs(TParam param);
    }
}