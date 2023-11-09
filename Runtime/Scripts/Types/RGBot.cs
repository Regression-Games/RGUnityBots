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

    /// <summary>
    /// Represents a bot record that comes from the Regression Games service.
    /// </summary>
    /// <remarks>
    /// Bots that come from the Regression Games service include some additional metadata that local bots do not have.
    /// </remarks>
    [Serializable]
    public class RGRemoteBot : RGBot
    {
        public DateTimeOffset? modifiedDate;
        public DateTimeOffset? createdDate;
    }
}
