using System;
using UnityEngine;

namespace StateRecorder
{
    public class ReplayDataPlaybackController: MonoBehaviour
    {

        private ReplayDataContainer _dataContainer;

        private bool _isPlaying;

        private float _lastStartTime;
        private float _lastPauseTime;
        // added to whenever we pause
        private float _timeElapsed;


        private bool _startPlaying;

        public void SetDataContainer(ReplayDataContainer dataContainer)
        {
            _isPlaying = false;
            _timeElapsed = 0f;
            _dataContainer = dataContainer;
        }

        public void Play()
        {
            if (!_startPlaying && !_isPlaying)
            {
                _startPlaying = true;
            }
        }

        public void Stop()
        {
            _isPlaying = false;
            _dataContainer = null;
        }

        public void Pause()
        {
            _lastPauseTime = Time.unscaledTime;
            _timeElapsed += _lastPauseTime - _lastStartTime;
            _isPlaying = false;
        }

        public void Update()
        {
            var currentTime = Time.unscaledTime;
            if (_startPlaying)
            {
                _lastStartTime = currentTime;
                _startPlaying = false;
                _isPlaying = true;
            }

            if (_isPlaying)
            {
                // find the time point into the replay we're at
                var timePoint = currentTime - _lastStartTime + _timeElapsed;

                // if we're waiting for a state.. wait


                // else get the next frame that is a key frame or that has input
            }
        }
    }
}
