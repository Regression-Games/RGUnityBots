using System;

namespace RegressionGames
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class RGActionAttribute : Attribute
    {
        public string ActionName { get; }

        public RGActionAttribute()
        {
            ActionName = null;
        }

        public RGActionAttribute(string actionName)
        {
            ActionName = actionName;
        }
    }
}