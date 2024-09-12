using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class RGDeleteSequence : MonoBehaviour
    {
        public TMP_Text sequenceNamePrefab; 
        
        public Button confirmButton;
        
        public Button cancelButton;

        private string _sequencePath;

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

        public void Initialize(RGSequenceEntry sequence)
        {
            _sequencePath = sequence.sequencePath;
            
            if (sequenceNamePrefab != null)
            {
                sequenceNamePrefab.text = sequence.sequenceName;
            }
            else
            {
                Debug.LogError("RGDeleteSequence is missing its sequenceNamePrefab");
            }
        }

        public void OnDelete()
        {
            if (string.IsNullOrEmpty(_sequencePath))
            {
                Debug.LogError($"RGDeleteSequence is missing its _sequencePath when attempting to delete a Sequence");
                return;
            }
            
            var botManager = RGBotManager.GetInstance();
            if (botManager != null)
            {
                var sequenceManager = botManager.GetComponent<RGSequenceManager>();
                if (sequenceManager != null)
                {
                    sequenceManager.DeleteSequenceByPath(_sequencePath);
                    sequenceManager.HideDeleteSequenceDialog();
                }
            }
        }

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
        }
    }
}