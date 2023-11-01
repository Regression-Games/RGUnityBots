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
        private GameObject _parent;

        public RGDataCollection(GameObject parent)
        {
            // Name the session, and setup a temporary directory for all data
            _sessionName = Guid.NewGuid().ToString();
            _parent = parent;
        }

        public void CaptureScreenshot(long tick)
        {
            Debug.Log($"Captured screenshot at tick {tick}");
            string path = GetSessionDirectory($"screenshots/{tick}.jpg");

            var texture = ScreenCapture.CaptureScreenshotAsTexture(1);

            // Encode the texture into a jpg byte array
            byte[] bytes = texture.EncodeToJPG(100);

            // Save the byte array as a jpg file
            File.WriteAllBytes(path, bytes);

            // Destroy the texture to free up memory
            Object.Destroy(texture);
        }

        public void RecordSession(long botInstanceId, RGClientConnectionType rgClientConnectionType)
        {
            Debug.Log("Ending data collection, uploading data to Regression Games...");

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