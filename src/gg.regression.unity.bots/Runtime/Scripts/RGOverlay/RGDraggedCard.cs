using TMPro;
using UnityEngine;

namespace RegressionGames
{
    /**
     * <summary>
     * The in-motion state of a RGDraggableCard
     * </summary>
     */
    public class RGDraggedCard : MonoBehaviour
    {
        public string draggedCardName;

        [SerializeField] public TMP_Text namePrefab;

        public void Start()
        {
            if (namePrefab != null)
            {
                namePrefab.text = draggedCardName;
            }
        }

        public void FadeOutAndDestroy()
        {
            GetComponent<RGFadeOutAnimation>()?.StartFadeOut(this.gameObject);
        }
    }
}