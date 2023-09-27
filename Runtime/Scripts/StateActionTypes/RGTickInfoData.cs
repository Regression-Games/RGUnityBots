using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public class RGTickInfoData
    {
        public long tick;
        public string sceneName;
        public Dictionary<string, object> gameState;

        [NonSerialized]
        // cache this so no matter how many clients we send to, we only convert to string one time
        private string _serializedForm = null;

        public RGTickInfoData(long t, string sceneName, Dictionary<string, object> gameState)
        {
            tick = t;
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
