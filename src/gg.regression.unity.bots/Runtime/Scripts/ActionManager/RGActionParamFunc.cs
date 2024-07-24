using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using Object = System.Object;

namespace RegressionGames.ActionManager
{
    public enum ActionParamFuncType
    {
        TYPE_CONSTANT,
        TYPE_MEMBER_ACCESS
    }
    
    [Serializable]
    internal struct SerializedMemberAccessFuncData
    {
        public SerializedMemberAccess[] MemberAccesses;
    }

    [Serializable]
    internal struct SerializedMemberAccess
    {
        public MemberTypes MemberType;
        public string DeclaringType;
        public string MemberName;
    }
    
    /// <summary>
    /// This represents a function of an object that returns a parameter needed for an action,
    /// such as the key code, button name, axis name, etc. It is designed to be serializable.
    /// </summary>
    public class RGActionParamFunc<T> : IEquatable<RGActionParamFunc<T>>
    {
        private readonly ActionParamFuncType _funcType;
        private readonly string _data;
        private readonly Func<Object, T> _func;

        private T _constantValue; // only present if _funcType == TYPE_CONSTANT
        private SerializedMemberAccessFuncData _memberAccesses; // only present if _funcType == TYPE_MEMBER_ACCESSES

        private RGActionParamFunc(ActionParamFuncType funcType, string data, Func<Object, T> func)
        {
            _funcType = funcType;
            _data = data;
            _func = func;
        }

        /// <summary>
        /// Create a constant function that always returns the given value.
        /// </summary>
        public static RGActionParamFunc<T> Constant(T value)
        {
            string data;
            var type = typeof(T);
            if (type == typeof(string) || type == typeof(int) || type.IsEnum)
            {
                data = value.ToString();
            } else if (type == typeof(object))
            {
                if (value is KeyCode keyCode)
                {
                    data = "keycode:" + keyCode;
                } else if (value is string str)
                {
                    data = "string:" + str;
                }
                else
                {
                    throw new Exception($"unexpected constant value of type {value.GetType()}");
                }
            } else
            {
                throw new Exception($"serialization of type {type} is unsupported");
            }
            Func<Object, T> func = _ => value;
            var result = new RGActionParamFunc<T>(ActionParamFuncType.TYPE_CONSTANT, data, func);
            result._constantValue = value;
            return result;
        }
        
        /// <summary>
        /// Create a function that performs a sequence of field or property accesses to obtain the value.
        /// </summary>
        public static RGActionParamFunc<T> MemberAccesses(IList<MemberInfo> members)
        {
            SerializedMemberAccess[] accesses = new SerializedMemberAccess[members.Count];
            for (int i = 0; i < members.Count; ++i)
            {
                ref SerializedMemberAccess access = ref accesses[i];
                var member = members[i];
                access.DeclaringType = member.DeclaringType.AssemblyQualifiedName;
                access.MemberType = member.MemberType;
                access.MemberName = member.Name;
            }

            var memberAccessData = new SerializedMemberAccessFuncData { MemberAccesses = accesses };
            string data = JsonUtility.ToJson(memberAccessData);
            
            Func<Object, T> func = obj =>
            {
                object currentObject = obj;
                foreach (var member in members)
                {
                    if (member is FieldInfo field)
                    {
                        currentObject = field.GetValue(currentObject);
                    } else if (member is PropertyInfo prop)
                    {
                        currentObject = prop.GetValue(currentObject);
                    }
                    else
                    {
                        throw new Exception($"Unexpected member {member} of type {member.GetType()})");
                    }
                }
                
                // Heuristic to convert LayerMask to int
                if (typeof(T) == typeof(int) && currentObject is LayerMask mask)
                {
                    currentObject = (int)mask;
                }

                return (T)Convert.ChangeType(currentObject, typeof(T));
            };

            var result = new RGActionParamFunc<T>(ActionParamFuncType.TYPE_MEMBER_ACCESS, data, func);
            result._memberAccesses = memberAccessData;
            return result;
        }
        
        public T Invoke(Object obj)
        {
            return _func(obj);
        }

        public static RGActionParamFunc<T> Deserialize(JToken token)
        {
            JObject obj = (JObject)token;
            ActionParamFuncType funcType = Enum.Parse<ActionParamFuncType>(obj["funcType"].ToString());
            string data = obj["data"].ToString();
            switch (funcType)
            {
                case ActionParamFuncType.TYPE_CONSTANT:
                {
                    var type = typeof(T);
                    if (type.IsEnum)
                    {
                        return Constant((T)Enum.Parse(type, data));
                    } else if (type == typeof(string))
                    {
                        return Constant((T)(object)data);
                    } else if (type == typeof(int))
                    {
                        return Constant((T)(object)int.Parse(data));
                    } else if (type == typeof(object))
                    {
                        if (data.StartsWith("keycode:"))
                        {
                            var keyCode = Enum.Parse<KeyCode>(data.Substring("keycode:".Length));
                            return Constant((T)(object)keyCode);
                        } else if (data.StartsWith("string:"))
                        {
                            var str = data.Substring("string:".Length);
                            return Constant((T)(object)str);
                        }
                        else
                        {
                            throw new Exception($"unexpected data {data}");
                        }
                    }
                    else
                    {
                        throw new Exception($"deserialization of type {type} is unsupported");
                    }
                }
                case ActionParamFuncType.TYPE_MEMBER_ACCESS:
                {
                    List<MemberInfo> members = new List<MemberInfo>();
                    var memberData = JsonUtility.FromJson<SerializedMemberAccessFuncData>(data);
                    foreach (var access in memberData.MemberAccesses)
                    {
                        Type type = Type.GetType(access.DeclaringType);
                        if (type == null)
                        {
                            throw new Exception($"type {access.DeclaringType} not available");
                        }
                        MemberInfo member;
                        switch (access.MemberType)
                        {
                            case MemberTypes.Field:
                                member = type.GetField(access.MemberName, 
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                break;
                            case MemberTypes.Property:
                                member = type.GetProperty(access.MemberName,
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                break;
                            default:
                                throw new Exception($"unsupported member type {access.MemberType}");
                        }
                        members.Add(member);
                    }
                    return MemberAccesses(members);
                }
                default:
                    throw new Exception($"unexpected function type {funcType}");
            }
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"funcType\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, _funcType.ToString());
            stringBuilder.Append(",\"data\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, _data);
            stringBuilder.Append("}");
        }

        public bool Equals(RGActionParamFunc<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _funcType == other._funcType && _data == other._data;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RGActionParamFunc<T>)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)_funcType, _data);
        }

        public static bool operator ==(RGActionParamFunc<T> left, RGActionParamFunc<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RGActionParamFunc<T> left, RGActionParamFunc<T> right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            switch (_funcType)
            {
                case ActionParamFuncType.TYPE_CONSTANT:
                    if (_constantValue is string)
                        return $"\"{_constantValue}\"";
                    else if (_constantValue is Enum)
                        return _constantValue.GetType().Name + "." + _constantValue;
                    else
                        return _constantValue.ToString();
                case ActionParamFuncType.TYPE_MEMBER_ACCESS:
                    return string.Join(".", _memberAccesses.MemberAccesses.Select(a => a.MemberName));
                default:
                    throw new Exception($"unexpected function type {_funcType}");
            }
        }
    }
}