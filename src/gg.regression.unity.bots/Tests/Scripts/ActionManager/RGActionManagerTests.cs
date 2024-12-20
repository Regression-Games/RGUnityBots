using System.Collections;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RegressionGames.ActionManager;
using RegressionGames.ActionManager.Actions;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.TestFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.ActionManager
{
    [TestFixture]
    public class RGActionManagerTests : InputTestFixture
    {
        private void TestRangeSerialization(IRGValueRange rng)
        {
            StringBuilder stringBuilder = new StringBuilder();
            rng.WriteToStringBuilder(stringBuilder);
            JObject obj = JObject.Parse(stringBuilder.ToString());
            var parsedRange = obj.ToObject<IRGValueRange>();
            Assert.IsTrue(parsedRange.RangeEquals(rng));
            Assert.IsTrue(rng.RangeEquals(parsedRange));
        }

        [UnityTest]
        public IEnumerator TestValueRanges()
        {
            // Wait for the scene
            SceneManager.LoadSceneAsync("EmptyScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("EmptyScene");

            {
                RGBoolRange rng = new RGBoolRange(false, true);
                TestRangeSerialization(rng);
                Assert.IsFalse((bool)rng.MinValue);
                Assert.IsTrue((bool)rng.MaxValue);
                Assert.AreEqual(rng.NumValues, 2);
                Assert.IsFalse((bool)rng[0]);
                Assert.IsTrue((bool)rng[1]);
            }
            {
                RGBoolRange rng = new RGBoolRange(true, true);
                TestRangeSerialization(rng);
                Assert.IsTrue((bool)rng.MinValue);
                Assert.IsTrue((bool)rng.MaxValue);
                Assert.AreEqual(rng.NumValues, 1);
                Assert.IsTrue((bool)rng[0]);
            }
            {
                RGIntRange rng = new RGIntRange(-1, 1);
                TestRangeSerialization(rng);
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
                TestRangeSerialization(rng);
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
                RGVector3IntRange rng = new RGVector3IntRange(new Vector3Int(3, 0, -1), new Vector3Int(5, 2, 1));
                RGVector3IntRange rng2 = new RGVector3IntRange(new Vector3Int(3, 0, -1), new Vector3Int(5, 2, 1));
                TestRangeSerialization(rng);
                Assert.IsTrue(rng.RangeEquals(rng2));
                Assert.AreEqual(rng.Width, 3);
                Assert.AreEqual(rng.Height, 3);
                Assert.AreEqual(rng.Length, 3);
                Assert.AreEqual(rng.NumValues, 27);
                Assert.AreEqual(rng[0], new Vector3Int(3, 0, -1));
                Assert.AreEqual(rng[1], new Vector3Int(4, 0, -1));
                Assert.AreEqual(rng[2], new Vector3Int(5, 0, -1));
                Assert.AreEqual(rng[3], new Vector3Int(3, 1, -1));
                Assert.AreEqual(rng[4], new Vector3Int(4, 1, -1));
                Assert.AreEqual(rng[5], new Vector3Int(5, 1, -1));
                Assert.AreEqual(rng[6], new Vector3Int(3, 2, -1));
                Assert.AreEqual(rng[7], new Vector3Int(4, 2, -1));
                Assert.AreEqual(rng[8], new Vector3Int(5, 2, -1));
                Assert.AreEqual(rng[9], new Vector3Int(3, 0, 0));
                Assert.AreEqual(rng[10], new Vector3Int(4, 0, 0));
                Assert.AreEqual(rng[11], new Vector3Int(5, 0, 0));
                Assert.AreEqual(rng[12], new Vector3Int(3, 1, 0));
                Assert.AreEqual(rng[13], new Vector3Int(4, 1, 0));
                Assert.AreEqual(rng[14], new Vector3Int(5, 1, 0));
                Assert.AreEqual(rng[15], new Vector3Int(3, 2, 0));
                Assert.AreEqual(rng[16], new Vector3Int(4, 2, 0));
                Assert.AreEqual(rng[17], new Vector3Int(5, 2, 0));
                Assert.AreEqual(rng[18], new Vector3Int(3, 0, 1));
                Assert.AreEqual(rng[19], new Vector3Int(4, 0, 1));
                Assert.AreEqual(rng[20], new Vector3Int(5, 0, 1));
                Assert.AreEqual(rng[21], new Vector3Int(3, 1, 1));
                Assert.AreEqual(rng[22], new Vector3Int(4, 1, 1));
                Assert.AreEqual(rng[23], new Vector3Int(5, 1, 1));
                Assert.AreEqual(rng[24], new Vector3Int(3, 2, 1));
                Assert.AreEqual(rng[25], new Vector3Int(4, 2, 1));
                Assert.AreEqual(rng[26], new Vector3Int(5, 2, 1));
            }
            {
                RGFloatRange rng = new RGFloatRange(-1.0f, 1.0f);
                TestRangeSerialization(rng);
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
                TestRangeSerialization(rng);
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

        private static bool IsValidAction(string name)
        {
            var actionInst = RGActionManager.GetValidActions().FirstOrDefault(actionInst =>
                actionInst.BaseAction.DisplayName == name);
            return actionInst != null;
        }

        private void FindAndPerformAction(string name, object param)
        {
            var actionInst = RGActionManager.GetValidActions().FirstOrDefault(actionInst =>
                actionInst.BaseAction.DisplayName == name);
            Debug.Assert(actionInst != null, $"Action {name} is missing");
            foreach (var inp in actionInst.GetInputs(param))
            {
                inp.Perform(0);
            }
        }

        private void ResetInputSystem()
        {
            TeardownEventSystemChanges();

            MouseEventSender.InitializeVirtualMouse();
        }

        private void TeardownEventSystemChanges()
        {
            if (Keyboard.current != null)
            {
                InputSystem.ResetDevice(Keyboard.current, true);
            }

            MouseEventSender.Reset();
            if (Mouse.current != null)
            {
                InputSystem.ResetDevice(Mouse.current, true);
            }
        }

        private bool _inputUpdateCompleted = false;

        [SetUp]
        public void SetUp()
        {
            InputSystem.onAfterUpdate += OnAfterInputSystemUpdate;
            MouseEventSender.Reset();
        }

        [TearDown]
        public override void TearDown()
        {
            RGActionManager.StopSession();
            TeardownEventSystemChanges();

            base.TearDown();

            InputSystem.onAfterUpdate -= OnAfterInputSystemUpdate;

            RGLegacyInputWrapper.UpdateMode = RGLegacyInputUpdateMode.AUTOMATIC;
            MouseEventSender.Reset();
        }

        private void OnAfterInputSystemUpdate()
        {
            _inputUpdateCompleted = true;
        }

        /// <summary>
        /// Wait for both legacy and new Input System event processing updates to complete, then
        /// immediately invoke the given callback.
        /// </summary>
        private IEnumerator WaitForInputUpdate()
        {
            RGLegacyInputWrapper.Update(); // manually trigger a legacy input processing step
            _inputUpdateCompleted = false;
            yield return new WaitUntil(() => _inputUpdateCompleted); // wait for new Input System update to complete
        }

        [UnityTest]
        public IEnumerator TestActionManager()
        {
            RGLegacyInputWrapper.UpdateMode = RGLegacyInputUpdateMode.MANUAL;

            if (Keyboard.current == null)
            {
                InputSystem.AddDevice<Keyboard>();
            }

            // get a clean scene
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // destroy any existing overlay before loading new test scene
                Object.Destroy(botManager.gameObject);
            }

            // Wait for the scene
            SceneManager.LoadSceneAsync("ActionManagerTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("ActionManagerTestScene");

            GameObject eventSystem = GameObject.Find("EventSystem");
            var eventSys = eventSystem.GetComponent<EventSystem>();

            ResetInputSystem();

            // Test Input System Keyboard
            RGActionManager.StartSession(0, eventSys);
            GameObject inputSysKeyListener = FindGameObject("InputSysKeyListener");
            inputSysKeyListener.SetActive(true);
            try
            {
                FindAndPerformAction("Any Key", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Keyboard.current.anyKey.wasPressedThisFrame");

                FindAndPerformAction("Key fireKey", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "keyboard[key].isPressed");

                FindAndPerformAction("Key Key.Backslash", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Keyboard.current.backslashKey.isPressed");

                FindAndPerformAction("Key Key.LeftAlt", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "keyboard.altKey.wasPressedThisFrame");

                FindAndPerformAction("Key Key.F2", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Keyboard.current[Key.F2].isPressed");
            }
            finally
            {
                inputSysKeyListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            #if ENABLE_LEGACY_INPUT_MANAGER
            // Test Legacy Input Manager Axis
            RGActionManager.StartSession(0, eventSys);
            GameObject legacyAxisListener = FindGameObject("LegacyAxisListener");
            legacyAxisListener.SetActive(true);
            try
            {
                FindAndPerformAction("Axis \"Mouse X\"", 1);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetAxisRaw(\"Mouse X\")");

                FindAndPerformAction("Axis \"Mouse ScrollWheel\"", 1);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetAxis(\"Mouse ScrollWheel\")");

                FindAndPerformAction("Axis axisName", 1);
                FindAndPerformAction("Axis axisName2", 1);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetAxis(axis)");
            }
            finally
            {
                legacyAxisListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test Legacy Input Manager Button
            RGActionManager.StartSession(0, eventSys);
            GameObject legacyButtonListener = FindGameObject("LegacyButtonListener");
            legacyButtonListener.SetActive(true);
            try
            {
                FindAndPerformAction("Button ButtonName", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetButton(ButtonName)");

                FindAndPerformAction("Button \"Fire1\"", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetButtonDown(btn)");

                FindAndPerformAction("Button \"Fire2\"", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Button \"Fire2\"", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetButtonUp(\"Fire2\")");
            }
            finally
            {
                legacyButtonListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test Legacy Input Manager Key
            RGActionManager.StartSession(0, eventSys);
            GameObject legacyKeyListener = FindGameObject("LegacyKeyListener");
            legacyKeyListener.SetActive(true);
            try
            {
                FindAndPerformAction("Any Key", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.anyKey");
                LogAssert.Expect(LogType.Log, "Input.anyKeyDown");

                FindAndPerformAction("Key _gameSettings.bindings.CrouchKey", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetKey(crouchKey)");

                FindAndPerformAction("Key _gameSettings.bindings.fireKey", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetKeyDown(_gameSettings.bindings.fireKey)");

                FindAndPerformAction("Key _gameSettings.bindings.jumpKey", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Key _gameSettings.bindings.jumpKey", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetKeyUp(_gameSettings.bindings.jumpKey)");

                FindAndPerformAction("Key KeyCode.LeftShift", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetKey(aimKey)");

                FindAndPerformAction("Key MOVE_RIGHT_KEY", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetKey(MOVE_RIGHT_KEY)");
            }
            finally
            {
                legacyKeyListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }
            #endif

            // Test Mouse Button
            RGActionManager.StartSession(0, eventSys);
            GameObject mouseBtnListener = FindGameObject("MouseBtnListener");
            mouseBtnListener.SetActive(true);
            try
            {
                #if ENABLE_LEGACY_INPUT_MANAGER
                FindAndPerformAction("Mouse Button 0", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetMouseButton(0)");

                FindAndPerformAction("Mouse Button mouseBtn", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetMouseButtonDown(mouseBtn)");

                FindAndPerformAction("Mouse Button otherMouseBtn", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Mouse Button otherMouseBtn", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Input.GetMouseButtonUp(btn)");
                #endif

                FindAndPerformAction("Mouse Button 3", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Mouse.current.forwardButton.isPressed");

                FindAndPerformAction("Mouse Button 4", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "mouse.backButton.wasPressedThisFrame");
            }
            finally
            {
                mouseBtnListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test Mouse Movement
            RGActionManager.StartSession(0, eventSys);
            GameObject mouseMovementListener = FindGameObject("MouseMovementListener");
            mouseMovementListener.SetActive(true);
            try
            {
                FindAndPerformAction("Mouse Position", new Vector2(0.1f, 0.1f));
                yield return WaitForInputUpdate();
                FindAndPerformAction("Mouse Position", new Vector2(0.8f, 0.7f));
                yield return WaitForInputUpdate();
#if ENABLE_LEGACY_INPUT_MANAGER
            LogAssert.Expect(LogType.Log, "mousePos1 != lastMousePos1");
#endif
                LogAssert.Expect(LogType.Log, "mousePos2 != lastMousePos2");

                FindAndPerformAction("Mouse Scroll", new Vector2Int(1, 1));
                yield return WaitForInputUpdate();
#if ENABLE_LEGACY_INPUT_MANAGER
            LogAssert.Expect(LogType.Log, "Input.mouseScrollDelta.sqrMagnitude");
#endif
                LogAssert.Expect(LogType.Log, "Mouse.current.scroll.value.sqrMagnitude");
            }
            finally
            {
                mouseMovementListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            #if ENABLE_LEGACY_INPUT_MANAGER
            // Test Mouse Handlers
            RGActionManager.StartSession(0, eventSys);
            GameObject mouseHandlerListener = FindGameObject("MouseHandlerListener");
            mouseHandlerListener.SetActive(true);
            try
            {
                string hoverAction = "Mouse Hover Over MouseHandlerObject";
                string pressAction = "Mouse Press On MouseHandlerObject";

                FindAndPerformAction(hoverAction, true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnMouseEnter MouseHandlerListener");
                LogAssert.Expect(LogType.Log, "OnMouseOver MouseHandlerListener");

                FindAndPerformAction(hoverAction, false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnMouseExit MouseHandlerListener");

                FindAndPerformAction(pressAction, true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnMouseDown MouseHandlerListener");
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnMouseDrag MouseHandlerListener");

                FindAndPerformAction(pressAction, false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnMouseUpAsButton MouseHandlerListener");
                LogAssert.Expect(LogType.Log, "OnMouseUp MouseHandlerListener");
            }
            finally
            {
                mouseHandlerListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }
            #endif

            // Test Mouse Raycast 2D
            RGActionManager.StartSession(0, eventSys);
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
                    inp.Perform(0);
                }

                yield return WaitForInputUpdate();

                LogAssert.Expect(LogType.Log, "Hit 2D game object The_Square");
            }
            finally
            {
                mouseRaycast2DListener.SetActive(false);
                theSquare.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test Mouse Raycast 3D
            RGActionManager.StartSession(0, eventSys);
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
                    inp.Perform(0);
                }

                yield return WaitForInputUpdate();

                LogAssert.Expect(LogType.Log, "Hit 3D game object The_Cube");
            }
            finally
            {
                mouseRaycast3DListener.SetActive(false);
                theCube.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test Unity UI interaction
            string[] uiObjects =
            {
                "The_Button", "The_Toggle",
                "Slider_Horizontal", "Slider_Vertical",
                "Scrollbar_Horizontal", "Scrollbar_Vertical",
                "Dropdown", "TMP_Dropdown", "InputField", "TMP_InputField"
            };

            RGActionManager.StartSession(0, eventSys);
            foreach (var uiObjectName in uiObjects)
            {
                GameObject uiObject = FindGameObject(uiObjectName);
                uiObject.SetActive(true);
            }

            try
            {
                // UI Button Click
                yield return null; // wait for one frame for the UI elements to become active
                FindAndPerformAction("ActionManagerTests.UIHandler.OnBtnClick", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("ActionManagerTests.UIHandler.OnBtnClick", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Clicked button The_Button");

                // Toggle Click
                FindAndPerformAction("Press The_Toggle", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press The_Toggle", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "The_Toggle changed to True");
                FindAndPerformAction("Press The_Toggle", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press The_Toggle", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "The_Toggle changed to False");

                // Dropdown Press
                FindAndPerformAction("Press Dropdown", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press Dropdown", false);
                yield return WaitForInputUpdate();
                yield return RGTestUtils.WaitUntil(() => IsValidAction("Press Item (Dropdown Dropdown)"), 5, "Dropdown item did not appear");
                FindAndPerformAction("Press Item (Dropdown Dropdown)", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press Item (Dropdown Dropdown)", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Dropdown value changed");
                yield return RGTestUtils.WaitUntil(() => GameObject.Find("Dropdown List") == null, 5, "Dropdown item did not disappear");

                // TextMeshPro Dropdown Press
                FindAndPerformAction("Press TMP_Dropdown", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press TMP_Dropdown", false);
                yield return WaitForInputUpdate();
                yield return RGTestUtils.WaitUntil(() => IsValidAction("Press Item (Dropdown TMP_Dropdown)"), 5, "TMP_Dropdown item did not appear");
                FindAndPerformAction("Press Item (Dropdown TMP_Dropdown)", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press Item (Dropdown TMP_Dropdown)", false);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "TMP_Dropdown value changed");
                yield return RGTestUtils.WaitUntil(() => GameObject.Find("Dropdown List") == null, 5, "TMP_Dropdown item did not disappear");

                // InputField text entry
                FindAndPerformAction("Press InputField", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press InputField", false);
                yield return WaitForInputUpdate();
                (Key, bool)[] keyEntryTest = { (Key.H, true), (Key.E, false), (Key.L, false), (Key.L, false), (Key.O, false) };
                foreach (var (key, shiftPressed) in keyEntryTest)
                {
                    int keyIndex = (key - UIInputFieldTextEntryAction.MinKey) * 2;
                    if (shiftPressed)
                        keyIndex += 1;
                    FindAndPerformAction("Text Entry InputField", UIInputFieldTextEntryAction.PARAM_FIRST_KEY + keyIndex);
                    yield return WaitForInputUpdate();
                    FindAndPerformAction("Text Entry InputField", UIInputFieldTextEntryAction.PARAM_NULL);
                    yield return WaitForInputUpdate();
                }

                FindAndPerformAction("Text Submit InputField", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "InputField submitted with text Hello");

                // TextMeshPro InputField text entry
                FindAndPerformAction("Press TMP_InputField", true);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Press TMP_InputField", false);
                yield return WaitForInputUpdate();
                (Key, bool)[] keyEntryTest2 = { (Key.W, true), (Key.O, false), (Key.R, false), (Key.L, false), (Key.D, false) };
                foreach (var (key, shiftPressed) in keyEntryTest2)
                {
                    int keyIndex = (key - UIInputFieldTextEntryAction.MinKey) * 2;
                    if (shiftPressed)
                        keyIndex += 1;
                    FindAndPerformAction("Text Entry TMP_InputField", UIInputFieldTextEntryAction.PARAM_FIRST_KEY + keyIndex);
                    yield return WaitForInputUpdate();
                    FindAndPerformAction("Text Entry TMP_InputField", UIInputFieldTextEntryAction.PARAM_NULL);
                    yield return WaitForInputUpdate();
                }

                FindAndPerformAction("Text Submit TMP_InputField", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "TMP_InputField submitted with text World");

                // Horizontal Slider Movement
                FindAndPerformAction("Press Slider_Horizontal", 0.25f);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Release Slider_Horizontal", 0.25f);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Slider_Horizontal changed to first half");
                FindAndPerformAction("Press Slider_Horizontal", 0.75f);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Release Slider_Horizontal", 0.75f);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Slider_Horizontal changed to second half");

                // Vertical Slider Movement
                FindAndPerformAction("Press Slider_Vertical", 0.25f);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Release Slider_Vertical", 0.25f);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Slider_Vertical changed to first half");
                FindAndPerformAction("Press Slider_Vertical", 0.75f);
                yield return WaitForInputUpdate();
                FindAndPerformAction("Release Slider_Vertical", 0.75f);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Slider_Vertical changed to second half");

                // Scrollbar tests do not work when running in the CI pipeline, only run these locally
                if (!Application.isBatchMode)
                {
                    // Horizontal Scrollbar Movement
                    FindAndPerformAction("Press Scrollbar_Horizontal", 0.25f);
                    yield return WaitForInputUpdate();
                    yield return null; // need to wait an extra frame for dragging the scrollbar
                    FindAndPerformAction("Release Scrollbar_Horizontal", 0.25f);
                    yield return WaitForInputUpdate();
                    LogAssert.Expect(LogType.Log, "Scrollbar_Horizontal changed to first half");
                    FindAndPerformAction("Press Scrollbar_Horizontal", 0.75f);
                    yield return WaitForInputUpdate();
                    yield return null; // need to wait an extra frame for dragging the scrollbar
                    FindAndPerformAction("Release Scrollbar_Horizontal", 0.75f);
                    yield return WaitForInputUpdate();
                    LogAssert.Expect(LogType.Log, "Scrollbar_Horizontal changed to second half");

                    // Vertical Scrollbar Movement
                    FindAndPerformAction("Press Scrollbar_Vertical", 0.25f);
                    yield return WaitForInputUpdate();
                    yield return null; // need to wait an extra frame for dragging the scrollbar
                    FindAndPerformAction("Release Scrollbar_Vertical", 0.25f);
                    yield return WaitForInputUpdate();
                    LogAssert.Expect(LogType.Log, "Scrollbar_Vertical changed to first half");
                    FindAndPerformAction("Press Scrollbar_Vertical", 0.75f);
                    yield return WaitForInputUpdate();
                    yield return null; // need to wait an extra frame for dragging the scrollbar
                    FindAndPerformAction("Release Scrollbar_Vertical", 0.75f);
                    yield return WaitForInputUpdate();
                    LogAssert.Expect(LogType.Log, "Scrollbar_Vertical changed to second half");
                }
            }
            finally
            {
                foreach (var uiObjectName in uiObjects)
                {
                    GameObject uiObject = FindGameObject(uiObjectName);
                    uiObject.SetActive(false);
                }

                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test interprocedural analysis
            RGActionManager.StartSession(0, eventSys);
            GameObject interprocObj = FindGameObject("InterprocListener");
            interprocObj.SetActive(true);
            try
            {
                FindAndPerformAction("Key jumpKeyCode", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Player jumped");
            }
            finally
            {
                interprocObj.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test triggering inputs handled via InputActionAsset and embedded InputAction
            ResetInputSystem();
            RGActionManager.StartSession(0, eventSys);
            GameObject inputActionListener = FindGameObject("InputActionListener");
            inputActionListener.SetActive(true);
            try
            {
                yield return null; // wait one frame for the InputActions to become active

                FindAndPerformAction("InputAction Move", new Vector2Int(1, 1));
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "moveVal.sqrMagnitude");

                FindAndPerformAction("InputAction Jump", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "jumpAction.IsPressed()");

                FindAndPerformAction("InputAction Fire", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "fireAction pressed this frame");

                FindAndPerformAction("InputAction Aim", new Vector2Int(-1, -1));
                yield return WaitForInputUpdate();
                FindAndPerformAction("InputAction Aim", new Vector2Int(1, 1));
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Aim changed");
            }
            finally
            {
                RGActionManager.StopSession();
                inputActionListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test triggering inputs handled via InputActionAsset C# wrapper
            RGActionManager.StartSession(0, eventSys);
            GameObject csWrapperListener = FindGameObject("CsWrapperInputListener");
            csWrapperListener.SetActive(true);
            try
            {
                yield return null; // wait one frame for the InputActions to become active

                FindAndPerformAction("InputAction Crouch", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "Crouch pressed");

                FindAndPerformAction("InputAction Horizontal", 1);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "horizVal > 0");

                FindAndPerformAction("InputAction Horizontal", -1);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "horizVal < 0");
            }
            finally
            {
                csWrapperListener.SetActive(false);
                RGActionManager.StopSession();
                ResetInputSystem();
            }

            // Test triggering inputs handled via PlayerInput
            RGActionManager.StartSession(0, eventSys);
            GameObject playerInputListener = FindGameObject("PlayerInputListener");
            playerInputListener.SetActive(true);
            try
            {
                yield return null; // wait one frame for the InputActions to become active

                FindAndPerformAction("InputAction Move", new Vector2Int(1, 1));
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnMove()");

                FindAndPerformAction("InputAction Jump", true);
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnJump()");

                FindAndPerformAction("InputAction Aim", new Vector2Int(-1, -1));
                yield return WaitForInputUpdate();
                FindAndPerformAction("InputAction Aim", new Vector2Int(1, 1));
                yield return WaitForInputUpdate();
                LogAssert.Expect(LogType.Log, "OnAim()");
            }
            finally
            {
                playerInputListener.SetActive(false);
                RGActionManager.StopSession();
                TeardownEventSystemChanges();
            }
        }
    }
}
