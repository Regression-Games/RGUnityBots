using System;
using System.Collections.Generic;
using System.IO;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.BotSegments.Models.BotCriteria;
using RegressionGames.StateRecorder.BotSegments.Models.BotActions.KeyMoments;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RegressionGames.StateRecorder.KeyMoments
{
    public class CVImageMouseKeyMomentEvaluator : IKeyMomentEvaluator
    {
        public readonly List<MouseInputActionData> MouseDataBuffer = new();

        // track an image from the GPU for every click or un-click
        public readonly List<(byte[], int, int)> ImageBuffer = new();

        private MouseInputActionData _previousKeyMomentMouseInputState = null;

        public BotSegment IsKeyMoment(long tickNumber, long keyMomentNumber)
        {
            // build out the list of clicks and un-clicks up to the last un-click
            List<(int, MouseInputActionData)> meaningfulInputs = new();
            MouseInputActionData priorMouseData = _previousKeyMomentMouseInputState;
            var mouseDataBufferCount = MouseDataBuffer.Count;
            var foundUnClick = false;
            for (var i = 0; !foundUnClick && i < mouseDataBufferCount; i++)
            {
                var mouseData = MouseDataBuffer[i];
                var isUnClick = priorMouseData != null && mouseData.IsButtonUnClick(priorMouseData);
                if (isUnClick || mouseData.IsButtonClick(priorMouseData))
                {
                    meaningfulInputs.Add((i,mouseData));
                }
                if (isUnClick)
                {
                    foundUnClick = true;
                    // we found an un-click, we're done
                }

                priorMouseData = mouseData;
            }

            if (foundUnClick)
            {
                // this should always be either length 1 - only an un-click, or length 2 - click/un-click pair

                // only take from the buffer up to the last 'un-clicked' state
                // this is 'hard'... we want to encapsulate click down/up as a single action together
                // but in cases like FPS where I might 'hold' right click to ADS then repeatedly click/release the left mouse button to fire.. i don't want that ALL as one action
                // each shot of the weapon would be an 'action', AND .. ads was an action.. so we break this up by any button release

                var inputsToProcess = new List<MouseInputActionData>();

                // track the images to consider as matches for each mouse action
                var actionImages = new List<List<byte[]>>();

                // add a 'positional' prior state before the click/un-click
                var priorIndex = meaningfulInputs[0].Item1 - 1;
                if (priorIndex >= 0)
                {
                    inputsToProcess.Add(MouseDataBuffer[priorIndex]);
                }

                if (ImageBuffer.Count < meaningfulInputs.Count)
                {
                    // still waiting on the screenshot
                    return null;
                }

                // if meaningfulInputs.Count == 1, then we just had an un-click and that's it... that would happen if you had many buttons held down, and release 1 button 1 frame and another button another frame
                if (meaningfulInputs.Count > 1)
                {
                    // add the click
                    var clickScreenshot = ImageBuffer[0];
                    var clickImageList = GenerateClickImagesAtPoint(meaningfulInputs[0].Item2.position, clickScreenshot);

                    inputsToProcess.Add(meaningfulInputs[0].Item2);
                    actionImages.Add(clickImageList);

                    // add the un-click
                    var unClickScreenshot = ImageBuffer[1];
                    // generate the images for the un-click position at the time of the click first
                    var unClickImageList = GenerateClickImagesAtPoint(meaningfulInputs[^1].Item2.position, clickScreenshot);
                    // also generate the images for the un-click position at the time of the un-click
                    unClickImageList.AddRange(GenerateClickImagesAtPoint(meaningfulInputs[^1].Item2.position, unClickScreenshot));

                    inputsToProcess.Add(meaningfulInputs[^1].Item2);
                    actionImages.Add(unClickImageList);
                }
                else
                {
                    // only had an un-click
                    // generate the images for the un-click position at the time of the un-click
                    var unClickScreenshot = ImageBuffer[0];
                    var unClickImageList = GenerateClickImagesAtPoint(meaningfulInputs[^1].Item2.position, unClickScreenshot);

                    inputsToProcess.Add(meaningfulInputs[^1].Item2);
                    actionImages.Add(unClickImageList);
                }


                var botSegment = new BotSegment
                {
                    name = $"KeyMoment: {keyMomentNumber}, Tick: {tickNumber} - Mouse Action Segment",

                    endCriteria = new List<KeyFrameCriteria>
                    {
                        new()
                        {
                            type = KeyFrameCriteriaType.ActionComplete,
                            data = new ActionCompleteKeyFrameCriteriaData()
                        }
                    },
                    botAction = new BotAction
                    {
                        type = BotActionType.KeyMoment_MouseAction_CVImage,
                        data = new CVImageKeyMomentMouseActionData
                        {
                            // the length of these 2 lists should match
                            mouseActions = inputsToProcess,
                            mouseActionImages = actionImages
                        }
                    }
                };

                if (inputsToProcess.Count == 2 && inputsToProcess[^1].clickedObjectNormalizedPaths.Length < 1)
                {
                    // we had an un-click only segment on something without any paths.. basically an un-click on nothing
                    // this can happen when there is a random click/un-click on nothing in the game or on something that is excluded from RG seeing it in the state

                    // we should NOT record this segment
                    return null;
                }

                // update the last state based on the end entry in our list
                _previousKeyMomentMouseInputState = meaningfulInputs[^1].Item2;

                // Dev DEBUG.... write out the images so that a human can see them
                for (var i = 0; i < actionImages.Count; i++)
                {
                    var isClick = actionImages.Count > 1 && i == 0;
                    var actionImageList = actionImages[i];
                    for (var j = 0; j < actionImageList.Count; j++)
                    {
                        var actionImage = actionImageList[j];
                        PersistImage(ScreenRecorder._currentGameplaySessionKeyMomentsDirectoryPrefix, keyMomentNumber, j+1, isClick, actionImage);
                    }
                }


                // persist the original screenshots for debug purposes
                if (actionImages.Count > 1)
                {
                    PersistImage(ScreenRecorder._currentGameplaySessionKeyMomentsDirectoryPrefix, keyMomentNumber, 0, true, ImageBuffer[0].Item1);
                    PersistImage(ScreenRecorder._currentGameplaySessionKeyMomentsDirectoryPrefix, keyMomentNumber, 0, false, ImageBuffer[1].Item1);
                }
                else
                {
                    PersistImage(ScreenRecorder._currentGameplaySessionKeyMomentsDirectoryPrefix, keyMomentNumber, 0, false, ImageBuffer[0].Item1);
                }

                var count = meaningfulInputs.Count;
                // remove everything up through the un-click
                MouseDataBuffer.RemoveRange(0, meaningfulInputs[^1].Item1 + 1);
                ImageBuffer.RemoveRange(0, count);

                return botSegment;
            }

            return null;
        }

        private void PersistImage(string directoryPath, long keyMomentNumber, long imageNumber, bool isClick, byte[] imageData)
        {
            try
            {
                // write out the json to file... these numbers do NOT align with segments or state data, but have those numbers in the path for convenience
                var path = $"{directoryPath}/{keyMomentNumber}_{(isClick?"click":"un-click")}_{imageNumber}.jpg";

                // Save the byte array as a file
                File.WriteAllBytes(path, imageData);
            }
            catch (Exception e)
            {
                RGDebug.LogException(e, $"ERROR: Unable to record Mouse Image for key moment # {keyMomentNumber}");
            }
        }

        private List<byte[]> GenerateClickImagesAtPoint(Vector2Int position, (byte[],int,int) screenShot)
        {
            // incoming data is jpg byte[], screenWidth, screenHeight

            var screenWidth = screenShot.Item2;
            var screenHeight = screenShot.Item3;

            var minWidthHeight = Math.Min(screenWidth, screenHeight);
            var oneThird = Mathf.RoundToInt(minWidthHeight / 3f);
            var oneSixth = Mathf.RoundToInt(minWidthHeight / 6f);
            var oneTwelfth = Mathf.RoundToInt(minWidthHeight / 12f);

            var oneTenth = Mathf.RoundToInt(minWidthHeight / 10f);
            var oneTwentieth = Mathf.RoundToInt(minWidthHeight / 20f);
            var oneFortieth = Mathf.RoundToInt(minWidthHeight / 40f);

            var result = new List<byte[]>();

            // Future: We may come back and capture different and/or more image sizes/shapes based on research of what works best for finding matches
            // Future: We may want to use the click point to do edge/object detection at that point and add that to the result set

            // Want to capture various images that are common to button / game object sizes
            // screen size is considered as the smaller of height or width for these comments
            // 1/10 screen size is about the size of buttons/objects in game in BossRoom, Match3, and other projects
            // a square, a lateral rect, a vertical rect
            // - square - small - 1/20 screen size width/height
            // - square - normal - 1/10 screen size width/height
            // - lateral rect - big - 1/10 screen size height + 1/3 screen size width
            // - lateral rect - normal - 1/20 screen size height + 1/6 screen size width
            // - vertical rect - big - 1/3 screen size height + 1/10 screen size width
            // - vertical rect - normal - 1/6 screen size height + 1/20 screen size width

            var ssTexture = new Texture2D(screenWidth, screenHeight, TextureFormat.RGBA32, false);
            if (!ssTexture.LoadImage(screenShot.Item1))
            {
                RGDebug.LogError($"Error reading input image data from JPG bte[] array width:{screenShot.Item2}, height: {screenShot.Item3}");
                return null;
            }

            // offset positions around the center click point
            var minX = Math.Max(0, position.x - oneFortieth);
            var minY = Math.Max(0, position.y - oneFortieth);
            var xClipped = Math.Abs(position.x - minX - oneFortieth);
            var yClipped = Math.Abs(position.y - minY - oneFortieth);
            var width = Math.Min(oneTwentieth-(xClipped*2), screenWidth - minX);
            var height = Math.Min(oneTwentieth-(yClipped*2), screenHeight - minY);
            var squareSmall = ssTexture.GetPixels(minX, minY, width, height);

            Texture2D destTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            destTex.SetPixels(squareSmall);

            result.Add(destTex.EncodeToJPG());

            minX = Math.Max(0, position.x - oneTwentieth);
            minY = Math.Max(0, position.y - oneTwentieth);
            xClipped = Math.Abs(position.x - minX - oneTwentieth);
            yClipped = Math.Abs(position.y - minY - oneTwentieth);
            width = Math.Min(oneTenth-(xClipped*2), screenWidth - minX);
            height = Math.Min(oneTenth-(yClipped*2), screenHeight - minY);
            var squareNormal = ssTexture.GetPixels(minX, minY, width, height);

            destTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            destTex.SetPixels(squareNormal);

            result.Add(destTex.EncodeToJPG());

            minX = Math.Max(0, position.x - oneSixth);
            minY = Math.Max(0, position.y - oneTwentieth);
            xClipped = Math.Abs(position.x - minX - oneSixth);
            yClipped = Math.Abs(position.y - minY - oneTwentieth);
            width = Math.Min(oneThird-(xClipped*2), screenWidth - minX);
            height = Math.Min(oneTenth-(yClipped*2), screenHeight - minY);
            var rectLateralBig = ssTexture.GetPixels(minX, minY, width, height);

            destTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            destTex.SetPixels(rectLateralBig);

            result.Add(destTex.EncodeToJPG());

            minX = Math.Max(0, position.x - oneTwelfth);
            minY = Math.Max(0, position.y - oneFortieth);
            xClipped = Math.Abs(position.x - minX - oneTwelfth);
            yClipped = Math.Abs(position.y - minY - oneFortieth);
            width = Math.Min(oneSixth-(xClipped*2), screenWidth - minX);
            height = Math.Min(oneTwentieth-(yClipped*2), screenHeight - minY);
            var rectLateralNormal = ssTexture.GetPixels(minX, minY, width, height);

            destTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            destTex.SetPixels(rectLateralNormal);

            result.Add(destTex.EncodeToJPG());

            minX = Math.Max(0, position.x - oneTwentieth);
            minY = Math.Max(0, position.y - oneSixth);
            xClipped = Math.Abs(position.x - minX - oneTwentieth);
            yClipped = Math.Abs(position.y - minY - oneSixth);
            width = Math.Min(oneTenth-(xClipped*2), screenWidth - minX);
            height = Math.Min(oneThird-(yClipped*2), screenHeight - minY);
            var recVerticalBig = ssTexture.GetPixels(minX, minY, width, height);

            destTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            destTex.SetPixels(recVerticalBig);

            result.Add(destTex.EncodeToJPG());

            minX = Math.Max(0, position.x - oneFortieth);
            minY = Math.Max(0, position.y - oneTwelfth);
            xClipped = Math.Abs(position.x - minX - oneFortieth);
            yClipped = Math.Abs(position.y - minY - oneTwelfth);
            width = Math.Min(oneTwentieth-(xClipped*2), screenWidth - minX);
            height = Math.Min(oneSixth-(yClipped*2), screenHeight - minY);
            var rectVerticalNormal = ssTexture.GetPixels(minX, minY, width, height);

            destTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            destTex.SetPixels(rectVerticalNormal);

            result.Add(destTex.EncodeToJPG());

            return result;
        }
    }
}
