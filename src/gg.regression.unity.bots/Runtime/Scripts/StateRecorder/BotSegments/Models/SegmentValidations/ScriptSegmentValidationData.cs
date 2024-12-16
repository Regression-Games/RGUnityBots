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

        private bool _isStopped;
        
        private GameObject _myGameObject;
        private RGValidateBehaviour _myValidationBehaviour;
        
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

                    // TODO(vontell): Need to check that this is an RGValidateBehaviour
                    if (t == null)
                    {
                        _error = $"Regression Games could not load Bot Segment Validation Script for Type - {classFullName}. Type was not found in any assembly in the current runtime.";
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

            string error;

            if (!_isStopped)
            {
                if (_readyToCreate)
                {
                    _readyToCreate = false;
                    if (_typeToCreate != null)
                    {
                        // load behaviour and set as a child of the playback controller
                        var pbController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
                        _myGameObject = new GameObject($"RGValidate_{classFullName}")
                        {
                            transform =
                            {
                                parent = pbController.transform
                            }
                        };

                        // Attach our validation script
                        _myValidationBehaviour = _myGameObject.AddComponent(_typeToCreate) as RGValidateBehaviour;
                    }
                    else
                    {
                        // couldn't load the type; don't stop or error reporting won't work
                        error = _error;
                    }
                }

                if (_myGameObject != null)
                {
                    if (!_myGameObject.TryGetComponent(_typeToCreate, out _))
                    {
                        ((IRGSegmentValidationData)this).StopValidation(segmentNumber);
                        _error = null;
                        error = _error;
                        // TODO(vontell): Here we used to return false - instead report result
                    }

                    // TODO(vontell): How do I want to support rogue scripts that keep running?
                    // if (maxRuntimeSeconds.HasValue && _startTime > 0 && time - _startTime > maxRuntimeSeconds.Value)
                    // {
                    //     // behaviour is still not destroyed at the time limit
                    //     // This represents a segment failure case where the timeout stops the Behaviour that did not find its criteria and end in a timely manner
                    //     // we destroy the behaviour + gameObject, but don't mark this stopped so that error reporting works
                    //     DestroyGameObject();
                    //     _error = $"Behaviour did not complete within maxRuntimeSeconds: {maxRuntimeSeconds.Value}";
                    //     error = _error;
                    //     return false;
                    // }

                    // Validation is expected to perform its actions in its own 'Update' or 'LateUpdate' calls... we don't directly call it
                    // TODO(vontell): When it reaches its own self-determined end condition, it should destroy itself

                    // It can get the current state information from the runtime directly... or can access our information by using
                    // UnityEngine.Object.FindObjectOfType<TransformObjectFinder>().GetObjectStatusForCurrentFrame();
                    // and/or
                    // UnityEngine.Object.FindObjectOfType<EntityObjectFinder>().GetObjectStatusForCurrentFrame(); - for runtimes with ECS support

                    // This is the regular update loop case while the behaviour is still actively running
                    _error = null;
                    error = _error;
                }
            }

            error = _error;
        }

        public void PauseValidation(int segmentNumber)
        {
            _myValidationBehaviour?.PauseValidation();
        }

        public void UnPauseValidation(int segmentNumber)
        {
            _myValidationBehaviour?.UnPauseValidation();
        }

        public void StopValidation(int segmentNumber)
        {
            if (_myValidationBehaviour)
            {
                Object.Destroy(_myValidationBehaviour);
            }
        }

        public void ResetResults()
        {
            _myValidationBehaviour?.ResetResults();
        }
        
        public bool HasSetAllResults()
        {
            return _myValidationBehaviour?.GetResults().validationResults.All(v => v.result != SegmentValidationStatus.UNKNOWN) ?? false;
        }

        public SegmentValidationResultSetContainer GetResults()
        {
            return _myValidationBehaviour?.GetResults();
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"classFullName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, classFullName);
            stringBuilder.Append("}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}