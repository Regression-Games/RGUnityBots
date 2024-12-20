﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using RegressionGames.StateRecorder.JsonConverters;

namespace RegressionGames.StateRecorder.Models
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class InputData : IComponentDataProvider
    {
        // version of this schema, update this if fields change
        public int apiVersion = SdkApiVersion.VERSION_1;

        public int ApiVersion()
        {
            return apiVersion;
        }

        public List<KeyboardInputActionData> keyboard;
        public List<MouseInputActionData> mouse;

        private List<object> allInputsSorted = null;

        public List<object> AllInputsSortedByTime() {
            if (allInputsSorted == null)
            {
                allInputsSorted = new();

                if (keyboard != null)
                {
                    // add all the keyboard data, and then we will interleave the mouse data into it
                    allInputsSorted.AddRange(keyboard);
                }

                if (mouse != null)
                {
                    foreach (var mouseInputActionData in mouse)
                    {
                        var allInputsSortedCount = allInputsSorted.Count;
                        var didAdd = false;
                        for (var i = 0; i < allInputsSortedCount; i++)
                        {
                            var input = allInputsSorted[i];
                            if (input is KeyboardInputActionData keyboardData)
                            {
                                // if a keyboard entry time is after the new mouse time.. insert the mouse before this one
                                if ((keyboardData.startTime.HasValue && keyboardData.startTime.Value > mouseInputActionData.startTime) ||
                                    (keyboardData.endTime.HasValue && keyboardData.endTime.Value > mouseInputActionData.startTime))
                                {
                                    allInputsSorted.Insert(i, mouseInputActionData);
                                    didAdd = true;
                                    break;
                                }
                            }
                            else if (input is MouseInputActionData mouseData)
                            {
                                // if an existing mouse time is after the new mouse time.. insert the new mouse before this one
                                if (mouseData.startTime > mouseInputActionData.startTime)
                                {
                                    allInputsSorted.Insert(i, mouseInputActionData);
                                    didAdd = true;
                                    break;
                                }
                            }
                        }

                        if (!didAdd)
                        {
                            // didn't find something to put it before, stick it on the end
                            allInputsSorted.Add(mouseInputActionData);
                        }
                    }
                }
            }
            return allInputsSorted;
        }

        public int EffectiveApiVersion => Math.Max(Math.Max(apiVersion, keyboard.DefaultIfEmpty().Max(a => a?.apiVersion ?? SdkApiVersion.CURRENT_VERSION)), mouse.DefaultIfEmpty().Max(a => a?.apiVersion ?? SdkApiVersion.CURRENT_VERSION));

        public void MarkSent()
        {
            if (keyboard != null)
            {
                foreach (var keyboardInputActionData in keyboard)
                {
                    keyboardInputActionData.HasBeenSent = true;
                }
            }
        }

        public void ReplayReset()
        {
            if (keyboard != null)
            {
                foreach (var keyboardInputActionData in keyboard)
                {
                    keyboardInputActionData.ReplayReset();
                }
            }

            if (mouse != null)
            {
                foreach (var mouseInputActionData in mouse)
                {
                    mouseInputActionData.ReplayReset();
                }
            }
        }

        public void WriteToStringBuilder(StringBuilder stringBuilder)
        {
            stringBuilder.Append("{\n\"apiVersion\":");
            IntJsonConverter.WriteToStringBuilder(stringBuilder, apiVersion);
            stringBuilder.Append(",\n\"keyboard\":[\n");
            if (keyboard != null)
            {
                var keyboardCount = keyboard.Count;
                for (var i = 0; i < keyboardCount; i++)
                {
                    keyboard[i].WriteToStringBuilder(stringBuilder);
                    if (i + 1 < keyboardCount)
                    {
                        stringBuilder.Append(",\n");
                    }
                }
            }

            stringBuilder.Append("\n],\n\"mouse\":[\n");
            if (mouse != null)
            {
                var mouseCount = mouse.Count;
                for (var i = 0; i < mouseCount; i++)
                {
                    mouse[i].WriteToStringBuilder(stringBuilder);
                    if (i + 1 < mouseCount)
                    {
                        stringBuilder.Append(",\n");
                    }
                }
            }
            stringBuilder.Append("\n]\n}");
        }

    }
}
