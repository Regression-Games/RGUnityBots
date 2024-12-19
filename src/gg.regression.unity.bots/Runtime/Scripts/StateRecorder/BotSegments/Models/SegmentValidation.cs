using System;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

namespace StateRecorder.BotSegments.Models
{
    [Serializable]
    public class SegmentValidation
    {
        
        // api version for this top level schema, update if we add/remove/change fields here
        public int apiVersion = SdkApiVersion.VERSION_29;

        public SegmentValidationType type;
        public IRGSegmentValidationData data;
        
        public int EffectiveApiVersion => Math.Max(apiVersion, data?.EffectiveApiVersion() ?? SdkApiVersion.CURRENT_VERSION);

        private bool _validationIsReady;
        
        // called once per frame
        // returns true if the validation is running, or false if it is still loading up. This is used to 
        // be able to wait for the validation to be ready before running the segments.
        public bool ProcessValidation(int segmentNumber)
        {
            if (!_validationIsReady)
            {
                _validationIsReady = data.AttemptPrepareValidation(segmentNumber);
            }
            
            // If the validation is now ready, we can start running it
            if (_validationIsReady)
            {
                data.ProcessValidation(segmentNumber);
            }

            return _validationIsReady;
        }

        // called when a segment ends to stop any validation processing
        // Validations should update their final results at this point
        public void StopValidation(int segmentNumber)
        {
            data.StopValidation(segmentNumber);
        }

        /**
         * Handles resuming the paused validation if un-paused from the UI
         */
        public void UnPauseValidation(int segmentNumber)
        {
            data.UnPauseValidation(segmentNumber);
        }

        /**
         * Handle pausing the validation if paused from the UI
         */
        public void PauseValidation(int segmentNumber)
        {
            data.PauseValidation(segmentNumber);
        }

        public void ReplayReset()
        {
            data.ResetResults();
        }

        public bool HasSetAllResults()
        {
            return data.HasSetAllResults();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"type\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, type.ToString());
            stringBuilder.Append(",\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"data\":");
            data.WriteToStringBuilder(stringBuilder);
            stringBuilder.Append("}");
        }
        
        public override string ToString()
        {
            return ((IStringBuilderWriteable) this).ToJsonString();
        }

    }
}
