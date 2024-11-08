using System.Collections.Generic;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RegressionGames.ActionManager
{
    /// <summary>
    /// This class represents a device input that is needed in order to perform
    /// an associated action instance.
    /// </summary>
    public abstract class RGActionInput
    {
        /// <summary>
        /// Simulate the input onto the game.
        /// </summary>
        /// <param name="segmentNumber">The bot segmentNumber</param>
        public abstract void Perform(int segmentNumber);

        /// <summary>
        /// Returns whether this input affects the same part of the device as another.
        ///
        /// This is used to decide whether two actions can be performed simultaneously:
        /// if two actions affect the same part of the input device then they should NOT be performed together.
        /// </summary>
        public abstract bool Overlaps(RGActionInput other);
    }

    public static class RGActionInputHelper
    {
        /// <summary>
        /// Helper method to check if two sets of action input lists have any overlap.
        /// </summary>
        public static bool Overlap(this IEnumerable<RGActionInput> inputListA, IEnumerable<RGActionInput> inputListB)
        {
            foreach (var inpA in inputListA)
            {
                foreach (var inpB in inputListB)
                {
                    if (inpA.Overlaps(inpB))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public class LegacyKeyInput : RGActionInput
    {
        public readonly KeyCode KeyCode;
        public readonly bool IsPressed;

        public LegacyKeyInput(KeyCode keyCode, bool isPressed)
        {
            KeyCode = keyCode;
            IsPressed = isPressed;
        }

        public override void Perform(int segmentNumber)
        {
            RGActionManager.SimulateKeyState(segmentNumber, KeyCode, IsPressed);
        }

        public override bool Overlaps(RGActionInput other)
        {
            if (other is LegacyKeyInput keyInput)
            {
                return KeyCode == keyInput.KeyCode;
            } else if (other is InputSystemKeyInput inputSysKeyInput)
            {
                return KeyCode == RGLegacyInputUtils.InputSystemKeyToKeyCode(inputSysKeyInput.Key);
            } else if (other is MouseButtonInput mouseButtonInput)
            {
                return KeyCode >= KeyCode.Mouse0 && KeyCode <= KeyCode.Mouse6 &&
                       KeyCode - KeyCode.Mouse0 == mouseButtonInput.MouseButton;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            return "LegacyKeyInput(" + KeyCode + ", " + IsPressed + ")";
        }
    }

    public class InputSystemKeyInput : RGActionInput
    {
        public readonly Key Key;
        public readonly bool IsPressed;

        public InputSystemKeyInput(Key key, bool isPressed)
        {
            Key = key;
            IsPressed = isPressed;
        }

        public override void Perform(int segmentNumber)
        {
            RGActionManager.SimulateKeyState(segmentNumber, Key, IsPressed);
        }

        public override bool Overlaps(RGActionInput other)
        {
            if (other is LegacyKeyInput keyInput)
            {
                return !(keyInput.KeyCode >= KeyCode.Mouse0 && keyInput.KeyCode <= KeyCode.Mouse6)
                       && Key == RGLegacyInputUtils.KeyCodeToInputSystemKey(keyInput.KeyCode);
            } else if (other is InputSystemKeyInput inputSysKeyInput)
            {
                return Key == inputSysKeyInput.Key;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            return "InputSystemKeyInput(" + Key + ", " + IsPressed + ")";
        }
    }

    public class MousePositionInput : RGActionInput
    {
        public readonly Vector2 MousePosition;

        public MousePositionInput(Vector2 mousePosition)
        {
            MousePosition = mousePosition;
        }

        public override void Perform(int segmentNumber)
        {
            RGActionManager.SimulateMouseMovement(segmentNumber, MousePosition);
        }

        public override bool Overlaps(RGActionInput other)
        {
            return other is MousePositionInput || other is MousePositionDeltaInput;
        }

        public override string ToString()
        {
            return "MousePositionInput(" + MousePosition + ")";
        }
    }

    public class MousePositionDeltaInput : RGActionInput
    {
        public readonly Vector2 MousePositionDelta;

        public MousePositionDeltaInput(Vector2 mousePositionDelta)
        {
            MousePositionDelta = mousePositionDelta;
        }

        public override void Perform(int segmentNumber)
        {
            RGActionManager.SimulateMouseMovementDelta(segmentNumber, MousePositionDelta);
        }

        public override bool Overlaps(RGActionInput other)
        {
            return other is MousePositionInput || other is MousePositionDeltaInput;
        }

        public override string ToString()
        {
            return "MousePositionDeltaInput(" + MousePositionDelta + ")";
        }
    }

    public class MouseScrollInput : RGActionInput
    {
        public readonly Vector2 MouseScroll;

        public MouseScrollInput(Vector2 mouseScroll)
        {
            MouseScroll = mouseScroll;
        }

        public override void Perform(int segmentNumber)
        {
            RGActionManager.SimulateMouseScroll(segmentNumber, MouseScroll);
        }

        public override bool Overlaps(RGActionInput other)
        {
            return other is MouseScrollInput;
        }

        public override string ToString()
        {
            return "MouseScrollInput(" + MouseScroll + ")";
        }
    }

    public class MouseButtonInput : RGActionInput
    {
        public const int LEFT_MOUSE_BUTTON = 0;
        public const int RIGHT_MOUSE_BUTTON = 1;
        public const int MIDDLE_MOUSE_BUTTON = 2;
        public const int FORWARD_MOUSE_BUTTON = 3;
        public const int BACK_MOUSE_BUTTON = 4;

        public readonly int MouseButton;
        public readonly bool IsPressed;

        public MouseButtonInput(int mouseButton, bool isPressed)
        {
            MouseButton = mouseButton;
            IsPressed = isPressed;
        }

        public override void Perform(int segmentNumber)
        {
            RGActionManager.SimulateMouseButton(segmentNumber, MouseButton, IsPressed);
        }

        public override bool Overlaps(RGActionInput other)
        {
            if (other is LegacyKeyInput keyInput)
            {
                return keyInput.KeyCode >= KeyCode.Mouse0 && keyInput.KeyCode <= KeyCode.Mouse6
                    && keyInput.KeyCode - KeyCode.Mouse0 == MouseButton;
            } else if (other is MouseButtonInput mouseButtonInput)
            {
                return MouseButton == mouseButtonInput.MouseButton;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            return "MouseButtonInput(" + MouseButton + ", " + IsPressed + ")";
        }
    }
}
