using System;
using System.Collections;
using System.Threading.Tasks;
using RegressionGames.StateRecorder.BotSegments;
using SimpleFileBrowser;
using TMPro;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public class ReplayToolbarManager : MonoBehaviour
    {
        public GameObject chooseReplayButton;

        public GameObject playButton;

        public GameObject pauseButton;

        public GameObject loopButton;
        public TextMeshProUGUI loopCount;

        public GameObject stopButton;

        public GameObject warningIcon;
        public GameObject successIcon;

        public GameObject recordButton;
        public RGIconPulse recordingPulse;

        public RGTextPulse uploadingIndicator;
        public RGTextPulse fileOpenIndicator;

        public string selectedReplayFilePath;
        public BotSegmentsPlaybackController replayDataController;

        private bool _recording;

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
            pauseButton.SetActive(false);
            loopButton.SetActive(false);
            stopButton.SetActive(false);
            loopCount.gameObject.SetActive(false);
        }

        public void SetInUseButtonStates()
        {
            chooseReplayButton.SetActive(false);
            successIcon.SetActive(false);
            playButton.SetActive(false);
            pauseButton.SetActive(true);
            loopButton.SetActive(false);
            stopButton.SetActive(true);
            recordButton.SetActive(false);
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
                "bot_segments.zip",
                "Select Replay Zip File",
                "Load Replay"
            );

            if (FileBrowser.Success)
            {
                // FileBrowser.Result is null, if FileBrowser.Success is false
                // Get the file path of the first selected file
                selectedReplayFilePath = FileBrowser.Result[0];
                RefreshSelectedFile();
            }
        }

        private volatile bool _parsingZipFile;

        void RefreshSelectedFile(Action onFileLoadComplete = null)
        {
            _parsingZipFile = true;
            recordButton.SetActive(false);
            chooseReplayButton.SetActive(false);
            fileOpenIndicator.Normal();

            // do this on background thread then return to the main thread for continuation
            Task.Run(() => ProcessDataContainerZipAndSetup(selectedReplayFilePath)).ContinueWith(_ =>
            {
                if (_playbackContainer != null)
                {
                    // setup the new replay data
                    replayDataController.SetDataContainer(_playbackContainer);
                    _playbackContainer = null;
                    SetDefaultButtonStates();
                    // set button states
                    chooseReplayButton.SetActive(false);
                    playButton.SetActive(true);
                    pauseButton.SetActive(false);
                    loopButton.SetActive(true);
                    stopButton.SetActive(true);
                    recordButton.SetActive(false);

                    // if we have something to do after loading then do that here
                    onFileLoadComplete?.Invoke();
                }
                else
                {
                    // failed to load
                    SetDefaultButtonStates();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ProcessDataContainerZipAndSetup(String filePath)
        {
            try
            {
                // do this on background thread
                var dataContainer = new BotSegmentsPlaybackContainer(BotSegmentZipParser.ParseBotSegmentZipFromSystemPath(filePath, out var sessionId), sessionId);
                _playbackContainer = dataContainer;
            }
            catch (Exception e)
            {
                RGDebug.LogException(e);
            }
            finally
            {
                fileOpenIndicator.Stop();
                _parsingZipFile = false;
            }
        }

        private volatile BotSegmentsPlaybackContainer _playbackContainer;

        public void PlayReplay()
        {
            if (selectedReplayFilePath == null)
            {
                // this happens when the user plays a bot sequence from the overlay
                // (no zip file to load)
                SetInUseButtonStates();
                replayDataController.Play();
            }
            else
            {
                if (replayDataController.IsPaused())
                {
                    // don't refresh from a pause
                    SetInUseButtonStates();
                    replayDataController.Play();
                }
                else
                {
                    // refresh on a new play after stop
                    RefreshSelectedFile(() =>
                    {
                        SetInUseButtonStates();
                        replayDataController.Play();
                    });
                }
            }
        }

        public void PauseReplay()
        {
            // reset the button states just in case
            SetInUseButtonStates();
            replayDataController.Pause();

            // make it so they can hit play again
            pauseButton.SetActive(false);
            playButton.SetActive(true);
        }

        public void LoopReplay()
        {
            SetInUseButtonStates();

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
                ScreenRecorder.GetInstance()?.StopRecording(true);
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
                    pauseButton.SetActive(false);
                    loopButton.SetActive(true);
                    stopButton.SetActive(true);
                    recordButton.SetActive(false);
                }
            }
        }

        public void SetKeyFrameWarningText(string text)
        {
            if (text == null)
            {
                warningIcon.SetActive(false);
                warningIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText("");
            }
            else
            {
                warningIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>().SetText(text);
                warningIcon.SetActive(true);
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
