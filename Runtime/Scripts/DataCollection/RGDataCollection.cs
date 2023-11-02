using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.DataCollection
{
    
    /**
     * An RGDataCollection instance is responsible for coordinating all data collection features on behalf
     * of Regression Games. It will collect the replay data, logs, screenshots, and then upload these items
     * to Regression Games. Currently only supports screenshots, with more to come in an upcoming PR.
     */
    public class RGDataCollection
    {

        private readonly string _sessionName;
        private Dictionary<long, List<RGTickInfoData>> _sessionTickInfo;

        public RGDataCollection()
        {
            // Name the session, and setup a temporary directory for all data
            _sessionName = Guid.NewGuid().ToString();
            
            // Current tick info for the running instance
            _sessionTickInfo = new Dictionary<long, List<RGTickInfoData>>();
        }

        public void CaptureScreenshot(long tick)
        {
            RGDebug.LogVerbose($"Captured screenshot at tick {tick}");
            string path = GetSessionDirectory($"screenshots/{tick}.jpg");
            var texture = ScreenCapture.CaptureScreenshotAsTexture(1);

            try
            {
                // Encode the texture into a jpg byte array
                byte[] bytes = texture.EncodeToJPG(100);

                // Save the byte array as a jpg file
                File.WriteAllBytes(path, bytes);
            }
            finally
            {
                // Destroy the texture to free up memory
                Object.Destroy(texture);
            }
            
        }

        public void SaveReplayTickInfo(long clientId, RGTickInfoData tickInfoData)
        {
            // Check if the dictionary already has the clientId key
            if (_sessionTickInfo.TryGetValue(clientId, out List<RGTickInfoData> existingList))
            {
                // Key exists, so add the tickInfoData to the existing list
                existingList.Add(tickInfoData);
            }
            else
            {
                // Key does not exist, so create a new list and add it to the dictionary
                _sessionTickInfo[clientId] = new List<RGTickInfoData> { tickInfoData };
            }
        }

        public void RecordSession(long botInstanceId, RGClientConnectionType rgClientConnectionType)
        {
            RGDebug.LogVerbose("Ending data collection, uploading data to Regression Games...");

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
                        RGDebug.LogVerbose($"Successfully uploaded screenshot for bot instance {botInstanceId} and tick {tick}");
                    },
                    () =>
                    {
                        
                    });
            }

            // Saves all the tick info in our session to disk
            WriteTickInfoToDisk();
            
            RGDebug.LogVerbose("Data uploaded to Regression Games");
            
        }

        private void WriteTickInfoToDisk()
        {
            // Construct the base replay directory path relative to Application.dataPath
            string parentPath = Directory.GetParent(Application.dataPath).FullName;
            string replayDirectory = Path.Combine(parentPath, "RegressionGamesReplayData");

            // Ensure the replay directory exists
            if (!Directory.Exists(replayDirectory))
            {
                Directory.CreateDirectory(replayDirectory);
            }

            // Iterate over each clientId and their corresponding tick info data list
            foreach (var kvp in _sessionTickInfo)
            {
                long clientId = kvp.Key;
                List<RGTickInfoData> tickInfoList = kvp.Value;

                // Process each tickInfoData item
                foreach (RGTickInfoData tickInfoData in tickInfoList)
                {
                    // Prepare the content to write
                    var content = new
                    {
                        tickInfo = tickInfoData,
                        clientId = clientId != 0 ? clientId : -1,
                        actions = string.Empty,
                        validationResults = string.Empty
                    };

                    // Serialize content to JSON
                    string jsonContent = JsonUtility.ToJson(content);

                    // Define the file name based on the tick number
                    string fileName = $"rgbot_replay_data_{tickInfoData.tick}.txt";
                    string filePath = Path.Combine(replayDirectory, fileName);

                    // Write to the file
                    try
                    {
                        File.WriteAllText(filePath, jsonContent);
                    }
                    catch (IOException e)
                    {
                        RGDebug.LogError($"ERROR: Failed to write replay data to file at {filePath}: {e}");
                    }
                }
            }
        }

        private string GetSessionDirectory(string path = "")
        {
            var fullPath = Path.Combine(Application.persistentDataPath, "RGData",  _sessionName, path);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
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