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
        // ensure we aren't appending new sequences on to the previous ones
        ClearExistingSequences();

        var sequences = ResolveSequenceFiles();
        
        // instantiate a prefab for each sequence file that has been loaded
        foreach (var sequence in sequences)
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

    private IList<BotSequence> EnumerateSequencesInDirectory(string path)
    {
        var sequenceFiles = Directory.EnumerateFiles(path, "*.json");
        return sequenceFiles
            .Select(fileName =>
            {
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
    }
    
    /**
     * <summary>Figure out where Bot Sequences are stored in the project, in both the Unity editor and packaged builds</summary>
     * <returns>Path to the directory containing Bot Sequence files</returns>
     */
    private IList<BotSequence> ResolveSequenceFiles()
    {
#if UNITY_EDITOR
        const string sequencePath = "Assets/RegressionGames/Resources/BotSequences";
        if (!Directory.Exists(sequencePath))
        {
            return new List<BotSequence>();
        }

        return EnumerateSequencesInDirectory(sequencePath);
#else
        IList<BotSequence> sequences = null;

        // 1. check the persistentDataPath for sequences
        var persistentDataPath = Application.persistentDataPath + "/BotSequences";
        if (Directory.Exists(persistentDataPath))
        {
            sequences = EnumerateSequencesInDirectory(persistentDataPath);
        }
    
        // 2. load Sequences from Resources, while skipping any that have already
        //    been fetched from the persistentDataPath
        const string runtimePath = "BotSequences";
        var jsons = Resources.LoadAll(runtimePath, typeof(TextAsset));
        foreach (var jsonObject in jsons)
        {
            try
            {
                var json = (jsonObject as TextAsset)?.text;
                var sequence = JsonConvert.DeserializeObject<BotSequence>(json);
            
                // add the new sequence if it doesn't already exist
                if (sequences != null && sequences.All(s => s.name != sequence.name))
                {
                    sequences.Add(sequence);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading Sequence json file from resource path: {runtimePath}", e);
            }
        }

        return sequences;
#endif
    }
}
