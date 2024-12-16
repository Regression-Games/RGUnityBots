using System.Collections.Generic;
using System.Text;
using RegressionGames.StateRecorder;
using StateRecorder.BotSegments.Models.SegmentValidations;

namespace StateRecorder.BotSegments.Models
{
    public interface IRGSegmentValidationData
    {
     
        /**
         * Conducts any setup for this validation type
         */
        public void PrepareValidation(int segmentNumber);

        /**
         * Called at least once per frame
         * The validation may choose to evaluate this turn or skip validation
         */
        public void ProcessValidation(int segmentNumber);

        /**
         * Handles pausing the validation from the UI
         */
        public void PauseValidation(int segmentNumber);

        /**
         * Handles unpausing the validation from the UI
         */
        public void UnPauseValidation(int segmentNumber);

        /**
         * Indicates that the segment has ended the validation phase
         */
        public void StopValidation(int segmentNumber);

        /**
         * Resets any results contained within this validation
         */
        public void ResetResults();

        /**
         * Returns true if there are no "UNKNOWN" validations.
         */
        public bool HasSetAllResults();

        /**
         * Returns all results for this particular validation. In some
         * cases, this can be a set of results rather than just a single
         * result.
         */
        public SegmentValidationResultSetContainer GetResults();
        
        public void WriteToStringBuilder(StringBuilder stringBuilder);
        
        public int EffectiveApiVersion(); 
        
    }
}