using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace StateRecorder
{
    public static class JsonUtils
    {

        private static readonly bool[] EscapeChars = new bool[128];

        private const int UnicodeTextLength = 6;

        static JsonUtils()
        {
            IList<char> escapeChars = new List<char>
            {
                '\n', '\r', '\t', '\\', '\f', '\b',
            };
            for (int i = 0; i < ' '; i++)
            {
                escapeChars.Add((char)i);
            }

            var union = escapeChars.Union(new[] { '"' });

            foreach (char escapeChar in union)
            {
                EscapeChars[escapeChar] = true;
            }

        }

        // supports up to 100k char length escaped strings
        private static char[] _bufferArray = new char[100_000];

        private static int _currentNextIndex = 0;

        public static string EscapeJsonString([CanBeNull] string input)
        {
            if (input == null)
            {
                return "null";
            }

            _currentNextIndex = 0;
            _bufferArray[_currentNextIndex++] = '"';

            var inputLength = input.Length;

            var startIndex = 0;
            var endIndex = 0;
            for (var i = 0; i < inputLength; i++)
            {
                var ch = input[i];
                if (!EscapeChars[ch])
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
                    _bufferArray[_currentNextIndex++] = ch;
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

            return new string(_bufferArray, 0, _currentNextIndex);
        }
    }
}
