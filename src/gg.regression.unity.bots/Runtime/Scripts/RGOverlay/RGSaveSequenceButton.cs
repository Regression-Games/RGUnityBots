using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * When clicked, save or update the Sequence currently loaded in the SequenceEditor
     * </summary>
     */
    public class RGSaveSequenceButton : MonoBehaviour
    {
        public GameObject overlayContainer;

        public Sprite CopyIcon;

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

        /**
         * <summary>
         * Show the correct icon and text depending on if a Sequence is being created or updated
         * </summary>
         * <param name="isEditing">If the Sequence Editor is loaded with an existing Sequence</param>
         */
        public void SetEditModeEnabled(bool isEditing, bool isCopying)
        {
            if (isCopying)
            {
                ButtonText.text = "Copy";
                ButtonIcon.GetComponent<Image>().sprite = CopyIcon;
            }
            else if (isEditing)
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

        /**
         * <summary>
         * When clicked, save or update the Sequence loaded in the Sequence Editor
         * </summary>
         */
        public void OnClick()
        {
            if (overlayContainer != null)
            {
                overlayContainer.GetComponent<RGSequenceManager>().SaveSequenceLoadedInEditor();
            }
        }
    }
}
