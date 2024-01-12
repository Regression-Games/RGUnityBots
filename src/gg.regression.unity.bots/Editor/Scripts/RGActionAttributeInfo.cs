using System;

namespace RegressionGames.Editor
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGActionAttributeInfo : RGActionInfo
    {
        // Only serialize the fields from the base RGActionInfo type

        [NonSerialized]
        // the directory path of the class that contains the [RGAction] attribute tags
        public string BehaviourFileDirectory;

        [NonSerialized]
        // the namespace of the class that contains the [RGAction] attribute tags
        public string BehaviourNamespace;

        [NonSerialized]
        // the name of the component class that contains the [RGAction] attribute tags
        public string BehaviourName;

        [NonSerialized]
        // the name of the method tagged with [RGAction]
        public string MethodName;

        [NonSerialized]
        // the type of the entity
        public string EntityTypeName;

        [NonSerialized]
        // Whether to generate a CS file for this action, default true, but false for sample project assets
        public bool ShouldGenerateCSFile = true;
    }
}