using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/**
 * <summary>
 * Displays the high-level information for a Segment, and enables the user to play it
 * </summary>
 */
public class RGSegmentEntry : MonoBehaviour
{
    public string segmentName;

    public string description;

    public string filePath;

    public string resourcePath;

    public BotSequenceEntryType type;
    
    /**
     * UI component fields
     */
    [SerializeField]
    public Button playButton;
    
    [SerializeField]
    public TMP_Text nameComponent;

    [SerializeField]
    public TMP_Text descriptionComponent;
    
    [SerializeField]
    public GameObject segmentListIndicatorComponent;

    /**
     * <summary>
     * Ensures all needed prefabs and fields are properly set. Shows an icon if this segment is of the SegmentList type
     * </summary>
     */
    public void Start()
    {
        // assign values to the UI components
        nameComponent.text = segmentName;
        descriptionComponent.text = description;
        playButton.onClick.AddListener(OnPlay);

        // create a tooltip containing the segment's description, but only if the description is populated
        var tooltip = GetComponentInChildren<RGTooltip>();
        if (tooltip != null && !string.IsNullOrEmpty(description))
        {
            tooltip.content = description;
        }
        else
        {
            tooltip.SetEnabled(false);
        }

        // show an icon indicating that this segment is a SegmentList (if needed)
        if (type == BotSequenceEntryType.SegmentList)
        {
            segmentListIndicatorComponent.SetActive(true);
        }
    }

    /**
     * <summary>
     * When the play button is clicked, start running the Segment and close the RGOverlay
     * </summary>
     */
    private void OnPlay()
    {
        var toolbarManager = FindObjectOfType<ReplayToolbarManager>();
        if (toolbarManager == null)
        {
            Debug.LogError("RGSegmentEntry cannot find the ReplayToolbarManager in its OnPlay function");
            return;
        }
        toolbarManager.selectedReplayFilePath = null;
        
        // set the recording toolbar's state for playing this segment
        var botManager = RGBotManager.GetInstance();
        botManager.OnBeginPlaying();

        // create the list of segments to play (in our case there will only be 1)
        var segmentList = new List<BotSegmentList>
        {
            BotSequence.CreateBotSegmentListForPath(filePath ?? resourcePath, out var sessId)
        };
        var sessionId = sessId ?? Guid.NewGuid().ToString();
        
        var playbackController = FindObjectOfType<BotSegmentsPlaybackController>();
        if (playbackController == null)
        {
            Debug.LogError("RGSegmentEntry cannot find the BotSegmentsPlaybackController in its OnPlay function");
            return;
        }
        
        // play the segment
        playbackController.Stop();
        playbackController.Reset();
        playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(segmentList.SelectMany(a => a.segments), sessionId));
        playbackController.Play();
    }
}
