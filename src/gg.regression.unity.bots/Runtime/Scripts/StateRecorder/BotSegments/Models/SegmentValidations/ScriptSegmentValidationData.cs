using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StateRecorder.BotSegments.Models.SegmentValidations
{
    
    /**
     * <summary>Data and functionality for a RGValidator script</summary>
     */
    [Serializable]
    public class ScriptSegmentValidationData: IRGSegmentValidationData
    {
        
        public int apiVersion = SdkApiVersion.VERSION_28;
        
        private static readonly Dictionary<string, Type> CachedTypes = new();

        public string classFullName;
        public float timeout = float.NegativeInfinity;

        private bool _isStopped;
        private float _startTime;
        
        private RGValidationScript _myValidationScript;
        private SegmentValidationResultSetContainer _storedResults;
        
        private volatile Type _typeToCreate = null;
        private volatile bool _readyToCreate;
        private volatile string _error = null;
        
        public void PrepareValidation(int segmentNumber)
        {
            if (!_isStopped)
            {
                // load the type on another thread to avoid 'hitching' the game
                new Thread(() =>
                {
                    if (!CachedTypes.TryGetValue(classFullName, out var t))
                    {
                        // load our script type without knowing the assembly name, just the full type
                        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            foreach (var type in a.GetTypes())
                            {
                                if (type.FullName != null && type.FullName.Equals(classFullName))
                                {
                                    t = type;
                                    CachedTypes[classFullName] = t;
                                    break;
                                }
                            }
                        }
                    }

                    // Make sure this type is not null and is inheriting from RGValidateBehaviour
                    if (t == null)
                    {
                        _error = $"({segmentNumber}) - Bot Segment Validations - Regression Games could not load Bot Segment Validation Script for Type - {classFullName}. Type was not found in any assembly in the current runtime.";
                        RGDebug.LogError(_error);
                    }
                    else if (!typeof(RGValidationScript).IsAssignableFrom(t))
                    {
                        _error = $"({segmentNumber}) - Bot Segment Validations - Regression Games could not load Bot Segment Validation Script for Type - {classFullName}. This Type does not inherit from RGValidateBehaviour.";
                        RGDebug.LogError(_error);
                    }
                    else
                    {
                        _typeToCreate = t;
                    }

                    _readyToCreate = true;
                }).Start();
            }
        }

        public void ProcessValidation(int segmentNumber)
        {

            if (!_isStopped)
            {
                if (_readyToCreate)
                {
                    _readyToCreate = false;
                    if (_typeToCreate != null)
                    {
                        // Create the type
                        _myValidationScript = Activator.CreateInstance(_typeToCreate) as RGValidationScript;
                        _myValidationScript?.Initialize();
                        _startTime = Time.time;
                    }
                    else
                    {
                        RGDebug.LogError($"({segmentNumber}) - Bot Segment Validations - Could not load type for validation script");
                        _isStopped = true;
                    }
                }

                if (_myValidationScript != null)
                {
                    
                    _myValidationScript.ProcessValidations();

                    if (timeout > 0 && _startTime > 0 && Time.time - _startTime > timeout)
                    {
                        // Validation is still not stopped at the time limit
                        RGDebug.LogInfo($"({segmentNumber}) - Bot Segment Validations - Time limit has been reached for validations in {classFullName}");
                        StopValidation(segmentNumber);
                    }
                    
                }
            }
            
        }

        public void PauseValidation(int segmentNumber)
        {
            _myValidationScript?.PauseValidation();
        }

        public void UnPauseValidation(int segmentNumber)
        {
            _myValidationScript?.UnPauseValidation();
        }

        public void StopValidation(int segmentNumber)
        {
            
            RGDebug.LogInfo($"({segmentNumber}) - Bot Segment Validations - Stopping validation for {classFullName}");
            
            // First, make sure RGValidateBehaviour marks the final results as pass or fail based on the desired
            // conditions.
            _myValidationScript?.StopValidations();
            
            // Then backup the results since we are going to be destroying the behaviour
            _storedResults = _myValidationScript?.GetResults();
            
            // Finally, make this null
            _myValidationScript = null;
            _isStopped = true;
            _startTime = float.NegativeInfinity;
        }

        public void ResetResults()
        {
            _storedResults = null; // Technically not needed, but here for safety
            _myValidationScript?.ResetResults();
            _isStopped = false;
        }
        
        public bool HasSetAllResults()
        {
            var results = _storedResults ?? _myValidationScript?.GetResults();
            return results?.validationResults.All(v => v.result != SegmentValidationStatus.UNKNOWN) ?? false;
        }

        public SegmentValidationResultSetContainer GetResults()
        {
            return _storedResults ?? _myValidationScript?.GetResults();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"classFullName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, classFullName);
            stringBuilder.Append(",\"timeout\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, timeout.ToString());
            stringBuilder.Append("}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}