using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.BotSegments.JsonConverters;
using RegressionGames.StateRecorder.JsonConverters;
using RegressionGames.StateRecorder.Models;
using UnityEngine;
using Object = UnityEngine.Object;

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

        [NonSerialized]
        public static readonly BotActionType Type = BotActionType.Behaviour;

        public string BehaviourFullName;

        private bool IsStopped;

        private GameObject myGameObject;

        public bool IsCompleted()
        {
            return IsStopped;
        }

        public void ReplayReset()
        {
        }

        public void StartAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            Type t = null;
            // load our script type without knowing the assembly name, just the full type
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in a.GetTypes())
                {
                    if (type.FullName != null && type.FullName.Equals(BehaviourFullName))
                    {
                        t = type;
                        break;
                    }
                }
            }
            if (t == null)
            {
                RGDebug.LogError($"Regression Games could not load Bot Segment Behaviour Action for Type - {BehaviourFullName}. Type was not found in any assembly in the current runtime.");
            }
            else
            {
                // load behaviour and set as a child of the playback controller
                var pbController = UnityEngine.Object.FindObjectOfType<ReplayDataPlaybackController>();
                myGameObject = new GameObject($"BehaviourAction_{BehaviourFullName}")
                {
                    transform =
                    {
                        parent = pbController.transform
                    }
                };

                // Attach our behaviour
                myGameObject.AddComponent(t);
            }
        }

        public bool ProcessAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities, out string error)
        {
            // Behaviour is expected to perform its actions in its own 'Update' or 'LateUpdate' calls
            // It can get the current state information from the runtime directly... or can access our information by using
            // UnityEngine.Object.FindObjectOfType<TransformObjectFinder>().GetObjectStatusForCurrentFrame();
            // and/or
            // UnityEngine.Object.FindObjectOfType<EntityObjectFinder>().GetObjectStatusForCurrentFrame(); - for runtimes with ECS support
            error = null;
            if (!IsStopped)
            {
                return true;
            }

            return false;
        }

        public void StopAction(int segmentNumber, Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            IsStopped = true;
            if (myGameObject != null)
            {
                UnityEngine.Object.Destroy(myGameObject);
            }
            myGameObject = null;
        }

        public void OnGUI(Dictionary<long, ObjectStatus> currentTransforms, Dictionary<long, ObjectStatus> currentEntities)
        {
            // OnGUI can be implemented on the behaviour itself
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\"behaviourFullName\":");
            StringJsonConverter.WriteToStringBuilder(stringBuilder, BehaviourFullName);
            stringBuilder.Append("}");
        }

        public int EffectiveApiVersion()
        {
            return apiVersion;
        }
    }
}
