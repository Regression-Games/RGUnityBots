using System;
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
    public Button deleteButton;

    /**
     * UI component fields
     */
    [SerializeField]
    public TMP_Text nameComponent;

    [SerializeField]
    public TMP_Text descriptionComponent;

    void Start()
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
        
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(OnDelete);
        }
    }

    /**
     * <summary>When the play button is clicked, start the Sequence and close the RGOverlay</summary>
     */
    public void OnPlay()
    {
        var botManager = RGBotManager.GetInstance();
        if (botManager != null && playAction != null)
        {
            botManager.OnBeginPlaying();
            playAction?.Invoke();
        }
    }

    /**
     * <summary></summary>
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
