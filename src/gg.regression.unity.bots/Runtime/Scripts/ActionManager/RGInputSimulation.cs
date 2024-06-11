
using System.Linq;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace RegressionGames.ActionManager
{
    /**
     * Unified API for simulating keyboard and mouse inputs for
     * all the input systems that are present.
     */
    public static class RGInputSimulation
    {
        public static void StartSimulation(MonoBehaviour context)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StartSimulation(context);
            #endif
        }

        public static void SimulateKey(RGKeyCode keyCode, bool isPressed)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            if (isPressed)
            {
                RGLegacyInputWrapper.SimulateKeyPress(keyCode.ToLegacyKeyCode());
            }
            else
            {
                RGLegacyInputWrapper.SimulateKeyRelease(keyCode.ToLegacyKeyCode());
            }
            #endif
            #if ENABLE_INPUT_SYSTEM
            {
                var keyboard = Keyboard.current;
                using (DeltaStateEvent.From(keyboard, out var eventPtr))
                {
                    var key = keyCode.ToInputSystemKeyCode();
                    var inputControl = keyboard.allControls
                        .FirstOrDefault(a => a is KeyControl kc && kc.keyCode == key) ?? keyboard.anyKey;
                    if (inputControl != null)
                    {
                        inputControl.WriteValueIntoEvent(isPressed ? 1f : 0f, eventPtr);
                        InputSystem.QueueEvent(eventPtr);
                    }
                    else
                    {
                        RGDebug.LogWarning($"Failed to find input control for key {key}");
                    }
                }
            }
            #endif
        }

        public static void SimulateMouseMovement(Vector2 newMousePosition, Vector2? newMouseDelta = null)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            {
                Vector3 legNewMousePos = new Vector3(newMousePosition.x, newMousePosition.y, 0.0f);
                Vector3? legNewMouseDelta = newMouseDelta.HasValue
                    ? new Vector3(newMouseDelta.Value.x, newMouseDelta.Value.y, 0.0f)
                    : null;
                RGLegacyInputWrapper.SimulateMouseMovement(legNewMousePos, legNewMouseDelta);
            }
            #endif
            #if ENABLE_INPUT_SYSTEM
            {
                
            }
            #endif
        }

        public static void SimulateMouseScrollWheel(Vector2 newMouseScrollDelta)
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.SimulateMouseScrollWheel(newMouseScrollDelta);
            #endif
            #if ENABLE_INPUT_SYSTEM
            
            #endif
        }
        
        public static void StopSimulation()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            RGLegacyInputWrapper.StopSimulation();
            #endif
        }
    }
}