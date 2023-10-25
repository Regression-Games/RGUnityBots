using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RGThirdPersonDemo.Events
{
    public class AttackEventListener : MonoBehaviour
    {
        public AttackEvent enemyEvent;
        [Space] 
        public AttackEventType onTriggered;

        private void OnEnable()
        {
            enemyEvent.AddListener(this);
        }

        private void OnDisable()
        {
            enemyEvent.RemoveListener(this);
        }

        public void Trigger(AttackAbility attackInfo)
        {
            onTriggered.Invoke(attackInfo);
        }
    }

    [System.Serializable]
    public class AttackEventType : UnityEvent<AttackAbility> { }
}