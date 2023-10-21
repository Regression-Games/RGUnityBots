using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo.Events
{
    [CreateAssetMenu(menuName = "Events/String Event")]
    public class StringEvent : ScriptableObject
    {
        private List<StringEventListener> _listeners = new List<StringEventListener>();

        public void AddListener(StringEventListener listener)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        public void RemoveListener(StringEventListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Trigger(string eventString)
        {
            foreach (var listener in _listeners)
            {
                listener.Trigger(eventString);
            }
        }
    }
}