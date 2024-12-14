using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace StateRecorder.BotSegments.Models.SegmentValidations
{
    
    [Serializable]
    public class SegmentValidationResultSetContainer
    {

        /**
         * <summary>
         * The optional name of this set of results. In some cases such
         * as that of the Script type, there is a name for the "suite" of
         * validations in the script. In other cases, like EntityExists,
         * it is just a standalone result, and therefore does not have a
         * name or any organizational structure.
         * </summary>
         */
        [CanBeNull] public string name;

        /**
         * <summary>
         * The results of the validations. Note that these containers are
         * instantiated right away for a segment, and so they may start off
         * in an "UNKNOWN" status state since they have not been evaluated
         * yet.
         * </summary>
         */
        public List<SegmentValidationResultContainer> validationResults;

    }
}