using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * When clicked, save the Sequence currently loaded in the SequenceEditor
     * </summary>
     */
    public class RGSaveSequenceButton : MonoBehaviour
    {
        public GameObject overlayContainer;

        public Sprite CreateIcon;

        public Sprite EditIcon;

        public GameObject ButtonIcon;

        public TMP_Text ButtonText;

        public void Start()
        {
            if (overlayContainer == null)
            {
                Debug.LogError("RGSaveSequenceButton is missing its overlayContainer");
            }
            
            if (CreateIcon == null)
            {
                Debug.LogError("RGSaveSequenceButton is missing its CreateIcon");
            }
            
            if (EditIcon == null)
            {
                Debug.LogError("RGSaveSequenceButton is missing its EditIcon");
            }
            
            if (ButtonIcon == null)
            {
                Debug.LogError("RGSaveSequenceButton is missing its ButtonIcon");
            }
            
            if (ButtonText == null)
            {
                Debug.LogError("RGSaveSequenceButton is missing its ButtonText");
            }
        }
        
        public void SetEditModeEnabled(bool isEditing)
        {
            if (isEditing)
            {
                ButtonText.text = "Update";
                ButtonIcon.GetComponent<Image>().sprite = EditIcon;
            }
            else
            {
                ButtonText.text = "Create";
                ButtonIcon.GetComponent<Image>().sprite = CreateIcon;
            }
        }
        
        public void OnClick()
        {
            if (overlayContainer != null)
            {
                overlayContainer.GetComponent<RGSequenceManager>().SaveSequenceLoadedInEditor();
            }
        }
    }
}