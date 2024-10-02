﻿using System;
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
    
    // will be a file path when in-editor, or a resources path when in a packaged build
    public string path;

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
            if (segmentListIndicatorComponent != null)
            {
                segmentListIndicatorComponent.SetActive(true);
            }
            else
            {
                Debug.LogError($"RGSegmentEntry is missing its segmentListIndicatorComponent");
            }
        }
    }

    /**
     * <summary>
     * When the play button is clicked, start running the Segment and close the RGOverlay
     * </summary>
     */
    private void OnPlay()
    {
        var botManager = RGBotManager.GetInstance();
        if (botManager == null)
        {
            Debug.LogError("RGSegmentEntry cannot find the RGBotManager in its OnPlay function");
            return;
        }
        
        var toolbarManager = FindObjectOfType<ReplayToolbarManager>();
        if (toolbarManager == null)
        {
            Debug.LogError("RGSegmentEntry cannot find the ReplayToolbarManager in its OnPlay function");
            return;
        }
        toolbarManager.selectedReplayFilePath = null;
        
        // set the recording toolbar's state for playing this segment
        botManager.OnBeginPlaying();

        // create the list of segments to play (in our case there will only be 1)
        var segmentList = new List<BotSegmentList>
        {
            BotSequence.CreateBotSegmentListForPath(path, out var sessId)
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
