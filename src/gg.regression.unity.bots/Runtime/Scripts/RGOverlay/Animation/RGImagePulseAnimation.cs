using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A simple animation that can be added to any UI component that also has a CanvasGroup
     * </summary>
     */
    public class RGImagePulseAnimation : MonoBehaviour
    {
        public float period = 1.0f;

        public float maxOpacity = 1.0f;

        public float minOpacity = 0f;

        public Image sourceImage;

        private bool _isIncreasing;
        
        private Color _currentColor;

        void Start()
        {
            _isIncreasing = true;
            _currentColor = sourceImage.color;
            
            StartCoroutine(Pulse());
        }

        private IEnumerator Pulse()
        {
            if (_isIncreasing)
            {
                float startAlpha = sourceImage.color.a;
                for (float t = 0; t < period; t += Time.deltaTime)
                {
                    _currentColor.a = Mathf.Lerp(startAlpha, maxOpacity, t / period);
                    sourceImage.color = _currentColor;
                    yield return null;
                }
                _isIncreasing = false;
            }
            else
            {
                float startAlpha = sourceImage.color.a;
                for (float t = 0; t < period; t += Time.deltaTime)
                {
                    _currentColor.a = Mathf.Lerp(startAlpha, minOpacity, t / period);
                    sourceImage.color = _currentColor;
                    yield return null;
                }
                _isIncreasing = true;
            }
            
            StartCoroutine(Pulse());
        }
    }
}