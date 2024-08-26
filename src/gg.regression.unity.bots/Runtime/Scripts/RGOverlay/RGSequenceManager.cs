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
public class RGSequenceManager : MonoBehaviour
{
    public GameObject sequencesPanel;

    public GameObject sequenceCardPrefab;

    private static RGSequenceManager _this;

    private IList<BotSequence> _sequences;

    private string _sequencePath;

    public static RGSequenceManager GetInstance()
    {
        return _this;
    }

    private void Awake()
    {
        _sequences = new List<BotSequence>();
        _sequencePath = ResolveSequencesDirectory();

        LoadSequences();

        _this = this;
        DontDestroyOnLoad(_this.gameObject);
    }

    /**
     * <summary>Loads all Sequence json files within the Unity project</summary>
     */
    public void LoadSequences()
    {
        if (Directory.Exists(_sequencePath))
        {
            // ensure we arent' appending new sequences on to the previous ones
            ClearExistingSequences();

            IEnumerable<string> sequenceFiles = Directory.EnumerateFiles(_sequencePath, "*.json");
            IList<BotSequence> sequences = sequenceFiles
                .Select(fileName => {
                    try 
                    {
                        return BotSequence.LoadSequenceJsonFromPath(fileName);
                    }
                    catch (Exception exception)
                    {
                        Debug.Log($"Error reading Bot Sequences from {fileName}: {exception}");
                        return null;
                    }
                })
                .Where(s => s != null)
                .ToList();

            // instantiate a prefab for each sequence file that has been loaded
            foreach (BotSequence sequence in sequences)
            {
                var instance = Instantiate(sequenceCardPrefab, Vector3.zero, Quaternion.identity);
                
                // map sequence data fields to new prefab
                instance.transform.SetParent(sequencesPanel.transform, false);
                var prefabComponent = instance.GetComponent<RGSequenceEntry>();
                if (prefabComponent != null)
                {
                    prefabComponent.sequenceName = sequence.name;
                    prefabComponent.description = sequence.description;
                    prefabComponent.playAction = sequence.Play;
                }

                _sequences.Add(sequence);
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

    /**
     * <summary>Figure out where Bot Sequences are stored in the project, in both the Unity editor and builds </summary>
     * <returns>Path to the directory containing Bot Sequence files</returns>
     */
    private string ResolveSequencesDirectory()
    {
        var editorPath = "Assets/RegressionGames/Resources/BotSequences";
#if UNITY_EDITOR
        return editorPath;
#else
        // check the persistentDataPath for sequences
        var persistentDataPath = Application.persistentDataPath + "/BotSequences"
        var shouldUsePersistentDataPath = Directory.Exists(persistentDataPath);
        if (shouldUsePersistentDataPath)
        {
            return persistentDataPath;
        }

        // default to editor path
        return editorPath;
#endif
    }
}
