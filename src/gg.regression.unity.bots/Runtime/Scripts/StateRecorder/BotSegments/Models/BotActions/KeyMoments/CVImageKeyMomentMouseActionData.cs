using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// ReSharper disable InconsistentNaming

namespace RegressionGames.StateRecorder.BotSegments.Models.BotActions.KeyMoments
{

    /**
     * <summary>Data for clicking on a key moment object in the frame.  This is used to record key moment bot segments based on mouse actions.</summary>
     */
    [Serializable]
    public class CVImageKeyMomentMouseActionData : IBotActionData, IKeyMomentExploration, IStringBuilderWriteable, IKeyMomentStringBuilderWriteable
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_29;

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.KeyMoment_MouseAction_CVImage;

        public List<MouseInputActionData> mouseActions = new();

        public List<List<byte[]>> mouseActionImages = new ();

        private bool _isStopped;

        // wait one frame after finishing to let the game process the action before moving to the next segment
        // this is important as we don't want the next segment to run on this same frame until the result of the mouse click action has processed in the game engine
        private bool _isDoneWaitOneFrame = false;

        // These are all used to track the un-click work across multiple Update calls
        private double _unClickTime = 0d;
        private Vector2? _lastClickPosition = null;
        private MouseInputActionData _unClickAction = null;

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public void ReplayReset()
        {
            _unClickAction = null;
            _unClickTime = 0d;
            _lastClickPosition = null;

            _isStopped = false;
            _isDoneWaitOneFrame = false;
        }

        public void KeyMomentExplorationReset()
        {
            // get ready to do this all over again
            _unClickAction = null;
            _unClickTime = 0d;
            _lastClickPosition = null;

            _isStopped = false;
            _isDoneWaitOneFrame = false;
        }

        public void ActivateExploration()
        {
            // start the exploration process
        }

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            if (!_isStopped)
            {
                // no-op
            }
        }

        private bool HandleUnClickAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            error = null;
            return false;
        }

        /**
         * Handles the mouse inputs to each send their event on a different update loop pass, otherwise the UI event system fails to process correctly as it can't handle 2 mouse input events on the same update
         */
        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            error = null;
            return false;
        }

        public void AbortAction(int segmentNumber)
        {
            _isStopped = true;
            _isDoneWaitOneFrame = true;
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"mouseActions\":[\n");
            var mouseActionsCount = mouseActions.Count;
            for (var i = 0; i < mouseActionsCount; i++)
            {
                var mouseAction = mouseActions[i];
                mouseAction.WriteToStringBuilder(stringBuilder);
                if (i + 1 < mouseActionsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n,\"mouseActionImages\":[\n");
            var mouseActionImagesCount = mouseActionImages.Count;
            for (var i = 0; i < mouseActionImagesCount; i++)
            {
                var mouseActionImageList = mouseActionImages[i];
                var mouseActionImageListCount = mouseActionImageList.Count;
                stringBuilder.Append("[\n");
                for (var j = 0; j < mouseActionImageListCount; j++)
                {
                    var mouseActionImage = mouseActionImageList[j];
                    var base64ImageData = Convert.ToBase64String(mouseActionImage);
                    StringJsonConverter.WriteToStringBuilder(stringBuilder, base64ImageData);
                    if (j + 1 < mouseActionImageListCount)
                    {
                        stringBuilder.Append(",\n");
                    }
                }
                stringBuilder.Append("\n]");
                if (i + 1 < mouseActionsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]}");
        }

        public void WriteKeyMomentToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"mouseActions\":[\n");
            var mouseActionsCount = mouseActions.Count;
            for (var i = 0; i < mouseActionsCount; i++)
            {
                var mouseAction = mouseActions[i];
                mouseAction.WriteKeyMomentToStringBuilder(stringBuilder);
                if (i + 1 < mouseActionsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n,\"mouseActionImages\":[\n");
            var mouseActionImagesCount = mouseActionImages.Count;
            for (var i = 0; i < mouseActionImagesCount; i++)
            {
                var mouseActionImageList = mouseActionImages[i];
                var mouseActionImageListCount = mouseActionImageList.Count;
                stringBuilder.Append("[\n");
                for (var j = 0; j < mouseActionImageListCount; j++)
                {
                    var mouseActionImage = mouseActionImageList[j];
                    var base64ImageData = Convert.ToBase64String(mouseActionImage);
                    StringJsonConverter.WriteToStringBuilder(stringBuilder, base64ImageData);
                    if (j + 1 < mouseActionImageListCount)
                    {
                        stringBuilder.Append(",\n");
                    }
                }
                stringBuilder.Append("\n]");
                if (i + 1 < mouseActionsCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
