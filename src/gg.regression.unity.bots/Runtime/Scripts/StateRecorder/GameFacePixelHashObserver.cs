using System;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;

namespace RegressionGames.StateRecorder
{
    public class GameFacePixelHashObserver : MonoBehaviour
    {
        private volatile Color32[] _priorPixels;

        private bool _firstRun = true;
        private bool _isActive;

        // uses null or not-null to do interlocked threadsafe updates
        private string _pixelHash;

        private string _requestInProgress;

        [NonSerialized]
        private Component _cohtmlViewInstance;

        [CanBeNull]
        private static readonly Type CohtmlViewType;

        private static readonly PropertyInfo CohtmlViewTextureProperty;

        private static GameFacePixelHashObserver _instance;

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

        public void ClearPixelHash()
        {
            Interlocked.Exchange(ref _pixelHash, null);
        }

        public bool HasPixelHashChanged(out string newHash)
        {
            newHash = Interlocked.CompareExchange(ref _pixelHash, null, null);
            return newHash != null;
        }

        private static RenderTexture GetRenderTexture()
        {
            if (_instance != null && _instance._cohtmlViewInstance != null)
            {
                return (RenderTexture)CohtmlViewTextureProperty.GetValue(_instance._cohtmlViewInstance);
            }

            return null;
        }

        private void UpdateGameFacePixelHash()
        {
            if (_isActive)
            {
                // have to re-get this every time because it changes on resolution and other screen changes that update the gameface render target
                var cohtmlViewTexture = GetRenderTexture();
                if (cohtmlViewTexture != null)
                {
                    var wasActive = Interlocked.CompareExchange(ref _requestInProgress, string.Empty, null);
                    if (wasActive == null)
                    {
                        var frame = Time.frameCount;
                        AsyncGPUReadback.Request(cohtmlViewTexture, 0, request =>
                        {
                            try
                            {
                                if (!request.hasError)
                                {
                                    var data = request.GetData<Color32>();
                                    var pixels = new Color32[data.Length];
                                    data.CopyTo(pixels);

                                    string newHash = _firstRun ? "FirstPass" : null;

                                    if (!_firstRun)
                                    {
                                        if (_priorPixels == null || pixels.Length != _priorPixels.Length)
                                        {
                                            // different size image or first pass
                                            newHash = "NewResolution";
                                        }
                                        else
                                        {
                                            var pixelsLength = pixels.Length;
                                            for (var i = 0; i < pixelsLength; i++)
                                            {
                                                if (pixels[i].r != _priorPixels[i].r || pixels[i].g != _priorPixels[i].g || pixels[i].b != _priorPixels[i].b || pixels[i].a != _priorPixels[i].a)
                                                {
                                                    newHash = $"{i} - ({pixels[i].r},{pixels[i].g},{pixels[i].b},{pixels[i].a})";
                                                    RGDebug.LogDebug($"Different GameFace UI pixel at index {i}");
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    _firstRun = false;

                                    Interlocked.Exchange(ref _pixelHash, newHash);

                                    _priorPixels = pixels;
                                }
                            }
                            finally
                            {
                                Interlocked.Exchange(ref _requestInProgress, null);
                            }
                        });
                    }
                }
            }
            else
            {
                Interlocked.Exchange(ref _pixelHash, null);
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
