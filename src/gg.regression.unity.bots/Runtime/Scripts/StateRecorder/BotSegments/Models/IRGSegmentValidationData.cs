using System.Text;
using StateRecorder.BotSegments.Models.SegmentValidations;

namespace StateRecorder.BotSegments.Models
{
    public interface IRGSegmentValidationData
    {
     
        /**
         * Attempts to conduct any preparation needed for validation. Returns true if the validations
         * are ready to be run, and false otherwise. Implementors should make sure that this method
         * can handle being called ever update once, and ignore those requests if the validation is preparing itself
         * or is already prepared.
         */
        public bool AttemptPrepareValidation(int segmentNumber);

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