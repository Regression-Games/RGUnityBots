using System;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace StateRecorder
{
    public class GameFacePixelHashObserver : MonoBehaviour
    {
        private const int _grayWidth = 128;
        private const int _grayHeight = 128;
        private byte[] _grayArray = new byte[_grayWidth * _grayHeight * 2];

        private bool _firstRun = true;
        private bool _isActive = false;

        // uses null or not-null to do interlocked threadsafe updates
        private string _pixelHash;

        private RenderTexture _cohtmlViewTexture;

        [CanBeNull]
        public static readonly Type CohtmlViewType;

        public static readonly PropertyInfo CohtmlViewTextureProperty;

        private static GameFacePixelHashObserver _instance;

        static GameFacePixelHashObserver()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (CohtmlViewType != null)
                {
                    break;
                }
                foreach (Type t in a.GetTypes())
                {
                    if (CohtmlViewType != null)
                    {
                        break;
                    }
                    if (t.FullName == "cohtml.CohtmlView")
                    {
                        // we are running in an environment with GameFace UI libraries
                        CohtmlViewType = t;
                        CohtmlViewTextureProperty = t.GetProperty("ViewTexture");
                    }
                }
            }
        }

        public void SetActive(bool active = true)
        {
            _isActive = active;
        }

        public int GrayScaleTiers = 255;

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
                        if (CohtmlViewType != null && _instance._cothmlViewTexture == null)
                        {
                            var cohtmlViewInstance = _instance.GetComponent(CohtmlViewType);
                            if (cohtmlViewInstance != null)
                            {
                                _instance._cothmlViewTexture = (RenderTexture)CohtmlViewTextureProperty.GetValue(cohtmlViewInstance);
                                RenderPipelineManager.endFrameRendering += _instance.OnEndFrame;
                            }
                        }
                    }
                }
            }

            return _instance;
        }

        private void UpdateGameFacePixelHash()
        {

            if (_isActive && _cothmlViewTexture != null)
            {
                // scale down the current UI texture to 256x256 using the GPU
                var scaledTexture = TextureScaling_GPU.ScaleRenderTextureAsCopy(_cothmlViewTexture, _grayWidth, _grayHeight);

                try
                {
                    // TODO: We should probably also be able to do the grayscale computation part on the GPU to save CPU cycles; tbd if its really necessary as this is fairly light lifting even on the cpu and we need to iteratively compare anyway
                    var pixels = scaledTexture.GetPixels();

                    var index = 0;
                    var isDifferent = _firstRun; // start false unless _firstRun
                    _firstRun = false;
                    foreach (var pixel in pixels)
                    {
                        // update for current frame
                        // alpha value
                        var alphaTier = (int)((TextureScaling_GPU.NTSC_Grayscale.a * pixel.a) * GrayScaleTiers);
                        var newAlpha = (byte)((255 * alphaTier) / GrayScaleTiers);
                        // gray value
                        var grayTier = (int)((TextureScaling_GPU.NTSC_Grayscale.r * pixel.r + TextureScaling_GPU.NTSC_Grayscale.g * pixel.g + TextureScaling_GPU.NTSC_Grayscale.b * pixel.b) * GrayScaleTiers);
                        var newGray = (byte)((255 * grayTier) / GrayScaleTiers);

                        // check for differences to prior frame
                        if (!isDifferent)
                        {
                            isDifferent |= _grayArray[index] != newGray;
                            if (!isDifferent)
                            {
                                isDifferent |= _grayArray[index + 1] != newAlpha;
                            }
                        }

                        _grayArray[index] = newGray;
                        _grayArray[index+1] = newAlpha;

                        index += 2;
                    }

                    if (isDifferent)
                    {
                        byte[] hash;
                        using (var sha256 = System.Security.Cryptography.SHA256.Create()) {
                            sha256.TransformFinalBlock(_grayArray, 0, _grayArray.Length);
                            hash = sha256.Hash;
                        }

                        var base64Hash = Convert.ToBase64String(hash);
                        Interlocked.Exchange(ref _pixelHash, base64Hash);
                    }
                }
                finally
                {
                    Object.Destroy(scaledTexture);
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

        private void OnDestroy()
        {
            if (_cothmlViewTexture != null)
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
