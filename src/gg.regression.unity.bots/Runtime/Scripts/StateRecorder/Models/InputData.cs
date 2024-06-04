using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using RegressionGames.StateRecorder;

namespace StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;

        public void ReplayReset()
        {
            foreach (var keyboardInputActionData in keyboard)
            {
                keyboardInputActionData.ReplayReset();
            }

            foreach (var mouseInputActionData in mouse)
            {
                mouseInputActionData.ReplayReset();
            }
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"keyboard\":[\n");
            var keyboardCount = keyboard.Count;
            for (var i = 0; i < keyboardCount; i++)
            {
                keyboard[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < keyboardCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n],\n\"mouse\":[\n");
            var mouseCount = mouse.Count;
            for (var i = 0; i < mouseCount; i++)
            {
                mouse[i].WriteToStringBuilder(stringBuilder);
                if (i + 1 < mouseCount)
                {
                    stringBuilder.Append(",\n");
                }
            }
            stringBuilder.Append("\n]\n}");
        }

    }
}
