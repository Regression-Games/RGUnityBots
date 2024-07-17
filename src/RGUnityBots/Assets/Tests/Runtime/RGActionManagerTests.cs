using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RegressionGames;
using RegressionGames.ActionManager;
using RegressionGames.ActionManager.Actions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class RGActionManagerTests : InputTestFixture
    {
        [Test]
        public void TestValueRanges()
        {
            {
                RGBoolRange rng = new RGBoolRange(false, true);
                Assert.IsFalse((bool)rng.MinValue);
                Assert.IsTrue((bool)rng.MaxValue);
                Assert.AreEqual(rng.NumValues, 2);
                Assert.IsFalse((bool)rng[0]);
                Assert.IsTrue((bool)rng[1]);
            }
            {
                RGBoolRange rng = new RGBoolRange(true, true);
                Assert.IsTrue((bool)rng.MinValue);
                Assert.IsTrue((bool)rng.MaxValue);
                Assert.AreEqual(rng.NumValues, 1);
                Assert.IsTrue((bool)rng[0]);
            }
            {
                RGIntRange rng = new RGIntRange(-1, 1);
                Assert.AreEqual(rng.MinValue, -1);
                Assert.AreEqual(rng.MaxValue, 1);
                Assert.AreEqual(rng.NumValues, 3);
                Assert.AreEqual(rng[0], -1);
                Assert.AreEqual(rng[1], 0);
                Assert.AreEqual(rng[2], 1);
            }
            {
                RGVector2IntRange rng = new RGVector2IntRange(new Vector2Int(-2, -1), new Vector2Int(2, 1));
                RGVector2IntRange rng2 = new RGVector2IntRange(new Vector2Int(-2, -1), new Vector2Int(2, 1));
                Assert.IsTrue(rng.RangeEquals(rng2));
                Assert.AreEqual(rng.Width, 5);
                Assert.AreEqual(rng.Height, 3);
                Assert.AreEqual(rng.NumValues, 15);
                Assert.AreEqual(rng[0], new Vector2Int(-2, -1));
                Assert.AreEqual(rng[1], new Vector2Int(-1, -1));
                Assert.AreEqual(rng[2], new Vector2Int(0, -1));
                Assert.AreEqual(rng[3], new Vector2Int(1, -1));
                Assert.AreEqual(rng[4], new Vector2Int(2, -1));
                Assert.AreEqual(rng[5], new Vector2Int(-2, 0));
                Assert.AreEqual(rng[6], new Vector2Int(-1, 0));
                Assert.AreEqual(rng[7], new Vector2Int(0, 0));
                Assert.AreEqual(rng[8], new Vector2Int(1, 0));
                Assert.AreEqual(rng[9], new Vector2Int(2, 0));
                Assert.AreEqual(rng[10], new Vector2Int(-2, 1));
                Assert.AreEqual(rng[11], new Vector2Int(-1, 1));
                Assert.AreEqual(rng[12], new Vector2Int(0, 1));
                Assert.AreEqual(rng[13], new Vector2Int(1, 1));
                Assert.AreEqual(rng[14], new Vector2Int(2, 1));
            }
            {
                RGFloatRange rng = new RGFloatRange(-1.0f, 1.0f);
                Assert.IsTrue(Mathf.Approximately((float)rng.MinValue, -1.0f));
                Assert.IsTrue(Mathf.Approximately((float)rng.MaxValue, 1.0f));

                RGContinuousValueRange[] disc = rng.Discretize(4);
                Assert.IsTrue(Mathf.Approximately((float)disc[0].MinValue, -1.0f));
                Assert.IsTrue(Mathf.Approximately((float)disc[0].MaxValue, -0.5f));
                Assert.IsTrue(Mathf.Approximately((float)disc[3].MinValue, 0.5f));
                Assert.IsTrue(Mathf.Approximately((float)disc[3].MaxValue, 1.0f));
            }
            {
                RGVector2Range rng = new RGVector2Range(new Vector2(-1.0f, -1.0f), new Vector2(1.0f, 1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)rng.MidPoint).x, 0.0f));
                RGContinuousValueRange[] disc = rng.Discretize(4);
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[0].MinValue).x, -1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[0].MaxValue).x, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[2].MinValue).y, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[2].MaxValue).y, 1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MinValue).x, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MaxValue).x, 1.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MinValue).y, 0.0f));
                Assert.IsTrue(Mathf.Approximately(((Vector2)disc[3].MaxValue).y, 1.0f));
            }
        }

        private GameObject FindGameObject(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (t.gameObject.name == name)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        private void FindAndPerformAction(string name, object param)
        {
            var actionInst = RGActionManager.GetValidActions().FirstOrDefault(actionInst =>
                actionInst.BaseAction.DisplayName == name);
            Debug.Assert(actionInst != null, $"Action {name} is missing");
            foreach (var inp in actionInst.GetInputs(param))
            {
                inp.Perform();
            }
        }

        [UnityTest]
        public IEnumerator TestActionManager()
        {
            if (Keyboard.current == null)
            {
                InputSystem.AddDevice<Keyboard>();
            }
            
            SceneManager.LoadSceneAsync("ActionManagerTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("ActionManagerTestScene");
            
            GameObject eventSystem = GameObject.Find("EventSystem");
            var eventSys = eventSystem.GetComponent<EventSystem>();
            
            // Test Input System Keyboard
            RGActionManager.StartSession(eventSys);
            GameObject inputSysKeyListener = FindGameObject("InputSysKeyListener");
            inputSysKeyListener.SetActive(true);
            try
            {
                FindAndPerformAction("Any Key", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Keyboard.current.anyKey.wasPressedThisFrame");

                FindAndPerformAction("Key fireKey", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "keyboard[key].isPressed");
                
                FindAndPerformAction("Key Key.Backslash", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Keyboard.current.backslashKey.isPressed");
                
                FindAndPerformAction("Key Key.LeftAlt", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "keyboard.altKey.wasPressedThisFrame");
                
                FindAndPerformAction("Key Key.F2", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Keyboard.current[Key.F2].isPressed");
            }
            finally
            {
                inputSysKeyListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Legacy Input Manager Axis
            RGActionManager.StartSession(eventSys);
            GameObject legacyAxisListener = FindGameObject("LegacyAxisListener");
            legacyAxisListener.SetActive(true);
            try
            {
                FindAndPerformAction("Axis \"Mouse X\"", 1);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetAxisRaw(\"Mouse X\")");
                
                FindAndPerformAction("Axis \"Mouse ScrollWheel\"", 1);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetAxis(\"Mouse ScrollWheel\")");
                
                FindAndPerformAction("Axis axisName", 1);
                FindAndPerformAction("Axis axisName2", 1);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetAxis(axis)");
            }
            finally
            {
                legacyAxisListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Legacy Input Manager Button 
            RGActionManager.StartSession(eventSys);
            GameObject legacyButtonListener = FindGameObject("LegacyButtonListener");
            legacyButtonListener.SetActive(true);
            try
            {
                FindAndPerformAction("Button ButtonName", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetButton(ButtonName)");
                
                FindAndPerformAction("Button \"Fire1\"", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetButtonDown(btn)");
                
                FindAndPerformAction("Button \"Fire2\"", true);
                yield return null;
                yield return null;
                FindAndPerformAction("Button \"Fire2\"", false);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetButtonUp(\"Fire2\")");
            }
            finally
            {
                legacyButtonListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Legacy Input Manager Key
            RGActionManager.StartSession(eventSys);
            GameObject legacyKeyListener = FindGameObject("LegacyKeyListener");
            legacyKeyListener.SetActive(true);
            try
            {
                FindAndPerformAction("Any Key", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.anyKey");
                LogAssert.Expect(LogType.Log, "Input.anyKeyDown");
                
                FindAndPerformAction("Key _gameSettings.bindings.CrouchKey", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetKey(crouchKey)");
                
                FindAndPerformAction("Key _gameSettings.bindings.fireKey", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetKeyDown(_gameSettings.bindings.fireKey)");
                
                FindAndPerformAction("Key _gameSettings.bindings.jumpKey", true);
                yield return null;
                yield return null;
                FindAndPerformAction("Key _gameSettings.bindings.jumpKey", false);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetKeyUp(_gameSettings.bindings.jumpKey)");
                
                FindAndPerformAction("Key KeyCode.LeftShift", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetKey(aimKey)");
                
                FindAndPerformAction("Key MOVE_RIGHT_KEY", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetKey(MOVE_RIGHT_KEY)");
            }
            finally
            {
                legacyKeyListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Mouse Button
            RGActionManager.StartSession(eventSys);
            GameObject mouseBtnListener = FindGameObject("MouseBtnListener");
            mouseBtnListener.SetActive(true);
            try
            {
                FindAndPerformAction("Mouse Button 0", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetMouseButton(0)");
                
                FindAndPerformAction("Mouse Button mouseBtn", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetMouseButtonDown(mouseBtn)");
                
                FindAndPerformAction("Mouse Button otherMouseBtn", true);
                yield return null;
                yield return null;
                FindAndPerformAction("Mouse Button otherMouseBtn", false);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.GetMouseButtonUp(btn)");
                
                FindAndPerformAction("Mouse Button 3", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Mouse.current.forwardButton.isPressed");
                
                FindAndPerformAction("Mouse Button 4", true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "mouse.backButton.wasPressedThisFrame");
            }
            finally
            {
                mouseBtnListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Mouse Movement
            RGActionManager.StartSession(eventSys);
            GameObject mouseMovementListener = FindGameObject("MouseMovementListener");
            mouseMovementListener.SetActive(true);
            try
            {
                FindAndPerformAction("Mouse Position", new Vector2(0.1f, 0.1f));
                yield return null;
                yield return null;
                FindAndPerformAction("Mouse Position", new Vector2(0.8f, 0.7f));
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "mousePos1 != lastMousePos");
                LogAssert.Expect(LogType.Log, "mousePos2 != lastMousePos");

                FindAndPerformAction("Mouse Scroll", new Vector2Int(1, 1));
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Input.mouseScrollDelta.sqrMagnitude");
                LogAssert.Expect(LogType.Log, "Mouse.current.scroll.value.sqrMagnitude");
            }
            finally
            {
                mouseMovementListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Mouse Handlers
            RGActionManager.StartSession(eventSys);
            GameObject mouseHandlerListener = FindGameObject("MouseHandlerListener");
            mouseHandlerListener.SetActive(true);
            try
            {
                string hoverAction = "Mouse Hover Over MouseHandlerObject";
                string pressAction = "Mouse Press On MouseHandlerObject";
                
                FindAndPerformAction(hoverAction, true);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "OnMouseEnter MouseHandlerListener");
                LogAssert.Expect(LogType.Log, "OnMouseOver MouseHandlerListener");
                
                FindAndPerformAction(hoverAction, false);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "OnMouseExit MouseHandlerListener");
                
                FindAndPerformAction(pressAction, true);
                yield return null;
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "OnMouseDown MouseHandlerListener");
                LogAssert.Expect(LogType.Log, "OnMouseDrag MouseHandlerListener");
                
                FindAndPerformAction(pressAction, false);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "OnMouseUpAsButton MouseHandlerListener");
                LogAssert.Expect(LogType.Log, "OnMouseUp MouseHandlerListener");
            }
            finally
            {
                mouseHandlerListener.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Mouse Raycast 2D
            RGActionManager.StartSession(eventSys);
            GameObject mouseRaycast2DListener = FindGameObject("MouseRaycast2DListener");
            GameObject theSquare = FindGameObject("The_Square");
            mouseRaycast2DListener.SetActive(true);
            theSquare.SetActive(true);
            try
            {
                var actionInst = RGActionManager.GetValidActions().FirstOrDefault(actionInst =>
                    actionInst.BaseAction is MousePositionAction && actionInst.TargetObject.GetType().Name == "MouseRaycast2DObject");
                Debug.Assert(actionInst != null);

                Vector2? hitCoord = null;
                int gridLength = 16;
                for (int x = 0; x < gridLength; ++x)
                {
                    for (int y = 0; y < gridLength; ++y)
                    {
                        Vector2 coord = new Vector2(x / (float)gridLength, y / (float)gridLength);
                        if (actionInst.IsValidParameter(coord))
                        {
                            hitCoord = coord;
                            break;
                        }
                    }
                }
                
                Debug.Assert(hitCoord.HasValue);
                foreach (var inp in actionInst.GetInputs(hitCoord.Value))
                {
                    inp.Perform();
                }

                yield return null;
                yield return null;
                
                LogAssert.Expect(LogType.Log, "Hit 2D game object The_Square");
            }
            finally
            {
                mouseRaycast2DListener.SetActive(false);
                theSquare.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test Mouse Raycast 3D
            RGActionManager.StartSession(eventSys);
            GameObject mouseRaycast3DListener = FindGameObject("MouseRaycast3DListener");
            GameObject theCube = FindGameObject("The_Cube");
            mouseRaycast3DListener.SetActive(true);
            theCube.SetActive(true);
            try
            {
                var actionInst = RGActionManager.GetValidActions().FirstOrDefault(actionInst =>
                    actionInst.BaseAction is MousePositionAction && actionInst.TargetObject.GetType().Name == "MouseRaycast3DObject");
                Debug.Assert(actionInst != null);
                
                Vector2? hitCoord = null;
                int gridLength = 16;
                for (int x = 0; x < gridLength; ++x)
                {
                    for (int y = 0; y < gridLength; ++y)
                    {
                        Vector2 coord = new Vector2(x / (float)gridLength, y / (float)gridLength);
                        if (actionInst.IsValidParameter(coord))
                        {
                            hitCoord = coord;
                            break;
                        }
                    }
                }
                
                Debug.Assert(hitCoord.HasValue);
                foreach (var inp in actionInst.GetInputs(hitCoord.Value))
                {
                    inp.Perform();
                }

                yield return null;
                yield return null;
                
                LogAssert.Expect(LogType.Log, "Hit 3D game object The_Cube");
            }
            finally
            {
                mouseRaycast3DListener.SetActive(false);
                theCube.SetActive(false);
                RGActionManager.StopSession();
            }
            
            // Test UI button press
            RGActionManager.StartSession(eventSys);
            GameObject theButton = FindGameObject("The_Button");
            theButton.SetActive(true);
            try
            {
                yield return null;
                yield return null;
                FindAndPerformAction("ActionManagerTests.ButtonHandler.OnBtnClick", true);
                yield return null;
                yield return null;
                FindAndPerformAction("ActionManagerTests.ButtonHandler.OnBtnClick", false);
                yield return null;
                yield return null;
                LogAssert.Expect(LogType.Log, "Clicked button The_Button");
            }
            finally
            {
                RGActionManager.StopSession();
            }
        }
    }
}