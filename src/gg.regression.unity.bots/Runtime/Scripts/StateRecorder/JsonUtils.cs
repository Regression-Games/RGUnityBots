using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RegressionGames;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.JsonConverters;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace StateRecorder
{
    public static class JsonUtils
    {

        private static readonly char[] EscapeCharReplacements = new char[128];

        static JsonUtils()
        {
            EscapeCharReplacements['\n'] = 'n';
            EscapeCharReplacements['\r'] = 'r';
            EscapeCharReplacements['\t'] = 't';
            EscapeCharReplacements['\f'] = 'f';
            EscapeCharReplacements['\b'] = 'b';
            EscapeCharReplacements['\\'] = '\\';
            EscapeCharReplacements['"'] = '"';
        }

        // supports up to 100k char length escaped strings
        private static char[] _bufferArray = new char[100_000];

        private static int _currentNextIndex = 0;

        public static void EscapeJsonStringIntoStringBuilder(StringBuilder stringBuilder, string input)
        {
            if (input == null)
            {
                stringBuilder.Append("null");
                return;
            }

            _currentNextIndex = 0;
            _bufferArray[_currentNextIndex++] = '"';

            var inputLength = input.Length;

            var startIndex = 0;
            var endIndex = 0;
            for (var i = 0; i < inputLength; i++)
            {
                var ch = input[i];
                var escapeReplacement = EscapeCharReplacements[ch];
                if (escapeReplacement == 0)
                {
                    // don't need to escape
                    endIndex = i;
                }
                else
                {
                    // need to escape.. copy existing range to result
                    var length = endIndex + 1 - startIndex;
                    input.CopyTo(startIndex, _bufferArray, _currentNextIndex,length);
                    _currentNextIndex += length;
                    // update indexes
                    endIndex = i + 1;
                    startIndex = i + 1;
                    // write the escaped value to the buffer
                    _bufferArray[_currentNextIndex++] = '\\';
                    _bufferArray[_currentNextIndex++] = escapeReplacement;
                }
            }

            if (startIndex != endIndex)
            {
                // got to the end
                var length = endIndex + 1 - startIndex;
                input.CopyTo(startIndex, _bufferArray, _currentNextIndex, length);
                _currentNextIndex += length;
            }

            _bufferArray[_currentNextIndex++] = '"';

            stringBuilder.Append(_bufferArray, 0, _currentNextIndex);
        }

        private static readonly StringBuilder _stringBuilder = new(5_000);

        public static string EscapeJsonString([CanBeNull] string input)
        {
            _stringBuilder.Clear();
            EscapeJsonStringIntoStringBuilder(_stringBuilder, input);
            return _stringBuilder.ToString();
        }


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
