using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using System.Text;
using Newtonsoft.Json.Linq;

namespace RegressionGames.ActionManager
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class RGActionProperty : Attribute
    {
        /// <summary>
        /// The display name of this property in user interfaces.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Whether the user is allowed to configure this parameter.
        /// </summary>
        public bool UserConfigurable;

        public RGActionProperty(string displayName, bool userConfigurable)
        {
            DisplayName = displayName;
            UserConfigurable = userConfigurable;
        }

        public static IEnumerable<RGActionPropertyInstance> GetProperties(RGGameAction action)
        {
            foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                RGActionProperty prop = (RGActionProperty)GetCustomAttribute(field, typeof(RGActionProperty));
                if (prop != null)
                {
                    yield return new RGActionPropertyInstance(action, field, prop);
                }
            }

            foreach (var property in action.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                RGActionProperty prop = (RGActionProperty)GetCustomAttribute(property, typeof(RGActionProperty));
                if (prop != null)
                {
                    yield return new RGActionPropertyInstance(action, property, prop);
                }
            }
        }
    }

    public class RGActionPropertyInstance
    {
        public RGGameAction Action { get; }
        
        public MemberInfo MemberInfo { get; }
        
        public RGActionProperty Attribute { get; }

        public RGActionPropertyInstance(RGGameAction action, MemberInfo memberInfo, RGActionProperty attr)
        {
            Action = action;
            MemberInfo = memberInfo;
            Attribute = attr;
        }

        public object GetValue()
        {
            if (MemberInfo is FieldInfo field)
            {
                return field.GetValue(Action);
            } else if (MemberInfo is PropertyInfo prop)
            {
                return prop.GetValue(Action);
            }
            else
            {
                throw new Exception("Unsupported member type " + MemberInfo);
            }
        }

        public void SetValue(object value)
        {
            if (MemberInfo is FieldInfo field)
            {
                field.SetValue(Action, value);
            } else if (MemberInfo is PropertyInfo prop)
            {
                prop.SetValue(Action, value);
            }
            else
            {
                throw new Exception("Unsupported member type " + MemberInfo);
            }
        }

        public void WriteValueToStringBuilder(StringBuilder stringBuilder)
        {
            object value = GetValue();
            if (value is IRGValueRange valueRange)
            {

            } 
        }

        public object Deserialize(JObject serializedValue)
        {
            
        }
    }
}