using UnityEngine;
using UnityEngine.UI;

namespace RegressionGames
{
    public class RGIconPulse : MonoBehaviour
    {
        private Image _image;

        public float pulseInterval = 3f;

        public float ogPulseInterval = 3f;

        public float pulseDuration = 1f;

        [Tooltip("Maximum alpha value")]
        public float maxAlpha = 255f;
        
        [Tooltip("Minimum alpha value")]
        public float minAlpha = 0f;

        [Tooltip("Alpha value when animation is stopped")]
        public float stoppedAlpha = 0f;

        private float _lastPulse = -1f;

        [Tooltip("Is this actively pulsing?")]
        public bool active = true;

        public void Fast()
        {
            active = true;
            pulseInterval = ogPulseInterval / 2;
        }

        public void Normal()
        {
            active = true;
            pulseInterval = ogPulseInterval;
        }

        public void Slow()
        {
            active = true;
            pulseInterval = ogPulseInterval * 2;
        }

        public void Stop()
        {
            active = false;
        }

        void Start()
        {
            if (pulseDuration > pulseInterval)
            {
                pulseInterval = pulseDuration;
            }
            // start in 1 second
            _lastPulse = -1f * pulseInterval + 1f;
            _image = GetComponent<Image>();
            ogPulseInterval = pulseInterval;
        }

        void LateUpdate()
        {
            if (active)
            {
                // animating
                var time = Time.time;
                var timeSinceLast = time - _lastPulse;

                if (timeSinceLast > pulseInterval)
                {
                    _lastPulse = time;
                }

                var halfAlpha = (maxAlpha-minAlpha) / 2.0f;

                // -1 -> 1
                var rangeVal = (timeSinceLast / pulseDuration) * 2 - 1;

                var alphaValue = (minAlpha+(Mathf.Abs(rangeVal) * -1 * halfAlpha + halfAlpha)) / 255f;

                _image.color = new Color(_image.color.r, _image.color.g, _image.color.b, alphaValue);
            }
            else
            {
                // not animating
                var alphaValue = stoppedAlpha;
                _image.color = new Color(_image.color.r, _image.color.g, _image.color.b, alphaValue);
            }
        }
    }
}
