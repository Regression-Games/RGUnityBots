using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class TesseractDriver
{
    private TesseractWrapper _tesseract;

    public bool CheckTessVersion()
    {
        _tesseract = new TesseractWrapper();

        try
        {
            string version = "Tesseract version: " + _tesseract.Version();
            Debug.Log(version);
            return true;
        }
        catch (Exception e)
        {
            string errorMessage = e.GetType() + " - " + e.Message;
            Debug.LogWarning("Tesseract version lookup error: " + errorMessage);
            Debug.LogException(e);
            return false;
        }
    }

    public bool Setup(UnityAction onSetupComplete)
    {
#if UNITY_EDITOR
        return OcrSetup(onSetupComplete);
#elif UNITY_ANDROID
        // TODO: Get Android files in the correct spot if we ever support it
        //CopyAllFilesToPersistentData(fileNames);
        //return OcrSetup(onSetupComplete);
#else
        return OcrSetup(onSetupComplete);
#endif
    }

    public bool OcrSetup(UnityAction onSetupComplete = null)
    {
        _tesseract = new TesseractWrapper();

#if UNITY_EDITOR
        string datapath = Path.GetFullPath("Packages/gg.regression.unity.bots/Plugins/Tesseract-Runtime/StreamingAssets/tessdata");
#elif UNITY_ANDROID
        // TODO: Get Android files in the correct spot if we ever support it
        string datapath = Application.persistentDataPath + "/tessdata/";
#else
        //TODO: Fix this up or copy files to the correct spot for production builds ?
        string datapath = Path.GetFullPath("Packages/gg.regression.unity.bots/Plugins/Tesseract-Runtime/StreamingAssets/tessdata");
#endif

        if (_tesseract.Init("eng", datapath))
        {
            Debug.Log("Tesseract Init Successful");
            onSetupComplete?.Invoke();
            return true;
        }
        else
        {
            Debug.LogError(_tesseract.GetErrorMessage());
            return false;
        }


    }

    public (string, Bounds?)[] Recognize(byte scalingFactor, int width, int height, Color32[] colors)
    {
        return _tesseract?.Recognize(scalingFactor, width, height, colors);
    }

}
