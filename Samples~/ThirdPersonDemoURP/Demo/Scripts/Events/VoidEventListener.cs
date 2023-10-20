using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RGThirdPersonDemo.Events
{
    public class VoidEventListener : MonoBehaviour
    {
        public VoidEvent voidEvent;
        [Space] 
        public VoidEventType onTriggered;

        private void OnEnable()
        {
            voidEvent.AddListener(this);
        }

        private void OnDisable()
        {
            voidEvent.RemoveListener(this);
        }

        public void Trigger()
        {
            onTriggered.Invoke();
        }
    }

    [System.Serializable]
    public class VoidEventType : UnityEvent { }
}