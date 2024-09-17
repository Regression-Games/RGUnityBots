using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * Initializes the delete sequence dialog with the correct content, and is responsible for
     * deleting sequences via RGSequenceManager
     * </summary>
     */
    public class RGDeleteSequence : MonoBehaviour
    {
        public TMP_Text sequenceNamePrefab;

        public Button confirmButton;

        public Button cancelButton;

        /**
         * <summary>Null if this is a resource load, or path if this is a file.</summary>
         */
        public string filePath = string.Empty;
        public string resourcePath = string.Empty;

        /**
         * <summary>
         * Initializes the delete sequence dialog with the correct content
         * </summary>
         */
        public void Start()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(OnDelete);
            }
            else
            {
                Debug.LogError("RGDeleteSequence is missing its confirmButton");
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancel);
            }
            else
            {
                Debug.LogError("RGDeleteSequence is missing its cancelButton");
            }
        }

        /**
         * <summary>
         * Set the path and name of the Sequence the user is wanting to delete
         * </summary>
         * <param name="sequence">The Sequence Entry to delete</param>
         */
        public void Initialize(RGSequenceEntry sequence)
        {
            filePath = sequence.filePath;
            resourcePath = sequence.resourcePath;

            if (sequenceNamePrefab != null)
            {
                sequenceNamePrefab.text = sequence.sequenceName;
            }
            else
            {
                Debug.LogError("RGDeleteSequence is missing its sequenceNamePrefab");
            }
        }

        /**
         * <summary>
         * When the deletion is confirmed, ensure that the initialized Sequence path is valid and complete the deletion
         * and reset the dialog's contents
         * </summary>
         */
        public void OnDelete()
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new Exception($"Cannot delete BotSequence resource at: {resourcePath}.  It was loaded as a Resource from your project's Assets.  If you wish to permanently delete it, remove it from your project's Assets and rebuild.");
            }

            var botManager = RGBotManager.GetInstance();
            if (botManager != null)
            {
                var sequenceManager = botManager.GetComponent<RGSequenceManager>();
                if (sequenceManager != null)
                {
                    sequenceManager.DeleteSequenceByPath(filePath);
                    sequenceManager.HideDeleteSequenceDialog();
                }
            }

            Reset();
        }

        /**
         * <summary>
         * When the deletion is canceled, hide this dialog and reset its contents
         * </summary>
         */
        public void OnCancel()
        {
            var botManager = RGBotManager.GetInstance();
            if (botManager != null)
            {
                var sequenceManager = botManager.GetComponent<RGSequenceManager>();
                if (sequenceManager != null)
                {
                    sequenceManager.HideDeleteSequenceDialog();
                }
            }

            Reset();
        }

        /**
         * <summary>
         * Reset the Sequence to delete path and name in the dialog
         * </summary>
         */
        private void Reset()
        {
            sequenceNamePrefab.text = string.Empty;
            filePath = string.Empty;
            resourcePath = string.Empty;
        }
    }
}
