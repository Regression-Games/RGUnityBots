using System;

namespace RegressionGames
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public class RGStateAttribute : Attribute
    {
        public static readonly Type Type = typeof(RGStateAttribute);
        public string StateName { get; }

        // This constructor allows the attribute to be used without a name parameter, e.g., [RGState]
        public RGStateAttribute()
        {
        }

        // This constructor allows a name parameter to be specified, e.g., [RGState("Some Name")]
        public RGStateAttribute(string stateName)
        {
            StateName = stateName;
        }

    }

}
