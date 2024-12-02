using System;
using System.Collections;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using Object = System.Object;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class StringJsonConverter : Newtonsoft.Json.JsonConverter, ITypedStringBuilderConverter<String>
    {

        private static readonly ThreadLocal<StringBuilder> _stringBuilder = new(() => new(5_000));

        // non-Latin chars, and obscure cases like 'TM', don't need to be escaped, but let's avoid index out of bounds errors
        private static readonly char[] EscapeCharReplacements = new char[char.MaxValue];

        static StringJsonConverter()
        {
            // skip these - control characters in ascii table
            for (int i = 0; i <= 7; i++)
            {
                EscapeCharReplacements[i] = char.MaxValue;
            }

            // skip these - control characters in ascii table
            for (int i = 14; i <= 27; i++)
            {
                EscapeCharReplacements[i] = char.MaxValue;
            }

            // replace these
            EscapeCharReplacements['\n'] = 'n';
            EscapeCharReplacements['\r'] = 'r';
            EscapeCharReplacements['\t'] = 't';
            EscapeCharReplacements['\f'] = 'f';
            EscapeCharReplacements['\b'] = 'b';
            EscapeCharReplacements['\\'] = '\\';
            EscapeCharReplacements['"'] = '"';
            //TestCode(); //- useful for testing
        }

        // supports up to 30m char length escaped strings .. this seems absolutely excessive, but when unity logs dumps out the listing of every class in every assembly as a single log message, 1m was overflowed.. even for match3 it was 6.45m
        private static readonly ThreadLocal<char[]> _bufferArray = new (() => new char[30_000_000]);

        void ITypedStringBuilderConverter<string>.WriteToStringBuilder(StringBuilder stringBuilder, string val)
        {
            WriteToStringBuilder(stringBuilder, val);
        }

        string ITypedStringBuilderConverter<string>.ToJsonString(string val)
        {
            return ToJsonString(val);
        }

        public static void WriteToStringBuilder(StringBuilder stringBuilder, string input)
        {
            if (input == null)
            {
                stringBuilder.Append("null");
                return;
            }

            var currentNextIndex = 0;
            var bufferArray = _bufferArray.Value;
            bufferArray[currentNextIndex++] = '"';

            var inputLength = input.Length;

            var startIndex = 0;
            var endIndex = 0;

            try
            {
                for (var i = 0; i < inputLength; i++)
                {

                    var ch = input[i];
                    var escapeReplacement = EscapeCharReplacements[ch];
                    if (escapeReplacement == 0)
                    {
                        // don't need to escape
                        endIndex = i + 1;
                    }
                    else
                    {
                        // need to escape or skip.. copy existing range to result
                        if (startIndex != endIndex)
                        {
                            var length = endIndex - startIndex;
                            input.CopyTo(startIndex, bufferArray, currentNextIndex, length);
                            currentNextIndex += length;
                        }

                        // update indexes
                        endIndex = i + 1;
                        startIndex = i + 1;

                        if (escapeReplacement == char.MaxValue)
                        {
                            // need to skip
                        }
                        else
                        {
                            // need to escape
                            // write the escaped value to the buffer
                            bufferArray[currentNextIndex++] = '\\';
                            bufferArray[currentNextIndex++] = escapeReplacement;
                        }
                    }
                }

                if (startIndex == 0)
                {
                    // didn't need to escape anything.. bail out early and avoid the extra buffer string copies
                    stringBuilder.Append("\"").Append(input).Append("\"");
                    return; // we're done
                }

                // There MIGHT be room for some further optimization here.. CopyTo and Append both do a memcopy of the string... right now we do the partial
                // with copyTo and then copy the full string with Append.. if we could find a way to do this with a single memcopy somehow that would be faster
                if (startIndex != endIndex)
                {
                    // got to the end
                    var length = endIndex - startIndex;
                    input.CopyTo(startIndex, bufferArray, currentNextIndex, length);
                    currentNextIndex += length;
                }

                bufferArray[currentNextIndex++] = '"';

            }
            catch (ArgumentOutOfRangeException)
            {
                // if we have a buffer overflow because the string is too large (more than 30m chars)
                RGDebug.LogWarning($"Buffer overflow while escaping json string.  Character indexes beyond {currentNextIndex} have been dropped");
            }

            stringBuilder.Append(bufferArray, 0, currentNextIndex);
        }

        public static string ToJsonString(string val)
        {
            if (val == null)
            {
                return "null";
            }

            _stringBuilder.Value.Clear();
            WriteToStringBuilder(_stringBuilder.Value, val);
            return _stringBuilder.Value.ToString();
        }


        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else
            {
                writer.WriteRawValue(ToJsonString((string)value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        /* Useful for testing
        public static void TestCode()
        {
            new Thread(StringJsonConverter.Test).Start();
            new Thread(StringJsonConverter.Test).Start();
            new Thread(StringJsonConverter.Test).Start();

            new Thread(StringJsonConverter.TestCorruption).Start();
            new Thread(StringJsonConverter.TestCorruption).Start();
            new Thread(StringJsonConverter.TestCorruption).Start();
        }

        private static void Test()
        {
            Debug.Log("StringJsonConverter Test");
            var input = "a";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "somethingLonger";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "HeroProfileManager";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "NormalizedPath";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "\nStartingNewline";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "EndingNewline\n";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "Middle\nNewline";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "N\newline\nAfter\nFirst\nAnd\nEach\nWord\na";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));
        }

        private static void TestCorruption()
        {
            Debug.Log("StringJsonConverter Corruption Test");
            StringBuilder sb = new StringBuilder(1000);

            var input = "a";
            sb.Append("Input: " + input + " , Output: ");
            WriteToStringBuilder(sb, input);
            sb.Append("\n");

            input = "somethingLonger";
            sb.Append("Input: " + input + " , Output: ");
            WriteToStringBuilder(sb, input);
            sb.Append("\n");

            input = "HeroProfileManager";
            sb.Append("Input: " + input + " , Output: ");
            WriteToStringBuilder(sb, input);
            sb.Append("\n");

            input = "NormalizedPath";
            sb.Append("Input: " + input + " , Output: ");
            WriteToStringBuilder(sb, input);
            sb.Append("\n");

            Debug.Log(sb.ToString());
        }
        */
    }
}
