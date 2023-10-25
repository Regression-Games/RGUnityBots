using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames.DataCollection
{
    
    /**
     * An RGSession is responsible for coordinating all data collection features on behalf
     * of Regression Games. It will collect the data, store it, and then send it to Regression
     * Games when completed.
     */
    public class RGDataCollection
    {

        private readonly string _sessionName;
        private ConcurrentQueue<LogDataPoint> _logDataPoints;
        private Dictionary<long, ReplayDataPoint> _replayDataForTick;

        public RGDataCollection()
        {
            // Name the session, and setup a temporary directory for all data
            _sessionName = Guid.NewGuid().ToString();
            _logDataPoints = new ConcurrentQueue<LogDataPoint>();
            _replayDataForTick = new ();

            // Setup all listener-based data collection (as opposed to tick-based)
            Application.logMessageReceivedThreaded += CaptureLog;

            Debug.Log("RGSession created and started!");
        }

        public void CaptureScreenshot(long tick)
        {
            Debug.Log($"Captured screenshot at tick {tick}");
            string path = GetSessionDirectory($"screenshots/{tick}.png");
            ScreenCapture.CaptureScreenshot(path);
        }

        public void CaptureReplayDataPoint(RGTickInfoData tickInfo, long playerId, RGActionRequest[] actions, RGValidationResult[] validations)
        {
            _replayDataForTick[tickInfo.tick] = new ReplayDataPoint(tickInfo, playerId, actions, validations);
        }

        public void CaptureLog(string logString, string stackTrace, LogType type)
        {
            _logDataPoints.Enqueue(new LogDataPoint(logString, stackTrace, type));
        }

        public void RecordSession(long botInstanceId, RGClientConnectionType rgClientConnectionType)
        {
            Debug.Log("Ending RGSession...");
            
            Debug.Log("Saving all replay data files and zipping...");
            foreach (var replayDataPoint in _replayDataForTick)
            {
                var replayData = JsonConvert.SerializeObject(replayDataPoint.Value, Formatting.None, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });
                File.WriteAllText(GetSessionDirectory($"replay_data/{replayDataPoint.Key}.txt"), replayData);
            }
            // Zip the replay data text files
            

            // Now, begin uploading
            Debug.Log($"Uploading data collected for bot {botInstanceId}...");
            
            // First, upload all of the screenshots
            var screenshotFiles = Directory.GetFiles(GetSessionDirectory($"screenshots"));
            foreach (var screenshotFilePath in screenshotFiles)
            {
                var screenshotFile = Path.GetFileName(screenshotFilePath);
                var tick = long.Parse(screenshotFile.Split(".")[0]);
                RGServiceManager.GetInstance()?.UploadScreenshot(
                    botInstanceId, tick, screenshotFilePath,
                    () =>
                    {
                        Debug.Log($"Successfully uploaded screenshot for bot instance {botInstanceId} and tick {tick}");
                    },
                    () =>
                    {
                        
                    });
            }
            
            // Second, if this is a remote bot, collect and upload all of the replay data, logs, and validations
            // Remote bots already handle saving and uploading their data
            if (rgClientConnectionType == RGClientConnectionType.LOCAL)
            {
                
            }

            Debug.Log("Data uploaded to Regression Games");
            
        }

        private string GetSessionDirectory(string path = "")
        {
            var fullPath = Path.Join(Application.persistentDataPath, $"RGData/{_sessionName}", path);
            // Trim the file name from the path if this is a file path and not directory
            var trimmedPath = fullPath.Substring(0, fullPath.LastIndexOf('/'));
            Directory.CreateDirectory(trimmedPath);
            return fullPath;
        }

        public void Cleanup()
        {
            // Delete everything in the session folder
            var sessionPath = GetSessionDirectory();
            Directory.Delete(sessionPath, true);
            Debug.Log("Deleted all local temp data collected for RG bots");
        }

    }
}