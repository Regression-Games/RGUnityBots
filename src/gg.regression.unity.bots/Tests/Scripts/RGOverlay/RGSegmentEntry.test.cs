using NUnit.Framework;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.TestFramework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RegressionGames.Tests.RGOverlay
{
    [TestFixture]
    public class RGSegmentEntryTests
    {
        private GameObject _uat;

        private RGSegmentEntry entry;

        [SetUp]
        public void SetUp()
        {
            _uat = new GameObject();
            entry = _uat.AddComponent<RGSegmentEntry>();
            entry.segmentName = "TestSegment";
            entry.description = "TestDescription";
            entry.filePath = "test/path";
            entry.type = BotSequenceEntryType.Segment;
            entry.nameComponent = RGTestUtils.CreateTMProPlaceholder();
            entry.descriptionComponent = RGTestUtils.CreateTMProPlaceholder();
            entry.playButton = _uat.AddComponent<Button>();
            
            // create tooltip child
            var tooltip = new GameObject() {
                transform =
                {
                    parent = entry.transform,
                },
            };
            tooltip.AddComponent<RGTooltip>();
            tooltip.transform.SetParent(entry.transform);

            // create segment list indicator w/Image
            var segmentListIndicator = new GameObject() {
                transform = {
                    parent = entry.transform,
                },
            };
            segmentListIndicator.AddComponent<Image>();
            entry.segmentListIndicatorComponent = segmentListIndicator;
            entry.segmentListIndicatorComponent.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_uat);
        }

        [Test]
        public void Start()
        {
            entry.Start();
            
            Assert.AreEqual(entry.filePath, "test/path");
            Assert.AreEqual(entry.nameComponent.text, entry.segmentName);
            Assert.AreEqual(entry.descriptionComponent.text, entry.description);
            Assert.IsFalse(entry.segmentListIndicatorComponent.activeSelf);
        }
        
        [Test]
        public void Start_SegmentList()
        {
            entry.type = BotSequenceEntryType.SegmentList;
            entry.Start();
            
            Assert.IsTrue(entry.segmentListIndicatorComponent.activeSelf);
        }
    }
}
