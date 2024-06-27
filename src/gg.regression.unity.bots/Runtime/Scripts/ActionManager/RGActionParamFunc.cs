using System;
using System.Reflection;
using UnityEngine;
using Object = System.Object;

namespace RegressionGames.ActionManager
{
    internal enum SerializedActionParamFuncType
    {
        TYPE_CONSTANT,
        TYPE_MEMBER_ACCESS
    }
    
    [Serializable]
    internal struct SerializedActionParamFunc
    {
        public SerializedActionParamFuncType Type;
        public object Value;
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
    /// such as the key code, button name, axis name, etc.
    /// </summary>
    public class RGActionParamFunc<T> : IEquatable<RGActionParamFunc<T>>
    {
        /// <summary>
        /// This identifier uniquely identifies this function.
        /// It is used for testing action equivalence, as well as action serialization/deserialization.
        /// </summary>
        public string Identifier { get; private set; }
        
        private Func<Object, T> _func;
        
        public RGActionParamFunc(string identifier)
        {
            Identifier = identifier;

            // TODO implement this after the analysis is done
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a constant function that always returns the given value.
        /// </summary>
        public RGActionParamFunc(T value)
        {
            InitConstantFunction(value);
        }

        /// <summary>
        /// Create a function that performs a sequence of field or property accesses to obtain the value.
        /// </summary>
        /// <param name="field"></param>
        public RGActionParamFunc(MemberInfo[] members)
        {
            InitMemberAccesses(members);
        }

        private void InitConstantFunction(T value)
        {
            Identifier = JsonUtility.ToJson(new SerializedActionParamFunc
            {
                Type = SerializedActionParamFuncType.TYPE_CONSTANT,
                Value = value
            });

            _func = _ => value;
        }

        private void InitMemberAccesses(MemberInfo[] members)
        {
            SerializedMemberAccess[] accesses = new SerializedMemberAccess[members.Length];
            for (int i = 0; i < members.Length; ++i)
            {
                ref SerializedMemberAccess access = ref accesses[i];
                var member = members[i];
                access.DeclaringType = member.DeclaringType.AssemblyQualifiedName;
                access.MemberType = member.MemberType;
                access.MemberName = member.Name;
            }

            Identifier = JsonUtility.ToJson(new SerializedActionParamFunc()
            {
                Type = SerializedActionParamFuncType.TYPE_MEMBER_ACCESS,
                Value = accesses
            });
            
            _func = obj =>
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
        }

        public T Invoke(Object obj)
        {
            return _func(obj);
        }

        public bool Equals(RGActionParamFunc<T> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Identifier == other.Identifier;
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
            return (Identifier != null ? Identifier.GetHashCode() : 0);
        }

        public static bool operator ==(RGActionParamFunc<T> left, RGActionParamFunc<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RGActionParamFunc<T> left, RGActionParamFunc<T> right)
        {
            return !Equals(left, right);
        }
    }
}