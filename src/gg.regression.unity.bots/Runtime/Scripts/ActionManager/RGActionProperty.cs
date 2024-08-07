using System;
using System.Collections;
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

        /// <summary>
        /// Finds a property by name for the given action.
        /// </summary>
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
        
        /// <summary>
        /// Enumerates all properties of the given action
        /// </summary>
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
        /// <summary>
        /// The action this property instance is bound to
        /// </summary>
        public RGGameAction Action { get; }

        /// <summary>
        /// The raw field/property information for the action property
        /// </summary>
        public MemberInfo MemberInfo { get; }

        /// <summary>
        /// The attribute defining this property
        /// </summary>
        public RGActionProperty Attribute { get; }

        /// <summary>
        /// Get the name of this property (equivalent to the member's name)
        /// </summary>
        public string Name => MemberInfo.Name;

        public Type MemberType
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

        /// <summary>
        /// Get the current value of the property
        /// </summary>
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

        /// <summary>
        /// Set the value of the property
        /// </summary>
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

        private string GetDisplayValue(object value)
        {
            if (value == null)
            {
                return "null";
            } else if (value is string str)
            {
                return str;
            } else if (value is ICollection collection)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                bool isFirst = true;
                foreach (object elem in collection)
                {
                    if (!isFirst)
                        sb.Append(", ");
                    else
                        isFirst = false;
                    sb.Append(GetDisplayValue(elem));
                }
                sb.Append("}");
                return sb.ToString();
            }
            else
            {
                return value.ToString();
            }
        }

        /// <summary>
        /// Get a representation of the property value suitable for display
        /// in a user interface
        /// </summary>
        public string GetDisplayValue()
        {
            return GetDisplayValue(GetValue());
        }

        /// <summary>
        /// Serialize the current value of this property to the given StringBuilder as JSON
        /// </summary>
        public void WriteValueToStringBuilder(StringBuilder stringBuilder)
        {
            object value = GetValue();
            string jsonSerialized = JsonConvert.SerializeObject(value, RGActionProvider.JSON_CONVERTERS);
            stringBuilder.Append(jsonSerialized);
        }

        /// <summary>
        /// Deserialize the property value.
        /// Note this does not yet store the value to this property. Call the SetValue() method to
        /// set the value.
        /// </summary>
        public object DeserializeValue(string serializedValue)
        {
            return JsonConvert.DeserializeObject(serializedValue, MemberType, RGActionProvider.JSON_CONVERTERS);
        }

        public override string ToString()
        {
            return Action.DisplayName + " " + Attribute.DisplayName;
        }
    }
}