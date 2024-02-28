using System;
using System.Collections;
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
            //File choose and load the replay
            StartCoroutine( ShowFileLoadDialog() );
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

        void OnFilesSelected( string[] filePaths )
        {

            // Get the file path of the first selected file
            var filePath = filePaths[0];

            try
            {
                // setup the new replay data
                _replayDataController.SetDataContainer(new ReplayDataContainer(filePath));

                SetDefaultButtonStates();
                // set button states
                playButton.SetActive(true);
                stopButton.SetActive(true);
                recordButton.SetActive(false);
            }
            catch (Exception e)
            {
                RGDebug.LogException(e);
            }
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
                chooseReplayButton.SetActive(false);
                playButton.SetActive(false);
                stopButton.SetActive(false);
                recordButton.SetActive(true);
            }
        }

        private void LateUpdate()
        {
            if (_replayDataController.ReplayCompletedSuccessfully() != null)
            {
                _replayDataController.Stop();
                // playback complete
                SetDefaultButtonStates();
                successIcon.SetActive(true);
            }

            if (_replayDataController.WaitingForKeyFrameConditions != null)
            {
                warningIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText(_replayDataController.WaitingForKeyFrameConditions);
                // don't show a warning when we're just waiting for timing
                if (_replayDataController.WaitingForKeyFrameConditions.StartsWith("Time until next"))
                {
                    warningIcon.SetActive(false);
                }
                else
                {
                    warningIcon.SetActive(true);
                }
            }
            else
            {
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
