using System;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public enum MouseButtonId
    {
        LeftMouseButton,
        MiddleMouseButton,
        RightMouseButton,
        ForwardMouseButton,
        BackMouseButton
    }

    public static class MouseButtonHelper
    {
        public static KeyCode ToKeyCode(this MouseButtonId mouseButton)
        {
            switch (mouseButton)
            {
                case MouseButtonId.LeftMouseButton:
                    return KeyCode.Mouse0;
                case MouseButtonId.MiddleMouseButton:
                    return KeyCode.Mouse2;
                case MouseButtonId.RightMouseButton:
                    return KeyCode.Mouse1;
                case MouseButtonId.ForwardMouseButton:
                    return KeyCode.Mouse3;
                case MouseButtonId.BackMouseButton:
                    return KeyCode.Mouse4;
                default:
                    throw new ArgumentException();
            }
        }

        public static MouseButtonId? FromKeyCode(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Mouse0:
                    return MouseButtonId.LeftMouseButton;
                case KeyCode.Mouse1:
                    return MouseButtonId.RightMouseButton;
                case KeyCode.Mouse2:
                    return MouseButtonId.MiddleMouseButton;
                case KeyCode.Mouse3:
                    return MouseButtonId.ForwardMouseButton;
                case KeyCode.Mouse4:
                    return MouseButtonId.BackMouseButton;
                default:
                    return null;
            }
        }
    }
}