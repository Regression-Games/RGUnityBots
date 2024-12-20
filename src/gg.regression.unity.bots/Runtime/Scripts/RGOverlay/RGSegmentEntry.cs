using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.BotSegments.Models;
using StateRecorder.BotSegments.Models;
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
     * <summary>Indicates whether the Segment or Segment List is overridden by a local file.</summary>
     */
    public bool isOverride;

    /**
     * UI component fields
     */
    [SerializeField]
    public Button playButton;

    [SerializeField]
    public TMP_Text nameComponent;

    [SerializeField]
    public TMP_Text resourcePathComponent;

    [SerializeField]
    public TMP_Text descriptionComponent;

    [SerializeField]
    public GameObject segmentListIndicatorComponent;

    [SerializeField]
    public GameObject overrideIndicator;

    /**
     * <summary>
     * Ensures all needed prefabs and fields are properly set. Shows an icon if this segment is of the SegmentList type
     * </summary>
     */
    public void Start()
    {
        // if we have a name set
        if (!string.IsNullOrEmpty(segmentName))
        {
            if (nameComponent != null)
            {
                nameComponent.text = segmentName;
            }

            if (resourcePathComponent != null)
            {
                resourcePathComponent.text = resourcePath;
                resourcePathComponent.gameObject.SetActive(true);
            }
        }
        else
        {
            // use the resource path as the name and hide the hint path
            nameComponent.text = resourcePath;
            if (resourcePathComponent != null)
            {
                resourcePathComponent.text = "";
                resourcePathComponent.gameObject.SetActive(false);
            }
        }

        // set indicator that this Segment is being overriden by a local file, within a build
        overrideIndicator.gameObject.SetActive(isOverride);

        // assign values to the UI components
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

        // create the list of segments to play. This list will either contain an individual segment, or a segment list
        var segmentList = BotSequence.CreateBotSegmentListForPath(filePath ?? resourcePath, out var sessId);
        var sessionId = sessId ?? Guid.NewGuid().ToString();

        var playbackController = FindObjectOfType<BotSegmentsPlaybackController>();
        if (playbackController == null)
        {
            Debug.LogError("RGSegmentEntry cannot find the BotSegmentsPlaybackController in its OnPlay function");
            return;
        }

        // play the segment
        playbackController.SetDataContainer(new BotSegmentsPlaybackContainer(new List<BotSegmentList>() {segmentList}, new List<SegmentValidation>(), sessionId));

        playbackController.Play();
    }
}
