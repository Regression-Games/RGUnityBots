using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using JetBrains.Annotations;
using RegressionGames;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Rect = UnityEngine.Rect;

namespace StateRecorder
{
    public class GameFacePixelHashObserver : MonoBehaviour
    {
        private Color32[] _priorPixels;

        private bool _firstRun = true;
        private bool _isActive;

        // uses null or not-null to do interlocked threadsafe updates
        private string _pixelHash;
        private string _requestInProgress;

        private (string, Bounds?)[] _uiTexts = null;

        [NonSerialized]
        private Component _cohtmlViewInstance;

        [CanBeNull]
        private static readonly Type CohtmlViewType;

        private static readonly PropertyInfo CohtmlViewTextureProperty;

        private static GameFacePixelHashObserver _instance;

        private TesseractDriver _tesseractEngine = null;

        void Awake()
        {
            if (_tesseractEngine == null)
            {
                _tesseractEngine = new TesseractDriver();

                if (_tesseractEngine.CheckTessVersion())
                {
                    if (!_tesseractEngine.Setup(OnTesseractSetupComplete))
                    {
                        _tesseractEngine = null;
                    }
                }
                else
                {
                    _tesseractEngine = null;
                }
            }
        }

        void OnTesseractSetupComplete()
        {

        }

        static GameFacePixelHashObserver()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                CohtmlViewType = a.GetType("cohtml.CohtmlView", false);
                if (CohtmlViewType != null)
                {
                    CohtmlViewTextureProperty = CohtmlViewType.GetProperty("ViewTexture");
                    break;
                }
            }
        }

        public void SetActive(bool active = true)
        {
            _isActive = active;
        }

        public static GameFacePixelHashObserver GetInstance()
        {
            // can't do this in onEnable or Start as gameface doesn't load/initialize that early
            if (CohtmlViewType != null && _instance == null)
            {
                var cothmlObject = FindAnyObjectByType(GameFacePixelHashObserver.CohtmlViewType) as MonoBehaviour;
                if (cothmlObject != null)
                {
                    _instance = cothmlObject.gameObject.GetComponent<GameFacePixelHashObserver>();
                    if (_instance == null)
                    {
                        _instance = cothmlObject.gameObject.AddComponent<GameFacePixelHashObserver>();
                        // we normally can't do this in Start because gameface hasn't loaded, but since ScreenRecorder creates us during an Update pass after gameface is loaded, we can
                        if (CohtmlViewType != null && _instance._cohtmlViewInstance == null)
                        {
                            _instance._cohtmlViewInstance = _instance.GetComponent(CohtmlViewType);
                            if (_instance._cohtmlViewInstance != null)
                            {
                                GetRenderTexture(); // just to force loading of any refs just in case
                                RenderPipelineManager.endFrameRendering += _instance.OnEndFrame;
                            }
                        }
                    }
                }
            }

            return _instance;
        }

        private static RenderTexture GetRenderTexture()
        {
            if (_instance != null && _instance._cohtmlViewInstance != null)
            {
                return (RenderTexture)CohtmlViewTextureProperty.GetValue(_instance._cohtmlViewInstance);
            }

            return null;
        }

        public static Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(newWidth, newHeight);
            Graphics.Blit(source, renderTexture);

            Texture2D texture = new Texture2D(newWidth, newHeight, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(Vector2.zero, new Vector2(newWidth, newHeight)), 0, 0);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);

            texture.Apply();
            return texture;
        }

        private void UpdateGameFacePixelHash()
        {
            if (_isActive)
            {
                // have to re-get this every time because it changes on resolution and other screen changes that update the gameface render target
                var cohtmlViewTexture = GetRenderTexture();
                if (cohtmlViewTexture != null)
                {
                    var screenWidth = cohtmlViewTexture.width;
                    var screenHeight = cohtmlViewTexture.height;
                    var wasActive = Interlocked.CompareExchange(ref _requestInProgress, string.Empty, null);
                    if (wasActive == null)
                    {
                        AsyncGPUReadback.Request(cohtmlViewTexture, 0, (request =>
                        {
                            try
                            {
                                if (!request.hasError)
                                {
                                    var data = request.GetData<Color32>();
                                    var pixels = new Color32[data.Length];
                                    data.CopyTo(pixels);

                                    if (SystemInfo.graphicsUVStartsAtTop)
                                    {
                                        var copyBuffer = new Color32[screenWidth];
                                        // the pixels from the GPU are upside down, we need to reverse this for it to be right side up or OCR won't work
                                        var halfHeight = screenHeight / 2;
                                        for (var i = 0; i <= halfHeight; i++)
                                        {
                                            // swap rows
                                            // bottom row to buffer
                                            Array.Copy(pixels, i * screenWidth, copyBuffer, 0, screenWidth);
                                            // top row to bottom
                                            Array.Copy(pixels, (screenHeight - i - 1) * screenWidth, pixels, i * screenWidth, screenWidth);
                                            // buffer to top row
                                            Array.Copy(copyBuffer, 0, pixels, (screenHeight - i - 1) * screenWidth, screenWidth);
                                        }
                                    } //else.. we're fine

                                    string newHash = _firstRun ? "FirstPass" : null;

                                    if (_priorPixels == null || pixels.Length != _priorPixels.Length)
                                    {
                                        // different size image or first pass
                                        newHash = "NewResolution";
                                    }
                                    else
                                    {
                                        _firstRun = false;
                                        var pixelsLength = pixels.Length;
                                        for (var i = 0; i < pixelsLength; i++)
                                        {
                                            if (newHash != null && pixels[i].r != _priorPixels[i].r || pixels[i].g != _priorPixels[i].g || pixels[i].b != _priorPixels[i].b || pixels[i].a != _priorPixels[i].a)
                                            {
                                                newHash = $"{i} - ({pixels[i].r},{pixels[i].g},{pixels[i].b},{pixels[i].a})";
                                                RGDebug.LogDebug($"Different GameFace UI pixel at index {i}");
                                                break;
                                            }
                                        }
                                    }

                                    if (newHash != null)
                                    {

                                        if (_tesseractEngine != null)
                                        {
                                            try
                                            {
                                                // in practice, scaling up the image did help find smaller words, but still didn't find them all :/
                                                // making this 4 or larger actually made things way worse.. instead of doing a fixed factor we may want to do a target min x or y resolution ??
                                                // for now.. leaving this at 1 as it didn't really help and dramatically slows things down having to read/write from the GPU
                                                byte scalingFactor = 1;
                                                // do not remove... here for ease of testing scaling factors
                                                if (scalingFactor != 1)
                                                {
                                                    // scale the texture size before analyzing for OCR
                                                    var tex = new Texture2D(screenWidth, screenHeight, TextureFormat.ARGB32, false);
                                                    tex.SetPixels32(pixels);
                                                    tex.Apply();

                                                    var resized = ResizeTexture(tex, screenWidth * scalingFactor, screenHeight * scalingFactor);
                                                    try
                                                    {
                                                        var resizedPixels = resized.GetPixels32();

                                                        var newWords = _tesseractEngine.Recognize(scalingFactor, screenWidth * scalingFactor, screenHeight * scalingFactor, resizedPixels);
                                                        Interlocked.Exchange(ref _uiTexts, newWords);
                                                    }
                                                    finally
                                                    {
                                                        Object.Destroy(resized);
                                                    }
                                                }
                                                else
                                                {
                                                    var newWords = _tesseractEngine.Recognize(scalingFactor, screenWidth, screenHeight, pixels);
                                                    Interlocked.Exchange(ref _uiTexts, newWords);
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                RGDebug.LogWarning("Exception reading UI Text Information - " + e);
                                            }
                                        }

                                        Interlocked.Exchange(ref _pixelHash, newHash);
                                    }

                                    _priorPixels = pixels;
                                }
                            }
                            finally
                            {
                                Interlocked.Exchange(ref _requestInProgress, null);
                            }
                        }));
                    }
                }
            }
            else
            {
                Interlocked.Exchange(ref _pixelHash, null);
            }
        }

        public string GetPixelHash(bool clearValueOnRead = false)
        {
            if (clearValueOnRead)
            {
                return Interlocked.Exchange(ref _pixelHash, null);
            }
            else
            {
                return Interlocked.CompareExchange(ref _pixelHash, null, null);
            }
        }

        public (string, Bounds?)[] GetUITexts(bool clearValueOnRead = false)
        {
            if (clearValueOnRead)
            {
                return Interlocked.Exchange(ref _uiTexts, null);
            }
            else
            {
                return Interlocked.CompareExchange(ref _uiTexts, null, null);
            }
        }

        private void OnDestroy()
        {
            if (_cohtmlViewInstance != null)
            {
                RenderPipelineManager.endFrameRendering -= OnEndFrame;
            }
        }


        void OnEndFrame(ScriptableRenderContext ctx, Camera[] cameras)
        {
            UpdateGameFacePixelHash();
        }
    }
}
