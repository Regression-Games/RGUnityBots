using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable once InconsistentNaming
    [Serializable]
    public class RGTickInfoData
    {
        public long tick;
        public string sceneName;
        // ReSharper disable once InconsistentNaming
        public Dictionary<string, IRGStateEntity> gameState;

        [NonSerialized]
        // cache this so no matter how many clients we send to, we only convert to string one time
        private string _serializedForm = null;

        public RGTickInfoData(long tick, string sceneName, Dictionary<string, IRGStateEntity> gameState)
        {
            this.tick = tick;
            this.sceneName = sceneName;
            this.gameState = gameState;
        }
        
        [JsonConstructor]
        // this is strongly typed so that the json convertor uses it to populate the map correctly
        public RGTickInfoData(long tick, string sceneName, Dictionary<string, RGStateEntity<RGState>> gameState)
        {
            this.tick = tick;
            this.sceneName = sceneName;
            this.gameState = new();
            foreach (var (key,value) in gameState)
            {
                this.gameState[key] = value;
            }
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
