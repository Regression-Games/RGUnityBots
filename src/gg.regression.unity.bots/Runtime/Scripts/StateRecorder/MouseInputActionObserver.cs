using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{

    public class MouseInputActionDataJsonConverter : JsonConverter<MouseInputActionData>
    {
        public override void WriteJson(JsonWriter writer, MouseInputActionData value, JsonSerializer serializer)
        {
            writer.WriteRawValue(value.ToJson());
        }

        public override bool CanRead => false;

        public override MouseInputActionData ReadJson(JsonReader reader, Type objectType, MouseInputActionData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [JsonConverter(typeof(MouseInputActionDataJsonConverter))]
    public class MouseInputActionData
    {
        public double startTime;

        public Vector2Int position;

        // non-fractional pixel accuracy
        //main 5 buttons
        public bool leftButton;
        public bool middleButton;
        public bool rightButton;
        public bool forwardButton;
        public bool backButton;

        // scroll wheel
        public Vector2 scroll;

        [JsonIgnore]
        public bool IsButtonHeld => leftButton || middleButton || rightButton || forwardButton || backButton ||
                                    scroll.y < -0.1f || scroll.y > 0.1f || scroll.x < -0.1f || scroll.x > 0.1f;

        public bool newButtonPress;

        public bool PositionsEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return (previous.position.x) == (this.position.x)
                       && (previous.position.y) == (this.position.y);
            }

            return false;
        }

        public bool ButtonStatesEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return previous.leftButton == this.leftButton
                       && previous.middleButton == this.middleButton
                       && previous.rightButton == this.rightButton
                       && previous.forwardButton == this.forwardButton
                       && previous.backButton == this.backButton
                       && Math.Abs(previous.scroll.y - this.scroll.y) < 0.1f
                       && Math.Abs(previous.scroll.x - this.scroll.x) < 0.1f;
            }

            return false;
        }

        public static bool NewButtonPressed(MouseInputActionData previous, MouseInputActionData current)
        {
            return !previous.leftButton && current.leftButton
                   || !previous.middleButton && current.middleButton
                   || !previous.rightButton && current.rightButton
                   || !previous.forwardButton && current.forwardButton
                   || !previous.backButton && current.backButton
                   || Math.Abs(previous.scroll.y - current.scroll.y) > 0.1f
                   || Math.Abs(previous.scroll.x - current.scroll.x) > 0.1f;
        }

        public string ToJson()
        {
            return "{\"startTime\":" + startTime
                                     + ",\"position\":" + VectorIntJsonConverter.ToJsonString(position)
                                     + ",\"leftButton\":" + (leftButton ? "true" : "false")
                                     + ",\"middleButton\":" + (middleButton ? "true" : "false")
                                     + ",\"rightButton\":" + (rightButton ? "true" : "false")
                                     + ",\"forwardButton\":" + (forwardButton ? "true" : "false")
                                     + ",\"backButton\":" + (backButton ? "true" : "false")
                                     + ",\"scroll\":" + VectorJsonConverter.ToJsonStringVector2(scroll)
                                     + ",\"newButtonPress\":" + (newButtonPress ? "true" : "false")
                                     + "}";
        }
    }

    public class MouseInputActionObserver : MonoBehaviour
    {
        private MouseInputActionData _priorMouseState;

        private static MouseInputActionObserver _this;

        private bool _recording;


        public static MouseInputActionObserver GetInstance()
        {
            return _this;
        }

        public void Awake()
        {
            if (_this != null)
            {
                // only allow 1 of these to be alive
                if (_this.gameObject != gameObject)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // keep this thing alive across scenes
            DontDestroyOnLoad(gameObject);
            _this = this;
        }

        public void StartRecording()
        {
            _recording = true;
        }

        public void StopRecording()
        {
            _recording = false;
        }

        private void Update()
        {
            if (_recording)
            {
                var newMouseState = GetCurrentMouseState();
                if (newMouseState != null)
                {
                    var time = Time.unscaledTime;
                    if (newMouseState.IsButtonHeld)
                    {
                        _mouseInputActions.Enqueue(newMouseState);
                    }
                    else if (_priorMouseState != null && !(_priorMouseState.ButtonStatesEqual(newMouseState) && _priorMouseState.PositionsEqual(newMouseState)) )
                    {
                        // are new mouse buttons are clicked
                        newMouseState.newButtonPress = MouseInputActionData.NewButtonPressed(_priorMouseState, newMouseState);
                        _mouseInputActions.Enqueue(newMouseState);
                    }

                    _priorMouseState = newMouseState;
                }
            }
        }

        public MouseInputActionData GetCurrentMouseState()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var position = mouse.position.ReadValue();
                var newMouseState = new MouseInputActionData()
                {
                    startTime = Time.unscaledTimeAsDouble,
                    position = new Vector2Int((int)position.x, (int)position.y),
                    leftButton = mouse.leftButton.isPressed,
                    middleButton = mouse.middleButton.isPressed,
                    rightButton = mouse.rightButton.isPressed,
                    forwardButton = mouse.forwardButton.isPressed,
                    backButton = mouse.backButton.isPressed,
                    scroll = mouse.scroll.ReadValue(),
                    newButtonPress = false
                };
                return newMouseState;
            }

            return null;
        }

        private readonly ConcurrentQueue<MouseInputActionData> _mouseInputActions = new();

        public List<MouseInputActionData> FlushInputDataBuffer(bool ensureOne = false)
        {
            List<MouseInputActionData> result = new();
            while (_mouseInputActions.TryPeek(out var action))
            {
                _mouseInputActions.TryDequeue(out _);
                result.Add(action);
            }

            if (ensureOne && result.Count == 0 && _priorMouseState != null)
            {
                // or.. we need at least 1 mouse observation per tick, otherwise hover over effects/etc don't function correctly
                result.Add(_priorMouseState);
            }

            return result;
        }
    }
}
