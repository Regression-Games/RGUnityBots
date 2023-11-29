using System.Collections.Concurrent;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace RegressionGames.RGBotConfigs
{
    // Only allow one of these on the Overlay game object
    // RGEntity does the enforcement of only looking for this on the RGOverlayMenu
    [DisallowMultipleComponent]
    public class RGAction_TouchWorldPosition : RGAction
    {
        private ConcurrentQueue<Vector3> _positionsToTouch = new();

        private Vector2? undoTouchAtPosition = null;
        public void Update()
        {
            // ONLY SUPPORTS 1 TOUCH PER FRAME
            
            var update = false;
            if (undoTouchAtPosition != null)
            {
                var screen = GetScreen();
                // end the active touch
                InputSystem.QueueStateEvent(screen, new TouchState
                {
                    // try avoid conflicts with real touchIds (not sure any screen supports 100 touches yet)
                    touchId = 111,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended,
                    position = (Vector2)undoTouchAtPosition,
                    delta = Vector2.zero, // no delta
                    pressure = 1, // full pressure
                    displayIndex = (byte)screen.displayIndex.value
                }, InputState.currentTime);

                update = true;
                undoTouchAtPosition = null;
            }
            
            // one click per frame update
            if (_positionsToTouch.TryDequeue(out Vector3 positionToTouch))
            {
                // this sends the event as though you actually clicked 
                // so you get click effects and everything

                var screenPosition = Camera.current.WorldToScreenPoint(positionToTouch);

                var screen = GetScreen();
                // stolen/adapted from Unity's `InputTestFixture.cs`
                InputSystem.QueueStateEvent(screen, new TouchState
                {
                    // try avoid conflicts with real touchIds (not sure any screen supports 100 touches yet)
                    touchId = 111,
                    phase = UnityEngine.InputSystem.TouchPhase.Began,
                    position = screenPosition,
                    delta = Vector2.zero, // no delta
                    pressure = 1, // full pressure
                    displayIndex = (byte)screen.displayIndex.value
                }, InputState.currentTime);

                undoTouchAtPosition = screenPosition;
                update = true;
            }

            if (update)
            {
                InputSystem.Update();
            }
        }

        private Touchscreen GetScreen()
        {
            var screen = Touchscreen.current;
            if (screen == null)
            {
                screen = InputSystem.AddDevice<Touchscreen>();
            }

            return screen;
        }

        public override string GetActionName()
        {
            return "TouchWorldPosition";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            if (input["x"] != null && input["y"] != null && input["z"] != null)
            {
                var x = (float)System.Convert.ToDecimal(input["x"]);
                var y = (float)System.Convert.ToDecimal(input["y"]);
                var z = (float)System.Convert.ToDecimal(input["z"]);
                _positionsToTouch.Enqueue(new Vector3(x,y,z));
            }
        }
    }

    public class RGActionRequest_TouchWorldPosition : RGActionRequest
    {
        public RGActionRequest_TouchWorldPosition(Vector3 position)
        {
            action = "TouchWorldPosition";
            Input = new()
            {
                { "x", position.x },
                { "y", position.y },
                { "z", position.z }
            };
        }
    }
}
