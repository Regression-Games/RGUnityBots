using System;
using UnityEngine;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBot : ScriptableObject
    {
        public long id;
        public new string name;
        public string gameEngine;
        public string programmingLanguage;
        public string codeSourceType;

        public override string ToString()
        {
            return "" + id + " - " + name;
        }
    }
    
}

