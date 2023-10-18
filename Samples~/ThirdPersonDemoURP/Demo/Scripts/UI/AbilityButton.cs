using System;
using System.Collections;
using System.Collections.Generic;
using RGThirdPersonDemo;
using RGThirdPersonDemo.Events;
using UnityEngine;
using UnityEngine.UI;

namespace RGThirdPersonDemo
{
    public class AbilityButton : MonoBehaviour
    {
        [SerializeField] private StringEvent alertEvent;
        [SerializeField] private AttackEvent attackBeginEvent;
        [SerializeField] private AttackAbility ability;
        [SerializeField] private Button button;
        
        private IEnumerator _reactivateAbilityCoroutine;
        private bool _preCastCooldown;
        private bool _playerIsMoving;

        private void Start()
        {
            button.onClick.AddListener(Attack);
        }

        // Trigger the attack event with the given attack ability
        private void Attack()
        {
            // Do not attack if player is currently moving
            if (_playerIsMoving)
            {
                alertEvent.Trigger("Cannot do that while player is moving");
                return;
            }
            
            attackBeginEvent.Trigger(ability);
            button.interactable = false;
            _reactivateAbilityCoroutine = ActiveButtonAfterCooldown();
            StartCoroutine(_reactivateAbilityCoroutine);
        }
        
        /*
         * Interrupts normal timer, to reset the ability button functionality
         */
        public void CancelAttack()
        {
            // only cancel cooldown if we haven't casted yet
            if (!_preCastCooldown)
            {
                return;
            }
            
            if (_reactivateAbilityCoroutine != null)
            {
                StopCoroutine(_reactivateAbilityCoroutine);
                _reactivateAbilityCoroutine = null;
            }
            Reactivate();
        }

        public void PlayerStartMoving()
        {
            _playerIsMoving = true;
        }

        public void PlayerStopMoving()
        {
            _playerIsMoving = false;
        }
        
        private void Reactivate()
        {
            button.interactable = true;
        }
        
        // Reactivates the ability button after the cooldown on the ability has ended
        private IEnumerator ActiveButtonAfterCooldown()
        {
            float curTime = 0;
            _preCastCooldown = true;
            while (curTime < ability.cooldownTime)
            {
                if (curTime > ability.castTime)
                {
                    _preCastCooldown = false;
                }
                curTime += Time.deltaTime;
                yield return 0;
            }

            button.interactable = true;
            _reactivateAbilityCoroutine = null;
        }
    }
}