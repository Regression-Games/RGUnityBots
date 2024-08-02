using System.Collections.Generic;

namespace RegressionGames.ActionManager
{
    public class RGActionAnalysisResult
    {
        public const int CURRENT_API_VERSION = 2; // Increment whenever breaking changes are made to the action format
        
        public int ApiVersion { get; set; } = CURRENT_API_VERSION; 
        
        public List<RGGameAction> Actions { get; set; }
    }
}