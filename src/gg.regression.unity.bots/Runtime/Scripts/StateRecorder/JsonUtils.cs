using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RegressionGames.StateRecorder.JsonConverters;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace RegressionGames.StateRecorder
{
    public static class JsonUtils
    {

        private static JsonSerializer _jsonSerializer = null;

        public static void WriteBehaviourStateToStringBuilder(StringBuilder stringBuilder, Behaviour state)
        {
            var stateType = state.GetType();

            var converter = JsonConverterContractResolver.GetConverterForType(stateType);

            if (converter is IBehaviourStringBuilderWritable bSBW)
            {
                bSBW.WriteBehaviourToStringBuilder(stringBuilder, state);
            }
            else
            {
                // use the generic and expensive serializer
                var sbLength = stringBuilder.Length;
                try
                {
                    // do this ourselves to bypass all the serializer creation junk for every object :/
                    if (_jsonSerializer == null)
                    {
                        _jsonSerializer = JsonSerializer.CreateDefault(JsonSerializerSettings);
                        _jsonSerializer.Formatting = Formatting.None;
                    }

                    var sw = new StringWriter(stringBuilder, CultureInfo.InvariantCulture);
                    using (var jsonWriter = new JsonTextWriter(sw))
                    {
                        jsonWriter.Formatting = _jsonSerializer.Formatting;
                        _jsonSerializer.Serialize(jsonWriter, state, stateType);
                    }

                    if (sbLength == stringBuilder.Length)
                    {
                        // nothing written ... shouldn't happen... but keeps us running if it does
                        stringBuilder.Append("{\"EXCEPTION\":\"Could not convert Behaviour to JSON\"}");
                    }
                }
                catch (Exception ex)
                {
                    RGDebug.LogException(ex, "Error converting behaviour to JSON - " + state.name);
                    stringBuilder.Append("{}");
                }
            }
        }

        private static readonly JsonSerializerSettings JsonSerializerSettings = new()
        {
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = JsonConverterContractResolver.Instance,
            Error = delegate(object _, ErrorEventArgs args)
            {
                // just eat certain errors
                if (args.ErrorContext.Error is MissingComponentException || args.ErrorContext.Error.InnerException is UnityException or NotSupportedException or MissingComponentException)
                {
                    args.ErrorContext.Handled = true;
                }
                else
                {
                    // do nothing anyway.. but useful for debugging which errors happened
                    args.ErrorContext.Handled = true;
                }
            },
        };
    }

    internal class JsonConverterContractResolver : DefaultContractResolver
    {
        public static readonly JsonConverterContractResolver Instance = new();

        [CanBeNull]
        public static JsonConverter GetConverterForType(Type objectType)
        {
            JsonConverter converter = null;
            if (objectType == typeof(float) || objectType == typeof(Single))
            {
                converter = new FloatJsonConverter();
            }
            else if (objectType == typeof(double) || objectType == typeof(Double))
            {
                converter = new DoubleJsonConverter();
            }
            else if (objectType == typeof(decimal) || objectType == typeof(Decimal))
            {
                converter = new DecimalJsonConverter();
            }
            else if (objectType == typeof(int) || objectType == typeof(Int32))
            {
                converter = new IntJsonConverter();
            }
            else if (objectType == typeof(long) || objectType == typeof(Int64))
            {
                converter = new FloatJsonConverter();
            }
            else if (objectType == typeof(short) || objectType == typeof(Int16))
            {
                converter = new FloatJsonConverter();
            }
            else if (objectType == typeof(string) || objectType == typeof(String))
            {
                converter = new StringJsonConverter();
            }
            else if (objectType == typeof(Color))
            {
                converter = new ColorJsonConverter();
            }
            else if (objectType == typeof(Bounds))
            {
                converter = new BoundsJsonConverter();
            }
            else if (objectType == typeof(Vector2Int) || objectType == typeof(Vector3Int))
            {
                converter = new VectorIntJsonConverter();
            }
            else if (objectType == typeof(Vector2) || objectType == typeof(Vector3) || objectType == typeof(Vector4))
            {
                converter = new VectorJsonConverter();
            }
            else if (objectType == typeof(Quaternion))
            {
                converter = new QuaternionJsonConverter();
            }
            else if (objectType == typeof(Image))
            {
                converter = new ImageJsonConverter();
            }
            else if (objectType == typeof(Button))
            {
                converter = new ButtonJsonConverter();
            }
            else if (objectType == typeof(TextMeshPro))
            {
                converter = new TextMeshProJsonConverter();
            }
            else if (objectType == typeof(TextMeshProUGUI))
            {
                converter = new TextMeshProUGUIJsonConverter();
            }
            else if (objectType == typeof(Text))
            {
                converter = new TextJsonConverter();
            }
            else if (objectType == typeof(Rect))
            {
                converter = new RectJsonConverter();
            }
            else if (objectType == typeof(RectInt))
            {
                converter = new RectIntJsonConverter();
            }
            else if (objectType == typeof(RawImage))
            {
                converter = new RawImageJsonConverter();
            }
            else if (objectType == typeof(Mask))
            {
                converter = new MaskJsonConverter();
            }
            else if (objectType == typeof(Animator))
            {
                converter = new AnimatorJsonConverter();
            }
            else if (objectType == typeof(Rigidbody))
            {
                converter = new RigidbodyJsonConverter();
            }
            else if (objectType == typeof(Rigidbody2D))
            {
                converter = new Rigidbody2DJsonConverter();
            }
            else if (objectType == typeof(Collider))
            {
                converter = new ColliderJsonConverter();
            }
            else if (objectType == typeof(Collider2D))
            {
                converter = new Collider2DJsonConverter();
            }
            else if (objectType == typeof(ParticleSystem))
            {
                converter = new ParticleSystemJsonConverter();
            }
            else if (objectType == typeof(MeshFilter))
            {
                converter = new MeshFilterJsonConverter();
            }
            else if (objectType == typeof(MeshRenderer))
            {
                converter = new MeshRendererJsonConverter();
            }
            else if (objectType == typeof(SkinnedMeshRenderer))
            {
                converter = new SkinnedMeshRendererJsonConverter();
            }
            else if (objectType == typeof(NavMeshAgent))
            {
                converter = new NavMeshAgentJsonConverter();
            }
            else if (IsUnityType(objectType) && InGameObjectFinder.GetInstance().collectStateFromBehaviours)
            {
                if (NetworkVariableJsonConverter.Convertable(objectType))
                {
                    // only support when netcode is in the project
                    converter = new NetworkVariableJsonConverter();
                }
                else
                {
                    converter = new UnityObjectFallbackJsonConverter();
                }
            }
            else if (typeof(Behaviour).IsAssignableFrom(objectType))
            {
                converter = new UnityObjectFallbackJsonConverter();
            }

            return converter;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = base.CreateContract(objectType);
            contract.Converter = GetConverterForType(objectType);
            return contract;
        }

        // leave out bossroom types as that is our main test project
        // (isUnity, isBossRoom)
        private static readonly Dictionary<Assembly, (bool, bool)> _unityAssemblies = new();

        private static bool IsUnityType(Type objectType)
        {
            var assembly = objectType.Assembly;
            if (!_unityAssemblies.TryGetValue(assembly, out var isUnityType))
            {
                var isUnity = assembly.FullName.StartsWith("Unity");
                var isBossRoom = false;
                if (isUnity)
                {
                    isBossRoom = assembly.FullName.StartsWith("Unity.BossRoom");
                }

                isUnityType = (isUnity, isBossRoom);
                _unityAssemblies[assembly] = isUnityType;
            }

            return isUnityType is { Item1: true, Item2: false };
        }
    }
}
