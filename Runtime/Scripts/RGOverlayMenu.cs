using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.Types;
using TMPro;
using UnityEditor;
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
        
        private static List<RGBotInstance> activeBots = new List<RGBotInstance>();
        
        private ConcurrentBag<long> invalidBotIds = new ConcurrentBag<long>();

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

            if (botListingRoot.transform.childCount != activeBots.Count)
            {
                // update all bot listings
                while (botListingRoot.transform.childCount > 0)
                {
                    DestroyImmediate(botListingRoot.transform.GetChild(0).gameObject);
                }

                for (int i =0; i< activeBots.Count; i++)
                {
                    RGBotInstance botEntry = activeBots[i];
                    
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

            if (activeBots.Count != lastCount)
            {
                if (activeBots.Count > 0)
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

            lastCount = activeBots.Count;

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
                    if (long.TryParse(nextBotDropdown.options[value].text.Split('-')[0].Trim(), out botId))
                    {
                        _ = rgServiceManager.QueueInstantBot(
                            botId,
                            botInstance =>
                            {
                                UpdateBots();
                                // close the overlay so it doesn't hide components the bot needs to click
                                OnOverlayClosed();
                                RGBotServerListener.GetInstance()?.AddClientConnectionForBotInstance(botInstance.id, RGClientConnectionType.REMOTE);
                            },
                            () => { RGDebug.LogWarning("WARNING: Failed to start new instant bot"); });
                    }
                }
            }
        }

        public void TempTestAddLocalBot()
        {
            // TODO (REG-1291): Temporary code to get bot 0.. this whole method will go away in
            RGBot bot = AssetDatabase.LoadAssetAtPath<RGBot>("Assets/RegressionGames/Runtime/Bots/NewRGBot_0/BotRecord.asset");

            if (bot != null)
            {
                // start the local bot 0
                RGBotRuntimeManager.GetInstance()?.StartBot(bot.id);
            }
            else
            {
                RGDebug.LogError("Test Unity Local Bot does not exist.  Please create bot 0 with the menu before using this button.");
            }
        }

        public void StopBotInstance(long id)
        {
            RGBotServerListener.GetInstance()?.HandleClientTeardown((uint) id);
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
            // update the latest bot list
            _ = rgServiceManager.GetBotsForCurrentUser(
                bots =>
                {
                    int count = 0;
                    List<TMP_Dropdown.OptionData> dropOptions = new List<TMP_Dropdown.OptionData>();
                    
                    ConcurrentBag<RGBotInstance> instances = new ConcurrentBag<RGBotInstance>();
                    foreach (RGBot bot in bots)
                    {
                        if (bot.programmingLanguage.Equals("UNITY"))
                        {
                            dropOptions.Add(new TMP_Dropdown.OptionData($"{bot.id} - {bot.name}"));
                            _ = rgServiceManager.GetRunningInstancesForBot(
                                bot.id,
                                botInstances =>
                                {
                                    foreach (RGBotInstance bi in botInstances)
                                    {
                                        instances.Add(bi);
                                    }

                                    if (Interlocked.Increment(ref count) >= bots.Length)
                                    {
                                        // we hit the last async result.. do the update processing
                                        ProcessBotUpdateList(instances);
                                    }
                                },
                                () =>
                                {
                                    invalidBotIds.Add(bot.id);
                                    if (Interlocked.Increment(ref count) >= bots.Length)
                                    {
                                        // we hit the last async result.. do the update processing
                                        ProcessBotUpdateList(instances);
                                    }
                                }
                            );
                        } 
                        else if (Interlocked.Increment(ref count) >= bots.Length)
                        {
                            // we hit the last async result.. do the update processing
                            ProcessBotUpdateList(instances);
                        }
                    }
                    nextBotDropdown.options = dropOptions;
                },
                () => { });
        }

        private void ProcessBotUpdateList(ConcurrentBag<RGBotInstance> instances)
        {
            // Log if we have any invalid bot ids
            if (invalidBotIds.Count > 0)
            {
                string botIds = string.Join(", ", invalidBotIds);
                RGDebug.LogWarning($"Failed to get running bot instances for bots: [{botIds}]");
            }
            invalidBotIds.Clear();
            List<RGBotInstance> botInstances = instances.Distinct().ToList();
            // sort by id ascending, since the ids are DB ids, this should keep them in creation order
            botInstances.Sort((a, b) => (int)(b.id - a.id));
            activeBots = botInstances;
        }
    }
}