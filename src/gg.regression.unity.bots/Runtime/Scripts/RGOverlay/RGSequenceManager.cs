using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
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

    public GameObject deleteSequenceDialog;
    
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

    public void DeleteSequenceByPath(string path)
    {
        Debug.Log($"Deleting: {path}");
        BotSequence.DeleteSequence(path);
        LoadSequences();
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
     * Loads all Sequence json files within the Unity project, and creates their relevant UI components
     * </summary>
     */
    public void LoadSequences()
    {
        // ensure we aren't appending new sequences on to the previous ones
        ClearExistingSequences();

        var sequences = ResolveSequenceFiles();
        
        // instantiate a prefab for each sequence file that has been loaded
        foreach (var sequenceKVPair in sequences)
        {
            var path = sequenceKVPair.Key;
            var sequence = sequenceKVPair.Value;
            var instance = Instantiate(sequenceCardPrefab, Vector3.zero, Quaternion.identity);
            
            // map sequence data fields to new prefab
            instance.transform.SetParent(sequencesPanel.transform, false);
            var prefabComponent = instance.GetComponent<RGSequenceEntry>();
            if (prefabComponent != null)
            {
                prefabComponent.sequenceName = sequence.name;
                prefabComponent.sequencePath = path;
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

    /**
     * <summary>
     * Search a directory for any *.json files, then attempt to read them as Bot Sequences
     * </summary>
     * <param name="path">The directory to look for Bot Sequences json files</param>
     * <returns>Dictionary containing (file name, Bot Sequence) entires</returns>
     */
    private Dictionary<string, BotSequence> EnumerateSequencesInDirectory(string path)
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
                    return (string.Empty, null);
                }
            })
            .Where(s => s.Item2 != null)
            .ToDictionary(s => s.Item1, s => s.Item2);
    }
    
    /**
     * <summary>
     * Load Bot Sequences relative to if we're running in the Unity editor, or in a packaged build. For the packaged
     * build we will first check the persistent data path for any Sequence files, and then check Resources for any. We
     * will use the persistent data path as an override, so any Sequences that are found in Resources will only be kept
     * if they are not also within the persistent data path
     * </summary>
     * <returns>List of Bot Sequences</returns>
     */
    private IDictionary<string, BotSequence> ResolveSequenceFiles()
    {
#if UNITY_EDITOR
        const string sequencePath = "Assets/RegressionGames/Resources/BotSequences";
        if (!Directory.Exists(sequencePath))
        {
            return new Dictionary<string, BotSequence>();
        }
        
        return EnumerateSequencesInDirectory(sequencePath);
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
        const string runtimePath = "BotSequences";
        var jsons = Resources.LoadAll(runtimePath, typeof(TextAsset));
        foreach (var jsonObject in jsons)
        {
            try
            {
                var resourceFilename = jsonObject.name;
                var json = (jsonObject as TextAsset)?.text ?? "";
                
                var sequence = JsonConvert.DeserializeObject<BotSequence>(json);

                // don't add sequences with duplicate names
                if (sequences.Values.Any(s => s.name == sequence.name))
                {
                    continue;
                }
                
                // add the new sequence if its filename doesn't already exist
                if (!sequences.ContainsKey(resourceFilename))
                {
                    sequences.Add(resourceFilename, sequence);
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
