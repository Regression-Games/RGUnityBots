using System;

using System.Collections.Concurrent;
using System.Collections.Generic;

using System.Linq;
using System.Threading;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateRecorder;
using RegressionGames.Types;
using TMPro;
using UnityEngine;

namespace RegressionGames
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    public class RGOverlayMenu : MonoBehaviour
    {

        private readonly string _sessionName = Guid.NewGuid().ToString();

        public RGIconPulse launcherPulse;

        public RGIconPulse recordingPulse;

        public GameObject recordingToolbar;

        private RGServiceManager _rgServiceManager;

        private bool? _lastState = null;

        public GameObject selectionPanel;

        public GameObject botListingRoot;

        public GameObject botEntryPrefab;

        public TMP_Dropdown nextBotDropdown;

        private static List<RGBotInstance> _activeBots = new();

        private int _lastCount = -1;

        private static RGOverlayMenu _this = null;

        private bool _closeOverlayOnBotStart = true;

        private bool _cvRecording = false;

        public static RGOverlayMenu GetInstance()
        {
            return _this;
        }

        public void Awake()
        {
            if (_this != null && this.gameObject != _this.gameObject)
            {
                // we only want one of us around.. kill the other one
                Destroy(this.gameObject);
                return;
            }

            _rgServiceManager = GetComponent<RGServiceManager>();
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

            UpdateBots();

        }

        public void Start()
        {
            selectionPanel.SetActive(false);
        }

        public void LateUpdate()
        {
            var state = false;
#if UNITY_EDITOR
            state = RGSettings.GetOrCreateSettings().GetEnableOverlay();
#endif
            if (_lastState != state)
            {
                if (state)
                {
                    this.transform.GetChild(0).gameObject.SetActive(true);
                }
                else
                {
                    this.transform.GetChild(0).gameObject.SetActive(false);
                }
                _lastState = state;
            }

            if (botListingRoot.transform.childCount != _activeBots.Count)
            {
                // update all bot listings
                while (botListingRoot.transform.childCount > 0)
                {
                    DestroyImmediate(botListingRoot.transform.GetChild(0).gameObject);
                }

                for (var i = 0; i < _activeBots.Count; i++)
                {
                    var botEntry = _activeBots[i];

                    var rt = botEntryPrefab.GetComponent<RectTransform>();
                    var position = new Vector3(0f, rt.rect.height * -i, 0f);

                    var newEntry = Instantiate(
                        original: botEntryPrefab,
                        parent: botListingRoot.transform
                        );

                    newEntry.transform.localPosition = position;

                    var uiElement = newEntry.GetComponent<ActiveRGBotUIElement>();
                    uiElement.PopulateBotEntry(botEntry);
                }
            }

            if (_activeBots.Count != _lastCount)
            {
                if (_activeBots.Count > 0)
                {
                    //set the overlay blinky thing to green
                    launcherPulse.SetColor((Color.green + Color.white) / 2);
                    launcherPulse.StopAtMidAlpha();
                }
                else
                {
                    //set the overlay blinky thing back to default
                    launcherPulse.SetColor();
                    launcherPulse.Normal();
                }
            }

            _lastCount = _activeBots.Count;
            if (_cvRecording)
            {
                recordingPulse.Fast();
                ScreenRecorder.GetInstance()?.StartRecording();
            }
            else
            {
                recordingPulse.Stop();
                ScreenRecorder.GetInstance()?.StopRecording();
            }

        }

        public void OnOverlayClick()
        {
            RGDebug.LogVerbose("Showing RG Overlay Menu");
            selectionPanel.SetActive(true);
            // before or after.. hard call. .we want the dialogue open
            // but we want the data correct too.. maybe block the overlay with a progress
            // indicator until loaded in the future ?
            UpdateBots();
        }

        public void OnOverlayClosed()
        {
            RGDebug.LogVerbose("Closing RG Overlay Menu");
            selectionPanel.SetActive(false);
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

        public void AddBot()
        {
            if (nextBotDropdown.options.Count > 0)
            {
                var value = nextBotDropdown.value;
                if (value >= 0)
                {
                    if (long.TryParse(nextBotDropdown.options[value].text.Split(':')[1].Trim(), out var botId))
                    {
                        // local vs remote
                        var localRemote = nextBotDropdown.options[value].text.Split('-')[0].Trim();
                        var isLocal = "Local" == localRemote;
                        if (!isLocal)
                        {
                            _ = _rgServiceManager.QueueInstantBot(
                                botId,
                                botInstance =>
                                {
                                    // don't update the bots as we'll update on re-open of overlay anyway
                                    //UpdateBots();
                                    // close the overlay so it doesn't hide components the bot needs to click
                                    if (_closeOverlayOnBotStart)
                                    {
                                        OnOverlayClosed();
                                    }
                                    RGBotServerListener.GetInstance()
                                        ?.AddClientConnectionForBotInstance(botInstance.id,
                                            RGClientConnectionType.REMOTE);
                                    if (!_closeOverlayOnBotStart)
                                    {
                                        UpdateBots();
                                    }
                                },
                                () => { RGDebug.LogWarning("WARNING: Failed to start new Remote bot"); });
                        }
                        else
                        {
                            RGBotRuntimeManager.GetInstance()?.StartBot(botId);
                            // close the overlay so it doesn't hide components the bot needs to click
                            if (_closeOverlayOnBotStart)
                            {
                                OnOverlayClosed();
                            }
                            else
                            {
                                UpdateBots();
                            }
                        }
                    }
                }
            }
        }

        public void StopBotInstance(long id)
        {
            RGBotServerListener.GetInstance()?.HandleClientTeardown(id);
        }

        /// <summary>
        /// Tear down all currently active bots
        /// </summary>
        public void StopAllBots()
        {
            RGBotServerListener.GetInstance()?.TeardownAllClients();
        }

        public void UpdateBots()
        {
            var botBag = new ConcurrentBag<RGBot>();
            var instances = new ConcurrentBag<RGBotInstance>();

            // update the latest bot list from Local Bots
            var localBotInstances = RGBotRuntimeManager.GetInstance()?.GetActiveBotInstances();
            if (localBotInstances != null)
            {
                foreach (var localBotInstance in localBotInstances)
                {
                    instances.Add(localBotInstance);
                }
            }

            var localBotDefinitions = RGBotAssetsManager.GetInstance()?.GetAvailableBots();
            if (localBotDefinitions != null)
            {
                foreach (var localBotDefinition in localBotDefinitions)
                {
                    botBag.Add(localBotDefinition);
                }
            }

            // update the latest bot list from RGService
            _ = _rgServiceManager.GetBotsForCurrentUser(
                bots =>
                {
                    var count = bots.Length;
                    if (bots.Length > 0)
                    {
                        foreach (var bot in bots)
                        {
                            if (bot is { IsUnityBot: true, IsLocal: false })
                            {
                                botBag.Add(bot);
                                _ = _rgServiceManager.GetRunningInstancesForBot(
                                    bot.id,
                                    botInstances =>
                                    {
                                        foreach (var bi in botInstances)
                                        {
                                            // may want to further narrow this down to only the bots we started at some point
                                            instances.Add(bi);
                                        }

                                        if (Interlocked.Decrement(ref count) <= 0)
                                        {
                                            ProcessBotUpdateList(instances);
                                            ProcessDropdownOptions(botBag);
                                        }
                                    },
                                    () =>
                                    {
                                        RGDebug.LogWarning(
                                            $"Failed to get running bot instances for bot id: [{bot.id}]");
                                        if (Interlocked.Decrement(ref count) <= 0)
                                        {
                                            ProcessBotUpdateList(instances);
                                            ProcessDropdownOptions(botBag);
                                        }
                                    }
                                );
                            }
                            else
                            {
                                if (Interlocked.Decrement(ref count) <= 0)
                                {
                                    ProcessBotUpdateList(instances);
                                    ProcessDropdownOptions(botBag);
                                }
                            }
                        }
                    }
                    else
                    {
                        ProcessBotUpdateList(instances);
                        ProcessDropdownOptions(botBag);
                    }
                },
                () =>
                {
                    ProcessBotUpdateList(instances);
                    ProcessDropdownOptions(botBag);
                });

        }

        private void ProcessDropdownOptions(ConcurrentBag<RGBot> botBag)
        {
            var botStrings = botBag.Distinct().Select(bot => bot.UIString).ToList();
            // sort alpha
            botStrings.Sort();

            List<TMP_Dropdown.OptionData> dropOptions = new();
            foreach (var optionString in botStrings)
            {
                dropOptions.Add(new TMP_Dropdown.OptionData(optionString));
            }
            nextBotDropdown.options = dropOptions;
        }

        private void ProcessBotUpdateList(ConcurrentBag<RGBotInstance> instances)
        {
            var botInstances = instances.Distinct().ToList();
            // sort by createdDate with the oldest at the end
            botInstances.Sort((a, b) => (int)(b.createdDate.ToUnixTimeMilliseconds() - a.createdDate.ToUnixTimeMilliseconds()));
            _activeBots = botInstances;
        }
    }
}
