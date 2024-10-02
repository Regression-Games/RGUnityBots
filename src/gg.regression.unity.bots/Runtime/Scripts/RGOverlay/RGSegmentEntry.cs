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

public class RGSegmentEntry : MonoBehaviour
{
    public string segmentName;

    public string description;

    /**
     * <summary>Null if this is a resource load, or path if this is a file.</summary>
     */
    public string path;
    
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
        if (botManager != null)
        {
            var toolbarManager = FindObjectOfType<ReplayToolbarManager>();
            toolbarManager.selectedReplayFilePath = null;
            
            botManager.OnBeginPlaying();

            var segmentList = new List<BotSegmentList>();
            segmentList.Add(CreateBotSegmentListForPath(path, out var sessId));
            var sessionId = sessId;
            sessionId ??= Guid.NewGuid().ToString();
            
            var playbackController = FindObjectOfType<BotSegmentsPlaybackController>();
            playbackController.Stop();
            playbackController.Reset();
            playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(segmentList.SelectMany(a => a.segments), sessionId));
            playbackController.Play();
        }
    }

    private BotSegmentList CreateBotSegmentListForPath(string path, out string sessionId)
    {
        var result = BotSequence.LoadBotSegmentOrBotSegmentListFromPath(path);
        if (result.Item3 is BotSegmentList bsl)
        {
            sessionId = bsl.segments.FirstOrDefault(a => !string.IsNullOrEmpty(a.sessionId))?.sessionId;
            return bsl;
        }
        else
        {
            var segment = (BotSegment)result.Item3;
            sessionId = segment.sessionId;
            var segmentList = new BotSegmentList(path, new List<BotSegment> { segment });
            segmentList.name = segment.name;
            segmentList.description = segment.description;
            segmentList.FixupNames();
            return segmentList;
        }
    }
}
