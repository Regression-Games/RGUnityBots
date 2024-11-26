using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using JetBrains.Annotations;
using RegressionGames;
using RegressionGames.StateRecorder;
using UnityEngine;
using UnityEngine.UI;
// ReSharper disable once RedundantUsingDirective - used in #if #else - do not remove
using RegressionGames.StateRecorder.BotSegments.Models;
// ReSharper disable once RedundantUsingDirective - used in #if #else - do not remove
using Newtonsoft.Json;
// ReSharper disable once RedundantUsingDirective - used in #if #else - do not remove
using System.Linq;

/**
 * <summary>
 * UI for managing operations performed on the user's set of Bot Sequences
 * </summary>
 */
public class RGSequenceManager : MonoBehaviour
{
    // a scrollable container populated with the project's Sequences
    public GameObject sequencesPanel;

    // a scrollable container populated with the project's Segments
    public GameObject segmentsPanel;

    public GameObject sequenceCardPrefab;

    public GameObject segmentCardPrefab;

    public GameObject sequenceEditor;

    public GameObject deleteSequenceDialog;
    
    public Button reloadButton;
    
    private static RGSequenceManager _this;

    private ReplayToolbarManager _replayToolbarManager;

    public static RGSequenceManager GetInstance()
    {
        if (_this == null)
        {
            _this = FindObjectOfType<RGSequenceManager>();
        }

        return _this;
    }

    /**
     * <summary>
     * Load and instantiate the Sequences list
     * </summary>
     */
    public void LoadSequences()
    {
        StartCoroutine(ResolveSequenceFiles(InstantiateSequences));
    }

    /**
     * <summary>
     * Load and instantiate the Segments list
     * </summary>
     */
    public void LoadSegments()
    {
        StartCoroutine(InstantiateSegments());
    }

    /**
     * Load and instantiate the prefabs relevant to the currently displayed tab
     * (used with the reload button)
     */
    public void LoadCurrentTab()
    {
        if (sequencesPanel.activeSelf)
        {
            LoadSequences();
        }
        else
        {
            LoadSegments();
        }
    }

