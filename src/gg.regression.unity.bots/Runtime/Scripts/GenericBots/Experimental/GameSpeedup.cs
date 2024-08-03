using System;
using UnityEngine;

namespace RegressionGames.GenericBots.Experimental
{
    /// <summary>
    /// Configures the game to run as fast as possible for faster training/search.
    /// The speedup is active as soon as this object is constructed.
    /// When the Dispose() method is invoked, the speedup is disabled.
    /// The user of this class needs to regularly invoke the Update() method to ensure the speedup
    /// is retained.
    /// </summary>
    public class GameSpeedup : IDisposable
    {
        private int _savedTargetFramerate;
        private int _savedVsyncCount;
        private float _savedTimeScale;
        private float _targetTimeScale;

        public GameSpeedup(float targetTimeScale = 20.0f)
        {
            _targetTimeScale = targetTimeScale;
            
            _savedTargetFramerate = Application.targetFrameRate;
            _savedVsyncCount = QualitySettings.vSyncCount;
            _savedTimeScale = Time.timeScale;
            
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (Application.targetFrameRate != 10000)
            {
                Application.targetFrameRate = 10000; // get as much fps as possible
            }
            if (QualitySettings.vSyncCount != 0)
            {
                QualitySettings.vSyncCount = 0; // disable v-sync
            }
            if (!Mathf.Approximately(Time.timeScale, _targetTimeScale))
            {
                Time.timeScale = _targetTimeScale;
            }
        }

        /// <summary>
        /// Repeatedly apply the settings
        /// </summary>
        public void Update()
        {
            ApplySettings();
        }
        
        public void Dispose()
        {
            Application.targetFrameRate = _savedTargetFramerate;
            QualitySettings.vSyncCount = _savedVsyncCount;
            Time.timeScale = _savedTimeScale;
        }
    }
}