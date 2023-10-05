using System;
using UnityEngine;

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

        public override string ToString()
        {
            return "" + id + " - " + name;
        }
    }
    
}

