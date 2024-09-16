using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * 
     * </summary>
     */
    public class RGTooltip : MonoBehaviour
    {
        [Multiline]
        public string content;

        public TMP_Text contentPrefab;

        public Image backgroundPrefab;

        public GameObject target;
        
        public bool isEnabled;

        public void Start()
        {
            if (contentPrefab != null)
            {
                contentPrefab.text = content;
            }
            else
            {
                Debug.LogError("RGTooltip is missing its contentPrefab");
            }

            if (target != null)
            {
                var eventTrigger = target.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener(_ => { OnShow(); });
                eventTrigger.triggers.Add(entry);
                
                var eventTrigger2 = target.AddComponent<EventTrigger>();
                var entry2 = new EventTrigger.Entry();
                entry2.eventID = EventTriggerType.PointerExit;
                entry2.callback.AddListener(_ => { OnHide(); });
                eventTrigger2.triggers.Add(entry2);
            }
            else
            {
                Debug.LogError("RGTooltip is missing its target");
            }

            gameObject.SetActive(isEnabled);

            contentPrefab.CrossFadeAlpha(0, 0, false);
            backgroundPrefab.CrossFadeAlpha(0, 0, false);
        }

        public void SetEnabled(bool newEnabled)
        {
            isEnabled = newEnabled;
            gameObject.SetActive(isEnabled);
        }

        public void OnShow()
        {
            if (isEnabled)
            {
                contentPrefab.CrossFadeAlpha(1, 0.1f, false);
                backgroundPrefab.CrossFadeAlpha(1, 0.1f, false);
            }
        }

        public void OnHide()
        {
            if (isEnabled)
            {
                contentPrefab.CrossFadeAlpha(0, 0.1f, false);
                backgroundPrefab.CrossFadeAlpha(0, 0.1f, false);
            }
        }
    }
}