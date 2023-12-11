using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo.Events
{
    [CreateAssetMenu(menuName = "Events/Transform Event")]
    public class TransformEvent : ScriptableObject
    {
        private List<TransformEventListener> _listeners = new List<TransformEventListener>();

        public void AddListener(TransformEventListener listener)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        public void RemoveListener(TransformEventListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Trigger(Transform eventTransform)
        {
            foreach (var listener in _listeners)
            {
                listener.Trigger(eventTransform);
            }
        }
    }
}