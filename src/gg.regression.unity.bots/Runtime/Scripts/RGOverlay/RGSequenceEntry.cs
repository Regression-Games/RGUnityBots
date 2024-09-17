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

    public Action playAction;

    [SerializeField]
    public Button playButton;

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
    }

    /**
     * <summary>When the play button is clicked, start the Sequence and close the RGOverlay</summary>
     */
    public void OnPlay()
    {
        var botManager = RGBotManager.GetInstance();
        botManager?.OnBeginPlaying();
        playAction?.Invoke();
    }
}
