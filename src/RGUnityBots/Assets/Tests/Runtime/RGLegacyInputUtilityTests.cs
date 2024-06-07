﻿#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections;
using System.Collections.Generic;
using RegressionGames;
using NUnit.Framework;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Runtime
{
    public class RGLegacyInputTestLogHandler : ILogHandler
    {
        private Queue<string> _logMessageQueue;
        private ILogHandler _existingLogHandler;

        public ILogHandler ExistingLogHandler => _existingLogHandler;
        
        public RGLegacyInputTestLogHandler(Queue<string> logMessageQueue, ILogHandler existingLogHandler)
        {
            _logMessageQueue = logMessageQueue;
            _existingLogHandler = existingLogHandler;
        }
        
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            string message = string.Format(format, args);
            _logMessageQueue.Enqueue(message);
            _existingLogHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context)
        {
            _existingLogHandler.LogException(exception, context);
        }
    }
    
    public class RGLegacyInputUtilityTests
    {
        private Queue<string> _logMessages;
        
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadSceneAsync("LegacyInputTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("LegacyInputTestScene");
            RGUtils.SetupEventSystem();
            _logMessages = new Queue<string>();
            Debug.unityLogger.logHandler = new RGLegacyInputTestLogHandler(_logMessages, Debug.unityLogger.logHandler);
        }

        private void ResetState()
        {
            _logMessages.Clear();
            RGLegacyInputWrapper.StopSimulation();
            GameObject eventSystem = GameObject.Find("EventSystem");
            var eventSys = eventSystem.GetComponent<EventSystem>();
            RGLegacyInputWrapper.StartSimulation(eventSys);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Debug.unityLogger.logHandler =
                ((RGLegacyInputTestLogHandler)Debug.unityLogger.logHandler).ExistingLogHandler;
            yield break;
        }
        
        [Test]
        public void TestKeyNameToCode()
        {
            Dictionary<string, KeyCode> testCases = new Dictionary<string, KeyCode>()
            {
                {"b", KeyCode.B},
                {"5", KeyCode.Alpha5},
                {"right", KeyCode.RightArrow},
                {"[3]", KeyCode.Keypad3},
                {"[+]", KeyCode.KeypadPlus},
                {"left shift", KeyCode.LeftShift},
                {"right ctrl", KeyCode.RightControl},
                {"enter", KeyCode.Return},
                {"[enter]", KeyCode.KeypadEnter},
                {"f6", KeyCode.F6},
                {"mouse 2", KeyCode.Mouse2},
                {"joystick button 4", KeyCode.JoystickButton4},
                {"joystick 6 button 5", KeyCode.Joystick6Button5}
            };

            foreach (var entry in testCases)
            {
                KeyCode result = RGLegacyInputWrapper.KeyNameToCode(entry.Key);
                Assert.That(result, Is.EqualTo(entry.Value), $"{entry.Key} should produce {entry.Value}");
            }
        }

        [UnityTest]
        public IEnumerator TestUIButtonClick()
        {
            ResetState();
            GameObject legacyBtn = GameObject.Find("LegacyButton");
            var pos = legacyBtn.transform.position;
            RGLegacyInputWrapper.SimulateMouseMovement(new Vector3(pos.x, pos.y, 0.0f));
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse0);
            yield return null;
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse0);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("ClickedHandler()");
        }

        [UnityTest]
        public IEnumerator TestKeyPressAndRelease()
        {
            ResetState();
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.X);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetKey(X)", "GetKeyDown(\"x\")", "anyKey", "anyKeyDown");
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.X);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetKeyUp(X)");
        }

        [UnityTest]
        public IEnumerator TestMouseHandling()
        {
            ResetState();
            
            GameObject col2DObj = GameObject.Find("Col2DObj");
            GameObject col3DObj = GameObject.Find("Col3DObj");

            var col3D = col3DObj.GetComponent<Collider>();
            var col3Dpt = Camera.main.WorldToScreenPoint(col3D.bounds.center);
            
            var col2D = col2DObj.GetComponent<Collider2D>();
            var col2Dpt = Camera.main.WorldToScreenPoint(col2D.bounds.center);

            string[] objNames = { col3DObj.name, col2DObj.name };
            Vector3[] screenPts = { col3Dpt, col2Dpt };

            for (int i = 0; i < objNames.Length; ++i)
            {
                string objName = objNames[i];
                Vector3 screenPt = screenPts[i];
                
                RGLegacyInputWrapper.SimulateMouseMovement(new Vector3(screenPt.x, screenPt.y, 0));
                yield return null;
                yield return null;
                AssertLogMessagesPresent(
                    $"{objName} OnMouseEnter()",
                    "GetAxisRaw(\"Mouse X\") != 0.0f",
                    "GetAxisRaw(\"Mouse Y\") != 0.0f");

                RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse0);
                yield return null;
                yield return null;
                AssertLogMessagesPresent($"{objName} OnMouseDown()");

                yield return null;
                AssertLogMessagesPresent($"{objName} OnMouseDrag()");

                RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse0);
                yield return null;
                yield return null;

                AssertLogMessagesPresent($"{objName} OnMouseUp()", 
                    $"{objName} OnMouseUpAsButton()",
                    "GetMouseButtonUp(0)");

                RGLegacyInputWrapper.SimulateMouseMovement(Vector3.zero);
                yield return null;
                yield return null;

                AssertLogMessagesPresent($"{objName} OnMouseExit()");
            }

            RGLegacyInputWrapper.SimulateMouseScrollWheel(new Vector2(-2.0f, 10.0f));
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetAxisRaw(\"Mouse ScrollWheel\") > 0.0f", "mouseScrollDelta.x < 0.0f");

            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse1);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetMouseButtonDown(1)");
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse2);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetMouseButton(2)");
        }

        [UnityTest]
        public IEnumerator TestAxisHandling()
        {
            ResetState();
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.RightArrow);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetAxis(\"Horizontal\") > 0.0f");
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.RightArrow);
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.LeftArrow);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetAxis(\"Horizontal\") < 0.0f");
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.LeftArrow);
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.UpArrow);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetAxisRaw(\"Vertical\") > 0.0f");
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.UpArrow);
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.DownArrow);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetAxisRaw(\"Vertical\") < 0.0f");
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.DownArrow);
        }

        [UnityTest]
        public IEnumerator TestButtonHandling()
        {
            ResetState();
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Space);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetButton(\"Jump\")");
            
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Space);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetButtonUp(\"Jump\")");
            
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.LeftControl);
            yield return null;
            yield return null;
            AssertLogMessagesPresent("GetButtonDown(\"Fire1\")");
        }

        private void AssertLogMessagesPresent(params string[] expectedMessages)
        {
            var actualMessages = DequeueLogMessages();
            foreach (string msg in expectedMessages)
            {
                Debug.Assert(actualMessages.Contains(msg), $"Expected log message {msg}");
            }
        }

        private ISet<string> DequeueLogMessages()
        {
            ISet<string> result = new HashSet<string>();
            while (_logMessages.TryDequeue(out string msg))
            {
                result.Add(msg);
            }
            return result;
        }
    }
}
#endif