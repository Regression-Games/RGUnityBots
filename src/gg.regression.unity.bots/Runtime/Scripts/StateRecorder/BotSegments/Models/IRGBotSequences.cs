using System.Collections.Generic;
using UnityEngine;

namespace StateRecorder.BotSegments.Models
{
    public class IRGBotSequences : ScriptableObject
    {

        public List<string> sequences = new();

        public List<string> segmentLists = new();

        public List<string> segments = new();

    }
}
