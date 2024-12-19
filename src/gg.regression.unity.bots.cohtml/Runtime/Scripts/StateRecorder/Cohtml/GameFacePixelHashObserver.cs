using System;
using System.Threading;
using cohtml;
using UnityEngine;
using UnityEngine.Rendering;

namespace RegressionGames.StateRecorder.Cohtml
{
    public class GameFacePixelHashObserver : RGThirdPartyUIObserver
    {
        private Color32[] _priorPixels;

        private volatile bool _firstRun = true;

        private bool _isActive;

        private int _hashFrameNumber = -1;

        private string _requestInProgress;

        // don't set this until update when we've fully initialized
        [NonSerialized]
        private CohtmlView _cohtmlViewInstance;

        public override void SetActive(bool active)
        {
            _firstRun = true;
            _isActive = active;
        }

        public void Update()
        {
            // can't do this in onEnable or Start as gameface doesn't load/initialize that early
            if (_cohtmlViewInstance == null)
            {
                _cohtmlViewInstance = FindObjectOfType<CohtmlView>();
                if (_cohtmlViewInstance != null)
                {
                    var existingInstance = _cohtmlViewInstance.gameObject.GetComponent<GameFacePixelHashObserver>();
                    if (existingInstance == null)
                    {
                        transform.parent = _cohtmlViewInstance.transform;
                        GetRenderTexture(); // just to force loading of any refs just in case
                        RenderPipelineManager.endFrameRendering += OnEndFrame;
                    }
                    else
                    {
                        Destroy(this);
                    }
                }
            }
        }

        public override bool HasUIChanged()
        {
            var hfn = Interlocked.CompareExchange(ref _hashFrameNumber, -1, -1);
            if (Time.frameCount == hfn)
            {
                return true;
            }

            return false;
        }


        private RenderTexture GetRenderTexture()
        {
            return _cohtmlViewInstance.ViewTexture;
        }

        private void UpdateGameFacePixelHash()
        {
            // If we are running in -nographics mode, the async task below fails, causing an exception inside
            // the AsyncGPUReadback.Request that is difficult to catch. This ensures that the image data
            // is only read when we have graphics
            if (_isActive && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
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


                                    var priorPixels = Interlocked.Exchange(ref _priorPixels, pixels);
                                    // on the first run we just record the starting state, subsequent runs we evaluate differences; we do this because the first tick already records either way
                                    if (!_firstRun)
                                    {
                                        if (priorPixels == null || pixels.Length != priorPixels.Length)
                                        {
                                            // different size image or first pass
                                            // mark the next frame as needing to send an update
                                            Interlocked.Exchange(ref _hashFrameNumber, Time.frameCount+1);
                                            RGDebug.LogDebug($"Different GameFace UI resolution");
                                        }
                                        else
                                        {
                                            var pixelsLength = pixels.Length;
                                            for (var i = 0; i < pixelsLength; i++)
                                            {
                                                if (pixels[i].r != priorPixels[i].r || pixels[i].g != priorPixels[i].g || pixels[i].b != priorPixels[i].b || pixels[i].a != priorPixels[i].a)
                                                {
                                                    // mark the next frame as needing to send an update
                                                    Interlocked.Exchange(ref _hashFrameNumber, Time.frameCount+1);
                                                    RGDebug.LogDebug($"Different GameFace UI pixel at index {i}");
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    _firstRun = false;
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
                // mark the next frame as needing to send an update
                Interlocked.Exchange(ref _hashFrameNumber, -1);
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
