using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBot
    {
        public long id;
        public string name;
        public string programmingLanguage;

        public override string ToString()
        {
            return "" + id + " - " + name;
        }
    }
    
}

