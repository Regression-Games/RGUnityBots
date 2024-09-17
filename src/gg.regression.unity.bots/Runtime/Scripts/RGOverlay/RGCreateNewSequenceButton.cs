using UnityEngine;

namespace RegressionGames
{
    /**
     * <summary>
     * When clicked, open the Sequence Editor
     * </summary>
     */
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