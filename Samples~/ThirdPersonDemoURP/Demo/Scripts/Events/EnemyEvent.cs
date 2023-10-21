using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo.Events
{
    [CreateAssetMenu(menuName = "Events/Enemy Event")]
    public class EnemyEvent : ScriptableObject
    {
        private List<EnemyEventListener> _listeners = new List<EnemyEventListener>();

        public void AddListener(EnemyEventListener listener)
        {
            if (!_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        public void RemoveListener(EnemyEventListener listener)
        {
            _listeners.Remove(listener);
        }

        public void Trigger(EnemyController enemyController)
        {
            foreach (var listener in _listeners)
            {
                listener.Trigger(enemyController);
            }
        }
    }
}