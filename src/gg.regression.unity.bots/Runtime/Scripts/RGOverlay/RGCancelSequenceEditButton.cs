using UnityEngine;

namespace RegressionGames
{
    /**
     * <summary>
     * When clicked, hide the Sequence Editor
     * </summary>
     */
    public class RGCancelSequenceEditButton : MonoBehaviour
    {
        public GameObject overlayContainer;

        public void OnClick()
        {
            if (overlayContainer != null)
            {
                overlayContainer.GetComponent<RGSequenceManager>().HideEditSequenceDialog();
            }
        }
    }
}