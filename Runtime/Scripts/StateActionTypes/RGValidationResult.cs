using System;
using JetBrains.Annotations;

namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public class RGValidationResult
    {
        
        public string name;
        public RGValidationResultType result;
        public string icon;
        public string id;
        public DateTime timestamp;
        public long tick;
        
        // These are kept for backwards compatibility
        public string message;
        public bool passed;

        public int version;

        public RGValidationResult(string name, RGValidationResultType result, long tick, [CanBeNull] string icon = null)
        {
            this.name = name;
            this.result = result;
            this.icon = icon;
            this.tick = tick;
            this.id = Guid.NewGuid().ToString();
            this.timestamp = DateTime.Now;
            
            // For backwards compatibility
            this.message = name;
            this.passed = result == RGValidationResultType.PASS;
            this.version = 1;
        }
    }
}
