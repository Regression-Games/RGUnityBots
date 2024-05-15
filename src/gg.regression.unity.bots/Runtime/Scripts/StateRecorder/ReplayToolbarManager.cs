using System;
using System.Collections;
using System.Threading.Tasks;
using RegressionGames;
using SimpleFileBrowser;
using RegressionGames.StateRecorder;
using TMPro;
using UnityEngine;

namespace Unity.Multiplayer.Samples.BossRoom
{
    public class ReplayToolbarManager : MonoBehaviour
    {
        public GameObject chooseReplayButton;

        public GameObject playButton;

        public GameObject loopButton;
        public TextMeshProUGUI loopCount;

        public GameObject stopButton;

        public GameObject warningIcon;
        public GameObject successIcon;

        public GameObject recordButton;
        public RGIconPulse recordingPulse;

        public RGTextPulse uploadingIndicator;
        public RGTextPulse fileOpenIndicator;

        public ReplayDataPlaybackController replayDataController;

        private bool _recording;

        private static ReplayToolbarManager _this;

        public static ReplayToolbarManager GetInstance()
        {
            return _this;
        }

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
        }

        // Start is called before the first frame update
        void Start()
        {
            SetDefaultButtonStates();
        }

        private void SetDefaultButtonStates()
        {
            chooseReplayButton.SetActive(true);
            recordButton.SetActive(true);

            warningIcon.SetActive(false);
            successIcon.SetActive(false);

            playButton.SetActive(false);
            loopButton.SetActive(false);
            stopButton.SetActive(false);
            loopCount.gameObject.SetActive(false);
        }

        public void ChooseReplay()
        {
            if (!_parsingZipFile)
            {
                //File choose and load the replay
                StartCoroutine(ShowFileLoadDialog());
            }
        }

        private IEnumerator ShowFileLoadDialog()
        {
            // Show a load file dialog and wait for a response from user
            // Load file/folder: file, Allow multiple selection: true
            // Initial path: default (Documents), Initial filename: empty
            // Title: "Load File", Submit button text: "Load"
            FileBrowser.SetFilters(false, new FileBrowser.Filter("Zip Files", ".zip"));
            yield return FileBrowser.WaitForLoadDialog(
                FileBrowser.PickMode.Files,
                false,
                null,
                null,
                "Select Replay Zip File",
                "Load Replay"
            );

            if (FileBrowser.Success)
            {
                OnFilesSelected(FileBrowser.Result); // FileBrowser.Result is null, if FileBrowser.Success is false
            }
        }

        private volatile bool _parsingZipFile;

        void OnFilesSelected(string[] filePaths)
        {
            // Get the file path of the first selected file
            var filePath = filePaths[0];

            _parsingZipFile = true;
            recordButton.SetActive(false);
            chooseReplayButton.SetActive(false);
            fileOpenIndicator.Normal();

            // do this on background thread
            Task.Run(() => ProcessDataContainerZipAndSetup(filePath));
        }

        private bool _justLoaded;

        private void ProcessDataContainerZipAndSetup(String filePath)
        {
            try
            {
                // do this on background thread
                var dataContainer = new ReplayDataContainer(filePath);
                _replayLoadedNextUpdate = dataContainer;
            }
            catch (Exception e)
            {
                RGDebug.LogException(e);
            }
            finally
            {
                fileOpenIndicator.Stop();
                _parsingZipFile = false;
                _justLoaded = true;
            }
        }

        private volatile ReplayDataContainer _replayLoadedNextUpdate;

        private void Update()
        {
            if (_justLoaded)
            {
                _justLoaded = false;
                if (_replayLoadedNextUpdate != null)
                {
                    // setup the new replay data
                    replayDataController.SetDataContainer(_replayLoadedNextUpdate);
                    _replayLoadedNextUpdate = null;
                    SetDefaultButtonStates();
                    // set button states
                    chooseReplayButton.SetActive(false);
                    playButton.SetActive(true);
                    loopButton.SetActive(true);
                    stopButton.SetActive(true);
                    recordButton.SetActive(false);
                }
                else
                {
                    // failed to load
                    SetDefaultButtonStates();
                }

            }
        }

        public void PlayReplay()
        {
            chooseReplayButton.SetActive(false);
            successIcon.SetActive(false);
            playButton.SetActive(false);
            loopButton.SetActive(false);
            stopButton.SetActive(true);
            recordButton.SetActive(false);

            replayDataController.Play();
        }

        public void LoopReplay()
        {
            chooseReplayButton.SetActive(false);
            successIcon.SetActive(false);
            playButton.SetActive(false);
            loopButton.SetActive(false);
            stopButton.SetActive(true);
            recordButton.SetActive(false);

            loopCount.text = "0";
            loopCount.gameObject.SetActive(true);

            replayDataController.Loop((newCount) =>
            {
                loopCount.text = "" + newCount;
            });
        }

        public void StopReplay()
        {
            SetDefaultButtonStates();
            // stop and clear the old replay data
            replayDataController.Stop();
        }

        public void ToggleRecording()
        {
            _recording = !_recording;
            if (!_recording)
            {
                recordingPulse.Stop();
                ScreenRecorder.GetInstance()?.StopRecording();
                SetDefaultButtonStates();
            }
            else
            {
                SetDefaultButtonStates();
                chooseReplayButton.SetActive(false);
                recordButton.SetActive(true);
                recordingPulse.Normal();
                ScreenRecorder.GetInstance()?.StartRecording(null);
            }
        }

        private string _lastKeyFrameError;

        private void LateUpdate()
        {
            if (replayDataController.ReplayCompletedSuccessfully() != null)
            {
                if (!replayDataController.IsPlaying())
                {
                    replayDataController.Reset();
                    // playback complete
                    chooseReplayButton.SetActive(false);
                    successIcon.SetActive(true);
                    playButton.SetActive(true);
                    loopButton.SetActive(true);
                    stopButton.SetActive(true);
                    recordButton.SetActive(false);
                }
            }

            if (replayDataController.WaitingForKeyFrameConditions != null && replayDataController.KeyFrameInputComplete)
            {
                // check that we've started all the last inputs as we got those up to the next key frame time
                // if we haven't started all those yet, we shouldn't be super worried about not hitting the key frame .. yet
                if (_lastKeyFrameError != replayDataController.WaitingForKeyFrameConditions)
                {
                    RGDebug.LogInfo(replayDataController.WaitingForKeyFrameConditions);
                    _lastKeyFrameError = replayDataController.WaitingForKeyFrameConditions;
                }

                //TODO: It might be nice if we got enough info here to draw the names and bounding boxes of the missing information
                warningIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText(replayDataController.WaitingForKeyFrameConditions);
                warningIcon.SetActive(true);
            }
            else
            {
                _lastKeyFrameError = null;
                warningIcon.SetActive(false);
            }
        }


        public void ShowUploadingIndicator(bool show)
        {
            if (!show)
            {
                uploadingIndicator.Stop();
            }
            else
            {
                uploadingIndicator.Normal();
            }
        }

    }
}
