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
    public class GameFaceDeltaObserver : MonoBehaviour
    {
        private const int _grayWidth = 256;
        private const int _grayHeight = 256;
        private byte[] _grayArray = new byte[_grayWidth * _grayHeight * 2];

        private bool _firstRun = true;
        private bool _isRecording = false;

        [NonSerialized]
        // uses null or not-null to do interlocked threadsafe updates
        public string _gameFaceDeltaHash;

        private RenderTexture _cohtmlViewTexture;

        [CanBeNull]
        public static readonly Type CohtmlViewType;

        public static readonly PropertyInfo CohtmlViewTextureProperty;

        private static GameFaceDeltaObserver _instance;

        static GameFaceDeltaObserver()
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

        public void StartRecording()
        {
            _isRecording = true;
        }

        public void StopRecording()
        {
            _isRecording = false;
        }

        public int GrayScaleTiers = 255;

        public static GameFaceDeltaObserver GetInstance()
        {
            // can't do this in onEnable or Start as gameface doesn't load/initialize that early
            if (CohtmlViewType != null && _instance == null)
            {
                var cothmlObject = FindAnyObjectByType(GameFaceDeltaObserver.CohtmlViewType) as MonoBehaviour;
                if (cothmlObject != null)
                {
                    _instance = cothmlObject.gameObject.GetComponent<GameFaceDeltaObserver>();
                    if (_instance == null)
                    {
                        _instance = cothmlObject.gameObject.AddComponent<GameFaceDeltaObserver>();
                    }
                }
            }

            return _instance;
        }

        private void ComputeGameFaceDelta()
        {
            if (_isRecording && _cohtmlViewTexture != null)
            {
                // scale down the current UI texture to 256x256 using the GPU
                var scaledTexture = TextureScaling_GPU.ScaleRenderTextureAsCopy(_cohtmlViewTexture, _grayWidth, _grayHeight);

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
                        Interlocked.Exchange(ref _gameFaceDeltaHash, base64Hash);
                    }
                }
                finally
                {
                    Object.Destroy(scaledTexture);
                }
            }
            else
            {
                Interlocked.Exchange(ref _gameFaceDeltaHash, null);
            }
        }

        public string GetPixelHash()
        {
            return Interlocked.Exchange(ref _gameFaceDeltaHash, null);
        }

        private void Start()
        {
            // we normally can't do this in Start because gameface hasn't loaded, but since ScreenRecorder creates us during an Update pass after gameface is loaded, we can
            if (CohtmlViewType != null && _cohtmlViewTexture == null)
            {
                var cohtmlViewInstance = GetComponent(CohtmlViewType);
                if (cohtmlViewInstance != null)
                {
                    _cohtmlViewTexture = (RenderTexture)CohtmlViewTextureProperty.GetValue(cohtmlViewInstance);
                    RenderPipelineManager.endFrameRendering += OnEndFrame;
                }
            }
        }

        private void OnDestroy()
        {
            if (_cohtmlViewTexture != null)
            {
                RenderPipelineManager.endFrameRendering -= OnEndFrame;
            }
        }


        void OnEndFrame(ScriptableRenderContext ctx, Camera[] cameras)
        {
            ComputeGameFaceDelta();
        }
    }
}
