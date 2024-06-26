﻿using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder.Models;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public interface IBotActionData
    {

        /**
         * Called before the first call to ProcessAction to allow data setup by the action
         */
        public void StartAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms);

        /**
         * Called at least once per frame
         * returns true if an action was performed
         * Returns null or an error message string
         */
        public bool ProcessAction(int segmentNumber, Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms, out string error);

        public void WriteToStringBuilder(StringBuilder stringBuilder);

        public void ReplayReset();

        /**
         * Returns null if the action runs until the keyframecriteria match
         */
        public bool? IsCompleted();

        public int EffectiveApiVersion();

        // Allows the impl to draw debug elements on the screen
        public void OnGUI(Dictionary<int, TransformStatus> currentUITransforms, Dictionary<int, TransformStatus> currentGameObjectTransforms)
        {
            // default to no-op impl
        }
    }
}
