using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.StateRecorder.BotSegments.Models
{
    public class IRGBotSequences : ScriptableObject
    {

        public List<string> sequences = new();

        public List<string> segmentLists = new();

        public List<string> segments = new();

    }
}
