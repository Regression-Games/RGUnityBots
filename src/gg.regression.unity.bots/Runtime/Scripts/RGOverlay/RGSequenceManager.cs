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

public class RGSequenceManager : MonoBehaviour
{
    public GameObject sequencesPanel;

    public GameObject sequenceCardPrefab;

    private IList<BotSequence> _sequences;

    private static RGSequenceManager _this;

    private const string SEQUENCES_PATH = "Assets/RegressionGames/Resources/BotSequences";

    public static RGSequenceManager GetInstance()
    {
        return _this;
    }

    private void Awake()
    {
        _sequences = new List<BotSequence>();

        LoadSequences();

        _this = this;
        DontDestroyOnLoad(_this.gameObject);
    }

    /**
     * <summary>Loads all Sequence json files within the Unity project</summary>
     */
    public void LoadSequences()
    {
        if (Directory.Exists(SEQUENCES_PATH))
        {
            // ensure we arent' appending new sequences on to the previous ones
            ClearExistingSequences();

            IEnumerable<string> sequenceFiles = Directory.EnumerateFiles(SEQUENCES_PATH, "*.json");
            foreach (string fileName in sequenceFiles)
            {
                try 
                {
                    BotSequence sequence = BotSequence.LoadSequenceJsonFromPath(fileName);
                    var instance = Instantiate(sequenceCardPrefab, Vector3.zero, Quaternion.identity);
                    
                    // instantiate UI component and set Sequence fields
                    instance.transform.SetParent(sequencesPanel.transform, false);
                    var prefabComponent = instance.GetComponent<RGSequenceEntry>();
                    if (prefabComponent != null)
                    {
                        prefabComponent.name = sequence.name;
                        prefabComponent.description = sequence.description;
                        prefabComponent.lastModified = sequence.lastModified;
                    }

                    _sequences.Add(sequence);
                }
                catch (Exception exception)
                {
                    Debug.Log($"Error reading Bot Sequences from {fileName}: {exception}");
                    continue;
                }
            }
        }
    }

    /**
     * <summary>Destroy all Sequence cards in the UI</summary>
     */
    private void ClearExistingSequences()
    {
        _sequences.Clear();

        for(int i = 0; i < sequencesPanel.transform.childCount; i++)
        {
            if (i == 0)
            {
                // don't destroy the 'create new sequence' button child. It is always the first one
                continue;
            }

            Destroy(sequencesPanel.transform.GetChild(i).gameObject);
        }
    }
}
