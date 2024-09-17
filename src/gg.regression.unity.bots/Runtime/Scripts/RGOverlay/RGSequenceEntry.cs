using System;
using RegressionGames;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/**
 * <summary>Displays the high-level information for a Sequence</summary>
 */
public class RGSequenceEntry : MonoBehaviour
{
    public string sequenceName;

    public string description;

    public string sequencePath;
    
    public Action playAction;

    [SerializeField]
    public Button playButton;

    [SerializeField]
    public Button editButton;
    
    [SerializeField]
    public Button deleteButton;

    /**
     * UI component fields
     */
    [SerializeField]
    public TMP_Text nameComponent;

    [SerializeField]
    public TMP_Text descriptionComponent;

    public void Start()
    {
        if (nameComponent != null)
        {
            nameComponent.text = sequenceName;
        }

        if (descriptionComponent != null)
        {
            descriptionComponent.text = description;
        }

        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlay);
        }

        if (editButton != null)
        {
            editButton.onClick.AddListener(OnEdit);
        }
        
        if (deleteButton != null)
        {
            /*
             * Sequences cannot be deleted in runtime builds, as we cannot delete resources
             * that have been loaded using Resources.Load()
             */
#if UNITY_EDITOR
                deleteButton.onClick.AddListener(OnDelete);
                
                // hide the delete button tooltip. We CAN delete
                // Sequences while playing in the editor
                var tooltip = GetComponentInChildren<RGTooltip>();
                if (tooltip != null)
                {
                    tooltip.SetEnabled(false);
                }
#else
                deleteButton.interactable = false;
#endif
        }
    }

    /**
     * <summary>
     * When the play button is clicked, start the Sequence and close the RGOverlay
     * </summary>
     */
    public void OnPlay()
    {
        var botManager = RGBotManager.GetInstance();
        botManager?.OnBeginPlaying();
        playAction?.Invoke();
    }

    /**
     * <summary>
     * When the edit button is pressed, open the Sequence Editor preloaded and populate it
     * </summary>
     */
    public void OnEdit()
    {
        var sequenceManager = RGSequenceManager.GetInstance();
        if (sequenceManager != null)
        {
            sequenceManager.ShowEditSequenceDialog(sequencePath);
        }
    }

    /**
     * <summary>
     * When the delete button is pressed, show a confirmation dialog
     * </summary>
     */
    public void OnDelete()
    {
        var botManager = RGBotManager.GetInstance();
        if (botManager != null)
        {
            var sequenceManager = botManager.GetComponent<RGSequenceManager>();
            if (sequenceManager != null)
            {
                sequenceManager.ShowDeleteSequenceDialog(this);
            }
        }
    }
}
