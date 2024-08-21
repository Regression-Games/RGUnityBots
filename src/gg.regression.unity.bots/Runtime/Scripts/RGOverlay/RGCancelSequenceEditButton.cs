using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames;
using TMPro;
using UnityEngine;
using RegressionGames.StateRecorder.BotSegments.Models;

/**
 * <summary>UI for managing operations performed on the user's set of Bot Sequences</summary>
 */
public class RGCancelSequenceEditButton : MonoBehaviour
{
    public GameObject overlayContainer;

    public void OnClick()
    {
        if (overlayContainer != null)
        {
            overlayContainer.GetComponent<RGSequenceManager>().HideEditSequenceDialog();
        }
    }
}