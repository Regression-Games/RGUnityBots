using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder;
using RegressionGames.StateRecorder.BotSegments.Models;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData
    {
        // version of this schema, update this if fields change
        public int apiVersion = BotSegment.SDK_API_VERSION_1;

        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;

        public int EffectiveApiVersion => Math.Max(Math.Max(apiVersion, keyboard.DefaultIfEmpty().Max(a => a?.apiVersion ?? 0)), mouse.DefaultIfEmpty().Max(a => a?.apiVersion ?? 0));

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
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"keyboard\":[\n");
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
