using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RegressionGames
{
    /**
     * <summary>
     * The in-motion state of a RGDraggableCard. The `payload` field is used to transfer data from this dragging state,
     * to the resting state of a card.
     * </summary>
     */
    public class RGDraggedCard : MonoBehaviour
    {
        public string draggedCardName;

        public string draggedCardResourcePath;

        public Dictionary<string, string> payload;

        public GameObject iconPrefab;

        [SerializeField]
        public TMP_Text nameComponent;
        
        [SerializeField]
        public TMP_Text resourcePathComponent;

        public void Start()
        {
            // if we have a name set
            if (!string.IsNullOrEmpty(draggedCardName))
            {
                if (nameComponent != null)
                {
                    nameComponent.text = draggedCardName;
                }

                if (resourcePathComponent != null)
                {
                    resourcePathComponent.text = draggedCardResourcePath;
                    resourcePathComponent.gameObject.SetActive(true);
                }
            }
            else
            {
                // use the resource path as the name and hide the hint path
                nameComponent.text = draggedCardResourcePath;
                if (resourcePathComponent != null)
                {
                    resourcePathComponent.text = "";
                    resourcePathComponent.gameObject.SetActive(false);
                }
            }
        }

        public void FadeOutAndDestroy()
        {
            GetComponent<RGFadeOutAnimation>()?.StartFadeOut(this.gameObject);
        }
    }
}
