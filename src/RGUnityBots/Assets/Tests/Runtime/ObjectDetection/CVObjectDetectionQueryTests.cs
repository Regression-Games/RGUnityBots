using NUnit.Framework;
using RegressionGames.StateRecorder.BotSegments.Models.AIService;
using UnityEngine;
using UnityEngine.TestTools;
using System;

namespace Tests.Runtime.ObjectDetection
{
    [TestFixture]
    public class CVObjectDetectionQueryTests
    {
        /// <summary>
        /// Tests the creation of a CVObjectDetectionRequest with a text query.
        /// </summary>
        [Test]
        public void TestCreateCVObjectDetectionTextQueryRequest()
        {
            // Arrange
            var screenshot = new CVImageBinaryData
            {
                width = 1920,
                height = 1080,
                data = new byte[] { 0, 1, 2, 3, 4 } // Dummy data
            };
            var textQuery = "button";
            var withinRect = new RectInt(100, 100, 200, 200);
            var threshold = 0.8f;

            // Act
            var request = new CVObjectDetectionRequest(
                screenshot: screenshot,
                textQuery: textQuery,
                imageQuery: null,
                withinRect: withinRect,
                threshold: threshold,
                index: 0
            );

            // Assert
            Assert.IsNotNull(request);
            Assert.AreEqual(screenshot, request.screenshot);
            Assert.AreEqual(textQuery, request.textQuery);
            Assert.IsNull(request.imageQuery);
            Assert.AreEqual(withinRect, request.withinRect);
            Assert.AreEqual(threshold, request.threshold);
            Assert.AreEqual(0, request.index);
        }

        /// <summary>
        /// Tests the creation of a CVObjectDetectionRequest with an image query.
        /// </summary>
        [Test]
        public void TestCreateCVObjectDetectionImageQueryRequest()
        {
            var screenshot = new CVImageBinaryData
            {
                width = 1920,
                height = 1080,
                data = new byte[] { 0, 1, 2, 3, 4 } // Dummy data
            };
            var imageQuery = Convert.ToBase64String(new byte[] { 5, 6, 7, 8, 9 }); // Base64 encoded dummy data
            var withinRect = new RectInt(100, 100, 200, 200);
            var threshold = 0.8f;

            var request = new CVObjectDetectionRequest(
                screenshot: screenshot,
                textQuery: null,
                imageQuery: imageQuery,
                withinRect: withinRect,
                threshold: threshold,
                index: 0
            );

            Assert.IsNotNull(request);
            Assert.AreEqual(screenshot, request.screenshot);
            Assert.IsNull(request.textQuery);
            Assert.AreEqual(imageQuery, request.imageQuery);
            Assert.AreEqual(withinRect, request.withinRect);
            Assert.AreEqual(threshold, request.threshold);
            Assert.AreEqual(0, request.index);
        }

        /// <summary>
        /// Tests the behavior of CVObjectDetectionRequest when both text and image queries are provided.
        /// </summary>
        [Test]
        public void TestCreateCVObjectDetectionRequestWithBothQueries()
        {
            var screenshot = new CVImageBinaryData
            {
                width = 1920,
                height = 1080,
                data = new byte[] { 0, 1, 2, 3, 4 } // Dummy data
            };
            var textQuery = "button";
            var imageQuery = Convert.ToBase64String(new byte[] { 5, 6, 7, 8, 9 }); // Base64 encoded dummy data
            var withinRect = new RectInt(100, 100, 200, 200);
            var threshold = 0.8f;

            // Check whether we are getting the error message.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@".*Both textQuery and imageQuery are provided\. Only one should be used\..*"));

            var request = new CVObjectDetectionRequest(
                screenshot: screenshot,
                textQuery: textQuery,
                imageQuery: imageQuery,
                withinRect: withinRect,
                threshold: threshold,
                index: 0
            );

            Assert.IsNotNull(request);
        }
    }
}
