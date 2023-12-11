using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo.Events
{
    [CreateAssetMenu(menuName = "Events/Void Event")]
    public class VoidEvent : ScriptableObject
    {
        private List<VoidEventListener> _listeners = new List<VoidEventListener>();

        public void AddListener(VoidEventListener listener)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        public void RemoveListener(VoidEventListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Trigger()
        {
            foreach (var listener in _listeners)
            {
                listener.Trigger();
            }
        }
    }
}