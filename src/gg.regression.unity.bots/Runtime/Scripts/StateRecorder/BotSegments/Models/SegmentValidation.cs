using System;
using System.Text;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;

namespace StateRecorder.BotSegments.Models
{
    [Serializable]
    public class SegmentValidation
    {
        
        // api version for this top level schema, update if we add/remove/change fields here
        public int apiVersion = SdkApiVersion.VERSION_28;

        public SegmentValidationType type;
        public IRGSegmentValidationData data;
        
        private bool _hasStarted = false;
        
        public int EffectiveApiVersion => Math.Max(apiVersion, data?.EffectiveApiVersion() ?? SdkApiVersion.CURRENT_VERSION);
        
        // called once per frame
        public void ProcessValidation(int segmentNumber)
        {
            if (!_hasStarted)
            {
                data.PrepareValidation(segmentNumber);
                _hasStarted = true;
            }
            data.ProcessValidation(segmentNumber);
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