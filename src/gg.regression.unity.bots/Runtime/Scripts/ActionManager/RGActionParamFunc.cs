using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
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
            return new RGActionParamFunc<T>(ActionParamFuncType.TYPE_CONSTANT, data, func);
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

            string data = JsonUtility.ToJson(new SerializedMemberAccessFuncData
            {
                MemberAccesses = accesses
            });
            
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
                return (T)currentObject;
            };

            return new RGActionParamFunc<T>(ActionParamFuncType.TYPE_MEMBER_ACCESS, data, func);
        }
        
        public T Invoke(Object obj)
        {
            return _func(obj);
        }

        public static RGActionParamFunc<T> Deserialize(ActionParamFuncType funcType, string data)
        {
            switch (funcType)
            {
                case ActionParamFuncType.TYPE_CONSTANT:
                {
                    
                    break;
                }
                case ActionParamFuncType.TYPE_MEMBER_ACCESS:
                {
                    
                    break;
                }
            }

            throw new NotImplementedException();
        }

        public (ActionParamFuncType, string) Serialize()
        {
            return (_funcType, _data);
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
            return "RGActionParamFunc<" + typeof(T) + "> { type: " + _funcType + ", data: " + _data + " }";
        }
    }
}