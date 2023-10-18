using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RGThirdPersonDemo
{
    [RequireComponent(typeof(TMP_Text))]
    [RequireComponent(typeof(Animator))]
    public class Alert : MonoBehaviour
    {
        private TMP_Text _alertText;
        private Animator _animator;
        
        void Awake()
        {
            _alertText = GetComponent<TMP_Text>();
            _animator = GetComponent<Animator>();
        }

        /*
         * Sets the alert text to the given message and begins the 'show alert' animation
         */
        public void ShowAlert(string message)
        {
            _alertText.text = message;
            _animator.Play("Alert-Show");
        }
    }
}