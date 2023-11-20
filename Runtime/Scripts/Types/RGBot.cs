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
        public string botCreationTool;
        public DateTimeOffset? modifiedDate;
        public DateTimeOffset? createdDate;

        public bool IsUnityBot => gameEngine == "UNITY";
        public bool IsLocal => id < 0 || programmingLanguage == "CSHARP";

        /// <summary>
        /// Gets a boolean indicating if the code is "owned" by the server.
        /// A Bot whose code is "owned" by the server is always downloaded when synchronizing, never uploaded.
        /// The server's copy is the source of truth.
        /// </summary>
        public bool CodeIsServerOwned => botCreationTool is "AGENT_BUILDER";

        public string UIString => $"{(IsLocal ? "Local" : "Remote")} - {name} : {id}";

        public override string ToString()
        {
            return "" + id + " - " + name;
        }
    }
}
