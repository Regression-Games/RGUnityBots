using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    /**
     * <summary>Data for clicking on a random pixel in the frame</summary>
     */
    [Serializable]
    [JsonConverter(typeof(BehaviourActionDataJsonConverter))]
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
                    RGDebug.LogError($"Regression Games could not load Bot Segment Behaviour Action for Type - {behaviourFullName}. Type was not found in any assembly in the current runtime.");
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
                    if (_typeToCreate != null)
                    {
                        // load behaviour and set as a child of the playback controller
                        var pbController = UnityEngine.Object.FindObjectOfType<ReplayDataPlaybackController>();
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
                        // couldn't load the type.. just stop
                        _isStopped = true;
                    }

                    _readyToCreate = false;
                }

                if (_myGameObject != null)
                {
                    if (maxRuntimeSeconds.HasValue && _startTime > 0 && time - _startTime > maxRuntimeSeconds.Value)
                    {
                        StopAction(segmentNumber, currentTransforms, currentEntities);
                        // will return false
                    }
                    else
                    {
                        error = null;
                        return true;
                    }
                    // Behaviour is expected to perform its actions in its own 'Update' or 'LateUpdate' calls
                    // It can get the current state information from the runtime directly... or can access our information by using
                    // UnityEngine.Object.FindObjectOfType<TransformObjectFinder>().GetObjectStatusForCurrentFrame();
                    // and/or
                    // UnityEngine.Object.FindObjectOfType<EntityObjectFinder>().GetObjectStatusForCurrentFrame(); - for runtimes with ECS support
                }
            }

            error = null;
            return false;
        }

        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            _isStopped = true;
            if (_myGameObject != null)
            {
                UnityEngine.Object.Destroy(_myGameObject);
            }
            _myGameObject = null;
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
