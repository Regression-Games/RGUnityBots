using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RegressionGames.RGLegacyInputUtility;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.Models;
using RegressionGames.TestFramework;
using RegressionGames.Types;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.BotSegments
{
    [TestFixture]
    public class BotSegmentsTests
    {

         [UnitySetUp]
        public IEnumerator SetUp()
        {
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // destroy any existing overlay before loading new test scene
                Object.Destroy(botManager.gameObject);
            }

            // Wait for the scene
            SceneManager.LoadSceneAsync("UniRxTestScene", LoadSceneMode.Single);
            yield return RGTestUtils.WaitForScene("UniRxTestScene");

            MouseEventSender.InitializeVirtualMouse();


            RGUtils.SetupOverrideEventSystem();

            GameObject eventSystem = GameObject.Find("EventSystem");
            var eventSys = eventSystem.GetComponent<EventSystem>();
            RGLegacyInputWrapper.UpdateMode = RGLegacyInputUpdateMode.AUTOMATIC;
            RGLegacyInputWrapper.StartSimulation(eventSys);
        }

        [UnityTest]
        public IEnumerator TestRGUtilsBotActionsAndCriteria()
        {
            PlaybackResult sequenceResult = null;

            // validate that the 'clicked' text is NOT visible
            var botCriteria = new List<KeyFrameCriteria>()
            {
                new ()
                {
                    type = KeyFrameCriteriaType.PartialNormalizedPath,
                    data = new PathKeyFrameCriteriaData()
                    {
                        count = 0,
                        countRule = CountRule.Zero,
                        path = "Text-Clicked"
                    }
                }
            };

            yield return RGTestUtils.WaitForBotCriteria(botCriteria,result => sequenceResult = result, timeout: 5);
            if (!sequenceResult.success)
            {
                RGDebug.LogWarning("Text-Clicked was visible, but shouldn't have been");
            }
            Assert.IsTrue(sequenceResult.success);

            // check that the negative case will result in a failure (false) value
            botCriteria = new List<KeyFrameCriteria>()
            {
                new ()
                {
                    type = KeyFrameCriteriaType.PartialNormalizedPath,
                    data = new PathKeyFrameCriteriaData()
                    {
                        count = 1,
                        countRule = CountRule.NonZero,
                        path = "Text-Clicked"
                    }
                }
            };
            yield return RGTestUtils.WaitForBotCriteria(botCriteria,result => sequenceResult = result, timeout: 5);
            if (sequenceResult.success)
            {
                RGDebug.LogWarning("Text-Clicked was visible, but shouldn't have been");
            }
            Assert.IsTrue(!sequenceResult.success);

            // click the Button Button (<- that is the title of the button)
            var botAction = new BotAction()
            {
                type = BotActionType.InputPlayback,
                data = new InputPlaybackActionData()
                {
                    startTime = 0,
                    inputData = new InputData()
                    {
                        mouse = new List<MouseInputActionData>()
                        {
                            new MouseInputActionData()
                            {
                                startTime = 0.1f,
                                leftButton = false,
                                position = new Vector2Int(700, 630),
                                screenSize = new Vector2Int(1920,1080)
                            },
                            new MouseInputActionData()
                            {
                                startTime = 0.2f,
                                leftButton = true,
                                position = new Vector2Int(700, 630),
                                screenSize = new Vector2Int(1920,1080),
                                clickedObjectNormalizedPaths = new []{"Canvas/LegacyButton (Button)"}
                            },
                            new MouseInputActionData()
                            {
                                startTime = 0.3f,
                                leftButton = false,
                                position = new Vector2Int(700, 630),
                                screenSize = new Vector2Int(1920,1080),
                                clickedObjectNormalizedPaths = new []{"Canvas/LegacyButton (Button)"}
                            },
                            new MouseInputActionData()
                            {
                                startTime = 0.4f,
                                leftButton = false,
                                position = new Vector2Int(700, 630),
                                screenSize = new Vector2Int(1920,1080)
                            }
                        }
                    }
                }
            };

            yield return RGTestUtils.PerformBotAction(botAction, result => sequenceResult = result, timeout: 5);

            if (!sequenceResult.success)
            {
                RGDebug.LogWarning("Bot action timed out");
            }
            Assert.IsTrue(sequenceResult.success);


            // validate that the 'clicked' text IS visible
            botCriteria = new List<KeyFrameCriteria>()
            {
                new ()
                {
                    type = KeyFrameCriteriaType.PartialNormalizedPath,
                    data = new PathKeyFrameCriteriaData()
                    {
                        count = 1,
                        countRule = CountRule.NonZero,
                        path = "Text-Clicked"
                    }
                }
            };

            yield return RGTestUtils.WaitForBotCriteria(botCriteria,result => sequenceResult = result, timeout: 5);
            if (!sequenceResult.success)
            {
                RGDebug.LogWarning("Text-Clicked was not visible");
            }
            Assert.IsTrue(sequenceResult.success);
        }

        [TearDown]
        public void TearDown()
        {
            // just get some component on the top level RGOverlayCanvas
            var botManager = Object.FindObjectOfType<RGBotManager>();
            if (botManager != null)
            {
                // remove our overlay
                Object.Destroy(botManager.gameObject);
            }

            SceneManager.UnloadSceneAsync("UniRxTestScene");
            MouseEventSender.Reset();

            RGLegacyInputWrapper.StopSimulation();

            RGUtils.TeardownOverrideEventSystem();
        }

    }
}
