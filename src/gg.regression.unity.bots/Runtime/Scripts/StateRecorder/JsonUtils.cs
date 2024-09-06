using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using RegressionGames.StateRecorder.JsonConverters;
using UnityEngine;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace RegressionGames.StateRecorder
{
    public static class JsonUtils
    {

        private static JsonSerializer _jsonSerializer = null;

        private static bool IsNullableType(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>);
        }

        public static void WriteObjectStateToStringBuilder(StringBuilder stringBuilder, object theObject, Type stateType = null)
        {
            if (theObject == null)
            {
                stringBuilder.Append("null");
                return;
            }

            JsonConverter converter = null;


            try
            {
                stateType ??= theObject.GetType();
                converter = JsonConverterContractResolver.Instance.GetConverterForType(stateType);
            }
            catch (Exception)
            {
                //TODO: A customer's project has Addressable ECS types that cause a null pointer trying .GetType() on the object
            }

            if (converter != null)
            {
                if (converter is IStringBuilderWriteable writeable)
                {
                    // we had one of our fancy converters .. use it to speed this way way up
                    if (IsNullableType(stateType))
                    {
                        writeable.WriteToStringBuilderNullable(stringBuilder, theObject);
                    }
                    else
                    {
                        writeable.WriteToStringBuilder(stringBuilder, theObject);
                    }
                }
                else if (converter is UnityObjectFallbackJsonConverter fallbackJsonConverter)
                {
                    stringBuilder.Append("{}");
                }
                else
                {
                    // use the generic and expensive serializer only if we know we have a registered converter.. else write out a blank object
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
                            _jsonSerializer.Serialize(jsonWriter, theObject, stateType);
                        }

                        if (sbLength == stringBuilder.Length)
                        {
                            // nothing written ... shouldn't happen... but keeps us running if it does
                            stringBuilder.Append("{\"EXCEPTION\":\"Could not convert object to JSON\"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        RGDebug.LogException(ex, "Error converting object to JSON");
                        stringBuilder.Append("{}");
                    }
                }
            }
            else
            {
                stringBuilder.Append("{}");
            }
        }

        public static void WriteBehaviourStateToStringBuilder(StringBuilder stringBuilder, Behaviour state)
        {
            var stateType = state.GetType();

            var converter = JsonConverterContractResolver.Instance.GetConverterForType(stateType);

            if (converter is IBehaviourStringBuilderWritable bSBW)
            {
                bSBW.WriteBehaviourToStringBuilder(stringBuilder, state);
            }
            else if (converter != null)
            {
                // use the generic and expensive serializer only if we know we have a converter registered
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
            else
            {
                stringBuilder.Append("{}");
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
}
