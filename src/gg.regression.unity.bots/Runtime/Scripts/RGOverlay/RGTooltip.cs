using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A simple tooltip that adds listeners for PointerEnter/PointerExit events on the `target`.
     * When moused over, the `target` will display the tooltip
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

            // Add OnPointerEnter + OnPointerExit events to the target. This will show and hide the
            // tooltip's contents respectively
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

            // the tooltip is hidden by default
            contentPrefab.CrossFadeAlpha(0, 0, false);
            backgroundPrefab.CrossFadeAlpha(0, 0, false);
            
            Debug.Log("SHOW THE TIP");
        }

        /**
         * <summary>
         * Enable the tooltip to allow it to show and hide its contents
         * </summary>
         * <param name="newEnabled">The new enabled state</param>
         */
        public void SetEnabled(bool newEnabled)
        {
            isEnabled = newEnabled;
            gameObject.SetActive(isEnabled);
        }

        /**
         * <summary>
         * If this tooltip is enabled, show its contents
         * </summary>
         */
        public void OnShow()
        {
            if (isEnabled)
            {
                contentPrefab.CrossFadeAlpha(1, 0.1f, false);
                backgroundPrefab.CrossFadeAlpha(1, 0.1f, false);
            }
        }

        /**
         * <summary>
         * If this tooltip is enabled, hide its contents
         * </summary>
         */
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