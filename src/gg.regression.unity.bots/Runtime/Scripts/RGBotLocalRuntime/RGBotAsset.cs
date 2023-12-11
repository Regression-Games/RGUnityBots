using RegressionGames.Types;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime
{
    //Wrapper object for saving RGBot as an asset
    public class RGBotAsset: ScriptableObject
    {
        /// <summary>
        /// Server-side bot metadata retrieved from RegressionGames.
        /// </summary>
        public RGBot Bot;

        /// <summary>
        /// The MD5 checksum of the ZIP file for this bot's code when this bot was last synced.
        /// </summary>
        /// <remarks>
        /// This will be null for bots that have not been synced yet.
        /// This checksum allows the synchronizer to identify when the local bot has been changed since it was last synced with the server.
        /// It may later use that to make conflict resolution decisions.
        /// </remarks>
        public string ChecksumAtLastSync;
    }
}
