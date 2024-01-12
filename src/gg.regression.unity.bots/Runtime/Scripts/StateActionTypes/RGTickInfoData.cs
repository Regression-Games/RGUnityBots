using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable once InconsistentNaming
    [Serializable]
    public class RGTickInfoData
    {
        public long tick;
        public string sceneName;
        // ReSharper disable once InconsistentNaming
        public Dictionary<string, RGStateEntity_Core> gameState;

        [NonSerialized]
        // cache this so no matter how many clients we send to, we only convert to string one time
        private string _serializedForm = null;

        [JsonConstructor]
        public RGTickInfoData(long tick, string sceneName, Dictionary<string, RGStateEntity_Core> gameState)
        {
            this.tick = tick;
            this.sceneName = sceneName;
            this.gameState = gameState;
        }

        private string ToSerialized()
        {
            if (_serializedForm == null)
            {
                lock (this)
                {
                    _serializedForm ??= JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    });
                }
            }
            return _serializedForm;
        }

        public override string ToString()
        {
            return ToSerialized();
        }
    }
}