    public void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void Start()
    {
        if (RGUtils.IsMobile())
        {
            SetMobileView();
        }
        
        _replayToolbarManager = FindObjectOfType<ReplayToolbarManager>();

        // load our assets and show the Sequences tab content
        LoadSequences();
        LoadSegments();
        SetSequencesTabActive();
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
     * When this component is viewed on a mobile device:
     * - Scale the entire canvas so that all components are larger
     * - Disable the reload Sequence/Segment button
     * - Hide the create Sequence button
     * </summary>
     */
    public void SetMobileView()
    {
        GetComponent<CanvasScaler>().referenceResolution = new Vector2(800, 600);
        reloadButton.gameObject.SetActive(false);
        
        // the create sequence button is always the first child in the sequences panel
        var createSequenceButton = sequencesPanel.transform.GetChild(0);
        createSequenceButton.gameObject.SetActive(false);
    }

    /**
     * <summary>
     * Show the Sequence Editor, and initialize its fields
     * </summary>
     * <param name="makingACopy">bool true if copying to a new file, or false if editing in place</param>
     * <param name="existingResourcePath">The resource path for an existing Sequence for editing</param>
     * <param name="existingFilePath">The file path for an existing Sequence for editing (optional)</param>
     * <param name="isOverride">If the Sequence being edited is a local file, overriding the packaged resource (optional)</param>
     */
    public void ShowEditSequenceDialog(bool makingACopy, string existingResourcePath, string existingFilePath = null, bool isOverride = false)
    {
        if (sequenceEditor != null)
        {
            sequenceEditor.SetActive(true);

            var script = sequenceEditor.GetComponent<RGSequenceEditor>();
            if (script != null)
            {
                script.Initialize(makingACopy, existingResourcePath, existingFilePath, isOverride);
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
     * Show the Sequences list and hide the Segments
     * </summary>
     */
    public void SetSequencesTabActive()
    {
        sequencesPanel.SetActive(true);
        segmentsPanel.SetActive(false);
    }

    /**
     * <summary>
     * Show the Segments list and hide the Sequences
     * </summary>
     */
    public void SetSegmentsTabActive()
    {
        sequencesPanel.SetActive(false);
        segmentsPanel.SetActive(true);
    }

    /**
     * <summary>
     * Instantiate a Sequences card prefab, add it to the Sequences list, and populate the prefab with
     * the required values
     * </summary>
     */
    private void InstantiateSequence(string resourcePath, (string, BotSequence) sequenceInfo)
    {
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
            prefabComponent.isOverride = sequenceInfo.Item2.isOverride;
            prefabComponent.playAction = () =>
            {
                _replayToolbarManager.selectedReplayFilePath = null;
                sequenceInfo.Item2.Play();
            };
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

            var latestRecording = sequences.FirstOrDefault(a => a.Key!= null && a.Key.EndsWith("/" + ScreenRecorder.RecordingPathName));

            // make the latest recording the first child
            if (latestRecording.Key != null)
            {
                var resourcePath = latestRecording.Key;
                var sequenceInfo = latestRecording.Value;
                InstantiateSequence(resourcePath, sequenceInfo);
            }

            // instantiate a prefab for each sequence file that has been loaded
            foreach (var sequenceKVPair in sequences)
            {
                var resourcePath = sequenceKVPair.Key;
                if (!resourcePath.EndsWith("/" + ScreenRecorder.RecordingPathName))
                {
                    var sequenceInfo = sequenceKVPair.Value;
                    InstantiateSequence(resourcePath, sequenceInfo);
                }
            }
        }
    }

    /**
     * <summary>
     * Load Segment files from disk and instantiate them as Segment card prefabs, and ensure that the required fields
     * are set
     * </summary>
     */
    private IEnumerator InstantiateSegments()
    {
        ClearExistingSegments();

        yield return null;

        var segments = BotSegment.LoadAllSegments();
        foreach (var segmentKV in segments)
        {
            var resourcePath = segmentKV.Key;
            var filePath = segmentKV.Value.Item1;
            var segment = segmentKV.Value.Item2;

            var instance = Instantiate(segmentCardPrefab, Vector3.zero, Quaternion.identity);

            // map segment data fields to new prefab
            instance.transform.SetParent(segmentsPanel.transform, false);
            var prefabComponent = instance.GetComponent<RGSegmentEntry>();
            if (prefabComponent != null)
            {
                prefabComponent.segmentName = segment.name;
                prefabComponent.resourcePath = resourcePath;
                prefabComponent.description = segment.description;
                prefabComponent.filePath = filePath;
                prefabComponent.resourcePath = resourcePath;
                prefabComponent.type = segment.type;
                prefabComponent.isOverride = segment.isOverride;
            }

            yield return null;
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
     * <summary>Destroy all Segment cards in the UI</summary>
     */
    private void ClearExistingSegments()
    {
        if (segmentsPanel != null)
        {
            for (var i = 0; i < segmentsPanel.transform.childCount; i++)
            {
                Destroy(segmentsPanel.transform.GetChild(i).gameObject);
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
     * <param name="sequenceHandlerAction"> Action to process the resolved sequences </param>
     * <returns>Dictionary of {key=resourcePath, value=(filePath[null if resource],Bot Sequence) tuple}</returns>
     */
    [CanBeNull]
    public static IEnumerator ResolveSequenceFiles(Action<IDictionary<string,(string,BotSequence)>> sequenceHandlerAction)
    {
        yield return null;
#if UNITY_EDITOR
        const string sequencePath = "Assets/RegressionGames/Resources/BotSequences";

        if (Directory.Exists(sequencePath))
        {
            sequenceHandlerAction(EnumerateSequencesInDirectory(sequencePath));
        }
        else
        {
            sequenceHandlerAction(new Dictionary<string, (string, BotSequence)>());
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
                    sequence.resourcePath = resourceFilename;
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
        sequenceHandlerAction(sequences);
#endif
    }

    /**
     * <summary>
     * Search a directory for any *.json files, then attempt to read them as Bot Sequences
     * </summary>
     * <param name="path">The directory to look for Bot Sequences json files</param>
     * <returns>Dictionary containing {key = resourcePath, value = (filePath[if writeable], Bot Sequence} entries</returns>
     */
    private static Dictionary<string, (string, BotSequence)> EnumerateSequencesInDirectory(string path)
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
                RGDebug.LogWarning($"Error reading Bot Sequence {fileName}: {exception}");
            }
        }

        return result;
    }
}
