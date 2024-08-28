using UnityEngine;

namespace RegressionGames
{
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