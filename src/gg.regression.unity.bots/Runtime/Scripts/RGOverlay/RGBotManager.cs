using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames;
using TMPro;
using UnityEngine;

public class RGBotManager : MonoBehaviour
{
    private static RGBotManager _this;

    [Header("Prefabs")]
    public List<GameObject> botPrefabs;

    [Header("References")]
    public RGIconPulse launcherPulse;

    public RGIconPulse recordingPulse;

    public GameObject recordingToolbar;

    public GameObject selectionPanel;

    [SerializeField]
    private TMP_Dropdown gameObjectsDropdown;

    [SerializeField]
    private TMP_Dropdown behaviorsDropdown;

    [SerializeField]
    private GameObject activeBotRoot;

    [SerializeField]
    private GameObject rgBotEntry;

    public static RGBotManager GetInstance()
    {
        return _this;
    }

    private bool _cvRecording = false;
    private bool _closeOverlayOnBotStart = true;
    private GameObject _selectedBotPrefab;
    private string _selectedBehavior;
    private IRGBotEntry[] _botEntries;

    private void Awake()
    {
        if (_this != null && this.gameObject != _this.gameObject)
        {
            // we only want one of us around.. kill the other one
            Destroy(this.gameObject);
            return;
        }
        _this = this;
        DontDestroyOnLoad(_this.gameObject);

        var newOverlayFeature = RGSettings.GetOrCreateSettings().GetFeatureStateRecordingAndReplay();
        if (!newOverlayFeature)
        {
            if (recordingToolbar != null)
            {
                // disable the toolbar
                recordingToolbar.SetActive(false);
            }
        }
    }

    private void Start()
    {
        gameObjectsDropdown.ClearOptions();
        behaviorsDropdown.ClearOptions();

        AddPrefabsToDropdown();
        AddBehaviorsToDropdown();

        // Initialize with "Empty" option selected
        gameObjectsDropdown.value = 0;
        behaviorsDropdown.value = 0;
        _selectedBotPrefab = null;
        _selectedBehavior = null;
        if (_botEntries.Length > 0)
        {
            _selectedBehavior = _botEntries[0].botName;
        }

        // Subscribe to the dropdown's onValueChanged event
        gameObjectsDropdown.onValueChanged.AddListener(
        delegate { PrefabSelected(gameObjectsDropdown); }
        );
        behaviorsDropdown.onValueChanged.AddListener(
        delegate { BehaviorSelected(behaviorsDropdown); }
        );

        var bots = FindObjectsOfType<MonoBehaviour>().OfType<IRGBot>();
        foreach (var bot in bots)
        {
            Type botType = bot.GetType();
            string fullyQualifiedName = botType.AssemblyQualifiedName;
            string botName = FindBotName(fullyQualifiedName);
            AddActiveBot(((MonoBehaviour)bot).gameObject, botName);
        }
    }

    public void OnOverlayClick()
    {
        RGDebug.LogVerbose("Showing RG Overlay Menu");
        selectionPanel.SetActive(true);
    }

    public void OnOverlayClosed()
    {
        selectionPanel.SetActive(false);
    }

    public void AddBot()
    {
        if (_selectedBotPrefab == null)
        {
            RGBots.SpawnBot(_selectedBehavior);
        }
        else
        {
            RGBots.SpawnBot(_selectedBehavior, _selectedBotPrefab);
        }

        if (_closeOverlayOnBotStart)
        {
            OnOverlayClosed();
        }
    }

    public void StopAllBots()
    {
        var activeBots = GetComponentsInChildren<RGBotEntry>();
        for (int i = activeBots.Length - 1; i >= 0; i--)
        {
            activeBots[i].Delete();
        }
    }

    public void SetCloseOnBotStart()
    {
        _closeOverlayOnBotStart = !_closeOverlayOnBotStart;
    }

    public void SetCvRecording()
    {
        if (!_cvRecording)
        {
            // close this before we switch the flag
            OnOverlayClosed();
        }
        _cvRecording = !_cvRecording;
    }

    public void AddActiveBot(GameObject runtimeObject, string behavior)
    {
        var uiObject = GameObject.Instantiate(rgBotEntry, activeBotRoot.transform);
        uiObject.GetComponent<RGBotEntry>().Initialize(runtimeObject, behavior);
    }

    void AddPrefabsToDropdown()
    {
        List<string> dropdownOptions = new List<string>();
        dropdownOptions.Add("Empty");
        foreach (var prefab in botPrefabs)
        {
            dropdownOptions.Add(prefab.name);
        }

        gameObjectsDropdown.AddOptions(dropdownOptions);
    }

    void AddBehaviorsToDropdown()
    {
        List<string> dropdownOptions = new List<string>();

        var botList = Resources.Load<IRGBotList>("RGBotList");
        if (!botList)
        {
            RGDebug.LogWarning("Failed to load RGBotList from Resources");
            return;
        }

        _botEntries = botList.botEntries;
        foreach (var bot in _botEntries)
        {
            dropdownOptions.Add(bot.botName);
        }

        if (dropdownOptions.Count == 0)
        {
            dropdownOptions.Add("Empty");
        }
        behaviorsDropdown.AddOptions(dropdownOptions);
    }

    void PrefabSelected(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;

        // Check if "Empty" is selected
        if (index == 0)
        {
            _selectedBotPrefab = null;
        }
        else
        {
            _selectedBotPrefab = botPrefabs[index - 1];
        }
    }

    void BehaviorSelected(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;

        // Check if "Empty" is selected
        _selectedBehavior = null;
        if (_botEntries.Length > 0)
        {
            _selectedBehavior = _botEntries[index - 1].botName;
        }
    }

    string FindBotName(string qualifiedName)
    {
        var botList = Resources.Load<IRGBotList>("RGBotList");

        var botEntry = botList.botEntries.FirstOrDefault(b => b.qualifiedName == qualifiedName);
        if (botEntry != null)
        {
            return botEntry.botName;
        }
        return null;
    }
}
