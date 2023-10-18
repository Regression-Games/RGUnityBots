using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RGThirdPersonDemo.Events
{
    public class StringEventListener : MonoBehaviour
    {
        public StringEvent stringEvent;
        [Space] 
        public StringEventType onTriggered;

        private void OnEnable()
        {
            stringEvent.AddListener(this);
        }

        private void OnDisable()
        {
            stringEvent.RemoveListener(this);
        }

        public void Trigger(string eventString)
        {
            onTriggered.Invoke(eventString);
        }
    }

    [System.Serializable]
    public class StringEventType : UnityEvent<string> { }
}