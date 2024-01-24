using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.Types;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames
{
    [HelpURL("https://docs.regression.gg/studios/unity/unity-sdk/overview")]
    public class RGOverlayMenu : MonoBehaviour
    {

        private readonly string _sessionName = Guid.NewGuid().ToString();
        
        public Image launcherIcon;
        public RGIconPulse launcherPulse;

        private RGServiceManager rgServiceManager;

        private bool? lastState = null;

        public GameObject selectionPanel;

        public GameObject botListingRoot;

        public GameObject botEntryPrefab;

        public TMP_Dropdown nextBotDropdown;

        private static List<RGBotInstance> _activeBots = new();

        private int lastCount = -1;

        private static RGOverlayMenu _this = null;

        private bool _closeOverlayOnBotStart = true;

        private bool _cvRecording = false;

        [Tooltip("FPS at which to analyze state using CV")]
        public int CVRecordingFPS = 30;

        private float _lastCVFrameTime = -1f;

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
            CleanupImageDirectory();
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

                for (int i = 0; i < _activeBots.Count; i++)
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
            if (_cvRecording)
            {
                StartCoroutine(RecordFrame());
            }

        }
        
        IEnumerator RecordFrame()
        {
            yield return new WaitForEndOfFrame();
            // handle recording
            var time = Time.unscaledTime;
            if ((int)(1000 * (time - _lastCVFrameTime)) >= (int)(1000.0f / CVRecordingFPS))
            {
                // estimating the time in int milliseconds .. won't exactly match FPS.. but will be close
                _lastCVFrameTime = time;

                // write out the image
                string path = GetImageDirectory($"{_tickNumber}".PadLeft(9,'0')+".jpg");
                    
                RGDebug.LogVerbose($"Capturing screenshot for CV evaluation: {path}");
                var texture = ScreenCapture.CaptureScreenshotAsTexture(1);
                try
                {
                    // Encode the texture into a jpg byte array
                    byte[] bytes = texture.EncodeToJPG(100);
                    // Save the byte array as a file
                    File.WriteAllBytesAsync(path, bytes);
                    
                    byte[] pngBytes = texture.EncodeToPNG();
                    var mat = Mat.FromImageData(pngBytes, ImreadModes.Color);


                    var backSub =  BackgroundSubtractorMOG2.Create();
                    var fgMask = new Mat();
                    backSub.Apply(mat, fgMask);
                    string pathMask = GetImageDirectory($"{_tickNumber}".PadLeft(9,'0')+".mask.jpg");
                    Cv2.ImWrite(pathMask, fgMask);
                    
                }
                finally
                {
                    ++_tickNumber;
                    // Destroy the texture to free up memory
                    Object.Destroy(texture);
                }
            }
        }


        private void OnDestroy()
        {
            string path = GetImageDirectory("DONE.txt");
            File.Create(path);
        }

        private long _tickNumber = 0;
        
        private string GetImageDirectory(string path = "")
        {
            var fullPath = Path.Combine(Application.persistentDataPath, "RGData", "cvImages", path);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            return fullPath;
        }

        private void CleanupImageDirectory()
        {
            var fullPath = Path.Combine(Application.persistentDataPath, "RGData", "cvImages");
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
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

        public void SetCVRecording()
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
                int value = nextBotDropdown.value;
                if (value >= 0)
                {
                    if (long.TryParse(nextBotDropdown.options[value].text.Split(':')[1].Trim(), out var botId))
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
            ConcurrentBag<RGBot> botBag = new ConcurrentBag<RGBot>();
            ConcurrentBag<RGBotInstance> instances = new ConcurrentBag<RGBotInstance>();

            // update the latest bot list from Local Bots
            var localBotInstances = RGBotRuntimeManager.GetInstance()?.GetActiveBotInstances();
            foreach (var localBotInstance in localBotInstances)
            {
                instances.Add(localBotInstance);
            }

            var localBotDefinitions = RGBotAssetsManager.GetInstance()?.GetAvailableBots();
            foreach (var localBotDefinition in localBotDefinitions)
            {
                botBag.Add(localBotDefinition);
            }

            // update the latest bot list from RGService
            _ = rgServiceManager.GetBotsForCurrentUser(
                bots =>
                {
                    var count = bots.Length;
                    if (bots.Length > 0)
                    {
                        foreach (RGBot bot in bots)
                        {
                            if (bot is { IsUnityBot: true, IsLocal: false })
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
            List<string> botStrings = botBag.Distinct().Select(bot => bot.UIString).ToList();
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
            List<RGBotInstance> botInstances = instances.Distinct().ToList();
            // sort by createdDate with the oldest at the end
            botInstances.Sort((a, b) => (int)(b.createdDate.ToUnixTimeMilliseconds() - a.createdDate.ToUnixTimeMilliseconds()));
            _activeBots = botInstances;
        }
    }
}
