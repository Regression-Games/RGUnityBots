using System;
using JetBrains.Annotations;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBotInstance
    {
        // ReSharper disable InconsistentNaming
        public long id;
        public RGBot bot;
        public long? lobby;
        public DateTimeOffset createdDate;
        // ReSharper enable InconsistentNaming

        public override string ToString()
        {
            return $"{id} - {lobby} - {bot}";
        }

        public override bool Equals(object obj)
        {
            if ( obj != null && obj.GetType() == typeof(RGBotInstance))
            {
                return ((RGBotInstance)obj).id == id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (int) id;
        }
    }
    
}