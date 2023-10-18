using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RGThirdPersonDemo.Events
{
    public class TransformEventListener : MonoBehaviour
    {
        public TransformEvent transformEvent;
        [Space] 
        public TransformEventType onTriggered;

        private void OnEnable()
        {
            transformEvent.AddListener(this);
        }

        private void OnDisable()
        {
            transformEvent.RemoveListener(this);
        }

        public void Trigger(Transform eventTransform)
        {
            onTriggered.Invoke(eventTransform);
        }
    }

    [System.Serializable]
    public class TransformEventType : UnityEvent<Transform> { }
}