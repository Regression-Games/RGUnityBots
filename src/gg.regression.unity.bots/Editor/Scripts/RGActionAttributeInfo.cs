using System;
using System.Collections.Generic;

namespace RegressionGames.Editor
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGActionAttributeInfo : RGActionInfo
    {
        // the namespace of the class that contains the [RGAction] attribute tags
        public string Namespace;
        // the name of the component class that contains the [RGAction] attribute tags
        public string Object;
        // the name of the method tagged with [RGAction]
        public string MethodName;
        // The fully qualified name of the class generated for this [RGAction] attribute
        public string GeneratedClassName;
        // Whether to generate a CS file for this action, default true, but false for sample project assets
        public bool ShouldGenerateCSFile = true;

        public RGActionInfo toRGActionInfo()
        {
            var _this = this;
            return new RGActionInfo()
            {
                ActionName = _this.ActionName,
                Parameters = _this.Parameters,
                ActionClassName = _this.GeneratedClassName
            };
        }
    }
}