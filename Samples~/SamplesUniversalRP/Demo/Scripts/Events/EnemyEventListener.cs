using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RGThirdPersonDemo.Events
{
    public class EnemyEventListener : MonoBehaviour
    {
        public EnemyEvent enemyEvent;
        [Space] 
        public EnemyEventType onTriggered;

        private void OnEnable()
        {
            enemyEvent.AddListener(this);
        }

        private void OnDisable()
        {
            enemyEvent.RemoveListener(this);
        }

        public void Trigger(EnemyController enemyController)
        {
            onTriggered.Invoke(enemyController);
        }
    }

    [System.Serializable]
    public class EnemyEventType : UnityEvent<EnemyController> { }
}