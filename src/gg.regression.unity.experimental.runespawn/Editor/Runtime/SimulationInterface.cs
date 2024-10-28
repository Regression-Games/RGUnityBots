using UnityEngine;
using UnityEngine.InputSystem;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.Models;
using RegressionGames.RGLegacyInputUtility;

public static class SimulationInterface
{

    /// <summary>
    /// Initializes the simulation interface.
    /// </summary>
    /// <param name="monoBehaviour">The MonoBehaviour instance to use for coroutine execution.</param>
    public static void Initialize(MonoBehaviour monoBehaviour)
    {
        #if ENABLE_LEGACY_INPUT_MANAGER
        RGLegacyInputWrapper.StartSimulation(monoBehaviour);
        #endif
        KeyboardEventSender.Initialize();
        MouseEventSender.InitializeVirtualMouse();
    }

    /// <summary>
    /// Tears down the simulation interface.
    /// </summary>
    public static void TearDown(){
        KeyboardEventSender.TearDown();
        MouseEventSender.Reset();
        #if ENABLE_LEGACY_INPUT_MANAGER
        RGLegacyInputWrapper.StopSimulation();
        #endif
        RGUtils.TeardownOverrideEventSystem();
        RGUtils.RestoreInputSettings();
    }

    /// <summary>
    /// Simulates a mouse event at the screen space position corresponding to the given Transform's world position.
    /// 
    /// If the transform is not on screen, the mouse event will NOT be simulated, and a warning will be logged.
    /// </summary>
    /// <param name="mouseTarget">The Transform whose world position will be converted to screen space for the mouse event.</param>
    /// <param name="leftButton">Simulates left mouse button press if true.</param>
    /// <param name="middleButton">Simulates middle mouse button press if true.</param>
    /// <param name="rightButton">Simulates right mouse button press if true.</param>
    /// <param name="forwardButton">Simulates forward mouse button press if true.</param>
    /// <param name="backButton">Simulates back mouse button press if true.</param>
    /// <param name="scroll">Simulates mouse scroll wheel movement.</param>
    /// <example>
    /// Example 1: Simulate a right-click on a game object:
    /// <code>
    /// Transform gameObjectTransform = someGameObject.transform;
    /// MouseEventSender.SendMouseEvent(gameObjectTransform, rightButton: true);
    /// </code>
    /// Example 2: Simulate a middle-click on an object with scroll input:
    /// <code>
    /// Transform objectTransform = someObject.transform;
    /// MouseEventSender.SendMouseEvent(objectTransform, middleButton: true, forwardButton: true);
    /// </code>
    /// </example>
    /// Example 3: Simulate a mouse click, drag and release:
    /// <code>
    /// MouseEventSender.SendMouseEvent(startTransform, leftButton: true);
    /// yield return new WaitForSeconds(0.1f); // Wait for a few frames
    /// MouseEventSender.SendMouseEvent(endTransform, leftButton: true);
    /// yield return new WaitForSeconds(0.1f); // Wait for a few frames
    /// MouseEventSender.SendMouseEvent(endTransform, leftButton: false);
    /// </code>
    public static void SendMouseEvent(Transform mouseTarget,
                                      bool leftButton=false,
                                      bool middleButton=false,
                                      bool rightButton=false,
                                      bool forwardButton=false,
                                      bool backButton=false,
                                      Vector2 scroll = default)
    {
        Vector2 screenPosition;
        var boundsInfo = TransformObjectFinder.SelectBoundsForTransform(Camera.main, Screen.width, Screen.height, mouseTarget);
        var ssBounds = boundsInfo.Item1;
        if (ssBounds.HasValue)
        {
            screenPosition = ssBounds.Value.center;
            SendMouseEvent(screenPosition, leftButton, middleButton, rightButton, forwardButton, backButton, scroll);
        }
        else
        {
            RGDebug.LogWarning($"mouseTarget transform is not on screen: {mouseTarget.name}");
        }
    }

    /// <summary>
    /// Simulates a mouse event at the specified screen position. Bottom left is (0,0) and top right is (Screen.width, Screen.height).
    /// </summary>
    /// <param name="mouseScreenPosition">The screen coordinates where the mouse event will be simulated.</param>
    /// <param name="leftButton">Simulates left mouse button press if true.</param>
    /// <param name="middleButton">Simulates middle mouse button press if true.</param>
    /// <param name="rightButton">Simulates right mouse button press if true.</param>
    /// <param name="forwardButton">Simulates forward mouse button press if true.</param>
    /// <param name="backButton">Simulates back mouse button press if true.</param>
    /// <param name="scroll">Simulates mouse scroll wheel movement.</param>
    public static void SendMouseEvent(Vector2 mouseScreenPosition,
                                      bool leftButton=false,
                                      bool middleButton=false,
                                      bool rightButton=false,
                                      bool forwardButton=false,
                                      bool backButton=false,
                                      Vector2 scroll = default)
    {
        MouseEventSender.SendRawPositionMouseEvent(0, mouseScreenPosition, leftButton, middleButton, rightButton, forwardButton, backButton, scroll);
    }

    /// <summary>
    /// Simulates a key press or release event.
    /// </summary>
    /// <param name="key">The key to simulate. Use values from the UnityEngine.InputSystem.Key enum.</param>
    /// <param name="isDown">True to simulate a key press, false to simulate a key release.</param>
    /// <example>
    /// Example 1: Simulate pressing and releasing the 'W' key:
    /// <code>
    /// SendKeyEvent(Key.W, true);
    /// </code>
    /// 
    /// Example 2: Simulate holding 'Shift' and pressing 'A' in an async method:
    /// <code>
    /// SendKeyEvent(Key.LeftShift, true);
    /// SendKeyEvent(Key.A, true);
    /// yield return new WaitForSeconds(0.1f);
    /// SendKeyEvent(Key.A, false);
    /// SendKeyEvent(Key.LeftShift, false);
    /// </code>
    /// </example>
    public static void SendKeyEvent(Key key, bool isDown)
    {
        KeyState upOrDown = isDown ? KeyState.Down : KeyState.Up;
        int replaySegment = 0;
        KeyboardEventSender.SendKeyEvent(replaySegment, key, upOrDown);
    }
}