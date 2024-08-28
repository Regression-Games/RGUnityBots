using UnityEngine;

namespace RegressionGames
{
    public class RGCreateNewSequenceButton : MonoBehaviour
    {
        public GameObject overlayContainer;

        public void OnClick()
        {
            if (overlayContainer != null)
            {
                overlayContainer.GetComponent<RGSequenceManager>().ShowEditSequenceDialog();
            }
        }
    }
}