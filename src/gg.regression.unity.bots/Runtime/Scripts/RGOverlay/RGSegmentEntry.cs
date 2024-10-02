using System;
using RegressionGames;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RGSegmentEntry : MonoBehaviour
{
    public string segmentName;

    public string description;

    /**
     * <summary>Null if this is a resource load, or path if this is a file.</summary>
     */
    public string path;
    
    
    public Action playAction;

    /**
     * UI component fields
     */
    [SerializeField]
    public Button playButton;
    
    [SerializeField]
    public TMP_Text nameComponent;

    [SerializeField]
    public TMP_Text descriptionComponent;

    public void Start()
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"RGSegmentEntry is missing its path");
        }
        
        if (nameComponent != null)
        {
            nameComponent.text = segmentName;
        }
        else
        {
            Debug.LogError($"RGSegmentEntry is missing its nameComponent");            
        }

        if (descriptionComponent != null)
        {
            descriptionComponent.text = description;
        }
        else
        {
            Debug.LogError($"RGSegmentEntry is missing its descriptionComponent");
        }

        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlay);
        }
        else
        {
            Debug.LogError($"RGSegmentEntry is missing its playButton");
        }

        var tooltip = GetComponentInChildren<RGTooltip>();
        if (tooltip != null && !string.IsNullOrEmpty(description))
        {
            tooltip.content = description;
        }
        else
        {
            tooltip.SetEnabled(false);
        }
    }

    /**
     * <summary>
     * When the play button is clicked, start the Segment and close the RGOverlay
     * </summary>
     */
    public void OnPlay()
    {
        var botManager = RGBotManager.GetInstance();
        botManager?.OnBeginPlaying();
        playAction?.Invoke();
    }
}
