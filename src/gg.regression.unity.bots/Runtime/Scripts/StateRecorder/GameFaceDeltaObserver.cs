using System;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
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

        // uses 0=false, 1=true to do interlocked threadsafe updates
        public int _gameFaceDelta;

        private RenderTexture _cohtmlViewTexture;

        [CanBeNull]
        public static readonly Type CohtmlViewType;

        public static readonly PropertyInfo CohtmlViewTextureProperty;

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
                        var newAlpha = (byte)(255 * TextureScaling_GPU.NTSC_Grayscale.a * pixel.a);
                        // gray value
                        var newGray = (byte)((255 * TextureScaling_GPU.NTSC_Grayscale.r * pixel.r) + (255 * TextureScaling_GPU.NTSC_Grayscale.g * pixel.g) + (255 * TextureScaling_GPU.NTSC_Grayscale.b * pixel.b));

                        // check for differences to prior frame
                        if (!isDifferent)
                        {
                            isDifferent |= _grayArray[index] != newAlpha;
                            if (!isDifferent)
                            {
                                isDifferent |= _grayArray[index + 1] != newGray;
                            }
                        }

                        _grayArray[index] = newAlpha;
                        _grayArray[index+1] = newGray;

                        index += 2;
                    }

                    if (isDifferent)
                    {
                        Interlocked.Exchange(ref _gameFaceDelta, 1);
                    }
                }
                finally
                {
                    Object.Destroy(scaledTexture);
                }
            }
            else
            {
                Interlocked.Exchange(ref _gameFaceDelta, 0);
            }
        }

        public bool HadDelta()
        {
            return 1 == Interlocked.Exchange(ref _gameFaceDelta, 0);
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
