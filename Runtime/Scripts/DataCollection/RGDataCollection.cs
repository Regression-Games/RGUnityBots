using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using RegressionGames.StateActionTypes;
using RegressionGames.Types;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RegressionGames.DataCollection
{
    
    /**
     * The RGDataCollection class is responsible for collecting all of the data that is generated over multiple bot
     * instance runs. More specifically, this means collecting screenshots and replay data (which includes state,
     * action, and validation information).
     * 
     * In order to be efficient, this class does the following:
     *  - Maintains a mapping from clientIds to botInstanceIds - this allows saving the data later
     *  - Maintains a mapping from clientIds to replay data - this allows us to do a few things:
     *      - The replay data stores the validations that occurred in that tick, allowing us to save validation data later
     *      - The replay data stores... the replay data, so it can be saved as a zip replay file
     *      - The validation ticks tell us which screenshots are relevant to each bot
     *  - Takes a screenshot whenever a new validation result comes in, so later that screenshot can be uploaded, and
     *    tracks the ticks that were screenshotted
     *
     * Once all clients have been disconnected, this class can be reset and all data left over (i.e. screenshots) can
     * be deleted from the system.
     * 
     */
    public class RGDataCollection
    {

        private readonly string _sessionName;
        private Dictionary<long, long> _clientIdToBotInstanceId;
        private Dictionary<long, List<RGStateActionReplayData>> _clientIdToReplayData;
        private HashSet<long> _screenshottedTicks;

        public RGDataCollection()
        {
            // Name the session, and setup a temporary directory for all data
            _sessionName = Guid.NewGuid().ToString();
            
            // Instantiate the dictionaries
            _clientIdToBotInstanceId = new();
            _clientIdToReplayData = new();
            _screenshottedTicks = new();
        }

        /**
         * Registers a bot instance under a specific client id
         */
        public void RegisterBotInstance(long clientId, long botInstanceId)
        {
            _clientIdToBotInstanceId[clientId] = botInstanceId;
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

        public void SaveReplayDataInfo(long clientId, RGStateActionReplayData replayData)
        {
            // Check if the dictionary already has the clientId key
            if (_clientIdToReplayData.TryGetValue(clientId, out List<RGStateActionReplayData> existingList))
            {
                // Key exists, so add the replayData to the existing list
                existingList.Add(replayData);
            }
            else
            {
                // Key does not exist, so create a new list and add it to the dictionary
                _clientIdToReplayData[clientId] = new List<RGStateActionReplayData> { replayData };
            }
            
            // If the replay data has a validation, also take a screenshot if not already taken
            if (replayData.validationResults?.Length > 0 && !_screenshottedTicks.Contains(replayData.tickInfo.tick))
            {
                var validationTick = replayData.tickInfo.tick;
                _screenshottedTicks.Add(validationTick);
                CaptureScreenshot(validationTick);
            }
        }

        public void SaveBotInstanceHistory(long clientId, RGClientConnectionType rgClientConnectionType)
        {
            RGDebug.LogVerbose("Ending data collection for clientId " + clientId);
            
            var botInstanceId = _clientIdToBotInstanceId[clientId];

            // First, create a bot history record for this bot
            RGServiceManager.GetInstance()?.CreateBotInstanceHistory(botInstanceId,
                (botInstanceHistoryRecord) =>
                {
                    RGDebug.LogVerbose($"Successfully created bot instance history for bot instance {botInstanceId}");
                },
                () =>
                {

                });
            
            // Next, save text files for each replay tick, zip it up, and then upload
            foreach (var replayData in _clientIdToReplayData[clientId])
            {
                var filePath = GetSessionDirectory($"replayData/{clientId}/rg_bot_replay_data_{replayData.tickInfo.tick}.txt");
                File.WriteAllText(JsonUtility.ToJson(replayData), filePath);
            }
            
            

            // Then, upload all of the screenshots
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