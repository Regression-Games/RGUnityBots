using System;
using System.Text;
using Newtonsoft.Json;

namespace RegressionGames.StateRecorder.JsonConverters
{
    public class StringJsonConverter : Newtonsoft.Json.JsonConverter
    {

        private static readonly StringBuilder _stringBuilder = new(5_000);

        // some games use obscure chars like 'TM' which is #8xxx in the table.. don't let us get index out of bounds on something silly
        private static readonly char[] EscapeCharReplacements = new char[32_768];

        static StringJsonConverter()
        {
            EscapeCharReplacements['\n'] = 'n';
            EscapeCharReplacements['\r'] = 'r';
            EscapeCharReplacements['\t'] = 't';
            EscapeCharReplacements['\f'] = 'f';
            EscapeCharReplacements['\b'] = 'b';
            EscapeCharReplacements['\\'] = '\\';
            EscapeCharReplacements['"'] = '"';
            //Main(); //- useful for testing
        }

        // supports up to 100k char length escaped strings
        private static char[] _bufferArray = new char[100_000];

        private static int _currentNextIndex = 0;

        public static void WriteToStringBuilder(StringBuilder stringBuilder, string input)
        {
            if (input == null)
            {
                stringBuilder.Append("null");
                return;
            }

            _currentNextIndex = 0;
            _bufferArray[_currentNextIndex++] = '"';

            var inputLength = input.Length;

            /*
             * \n a b c
             * 0  1 2 3
             *
             * s = 1 , e = 1
             */

            var startIndex = 0;
            var endIndex = 0;
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
                    // need to escape.. copy existing range to result
                    if (startIndex != endIndex)
                    {
                        var length = endIndex - startIndex;
                        input.CopyTo(startIndex, _bufferArray, _currentNextIndex, length);
                        _currentNextIndex += length;
                    }
                    // update indexes
                    endIndex = i + 1;
                    startIndex = i + 1;
                    // write the escaped value to the buffer
                    _bufferArray[_currentNextIndex++] = '\\';
                    _bufferArray[_currentNextIndex++] = escapeReplacement;
                }
            }

            if (startIndex != endIndex || startIndex == inputLength - 1)
            {
                // got to the end
                var length = endIndex - startIndex;
                input.CopyTo(startIndex, _bufferArray, _currentNextIndex, length);
                _currentNextIndex += length;
            }

            _bufferArray[_currentNextIndex++] = '"';

            stringBuilder.Append(_bufferArray, 0, _currentNextIndex);
        }

        public static string ToJsonString(string val)
        {
            if (val == null)
            {
                return "null";
            }

            _stringBuilder.Clear();
            WriteToStringBuilder(_stringBuilder, val);
            return _stringBuilder.ToString();
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
        public static void Main()
        {
            Debug.Log("StringJsonConverter Test Output");
            var input = "a";
            Debug.Log("Input: " + input + " , Output: " + ToJsonString(input));

            input = "somethingLonger";
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
        */
    }
}
