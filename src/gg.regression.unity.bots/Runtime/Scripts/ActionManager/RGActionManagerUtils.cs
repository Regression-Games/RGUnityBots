using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace RegressionGames.ActionManager
{
    public static class RGActionManagerUtils
    {
        private static FieldInfo _persistentCallsField;
        private static MethodInfo _countMethod;
        private static MethodInfo _getListenerMethod;
        private static FieldInfo _targetField;
        private static FieldInfo _methodNameField;
        
        public static IEnumerable<string> GetEventListenerMethodNames(UnityEvent evt)
        {
            if (_persistentCallsField == null)
            {
                _persistentCallsField = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            
            object persistentCalls = _persistentCallsField.GetValue(evt);

            if (_countMethod == null)
            {
                _countMethod = persistentCalls.GetType().GetMethod("get_Count", BindingFlags.Public | BindingFlags.Instance);
                _getListenerMethod = persistentCalls.GetType().GetMethod("GetListener", BindingFlags.Public | BindingFlags.Instance);
            }
        
            int listenerCount = (int)_countMethod.Invoke(persistentCalls, null);

            for (int i = 0; i < listenerCount; i++)
            {
                object listener = _getListenerMethod.Invoke(persistentCalls, new object[] { i });
                if (_targetField == null)
                {
                    _targetField = listener.GetType().GetField("m_Target", BindingFlags.NonPublic | BindingFlags.Instance);
                    _methodNameField = listener.GetType()
                                        .GetField("m_MethodName", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                object target = _targetField.GetValue(listener);
                string methodName = (string)_methodNameField.GetValue(listener);
            
                if (target != null && !string.IsNullOrEmpty(methodName))
                {
                    yield return target.GetType().FullName + "." + methodName;
                }
            }
        }

        public static Func<Object, T> DeserializeFuncFromName<T>(string funcName)
        {
            // TODO implement this after the analysis is done
            throw new NotImplementedException();
        }
    }
}