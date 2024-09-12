using System;
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

        public string sequencePath;

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
            sequencePath = sequence.sequencePath;
            
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
            if (string.IsNullOrEmpty(sequencePath))
            {
                throw new Exception($"RGDeleteSequence is missing its _sequencePath when attempting to delete a Sequence");
            }
            
            var botManager = RGBotManager.GetInstance();
            if (botManager != null)
            {
                var sequenceManager = botManager.GetComponent<RGSequenceManager>();
                if (sequenceManager != null)
                {
                    sequenceManager.DeleteSequenceByPath(sequencePath);
                    sequenceManager.HideDeleteSequenceDialog();
                }
            }

            Reset();
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

            Reset();
        }

        private void Reset()
        {
            sequenceNamePrefab.text = string.Empty;
            sequencePath = string.Empty;
        }
    }
}