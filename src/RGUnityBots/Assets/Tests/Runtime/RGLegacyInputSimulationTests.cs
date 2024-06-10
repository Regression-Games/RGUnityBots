#if ENABLE_LEGACY_INPUT_MANAGER
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using RegressionGames;
using RegressionGames.RGLegacyInputUtility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Runtime
{
    public class RGLegacyInputSimulationTests
    {
        private void ResetState()
        {
            RGLegacyInputWrapper.StopSimulation();
            GameObject eventSystem = GameObject.Find("EventSystem");
            var eventSys = eventSystem.GetComponent<EventSystem>();
            RGLegacyInputWrapper.StartSimulation(eventSys);
        }
        
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadSceneAsync("LegacyInputTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("LegacyInputTestScene");
            RGUtils.SetupEventSystem();
        }

        /**
         * All the test cases for the legacy input simulation go in one test case.
         * Otherwise, we run into test flakiness when running in batch mode.
         */
        [UnityTest]
        public IEnumerator TestLegacyInputSimulation()
        {
            // UI button click
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
            
            // Key press/release
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

            // Mouse handling
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
            
            // Axis handling
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
            
            // Button handling
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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RGLegacyInputWrapper.StopSimulation();
            yield break;
        }

        private void AssertLogMessagesPresent(params string[] expectedMessages)
        {
            foreach (string msg in expectedMessages)
            {
                LogAssert.Expect(LogType.Log, msg);
            }
        }
    }
}
#endif