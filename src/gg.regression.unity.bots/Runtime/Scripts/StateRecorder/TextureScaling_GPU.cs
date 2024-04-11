using UnityEditor;
using UnityEngine;

namespace StateRecorder
{
    // Note: This code overrides the current render target temporarily but will set it back
    public class TextureScaling_GPU
    {
        /** <summary>Returns a scaled copy of given texture.</summary>
         * <param name="sourceRenderTexture">texture to scale</param>
         * <param name="width">Desired width</param>
         * <param name="height">Desired height</param>
         * <param name="filterMode">Filtering mode</param>
         */
        public static Texture2D ScaleRenderTextureAsCopy(RenderTexture sourceRenderTexture, int width, int height, FilterMode filterMode = FilterMode.Trilinear)
        {
            // save off the current render target
            var currentRenderTarget = RenderTexture.active;
            try
            {
                RenderTexture.active = sourceRenderTexture;

                var source = new Texture2D(sourceRenderTexture.width, sourceRenderTexture.height, TextureFormat.RGBA32, false, true);
                try
                {
                    source.ReadPixels(new Rect(0, 0, sourceRenderTexture.width, sourceRenderTexture.height), 0, 0, false);
                    source.Apply(false);

                    var renderTexture = GpuScale(source, width, height);
                    try
                    {
                        //Get rendered data back to a new texture
                        Rect textureRect = new(0, 0, width, height);
                        Texture2D result = new(width, height);
                        result.ReadPixels(textureRect, 0, 0, true);
                        //result.Apply(true); - don't need to copy this back to the GPU, we process it on the CPU
                        return result;
                    }
                    finally
                    {
                        RenderTexture.active = null;
                        Object.Destroy(renderTexture);
                    }
                }
                finally
                {
                    Object.Destroy(source);
                }
            }
            finally
            {
                // restore the prior render target
                RenderTexture.active = currentRenderTarget;
            }
        }

        // NTSC formula for grayscale
        public static readonly Color NTSC_Grayscale = new Color(0.299f, 0.587f, 0.114f);

        private static RenderTexture GpuScale(Texture2D source, int width, int height)
        {
            RenderTexture rtt = new(width, height, 32);
            RenderTexture.active = rtt;
            GL.LoadPixelMatrix(0, 1, 1, 0);
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            Graphics.DrawTexture(new Rect(0, 0, 1, 1), source);
            return rtt;
        }
    }
}
