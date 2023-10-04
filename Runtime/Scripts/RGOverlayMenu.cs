using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class RGOverlayMenu : MonoBehaviour
    {
        
        public Image launcherIcon;
        public RGIconPulse launcherPulse;

        private RGServiceManager rgServiceManager;

        private bool? lastState = null;

        public GameObject selectionPanel;

        public GameObject botListingRoot;

        public GameObject botEntryPrefab;

        public TMP_Dropdown nextBotDropdown;
        
        private static List<RGBotInstance> _activeBots = new ();

        private int lastCount = -1;

        private static RGOverlayMenu _this = null;

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

            rgServiceManager = GetComponent<RGServiceManager>();
            _this = this;
            DontDestroyOnLoad(_this.gameObject);
            
            UpdateBots();

        }

        public void Start()
        {
            selectionPanel.SetActive(false);
        }

        public void LateUpdate()
        {
            bool state = false;
#if UNITY_EDITOR
            state = RGSettings.GetOrCreateSettings().GetEnableOverlay();
#endif
            if (lastState != state)
            {
                if (state)
                {
                    this.transform.GetChild(0).gameObject.SetActive(true);
                }
                else
                {
                    this.transform.GetChild(0).gameObject.SetActive(false);
                }

                lastState = state;
            }

            if (botListingRoot.transform.childCount != _activeBots.Count)
            {
                // update all bot listings
                while (botListingRoot.transform.childCount > 0)
                {
                    DestroyImmediate(botListingRoot.transform.GetChild(0).gameObject);
                }

                for (int i =0; i< _activeBots.Count; i++)
                {
                    RGBotInstance botEntry = _activeBots[i];
                    
                    RectTransform rt = botEntryPrefab.GetComponent<RectTransform>();
                    Vector3 position = new Vector3(0f, rt.rect.height * -i, 0f);
                    
                    GameObject newEntry = Instantiate(
                        original: botEntryPrefab,
                        parent: botListingRoot.transform
                        );

                    newEntry.transform.localPosition = position;
                    
                    ActiveRGBotUIElement uiElement = newEntry.GetComponent<ActiveRGBotUIElement>();
                    uiElement.PopulateBotEntry(botEntry);
                }
            }

            if (_activeBots.Count != lastCount)
            {
                if (_activeBots.Count > 0)
                {
                    //set the overlay blinky thing to green
                    launcherIcon.color = new Color(Color.green.r, Color.green.g, Color.green.b, launcherIcon.color.a);
                    launcherPulse.Fast();
                }
                else
                {
                    //set the overlay blinky thing to white
                    launcherIcon.color = new Color(Color.white.r, Color.white.g, Color.white.b, launcherIcon.color.a);
                    launcherPulse.Normal();
                }
            }

            lastCount = _activeBots.Count;

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

        public void AddBot()
        {
            if (nextBotDropdown.options.Count > 0)
            {
                int value = nextBotDropdown.value;
                if (value >= 0)
                {
                    long botId;
                    if (long.TryParse(nextBotDropdown.options[value].text.Split(':')[1].Trim(), out botId))
                    {
                        // local vs remote
                        var localRemote = nextBotDropdown.options[value].text.Split('-')[0].Trim();
                        var isLocal = "Local" == localRemote;
                        if (!isLocal)
                        {
                            _ = rgServiceManager.QueueInstantBot(
                                botId,
                                botInstance =>
                                {
                                    UpdateBots();
                                    // close the overlay so it doesn't hide components the bot needs to click
                                    OnOverlayClosed();
                                    RGBotServerListener.GetInstance()
                                        ?.AddClientConnectionForBotInstance(botInstance.id,
                                            RGClientConnectionType.REMOTE);
                                },
                                () => { RGDebug.LogWarning("WARNING: Failed to start new Remote bot"); });
                        }
                        else
                        {
                            RGBotRuntimeManager.GetInstance()?.StartBot(botId);
                            UpdateBots();
                            // close the overlay so it doesn't hide components the bot needs to click
                            OnOverlayClosed();
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
            ConcurrentBag<RGBot> botBag = new ConcurrentBag<RGBot>();
            ConcurrentBag<RGBotInstance> instances = new ConcurrentBag<RGBotInstance>();

            // update the latest bot list from Local Bots
            var localBotInstances = RGBotRuntimeManager.GetInstance()?.GetActiveBotInstances();
            foreach (var localBotInstance in localBotInstances)
            {
                instances.Add(localBotInstance);
            }
            
            var localBotDefinitions = RGBotRuntimeManager.GetInstance()?.GetAvailableBots();
            foreach (var localBotDefinition in localBotDefinitions)
            {
                botBag.Add(localBotDefinition);    
            }
            
            // update the latest bot list from RGService
            _ = rgServiceManager.GetBotsForCurrentUser(
                bots =>
                {
                    var count = bots.Length;
                    foreach (RGBot bot in bots)
                    {
                        //TODO (post REG-988): Handle REG-988 Changes
                        // want Unity bots that are NOT  `CSHARP`
                        if (bot != null && bot.programmingLanguage.Equals("UNITY"))
                        {
                            botBag.Add(bot);
                            _ = rgServiceManager.GetRunningInstancesForBot(
                                bot.id,
                                botInstances =>
                                {
                                    foreach (RGBotInstance bi in botInstances)
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
                                    RGDebug.LogWarning($"Failed to get running bot instances for bot id: [{bot.id}]");
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
                },
                () => { });
            
        }

        private void ProcessDropdownOptions(ConcurrentBag<RGBot> botBag)
        {
            List<RGBot> bots = botBag.Distinct().ToList();
            bots.Sort((a,b) => (int)(a.id-b.id));
            
            List<TMP_Dropdown.OptionData> dropOptions = new ();
            foreach (var bot in bots)
            {
                var localRemote = bot.id < 0 ? "Local" : "Remote";
                dropOptions.Add(new TMP_Dropdown.OptionData($"{localRemote} - {bot.name} : {bot.id}"));
            }
            
            
            nextBotDropdown.options = dropOptions; 
        }

        private void ProcessBotUpdateList(ConcurrentBag<RGBotInstance> instances)
        {
            List<RGBotInstance> botInstances = instances.Distinct().ToList();
            // sort by createdDate ascending
            botInstances.Sort((a, b) => (int)(b.createdDate.Ticks - a.createdDate.Ticks));
            _activeBots = botInstances;
        }
    }
}
