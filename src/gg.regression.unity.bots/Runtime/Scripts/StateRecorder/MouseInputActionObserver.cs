using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.StateRecorder
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
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
                    if (_priorMouseState == null)
                    {
                        // our first mouse state observation
                        _completedInputActions.Enqueue(newMouseState);
                    }
                    else
                    {
                        if (!_priorMouseState.ButtonStatesEqual(newMouseState))
                        {
                            newMouseState.newButtonPress = MouseInputActionData.NewButtonPressed(_priorMouseState, newMouseState);
                            // different mouse buttons are clicked
                            _completedInputActions.Enqueue(newMouseState);
                        }
                        else if (newMouseState.IsButtonHeld && !_priorMouseState.PositionsEqual(newMouseState))
                        {
                            // mouse buttons are held and the mouse moved (click-drag)
                            _completedInputActions.Enqueue(newMouseState);
                        }
                        // the case where buttons are released is handled by the !ButtonStatesEqual check at the start of this if/else chain
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

        private readonly ConcurrentQueue<MouseInputActionData> _completedInputActions = new();

        public List<MouseInputActionData> FlushInputDataBuffer()
        {
            List<MouseInputActionData> result = new();
            while (_completedInputActions.TryDequeue(out var completedAction))
            {
                result.Add(completedAction);
            }

            return result;
        }
    }
}
