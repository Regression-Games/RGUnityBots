using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RegressionGames.StateRecorder.JsonConverters;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace RegressionGames.StateRecorder
{
    public class JsonConverterContractResolver : DefaultContractResolver
    {
        public static readonly JsonConverterContractResolver Instance = new();

        private readonly Dictionary<Type, JsonConverter> _customConverters = new()
        {
            { typeof(float), new FloatJsonConverter() },
            { typeof(double), new DoubleJsonConverter() },
            { typeof(decimal), new DecimalJsonConverter() },
            { typeof(int), new IntJsonConverter() },
            { typeof(long), new LongJsonConverter() },
            { typeof(short), new ShortJsonConverter() },
            { typeof(string), new StringJsonConverter() },
            { typeof(uint), new UIntJsonConverter()},
            { typeof(ulong), new ULongJsonConverter()},
            { typeof(ushort), new UShortJsonConverter()},
            { typeof(bool), new BooleanJsonConverter()},

            { typeof(Bounds), new BoundsJsonConverter() },
            { typeof(Vector2Int), new Vector2IntJsonConverter() },
            { typeof(Vector3Int), new Vector3IntJsonConverter() },
            { typeof(Vector2), new Vector2JsonConverter() },
            { typeof(Vector3), new Vector3JsonConverter() },
            { typeof(Vector4), new Vector4JsonConverter() },
            { typeof(Quaternion), new QuaternionJsonConverter() },
            { typeof(Rect), new RectJsonConverter() },
            { typeof(RectInt), new RectIntJsonConverter() },

            { typeof(Color), new ColorJsonConverter() },
            { typeof(Image), new ImageJsonConverter() },
            { typeof(RawImage), new RawImageJsonConverter() },
            { typeof(Button), new ButtonJsonConverter() },
            { typeof(Text), new TextJsonConverter() },
            { typeof(TextMeshPro), new TextMeshProJsonConverter() },
            { typeof(TextMeshProUGUI), new TextMeshProUGUIJsonConverter() },
            { typeof(Mask), new MaskJsonConverter() },
            { typeof(Animator), new AnimatorJsonConverter() },
            { typeof(Rigidbody), new RigidbodyJsonConverter() },
            { typeof(Rigidbody2D), new Rigidbody2DJsonConverter() },
            { typeof(Collider), new ColliderJsonConverter() },
            { typeof(Collider2D), new Collider2DJsonConverter() },
            { typeof(ParticleSystem), new ParticleSystemJsonConverter() },
            { typeof(MeshFilter), new MeshFilterJsonConverter() },
            { typeof(MeshRenderer), new MeshRendererJsonConverter() },
            { typeof(SkinnedMeshRenderer), new SkinnedMeshRendererJsonConverter() },
            { typeof(NavMeshAgent), new NavMeshAgentJsonConverter() },

        };


        public void RegisterJsonConverterForType(Type type, JsonConverter converter)
        {
            _customConverters[type] = converter;
        }

        [CanBeNull]
        public JsonConverter GetConverterForType(Type objectType)
        {
            JsonConverter converter = null;
            if (objectType != null)
            {
                if (_customConverters.TryGetValue(objectType, out converter))
                {
                    // found our converter
                }
                else if (NetworkVariableJsonConverter.Convertable(objectType))
                {
                    // only support when netcode is in the project
                    converter = new NetworkVariableJsonConverter();
                }
                else if (UnityObjectFallbackJsonConverter.Convertable(objectType))
                {
                    converter = new UnityObjectFallbackJsonConverter();
                }
            }

            return converter;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = base.CreateContract(objectType);
            contract.Converter = GetConverterForType(objectType);
            return contract;
        }

    }
}
