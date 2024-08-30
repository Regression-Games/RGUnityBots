using System.Collections;
using UnityEngine;

namespace RegressionGames
{
    /**
     * <summary>
     * A simple animation that can be added to any UI component that also has a CanvasGroup
     * </summary>
     */
    public class RGFadeOutAnimation : MonoBehaviour
    {
        // duration of the fade out effect (in seconds)
        public float fadeDuration = 0.1f;
        
        private CanvasGroup _canvasGroup;
        
        private bool _isFading;
        
        private GameObject _destroyAtEnd;

        void Start()
        {
            _isFading = false;
            
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                Debug.LogError("RGFadeOutAnimation is missing its Canvas Group");
            }
        }

        /**
         * <summary>
         * Start this animation, and Destroy the GameObject (if provided) at the end of it
         * </summary>
         * <param name="destroyAtEnd">The GameObject to Destroy at the end of this animation (optional)</param>
         */
        public void StartFadeOut(GameObject destroyAtEnd = null)
        {
            if (!_isFading)
            {
                _destroyAtEnd = destroyAtEnd;
                StartCoroutine(FadeOutRoutine());
            }
        }

        /**
         * <summary>
         * A coroutine to run the fade out animation
         * </summary>
         */
        private IEnumerator FadeOutRoutine()
        {
            _isFading = true;
            float startAlpha = _canvasGroup.alpha;

            // fade out the CanvasGroup over fadeDuration 
            for (float t = 0; t < fadeDuration; t += Time.deltaTime)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, t / fadeDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0;
            _isFading = false;

            if (_destroyAtEnd != null)
            {
                Destroy(_destroyAtEnd);
            }
        }
    }
}