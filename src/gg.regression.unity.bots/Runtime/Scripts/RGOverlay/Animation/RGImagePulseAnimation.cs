using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A simple, looping animation that can be added to any UI component that has an Image
     * </summary>
     */
    public class RGImagePulseAnimation : MonoBehaviour
    {
        // how long should the increasing/decreasing phase of the animation last
        public float period = 1.0f;

        // the highest alpha value to use when increasing
        public float maxAlpha = 1.0f;

        // the lowest alpha value to use when decreasing
        public float minAlpha = 0f;

        // the image to apply the animation to
        public Image sourceImage;

        // is the alpha value increasing or decreasing
        private bool _isIncreasing;
        
        private Color _currentColor;

        void Start()
        {
            _isIncreasing = true;
            _currentColor = sourceImage.color;

            StartCoroutine(Pulse());
        }

        /**
         * <summary>
         * Will oscillate the sourceImage's alpha value up and down, between the minAlpha and maxAlpha fields
         * </summary>
         */
        private IEnumerator Pulse()
        {
            if (_isIncreasing)
            {
                var startAlpha = sourceImage.color.a;
                for (float t = 0; t < period; t += Time.deltaTime)
                {
                    _currentColor.a = Mathf.Lerp(startAlpha, maxAlpha, t / period);
                    sourceImage.color = _currentColor;
                    yield return null;
                }
                _isIncreasing = false;
            }
            else
            {
                var startAlpha = sourceImage.color.a;
                for (float t = 0; t < period; t += Time.deltaTime)
                {
                    _currentColor.a = Mathf.Lerp(startAlpha, minAlpha, t / period);
                    sourceImage.color = _currentColor;
                    yield return null;
                }
                _isIncreasing = true;
            }
            
            StartCoroutine(Pulse());
        }
    }
}