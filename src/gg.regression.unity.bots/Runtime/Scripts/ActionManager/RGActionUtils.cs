
using System;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RegressionGames.ActionManager
{
    public static class RGActionUtils
    {
        public static void SimulateLegacyKeyState(KeyCode keyCode, bool isPressed)
        {
            if (keyCode >= KeyCode.Mouse0 && keyCode <= KeyCode.Mouse6)
            {
                bool leftButton = keyCode == KeyCode.Mouse0 ? isPressed : RGLegacyInputWrapper.GetKey(KeyCode.Mouse0);
                bool middleButton = keyCode == KeyCode.Mouse2 ? isPressed : RGLegacyInputWrapper.GetKey(KeyCode.Mouse2);
                bool rightButton = keyCode == KeyCode.Mouse1 ? isPressed : RGLegacyInputWrapper.GetKey(KeyCode.Mouse1);
                bool forwardButton =
                    keyCode == KeyCode.Mouse3 ? isPressed : RGLegacyInputWrapper.GetKey(KeyCode.Mouse3);
                bool backButton =
                    keyCode == KeyCode.Mouse4 ? isPressed : RGLegacyInputWrapper.GetKey(KeyCode.Mouse4);
                MouseEventSender.SendRawPositionMouseEvent(0,
                    RGLegacyInputWrapper.mousePosition,
                    leftButton: leftButton, middleButton: middleButton, rightButton: rightButton,
                    forwardButton: forwardButton, backButton: backButton);
            }
            else
            {
                Key key = RGLegacyInputUtils.KeyCodeToInputSystemKey(keyCode);
                if (key != Key.None)
                {
                    KeyControl control = Keyboard.current[key];
                    KeyboardInputActionData data = new KeyboardInputActionData()
                    {
                        action = control.name,
                        binding = control.path,
                        startTime = Time.unscaledTime,
                        endTime = isPressed ? null : Time.unscaledTime
                    };
                    KeyboardEventSender.SendKeyEvent(0, data, isPressed ? KeyState.Down : KeyState.Up);
                }
            }
        }
    }
}