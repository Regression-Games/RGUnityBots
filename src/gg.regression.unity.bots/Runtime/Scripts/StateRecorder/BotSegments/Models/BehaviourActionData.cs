using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on a random pixel in the frame</summary>
     */
    [Serializable]
    public class BehaviourActionData : IBotActionData
    {
        // api version for this object, update if object format changes
        public int apiVersion = SdkApiVersion.VERSION_5;

        private static readonly Dictionary<string, Type> CachedTypes = new();

        public string behaviourFullName;

        public float? maxRuntimeSeconds;

        private bool _isStopped;

        private GameObject _myGameObject;

        public bool IsCompleted()
        {
            return _isStopped;
        }

        public void ReplayReset()
        {
            _isStopped = false;
        }

        private volatile Type _typeToCreate = null;
        private volatile bool _readyToCreate;
        private volatile string _error = null;

        private float _startTime = -1f;

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // load the type on another thread to avoid 'hitching' the game
            new Thread(() =>
            {
                if (!CachedTypes.TryGetValue(behaviourFullName, out var t))
                {
                    // load our script type without knowing the assembly name, just the full type
                    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in a.GetTypes())
                        {
                            if (type.FullName != null && type.FullName.Equals(behaviourFullName))
                            {
                                t = type;
                                CachedTypes[behaviourFullName] = t;
                                break;
                            }
                        }
                    }
                }

                if (t == null)
                {
                    _error = $"Regression Games could not load Bot Segment Behaviour Action for Type - {behaviourFullName}. Type was not found in any assembly in the current runtime.";
                    RGDebug.LogError(_error);
                }
                else
                {
                    _typeToCreate = t;
                }

                _readyToCreate = true;
            }).Start();
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            var time = Time.unscaledTime;

            if (!_isStopped)
            {
                if (_readyToCreate)
                {
                    _readyToCreate = false;
                    if (_typeToCreate != null)
                    {
                        // load behaviour and set as a child of the playback controller
                        var pbController = UnityEngine.Object.FindObjectOfType<BotSegmentsPlaybackController>();
                        _myGameObject = new GameObject($"BehaviourAction_{behaviourFullName}")
                        {
                            transform =
                            {
                                parent = pbController.transform
                            }
                        };

                        // Attach our behaviour
                        _myGameObject.AddComponent(_typeToCreate);
                        _startTime = time;
                    }
                    else
                    {
                        // couldn't load the type; don't stop or error reporting won't work
                        error = _error;
                        return false;
                    }
                }

                if (_myGameObject != null)
                {
                    if (!_myGameObject.GetComponent(_typeToCreate))
                    {
                        // StopAction really calls AbortAction by default, but this is a code sample of how to call StopAction in case it is overridden
                        // This represents the 'clean end' case where the behaviour destroyed itself after completing
                        ((IBotActionData)this).StopAction(segmentNumber, currentTransforms, currentEntities);
                        _error = null;
                        error = _error;
                        return false;
                    }

                    if (maxRuntimeSeconds.HasValue && _startTime > 0 && time - _startTime > maxRuntimeSeconds.Value)
                    {
                        // behaviour is still not destroyed at the time limit
                        // This represents a segment failure case where the timeout stops the Behaviour that did not find its criteria and end in a timely manner
                        // we destroy the behaviour + gameObject, but don't mark this stopped so that error reporting works
                        DestroyGameObject();
                        _error = $"Behaviour did not complete within maxRuntimeSeconds: {maxRuntimeSeconds.Value}";
                        error = _error;
                        return false;
                    }

                    // Behaviour is expected to perform its actions in its own 'Update' or 'LateUpdate' calls... we don't directly call it
                    // When it reaches its own self determined end condition, it should destroy itself

                    // It can get the current state information from the runtime directly... or can access our information by using
                    // UnityEngine.Object.FindObjectOfType<TransformObjectFinder>().GetObjectStatusForCurrentFrame();
                    // and/or
                    // UnityEngine.Object.FindObjectOfType<EntityObjectFinder>().GetObjectStatusForCurrentFrame(); - for runtimes with ECS support

                    // This is the regular update loop case while the behaviour is still actively running
                    _error = null;
                    error = _error;
                    return true;
                }
            }

            error = _error;
            return false;
        }

        private void DestroyGameObject()
        {
            if (_myGameObject != null)
            {
                UnityEngine.Object.Destroy(_myGameObject);
            }
            _myGameObject = null;
        }

        public void AbortAction(int segmentNumber)
        {
            _isStopped = true;
            DestroyGameObject();
        }

        public void OnGUI(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // OnGUI can be implemented on the behaviour itself
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"maxRuntimeSeconds\":");
            FloatJsonConverter.WriteToStringBuilderNullable(stringBuilder, maxRuntimeSeconds);
            stringBuilder.Append(",\"behaviourFullName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, behaviourFullName);
            stringBuilder.Append("}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
