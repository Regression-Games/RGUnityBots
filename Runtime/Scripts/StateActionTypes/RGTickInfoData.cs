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
        public Dictionary<string, RGStateEntity> gameState;

        [NonSerialized]
        // cache this so no matter how many clients we send to, we only convert to string one time
        private string _serializedForm = null;

        public RGTickInfoData(long t, string sceneName, Dictionary<string, RGStateEntity> gameState)
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
