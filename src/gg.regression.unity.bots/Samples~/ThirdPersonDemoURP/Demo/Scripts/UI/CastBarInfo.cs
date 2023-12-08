using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RGThirdPersonDemo
{
    public class CastBarInfo : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image progressBarImage;
        [SerializeField] private TMP_Text nameText;

        private IEnumerator _updateCastProgressBarCoroutine;
        
        void Start()
        {

        }

        public void BeginCast(AttackAbility attackInfo)
        {
            _updateCastProgressBarCoroutine = UpdateCastProgressBar(attackInfo.castTime);
                
            // setup the ui
            nameText.text = attackInfo.abilityName;
            progressBarImage.color = attackInfo.castColor;
            progressBarImage.fillAmount = 0f;
            iconImage.sprite = attackInfo.abilityIcon;
            gameObject.ActivateChildren();
            
            // update progress bar over time
            StartCoroutine(_updateCastProgressBarCoroutine);
        }

        public void CancelCast()
        {
            if (_updateCastProgressBarCoroutine != null)
            {
                StopCoroutine(_updateCastProgressBarCoroutine);
                gameObject.DeactivateChildren();
                _updateCastProgressBarCoroutine = null;
            }
        }
        
        private IEnumerator UpdateCastProgressBar(float castTime)
        {
            // fill the cast bar
            float curTime = 0f;
            while (curTime < castTime)
            {
                float normalizedTime = curTime / castTime;
                progressBarImage.fillAmount = normalizedTime;
                curTime += Time.deltaTime;
                yield return 0;
            }

            progressBarImage.fillAmount = 1f;
            
            // add a small delay for visual clarity that the bar is full
            yield return new WaitForSeconds(0.15f);
            
            // hide the cast bar
            gameObject.DeactivateChildren();
            _updateCastProgressBarCoroutine = null;
        }
    }
}