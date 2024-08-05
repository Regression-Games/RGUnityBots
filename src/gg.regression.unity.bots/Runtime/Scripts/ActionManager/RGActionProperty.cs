using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// Represents a property of an RGGameAction.
    /// The primary purpose of this class is to facilitate the display and modification of
    /// individual action properties in the action manager user interface.
    /// 
    /// If the user desires to make a change to the property, this API can be used to
    /// serialize and deserialize individual property changes without the need to store the entire action.
    ///
    /// Note that this class can refer to either a C# field or property on a class derived from RGGameAction.
    /// </summary>
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

        public static RGActionPropertyInstance FindProperty(RGGameAction action, string name)
        {
            var field = action.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                RGActionProperty prop = (RGActionProperty)GetCustomAttribute(field, typeof(RGActionProperty));
                if (prop != null)
                {
                    return new RGActionPropertyInstance(action, field, prop);
                }
            }

            var property = action.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                RGActionProperty prop = (RGActionProperty)GetCustomAttribute(property, typeof(RGActionProperty));
                if (prop != null)
                {
                    return new RGActionPropertyInstance(action, property, prop);
                }
            }
            
            return null;
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

        public Type ValueType
        {
            get
            {
                if (MemberInfo is FieldInfo field)
                {
                    return field.FieldType;
                } 
                else if (MemberInfo is PropertyInfo prop)
                {
                    return prop.PropertyType;
                }
                else
                {
                    throw new Exception("Unsupported member type " + MemberInfo);
                }
            }
        }

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
            string jsonSerialized = JsonConvert.SerializeObject(value, RGActionProvider.JSON_CONVERTERS);
            stringBuilder.Append(jsonSerialized);
        }

        public object DeserializeValue(string serializedValue)
        {
            return JsonConvert.DeserializeObject(serializedValue, ValueType, RGActionProvider.JSON_CONVERTERS);
        }
    }
}