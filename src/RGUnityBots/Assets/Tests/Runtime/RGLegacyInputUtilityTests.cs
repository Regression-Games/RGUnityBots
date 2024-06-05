#if ENABLE_LEGACY_INPUT_MANAGER
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
            GameObject eventSystem = GameObject.Find("EventSystem");
            _logMessages = new Queue<string>();
            Debug.unityLogger.logHandler = new RGLegacyInputTestLogHandler(_logMessages, Debug.unityLogger.logHandler);
            var eventSys = eventSystem.GetComponent<EventSystem>();
            RGLegacyInputWrapper.StartSimulation(eventSys);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RGLegacyInputWrapper.StopSimulation();
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
        public IEnumerator TestUnityUIButton()
        {
            GameObject legacyBtn = GameObject.Find("LegacyButton");
            var pos = legacyBtn.transform.position;
            RGLegacyInputWrapper.SimulateMouseMovement(new Vector3(pos.x, pos.y, 0.0f));
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.Mouse0);
            yield return null;
            RGLegacyInputWrapper.SimulateKeyRelease(KeyCode.Mouse0);
            yield return null;
            Debug.Log(_logMessages.Count);
            yield break;
        }

        [UnityTest]
        public IEnumerator TestKeyPress()
        {
            RGLegacyInputWrapper.SimulateKeyPress(KeyCode.X);
            yield return null;
            yield return null;
            Debug.Assert(_logMessages.TryDequeue(out string message));
            Debug.Assert(message == "Key X pressed");
        }
    }
}
#endif