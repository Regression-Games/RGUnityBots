using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class TesseractWrapper
{
#if UNITY_EDITOR
    private const string TesseractDllName = "tesseract.dll";
    private const string LeptonicaDllName = "tesseract.dll";
#elif UNITY_ANDROID
    private const string TesseractDllName = "libtesseract.so";
    private const string LeptonicaDllName = "liblept.so";
#else
    private const string TesseractDllName = "tesseract.dll";
    private const string LeptonicaDllName = "tesseract.dll";
#endif

    private IntPtr _tessHandle;

    private string _errorMsg;
    // anything less than 80 starts to detect single letter jibberish on PW
    private const float MinimumConfidence = 80;

    [DllImport(TesseractDllName)]
    private static extern IntPtr TessVersion();

    [DllImport(TesseractDllName)]
    private static extern IntPtr TessBaseAPICreate();

    [DllImport(TesseractDllName)]
    private static extern int TessBaseAPIInit3(IntPtr handle, string dataPath, string language);

    [DllImport(TesseractDllName)]
    private static extern void TessBaseAPIDelete(IntPtr handle);

    [DllImport(TesseractDllName)]
    private static extern void TessBaseAPISetImage(IntPtr handle, IntPtr imagedata, int width, int height,
        int bytes_per_pixel, int bytes_per_line);

    [DllImport(TesseractDllName)]
    private static extern void TessBaseAPISetImage2(IntPtr handle, IntPtr pix);

    [DllImport(TesseractDllName)]
    private static extern int TessBaseAPIRecognize(IntPtr handle, IntPtr monitor);

    [DllImport(TesseractDllName)]
    private static extern void TessBaseAPISetPageSegMode(IntPtr handle, int mode);

    [DllImport(TesseractDllName)]
    private static extern IntPtr TessBaseAPIGetUTF8Text(IntPtr handle);

    [DllImport(TesseractDllName)]
    private static extern void TessDeleteText(IntPtr text);

    [DllImport(TesseractDllName)]
    private static extern void TessBaseAPIEnd(IntPtr handle);

    [DllImport(TesseractDllName)]
    private static extern void TessBaseAPIClear(IntPtr handle);

    [DllImport(TesseractDllName)]
    private static extern IntPtr TessBaseAPIGetWords(IntPtr handle, IntPtr pixa);

    [DllImport(TesseractDllName)]
    private static extern IntPtr TessBaseAPIAllWordConfidences(IntPtr handle);

    public TesseractWrapper()
    {
        _tessHandle = IntPtr.Zero;
    }

    public string Version()
    {
        IntPtr strPtr = TessVersion();
        string tessVersion = Marshal.PtrToStringAnsi(strPtr);
        return tessVersion;
    }

    public string GetErrorMessage()
    {
        return _errorMsg;
    }

    public bool Init(string lang, string dataPath)
    {
        if (!_tessHandle.Equals(IntPtr.Zero))
            Close();

        try
        {
            _tessHandle = TessBaseAPICreate();
            if (_tessHandle.Equals(IntPtr.Zero))
            {
                _errorMsg = "TessAPICreate failed";
                return false;
            }

            if (string.IsNullOrWhiteSpace(dataPath))
            {
                _errorMsg = "Invalid DataPath";
                return false;
            }

            int init = TessBaseAPIInit3(_tessHandle, dataPath, lang);
            if (init != 0)
            {
                Close();
                _errorMsg = "TessAPIInit failed. Output: " + init;
                return false;
            }
        }
        catch (Exception ex)
        {
            _errorMsg = ex + " -- " + ex.Message;
            return false;
        }

        return true;
    }


    private int imageOutputNumber = 0;

    public (string, Bounds?)[] Recognize(byte scalingFactor, int width, int height, Color32[] colors)
    {
        var results = new List<(string, Bounds?)>();

        // horizontal/vertical segments to split the screen up into for analysis
        var segments = (1,1); // ideally this would be 4:3 or 16:9, but if we split it up too much we get too many chars lost on edges... iow.. segmenting didn't really help, it hurt

        var bytesPerPixel = 4;
        var colorsLength = colors.Length;
        var byteLength = bytesPerPixel * colorsLength / segments.Item1 / segments.Item2;
        byte[] dataBytes = new byte[byteLength];

        var segmentWidth = width / segments.Item1;
        var segmentHeight = height / segments.Item2;

        for (var segmentX = 0; segmentX < segments.Item1; segmentX++)
        {
            for (var segmentY = 0; segmentY < segments.Item2; segmentY++)
            {
                int byteIndex = 0;

                var segmentStartX = segmentWidth * segmentX;
                var segmentStartY = segmentHeight * segmentY;

                // tesseract needs the image bytes from 0,0 being the top left
                for (int y = segmentStartY + segmentHeight - 1; y >= segmentStartY; y--)
                {
                    for (int x = segmentStartX; x < segmentStartX + segmentWidth; x++)
                    {
                        int colorIdx = y * width + x;
                        var theColor = colors[colorIdx];
                        // NTSC grayscale for better contrast 0.299 ∙ Red + 0.587 ∙ Green + 0.114 ∙ Blue
                        // Also invert colors (TODO) How do we figure out whether to do this dynamically for games ?
                        byte grayscaleValue = (byte)(255-Mathf.RoundToInt(theColor.r * 0.299f + theColor.g * 0.587f + theColor.b * 0.114f));

                        dataBytes[byteIndex++] = grayscaleValue;
                        dataBytes[byteIndex++] = grayscaleValue;
                        dataBytes[byteIndex++] = grayscaleValue;
                        dataBytes[byteIndex++] = theColor.a;
                    }
                }

                // write out the pixels

                var imageOutput =
                    ImageConversion.EncodeArrayToJPG(dataBytes, GraphicsFormat.R8G8B8A8_SRGB, (uint)segmentWidth, (uint)segmentHeight);

                // write out the image to file
                var path = $"c:/Users/zack/RAGE/{imageOutputNumber}_{segmentX}_{segmentY}".PadLeft(9, '0') + ".jpg";
                // Save the byte array as a file
                Directory.CreateDirectory("c:/Users/zack/RAGE");
                File.Delete(path);
                File.WriteAllBytes(path, imageOutput);

                IntPtr imagePtr = Marshal.AllocHGlobal(byteLength);
                Marshal.Copy(dataBytes, 0, imagePtr, byteLength);

                // 12 is PSM_SPARSE_TEXT_OSD (https://tesseract-ocr.github.io/tessapi/3.x/a01278.html#a338d4c8b5d497b5ec3e6e4269d8ac66a)
                // see also (https://pyimagesearch.com/2021/11/15/tesseract-page-segmentation-modes-psms-explained-how-to-improve-your-ocr-accuracy/)
                TessBaseAPISetPageSegMode(_tessHandle, 12);

                TessBaseAPISetImage(_tessHandle, imagePtr, width/segments.Item1, height/segments.Item2, bytesPerPixel, width/segments.Item1 * bytesPerPixel);

                if (TessBaseAPIRecognize(_tessHandle, IntPtr.Zero) != 0)
                {
                    Marshal.FreeHGlobal(imagePtr);
                    return null;
                }

                IntPtr confidencesPointer = TessBaseAPIAllWordConfidences(_tessHandle);
                int confidenceIndex = 0;
                List<int> confidence = new List<int>();

                while (true)
                {
                    int tempConfidence = Marshal.ReadInt32(confidencesPointer, confidenceIndex * 4);

                    if (tempConfidence == -1)
                    {
                        break;
                    }

                    confidenceIndex++;
                    confidence.Add(tempConfidence);
                }

                IntPtr stringPtr = TessBaseAPIGetUTF8Text(_tessHandle);
                Marshal.FreeHGlobal(imagePtr);
                if (stringPtr.Equals(IntPtr.Zero))
                {
                    return null;
                }

        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                string recognizedText = Marshal.PtrToStringAnsi(stringPtr);
        #else
                string recognizedText = Marshal.PtrToStringAuto(stringPtr);
        #endif

                string[] words = recognizedText.Split(new[] {' ', '\n'},
                    StringSplitOptions.RemoveEmptyEntries);

                int pointerSize = Marshal.SizeOf(typeof(IntPtr));
                IntPtr intPtr = TessBaseAPIGetWords(_tessHandle, IntPtr.Zero);
                Boxa boxa = Marshal.PtrToStructure<Boxa>(intPtr);
                Box[] boxes = new Box[boxa.n];

                for (int index = 0; index < boxes.Length; index++)
                {
                    if (confidence[index] >= MinimumConfidence)
                    {
                        IntPtr boxPtr = Marshal.ReadIntPtr(boxa.box, index * pointerSize);
                        boxes[index] = Marshal.PtrToStructure<Box>(boxPtr);
                        Box box = boxes[index];
                        Debug.Log("Tesseract Word: " + words[index] + " -> " + confidence[index] + " , bounds: " + box.x + "," + box.y + "|" + box.w + ","+box.h + " , scalingFactor: " + scalingFactor);
                        var center = new Vector3((box.x + box.w / 2.0f)/scalingFactor, (box.y + box.h / 2.0f)/scalingFactor, 0f);
                        var size = new Vector3((box.w+0.0f)/scalingFactor, (box.h +0.0f)/scalingFactor, 0.05f);
                        results.Add((words[index], new Bounds(center, size)));
                    }
                }

                TessBaseAPIClear(_tessHandle);
                TessDeleteText(stringPtr);
            }
        }

        ++imageOutputNumber;

        return results.ToArray();
    }

    public void Close()
    {
        if (_tessHandle.Equals(IntPtr.Zero))
            return;
        TessBaseAPIEnd(_tessHandle);
        TessBaseAPIDelete(_tessHandle);
        _tessHandle = IntPtr.Zero;
    }
}
