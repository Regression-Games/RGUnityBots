using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RGThirdPersonDemo
{
    public class AbilityCooldown : MonoBehaviour
    {
        [SerializeField] private Image enabledImage;
        [SerializeField] private Image disabledImage;

        private IEnumerator _cooldownCoroutine;
        private bool _preCastCooldown;
        
        // Start is called before the first frame update
        void Start()
        {

        }
        
        public void StartCooldown(AttackAbility attackInfo)
        {
            _cooldownCoroutine = CooldownCoroutine(attackInfo.castTime, attackInfo.cooldownTime);
            StartCoroutine(_cooldownCoroutine);
        }

        public void CancelCooldown()
        {
            // only cancel cooldown if we haven't casted yet
            if (!_preCastCooldown)
            {
                return;
            }
            
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                SetFillAmount(1f);
                _cooldownCoroutine = null;
            }
        }
        
        // Gradually fill the ability icon images as the cooldown progresses
        private IEnumerator CooldownCoroutine(float castTime, float cooldownTime)
        {
            _preCastCooldown = true;
            float curTime = 0;
            while (curTime < cooldownTime)
            {
                if (curTime > castTime)
                {
                    _preCastCooldown = false;
                }
                float normalizedTime = curTime / cooldownTime;
                SetFillAmount(normalizedTime);
                curTime += Time.deltaTime;
                yield return 0;
            }
            SetFillAmount(1f);
            _cooldownCoroutine = null;
        }

        // Set the fill amount on icon images
        private void SetFillAmount(float fill)
        {
            enabledImage.fillAmount = fill;
            disabledImage.fillAmount = fill;
        }
    }
}