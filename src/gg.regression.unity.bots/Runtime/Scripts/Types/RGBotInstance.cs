using System;

namespace RegressionGames.Types
{
    [Serializable]
    public class RGBotInstance
    {
        // ReSharper disable InconsistentNaming
        public long id;
        public RGBot bot;
        public DateTimeOffset createdDate;
        // ReSharper enable InconsistentNaming

        public override string ToString()
        {
            return $"{id} - {bot}";
        }

        public override bool Equals(object obj)
        {
            if (obj != null && obj.GetType() == typeof(RGBotInstance))
            {
                return ((RGBotInstance)obj).id == id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return (int)id;
        }
    }

}