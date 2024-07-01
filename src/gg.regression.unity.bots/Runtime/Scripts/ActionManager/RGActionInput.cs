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
        public abstract void Perform();

        /// <summary>
        /// Returns whether this input affects the same part of the device as another.
        /// 
        /// This is used to decide whether two actions can be performed simultaneously:
        /// if two actions affect the same part of the input device then they should NOT be performed together.
        /// </summary>
        public abstract bool Overlaps(RGActionInput other);
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
        
        public override void Perform()
        {
            RGActionManager.SimulateKeyState(KeyCode, IsPressed);
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
                var mb = MouseButtonHelper.FromKeyCode(KeyCode);
                return mb.HasValue && mb.Value == mouseButtonInput.MouseButton;
            }
            else
            {
                return false;
            }
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
        
        public override void Perform()
        {
            RGActionManager.SimulateKeyState(Key, IsPressed);
        }

        public override bool Overlaps(RGActionInput other)
        {
            if (other is LegacyKeyInput keyInput)
            {
                if (MouseButtonHelper.FromKeyCode(keyInput.KeyCode).HasValue)
                {
                    return false;
                }
                else
                {
                    return Key == RGLegacyInputUtils.KeyCodeToInputSystemKey(keyInput.KeyCode);
                }
            } else if (other is InputSystemKeyInput inputSysKeyInput)
            {
                return Key == inputSysKeyInput.Key;
            }
            else
            {
                return false;
            }
        }
    }

    public class MousePositionInput : RGActionInput
    {
        public readonly Vector2 MousePosition;

        public MousePositionInput(Vector2 mousePosition)
        {
            MousePosition = mousePosition;
        }
        
        public override void Perform()
        {
            RGActionManager.SimulateMouseMovement(MousePosition);
        }

        public override bool Overlaps(RGActionInput other)
        {
            return other is MousePositionInput || other is MousePositionDeltaInput;
        }
    }

    public class MousePositionDeltaInput : RGActionInput
    {
        public readonly Vector2 MousePositionDelta;

        public MousePositionDeltaInput(Vector2 mousePositionDelta)
        {
            MousePositionDelta = mousePositionDelta;
        }
        
        public override void Perform()
        {
            RGActionManager.SimulateMouseMovementDelta(MousePositionDelta);
        }

        public override bool Overlaps(RGActionInput other)
        {
            return other is MousePositionInput || other is MousePositionDeltaInput;
        }
    }

    public class MouseScrollInput : RGActionInput
    {
        public readonly Vector2 MouseScroll;

        public MouseScrollInput(Vector2 mouseScroll)
        {
            MouseScroll = mouseScroll;
        }
        
        public override void Perform()
        {
            RGActionManager.SimulateMouseScroll(MouseScroll);
        }

        public override bool Overlaps(RGActionInput other)
        {
            return other is MouseScrollInput;
        }
    }

    public class MouseButtonInput : RGActionInput
    {
        public readonly MouseButtonId MouseButton;
        public readonly bool IsPressed;

        public MouseButtonInput(MouseButtonId mouseButton, bool isPressed)
        {
            MouseButton = mouseButton;
            IsPressed = isPressed;
        }
        
        public override void Perform()
        {
            RGActionManager.SimulateMouseButton(MouseButton, IsPressed);
        }

        public override bool Overlaps(RGActionInput other)
        {
            if (other is LegacyKeyInput keyInput)
            {
                var mb = MouseButtonHelper.FromKeyCode(keyInput.KeyCode);
                return mb.HasValue && mb.Value == MouseButton;
            } else if (other is MouseButtonInput mouseButtonInput)
            {
                return MouseButton == mouseButtonInput.MouseButton;
            }
            else
            {
                return false;
            }
        }
    }
}