using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using RegressionGames;
using RegressionGames.StateRecorder;
using UnityEngine;
using RegressionGames.StateRecorder.BotSegments.Models;

// ReSharper disable once RedundantUsingDirective - used in #if
using Newtonsoft.Json;
// ReSharper disable once RedundantUsingDirective - used in #if
using StateRecorder.BotSegments.Models;

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

    public GameObject deleteSequenceDialog;

    private static RGSequenceManager _this;

    private ReplayToolbarManager _replayToolbarManager;

    public static RGSequenceManager GetInstance()
    {
        return _this;
    }

    /**
     * <summary>
     * Load and instantiate the Sequences list
     * </summary>
     */
    public void LoadSequences()
    {
        StartCoroutine(ResolveSequenceFiles());
    }

    public void Start()
    {
        LoadSequences();

        _replayToolbarManager = FindObjectOfType<ReplayToolbarManager>();
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

            LoadSequences();
        }
    }

    /**
     * <summary>
     * Deletes the Sequence provided by the path param, then reload all Sequences
     * </summary>
     * <param name="path">The path of the Sequence to delete</param>
     */
    public void DeleteSequenceByPath(string path)
    {
        BotSequence.DeleteSequenceAtPath(path);

        LoadSequences();
    }

    /**
     * <summary>
     * Show the Sequence Editor, and initialize its fields
     * </summary>
     * <param name="makingACopy">bool true if copying to a new file, or false if editing in place</param>
     * <param name="existingResourcePath">The resource path for an existing Sequence for editing</param>
     * <param name="existingFilePath">The file path for an existing Sequence for editing (optional)</param>
     */
    public void ShowEditSequenceDialog(bool makingACopy, string existingResourcePath, string existingFilePath = null)
    {
        if (sequenceEditor != null)
        {
            sequenceEditor.SetActive(true);

            var script = sequenceEditor.GetComponent<RGSequenceEditor>();
            if (script != null)
            {
                script.Initialize(makingACopy, existingResourcePath, existingFilePath);
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
     * Show the Delete Sequence confirmation dialog and set its fields
     * </summary>
     */
    public void ShowDeleteSequenceDialog(RGSequenceEntry sequence)
    {
        if (deleteSequenceDialog != null)
        {
            var script = deleteSequenceDialog.GetComponent<RGDeleteSequence>();
            if (script != null)
            {
                script.Initialize(sequence);
                deleteSequenceDialog.SetActive(true);
            }
        }
    }

    /**
     * <summary>
     * Hide the Delete Sequence confirmation dialog
     * </summary>
     */
    public void HideDeleteSequenceDialog()
    {
        if (deleteSequenceDialog != null)
        {
            deleteSequenceDialog.SetActive(false);
        }
    }

    /**
     * <summary>
     * Instantiates Sequence prefabs and adds them to the list of available Sequences
     * </summary>
     * <param name="sequences">Dictionary of {key=resourcePath, value=(filePath[null if not writeable],Bot Sequence) tuples} we want to instantiate as prefabs</param>
     */

    public void InstantiateSequences(IDictionary<string, (string,BotSequence)> sequences)

    {
        if (sequencesPanel != null && sequenceCardPrefab != null)
        {
            // ensure we aren't appending new sequences on to the previous ones
            ClearExistingSequences();

            // instantiate a prefab for each sequence file that has been loaded
            foreach (var sequenceKVPair in sequences)
            {
                var resourcePath = sequenceKVPair.Key;
                var sequenceInfo = sequenceKVPair.Value;
                var instance = Instantiate(sequenceCardPrefab, Vector3.zero, Quaternion.identity);

                // map sequence data fields to new prefab
                instance.transform.SetParent(sequencesPanel.transform, false);
                var prefabComponent = instance.GetComponent<RGSequenceEntry>();
                if (prefabComponent != null)
                {
                    prefabComponent.sequenceName = sequenceInfo.Item2.name;
                    prefabComponent.filePath = sequenceInfo.Item1;
                    prefabComponent.resourcePath = resourcePath;
                    prefabComponent.description = sequenceInfo.Item2.description;
                    prefabComponent.playAction = () =>
                    {
                        _replayToolbarManager.selectedReplayFilePath = null;
                        sequenceInfo.Item2.Play();
                    };

                }
            }
        }
    }

    /**
     * <summary>Destroy all Sequence cards in the UI</summary>
     */
    private void ClearExistingSequences()
    {
        if (sequencesPanel != null)
        {
            for (int i = 0; i < sequencesPanel.transform.childCount; i++)
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

    /**
     * <summary>
     * Load Bot Sequences relative to if we're running in the Unity editor, or in a packaged build. For the packaged
     * build we will first check the persistent data path for any Sequence files, and then check Resources for any. We
     * will use the persistent data path as an override, so any Sequences that are found in Resources will only be kept
     * if they are not also within the persistent data path
     * </summary>
     * <returns>Dictionary of {key=resourcePath, value=(filePath[null if resource],Bot Sequence) tuple}</returns>
     */
    [CanBeNull]
    private IEnumerator ResolveSequenceFiles()
    {
        yield return null;
#if UNITY_EDITOR
        const string sequencePath = "Assets/RegressionGames/Resources/BotSequences";

        if (Directory.Exists(sequencePath))
        {
            InstantiateSequences(EnumerateSequencesInDirectory(sequencePath));
        }
        else
        {
            InstantiateSequences(new Dictionary<string, (string, BotSequence)>());
        }
#else
        var sequences = new Dictionary<string, (string,BotSequence)>();

        // 1. check the persistentDataPath for sequences
        var persistentDataPath = Application.persistentDataPath + "/RegressionGames/Resources/BotSequences";
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
            yield return null;
            try
            {
                if (!sequences.ContainsKey(resourceFilename))
                {
                    var sequenceInfo = Resources.Load<TextAsset>(resourceFilename);
                    var sequence = JsonConvert.DeserializeObject<BotSequence>(sequenceInfo.text ?? "", JsonUtils.JsonSerializerSettings);

                    // don't add sequences with duplicate names
                    if (sequences.Values.Any(s => s.Item2.name == sequence.name))
                    {
                        continue;
                    }

                    // add the new sequence if its filename doesn't already exist
                    sequences.Add(resourceFilename, (null,sequence));
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Exception reading Sequence json file from resource path: {resourceFilename}", e);
            }

        }

        yield return null;
        InstantiateSequences(sequences);
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
        var sequenceFiles = Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories);
        Dictionary<string, (string, BotSequence)> result = new();
        foreach (var fileName in sequenceFiles)
        {
            try
            {
                var sequenceJsonInfo = BotSequence.LoadSequenceJsonFromPath(fileName);
                if (sequenceJsonInfo.Item2 != null)
                {
                    result.Add(sequenceJsonInfo.Item2, (sequenceJsonInfo.Item1, sequenceJsonInfo.Item3));
                }
            }
            catch (Exception exception)
            {
                Debug.Log($"Error reading Bot Sequence {fileName}: {exception}");
            }
        }

        return result;
    }
}
