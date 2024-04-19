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
        public GameObject stopButton;

        public GameObject warningIcon;
        public GameObject successIcon;

        public GameObject recordButton;
        public RGIconPulse recordingPulse;

        private bool _recording;

        private ReplayDataPlaybackController _replayDataController;

        private void OnEnable()
        {
            _replayDataController = GetComponent<ReplayDataPlaybackController>();
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
            stopButton.SetActive(false);
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

        private volatile bool _parsingZipFile = false;

        void OnFilesSelected(string[] filePaths)
        {
            // Get the file path of the first selected file
            var filePath = filePaths[0];

            _parsingZipFile = true;
            recordButton.SetActive(false);

            // do this on background thread
            Task.Run(() => ProcessDataContainerZipAndSetup(filePath));
        }

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
                _parsingZipFile = false;
            }
        }

        private volatile ReplayDataContainer _replayLoadedNextUpdate = null;

        private void Update()
        {
            if (_replayLoadedNextUpdate != null)
            {
                EndProcessContainer(_replayLoadedNextUpdate);
                _replayLoadedNextUpdate = null;
            }
        }

        private void EndProcessContainer(ReplayDataContainer dataContainer)
        {
            // setup the new replay data
            _replayDataController.SetDataContainer(dataContainer);

            SetDefaultButtonStates();
            // set button states
            playButton.SetActive(true);
            stopButton.SetActive(true);
            recordButton.SetActive(false);
        }

        public void PlayReplay()
        {
            chooseReplayButton.SetActive(false);
            playButton.SetActive(false);
            stopButton.SetActive(true);
            recordButton.SetActive(false);

            _replayDataController.Play();
        }

        public void StopReplay()
        {
            // stop and clear the old replay data
            _replayDataController.Stop();

            SetDefaultButtonStates();
        }

        public void ToggleRecording()
        {
            _recording = !_recording;
            if (!_recording)
            {
                SetDefaultButtonStates();
            }
            else
            {
                SetDefaultButtonStates();
                chooseReplayButton.SetActive(false);
                recordButton.SetActive(true);
            }
        }

        private string _lastKeyFrameError = null;

        private void LateUpdate()
        {
            if (_replayDataController.ReplayCompletedSuccessfully() != null)
            {
                _replayDataController.Stop();
                // playback complete
                SetDefaultButtonStates();
                successIcon.SetActive(true);
            }

            if (_replayDataController.WaitingForKeyFrameConditions != null && _replayDataController.KeyFrameInputComplete)
            {
                // check that we've started all the last inputs as we got those up to the next key frame time
                // if we haven't started all those yet, we shouldn't be super worried about not hitting the key frame .. yet
                if (_lastKeyFrameError != _replayDataController.WaitingForKeyFrameConditions)
                {
                    RGDebug.LogInfo(_replayDataController.WaitingForKeyFrameConditions);
                    _lastKeyFrameError = _replayDataController.WaitingForKeyFrameConditions;
                }

                //TODO: It might be nice if we got enough info here to draw the names and bounding boxes of the missing information
                warningIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText(_replayDataController.WaitingForKeyFrameConditions);
                warningIcon.SetActive(true);
            }
            else
            {
                _lastKeyFrameError = null;
                warningIcon.SetActive(false);
            }

            if (_recording)
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
    }
}
