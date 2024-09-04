using UnityEngine;

namespace RegressionGames
{
    /**
     * <summary>
     * When clicked, create or update the Sequence loaded in the SequenceEditor
     * </summary>
     */
    public class RGSaveSequenceButton : MonoBehaviour
    {
        public GameObject overlayContainer;

        public void OnClick()
        {
            if (overlayContainer != null)
            {
                overlayContainer.GetComponent<RGSequenceManager>().SaveSequenceLoadedInEditor();
            }
        }
    }
}