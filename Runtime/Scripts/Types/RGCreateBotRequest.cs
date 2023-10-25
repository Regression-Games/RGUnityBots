using System;
using UnityEngine;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGCreateBotRequest
    {
        // ReSharper disable InconsistentNaming
        public string name;
        public string gameEngine = "UNITY";
        public string programmingLanguage = "CSHARP";
        public string codeSourceType = "ZIPFILE";
        public string path = "/";
        public string description = "Created from Unity Local Bot synchronization.";
        // ReSharper enable InconsistentNaming

        public RGCreateBotRequest(string name)
        {
            this.name = name;
        }
    }
}

