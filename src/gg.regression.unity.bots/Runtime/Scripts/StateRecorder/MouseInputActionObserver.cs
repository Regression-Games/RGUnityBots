using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StateRecorder
{
    
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class MouseInputActionData : InputActionData
    {
        // non-fractional pixel accuracy
        public int[] position;
        public bool[] leftMiddleRightForwardBackButton;
        // scroll wheel
        public bool[] scrollDownUpLeftRight;
        [JsonIgnore]
        public bool IsButtonHeld => leftMiddleRightForwardBackButton[0] || leftMiddleRightForwardBackButton[1] || leftMiddleRightForwardBackButton[2] || leftMiddleRightForwardBackButton[3] || leftMiddleRightForwardBackButton[4] ||
                                    scrollDownUpLeftRight[0] || scrollDownUpLeftRight[1] || scrollDownUpLeftRight[2] || scrollDownUpLeftRight[3];

        [JsonIgnore]
        public bool NewButtonPress;
        
        public bool PositionsEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return (previous.position[0]) == (this.position[0])
                    && (previous.position[1]) == (this.position[1]);
            }

            return false;
        }
        
        public bool ButtonStatesEqual(object obj)
        {
            if (obj is MouseInputActionData previous)
            {
                return previous.leftMiddleRightForwardBackButton[0] == this.leftMiddleRightForwardBackButton[0]
                       && previous.leftMiddleRightForwardBackButton[1] == this.leftMiddleRightForwardBackButton[1]
                       && previous.leftMiddleRightForwardBackButton[2] == this.leftMiddleRightForwardBackButton[2]
                       && previous.leftMiddleRightForwardBackButton[3] == this.leftMiddleRightForwardBackButton[3]
                       && previous.leftMiddleRightForwardBackButton[4] == this.leftMiddleRightForwardBackButton[4]
                       && previous.scrollDownUpLeftRight[0] == this.scrollDownUpLeftRight[0]
                       && previous.scrollDownUpLeftRight[1] == this.scrollDownUpLeftRight[1]
                       && previous.scrollDownUpLeftRight[2] == this.scrollDownUpLeftRight[2]
                       && previous.scrollDownUpLeftRight[3] == this.scrollDownUpLeftRight[3];
            }

            return false;
        }

        public static bool NewButtonPressed(MouseInputActionData previous, MouseInputActionData current)
        {
            return !previous.leftMiddleRightForwardBackButton[0] && current.leftMiddleRightForwardBackButton[0]
                   || !previous.leftMiddleRightForwardBackButton[1] && current.leftMiddleRightForwardBackButton[1]
                   || !previous.leftMiddleRightForwardBackButton[2] && current.leftMiddleRightForwardBackButton[2]
                   || !previous.leftMiddleRightForwardBackButton[3] && current.leftMiddleRightForwardBackButton[3]
                   || !previous.leftMiddleRightForwardBackButton[4] && current.leftMiddleRightForwardBackButton[4]
                   || !previous.scrollDownUpLeftRight[0] && current.scrollDownUpLeftRight[0]
                   || !previous.scrollDownUpLeftRight[1] && current.scrollDownUpLeftRight[1]
                   || !previous.scrollDownUpLeftRight[2] && current.scrollDownUpLeftRight[2]
                   || !previous.scrollDownUpLeftRight[3] && current.scrollDownUpLeftRight[3];
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
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var scroll = mouse.scroll.ReadValue();
                    var position = mouse.position.ReadValue();
                    var newMouseState = new MouseInputActionData()
                    {
                        startTime = Time.unscaledTimeAsDouble,
                        position = new [] { (int)position.x, (int)position.y},
                        leftMiddleRightForwardBackButton = new []
                        {
                            mouse.leftButton.isPressed, 
                            mouse.middleButton.isPressed, 
                            mouse.rightButton.isPressed, 
                            mouse.forwardButton.isPressed,
                            mouse.backButton.isPressed
                        },
                        scrollDownUpLeftRight = new []
                        {
                            scroll.y<0, 
                            scroll.y>0,
                            scroll.x<0, 
                            scroll.x>0
                        },
                        NewButtonPress = false
                    };

                    if (_priorMouseState == null)
                    {
                        // our first mouse state observation
                        _completedInputActions.Enqueue(newMouseState);
                    }
                    else
                    {
                        if (!_priorMouseState.ButtonStatesEqual(newMouseState))
                        {
                            newMouseState.NewButtonPress = MouseInputActionData.NewButtonPressed(_priorMouseState, newMouseState);
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

        private readonly ConcurrentQueue<InputActionData> _completedInputActions = new();
        
        public List<InputActionData> FlushInputDataBuffer()
        {
            List<InputActionData> result = new();
            while (_completedInputActions.TryDequeue(out var completedAction))
            {
                result.Add(completedAction);
            }
            
            return result;
        }
    }
}
