using System;

namespace RegressionGames.Types
{

    [Serializable]
    public class RGValidationSummary
    {
        public long passed;
        public long failed;
        public long warnings;
        public long total;

        public RGValidationSummary(long passed, long failed, long warnings)
        {
            this.passed = passed;
            this.failed = failed;
            this.warnings = warnings;
            this.total = passed + failed + warnings;
        }
    }
}