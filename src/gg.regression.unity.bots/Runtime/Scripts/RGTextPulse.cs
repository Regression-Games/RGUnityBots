using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace RegressionGames
{
    public class RGTextPulse : MonoBehaviour
    {
        private TextMeshProUGUI _textUI;

        [NonSerialized]
        private Color ogColor = Color.white;

        public float pulseInterval = 3f;

        [NonSerialized]
        private float ogPulseInterval = 3f;

        public float pulseDuration = 1f;

        [FormerlySerializedAs("maxAlpha")]
        [Tooltip("Pulse midpoint alpha value")]
        public float pulseMidpointAlpha = 255f;

        [FormerlySerializedAs("minAlpha")]
        [Tooltip("Pulse start/end alpha value")]
        public float pulseStartEndAlpha = 0f;

        [Tooltip("Alpha value when animation is stopped")]
        public float stoppedAlpha = 0f;

        private float ogStoppedAlpha = 0f;

        private float _lastPulse = -1f;

        [Tooltip("Is this actively pulsing?")]
        public bool active = true;

        public void SetColor(Color? color = null)
        {
            if (color == null)
            {
                // reset
                _textUI.color = new Color(ogColor.r, ogColor.g, ogColor.b, _textUI.color.a);
            }
            else
            {
                var theColor = color.Value;
                _textUI.color = new Color(theColor.r, theColor.g, theColor.b, _textUI.color.a);
            }
        }

        public void StopAtStartEndAlpha()
        {
            active = false;
            stoppedAlpha = pulseStartEndAlpha;
        }

        public void StopAtMidAlpha()
        {
            active = false;
            stoppedAlpha = pulseMidpointAlpha;
        }

        public void VeryFast()
        {
            active = true;
            pulseInterval = ogPulseInterval / 4.0f;
            stoppedAlpha = ogStoppedAlpha;
        }

        public void Fast()
        {
            active = true;
            pulseInterval = ogPulseInterval / 2.0f;
            stoppedAlpha = ogStoppedAlpha;
        }

        public void Normal()
        {
            active = true;
            pulseInterval = ogPulseInterval;
            stoppedAlpha = ogStoppedAlpha;
        }

        public void Slow()
        {
            active = true;
            pulseInterval = ogPulseInterval * 2.0f;
            stoppedAlpha = ogStoppedAlpha;
        }

        public void VerySlow()
        {
            active = true;
            pulseInterval = ogPulseInterval * 4.0f;
            stoppedAlpha = ogStoppedAlpha;
        }

        public void Stop()
        {
            active = false;
            stoppedAlpha = ogStoppedAlpha;
        }

        void Start()
        {
            if (pulseDuration > pulseInterval)
            {
                pulseInterval = pulseDuration;
            }
            // start in 1 second
            _lastPulse = -1f * pulseInterval + 1f;
            _textUI = GetComponent<TextMeshProUGUI>();
            ogPulseInterval = pulseInterval;
            ogColor = _textUI.color;
            ogStoppedAlpha = stoppedAlpha;
        }

        void LateUpdate()
        {
            if (active)
            {
                var time = Time.time;
                var timeSinceLast = time - _lastPulse;

                // in between pulses we are 'stopped'
                var alphaValue = pulseStartEndAlpha / 255f;

                if (timeSinceLast >= pulseInterval)
                {
                    // time for the next pulse
                    _lastPulse = time;
                    timeSinceLast -= pulseInterval;
                }

                if (timeSinceLast <= pulseDuration)
                {
                    // animating the pulse

                    var pulseRange = pulseMidpointAlpha - pulseStartEndAlpha;

                    var halfPulseTime = pulseDuration / 2;

                    var newValue = (float) (-1 * pulseRange / Math.Pow(halfPulseTime,2) * Math.Pow(timeSinceLast, 2)) + 2.0f*pulseRange/halfPulseTime  * timeSinceLast + pulseStartEndAlpha;

                    alphaValue = newValue / 255f;
                }
                // else in between pulses

                _textUI.color = new Color(_textUI.color.r, _textUI.color.g, _textUI.color.b, alphaValue);
            }
            else
            {
                // not animating
                var alphaValue = stoppedAlpha / 255f;
                _textUI.color = new Color(_textUI.color.r, _textUI.color.g, _textUI.color.b, alphaValue);
            }
        }
    }
}
