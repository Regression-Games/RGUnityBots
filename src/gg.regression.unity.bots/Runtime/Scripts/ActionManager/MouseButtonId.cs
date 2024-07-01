using System;
using UnityEngine;

namespace RegressionGames.ActionManager
{
    public enum MouseButtonId
    {
        LEFT_MOUSE_BUTTON,
        MIDDLE_MOUSE_BUTTON,
        RIGHT_MOUSE_BUTTON,
        FORWARD_MOUSE_BUTTON,
        BACK_MOUSE_BUTTON
    }

    public static class MouseButtonHelper
    {
        public static KeyCode ToKeyCode(this MouseButtonId mouseButton)
        {
            switch (mouseButton)
            {
                case MouseButtonId.LEFT_MOUSE_BUTTON:
                    return KeyCode.Mouse0;
                case MouseButtonId.MIDDLE_MOUSE_BUTTON:
                    return KeyCode.Mouse2;
                case MouseButtonId.RIGHT_MOUSE_BUTTON:
                    return KeyCode.Mouse1;
                case MouseButtonId.FORWARD_MOUSE_BUTTON:
                    return KeyCode.Mouse3;
                case MouseButtonId.BACK_MOUSE_BUTTON:
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
                    return MouseButtonId.LEFT_MOUSE_BUTTON;
                case KeyCode.Mouse1:
                    return MouseButtonId.RIGHT_MOUSE_BUTTON;
                case KeyCode.Mouse2:
                    return MouseButtonId.MIDDLE_MOUSE_BUTTON;
                case KeyCode.Mouse3:
                    return MouseButtonId.FORWARD_MOUSE_BUTTON;
                case KeyCode.Mouse4:
                    return MouseButtonId.BACK_MOUSE_BUTTON;
                default:
                    return null;
            }
        }
    }
}