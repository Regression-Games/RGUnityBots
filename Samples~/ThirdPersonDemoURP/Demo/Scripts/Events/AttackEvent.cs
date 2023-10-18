using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo.Events
{
    [CreateAssetMenu(menuName = "Events/Attack Event")]
    public class AttackEvent : ScriptableObject
    {
        private List<AttackEventListener> _listeners = new List<AttackEventListener>();

        public void AddListener(AttackEventListener listener)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        public void RemoveListener(AttackEventListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Trigger(AttackAbility attackInfo)
        {
            foreach (var listener in _listeners)
            {
                listener.Trigger(attackInfo);
            }
        }
    }
}