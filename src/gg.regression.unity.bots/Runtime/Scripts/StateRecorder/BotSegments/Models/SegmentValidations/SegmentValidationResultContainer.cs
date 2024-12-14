using System;
using JetBrains.Annotations;

namespace StateRecorder.BotSegments.Models.SegmentValidations
{
    
    [Serializable]
    public class SegmentValidationResultContainer
    {

        /**
         * <summary>
         * The name of the validation. Sometimes this is just the name
         * of the test function.
         * </summary>
         */
        public string name;

        /**
         * <summary>
         * An optional description of the validation.
         * </summary>
         */
        [CanBeNull] public string description;

        /**
         * <summary>
         * The actual state of this validation result
         * </summary>
         */
        public SegmentValidationStatus result;

        public SegmentValidationResultContainer(string name, [CanBeNull] string description, SegmentValidationStatus result)
        {
            this.name = name;
            this.description = description;
            this.result = result;
        }
    }
}