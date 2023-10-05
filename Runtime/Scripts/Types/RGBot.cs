using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBot
    {
        public long id;
        public string name;
        public string gameEngine;
        public string programmingLanguage;
        public string codeSourceType;
        
        public bool IsUnityBot => gameEngine == "UNITY";
        public bool IsLocal => id < 0 || programmingLanguage == "CSHARP";

        public string UIString => $"{(IsLocal ? "Local" : "Remote")} - {name} : {id}";

        public override string ToString()
        {
            return "" + id + " - " + name;
        }
    }
    
}

