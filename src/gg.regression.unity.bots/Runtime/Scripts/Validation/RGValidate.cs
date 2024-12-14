using System;
using JetBrains.Annotations;
using StateRecorder.BotSegments.Models.SegmentValidations;
using UnityEngine;

namespace RegressionGames.Validation
{
        
    /**
     * An attribute that marks a method as a validation method
     * to be run by Regression Games during a bot sequence or bot segment.
     * You can set a frequency attribute to indicate how often the validation
     * is running (e.g. every 10th frame).
     * You can also indicate whether this is a validation that should
     * always be true, never be true, or be true at least once. 
     */
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    public class RGValidate: Attribute {
        
        public int Frequency { get; private set; }
        public ValidationMode Mode { get; private set; }

        public RGValidate(ValidationMode mode, int frequency = 1)
        {
            Mode = mode;
            Frequency = Mathf.Max(1, frequency); // Ensure frequency is at least 1
        }
        
    }
}