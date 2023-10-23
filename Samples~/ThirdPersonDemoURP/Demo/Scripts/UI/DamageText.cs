using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RGThirdPersonDemo
{
    [RequireComponent(typeof(TMP_Text))]
    [RequireComponent(typeof(Animator))]
    public class DamageText : MonoBehaviour
    {
        private TMP_Text _damageText;
        private Animator _animator;
        
        void Awake()
        {
            _damageText = GetComponent<TMP_Text>();
            _animator = GetComponent<Animator>();
        }

        public void SetDamageText(string damageText)
        {
            _damageText.text = damageText;
            _animator.Play("DamageText-Show");
        }
    }
}