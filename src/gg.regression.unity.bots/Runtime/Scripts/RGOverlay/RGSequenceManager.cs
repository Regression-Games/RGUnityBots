using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using RegressionGames;
using UnityEngine;
using RegressionGames.StateRecorder.BotSegments.Models;

/**
 * <summary>
 * UI for managing operations performed on the user's set of Bot Sequences
 * </summary>
 */
public class RGSequenceManager : MonoBehaviour
{
    public GameObject sequencesPanel;

    public GameObject sequenceCardPrefab;

    public GameObject sequenceEditor;

    private static RGSequenceManager _this;

    public static RGSequenceManager GetInstance()
    {
        return _this;
    }

    public void LoadSequences()
    {
        var sequences = ResolveSequenceFiles();
        InstantiateSequences(sequences);
    }

    public void Start()
    {
        LoadSequences();

        _this = this;
        DontDestroyOnLoad(_this.gameObject);
    }

    /**
     * <summary>
     * Saves the Sequence currently loaded in the Sequence Editor, hides the Sequence Editor, and reloads the list
     * of Sequences
     * </summary>
     */
    public void SaveSequenceLoadedInEditor()
    {
        if (sequenceEditor != null)
        {

            var script = sequenceEditor.GetComponent<RGSequenceEditor>();
            if (script != null)
            {
                script.SaveSequence();
            }

            sequenceEditor.SetActive(false);

            var sequences = ResolveSequenceFiles();
            InstantiateSequences(sequences);
        }
    }

    /**
     * <summary>
     * Show the Sequence Editor, and initialize its fields
     * </summary>
     */
    public void ShowEditSequenceDialog()
    {
        if (sequenceEditor != null)
        {
            sequenceEditor.SetActive(true);

            var script = sequenceEditor.GetComponent<RGSequenceEditor>();
            if (script != null)
            {
                script.Initialize();
            }
        }
    }

    /**
     * <summary>
     * Hide the Sequence Editor
     * </summary>
     */
    public void HideEditSequenceDialog()
    {
        if (sequenceEditor != null)
        {
            sequenceEditor.SetActive(false);
        }
    }

    /**
     * <summary>
     * Instantiates Sequence prefabs and adds them to the list of available Sequences
     * </summary>
     * <param name="sequences">List of (filePath[null if not writeable],Bot Sequence) tuples we want to instantiate as prefabs</param>
     */
    public void InstantiateSequences(IList<(string,BotSequence)> sequences)
    {
        // ensure we aren't appending new sequences on to the previous ones
        ClearExistingSequences();

        // instantiate a prefab for each sequence file that has been loaded
        foreach (var sequence in sequences)
        {
            var instance = Instantiate(sequenceCardPrefab, Vector3.zero, Quaternion.identity);

            // map sequence data fields to new prefab
            instance.transform.SetParent(sequencesPanel.transform, false);
            var prefabComponent = instance.GetComponent<RGSequenceEntry>();
            if (prefabComponent != null)
            {
                prefabComponent.sequenceName = sequence.Item2.name;
                prefabComponent.description = sequence.Item2.description;
                prefabComponent.playAction = sequence.Item2.Play;
            }
        }
    }

    /**
     * <summary>Destroy all Sequence cards in the UI</summary>
     */
    private void ClearExistingSequences()
    {
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
     * <summary>
     * Load Bot Sequences relative to if we're running in the Unity editor, or in a packaged build. For the packaged
     * build we will first check the persistent data path for any Sequence files, and then check Resources for any. We
     * will use the persistent data path as an override, so any Sequences that are found in Resources will only be kept
     * if they are not also within the persistent data path
     * </summary>
     * <returns>List of (filePath[null if not writeable],Bot Sequence) tuples</returns>
     */
    private IList<(string, BotSequence)> ResolveSequenceFiles()
    {
#if UNITY_EDITOR
        const string sequencePath = "Assets/RegressionGames/Resources/BotSequences";
        if (!Directory.Exists(sequencePath))
        {
            return new List<(string,BotSequence)>();
        }

        return EnumerateSequencesInDirectory(sequencePath).Values.ToList();
#else
        var sequences = new Dictionary<string, BotSequence>();

        // 1. check the persistentDataPath for sequences
        var persistentDataPath = Application.persistentDataPath + "/BotSequences";
        if (Directory.Exists(persistentDataPath))
        {
            sequences = EnumerateSequencesInDirectory(persistentDataPath);
        }

        // 2. load Sequences from Resources, while skipping any that have already been fetched from the
        //    persistentDataPath. We will compare Sequences by their filename (without extension), and by the actual
        //    Sequence name
        var rgBotSequencesAsset = Resources.Load<IRGBotSequences>("RGBotSequences");
        foreach (var resourceFilename in rgBotSequencesAsset.sequences)
        {
            try
            {
                if (!sequences.ContainsKey(resourceFilename))
                {
                    var sequenceInfo = Resources.Load<TextAsset>(resourceFilename);
                    var sequence = JsonConvert.DeserializeObject<BotSequence>(sequenceInfo.text ?? "");

                    // don't add sequences with duplicate names
                    if (sequences.Values.Any(s => s.name == sequence.name))
                    {
                        continue;
                    }

                    // add the new sequence if its filename doesn't already exist
                    sequences.Add(resourceFilename, sequence);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading Sequence json file from resource path: {runtimePath}", e);
            }
        }

        return sequences.Values.ToList();
#endif
    }

    /**
     * <summary>
     * Search a directory for any *.json files, then attempt to read them as Bot Sequences
     * </summary>
     * <param name="path">The directory to look for Bot Sequences json files</param>
     * <returns>Dictionary containing {key = resourcePath, value = (filePath[if writeable], Bot Sequence} entries</returns>
     */
    private Dictionary<string, (string, BotSequence)> EnumerateSequencesInDirectory(string path)
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
                    Debug.Log($"Error reading Bot Sequence {fileName}: {exception}");
                    return (null,null,null);
                }
            })
            .Where(s => s.Item2 != null)
            .ToDictionary(s => s.Item2, s => (s.Item1, s.Item3));
    }
}
