using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using UnityEngine;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable InconsistentNaming
    /**
     * Easily expose the most commonly used fields of entities
     * in the game state.  Allows bot code to avoid Dictionary lookup syntax
     * for these commonly used fields.
     */
    [Serializable]
    public class RGStateEntity<T> : Dictionary<string, object>, IRGStateEntity where T : IRGState
    {
        // handle long to int conversion
        public int id => int.Parse(this.GetValueOrDefault("id", 0).ToString());
        public string type => (string)this.GetValueOrDefault("type", null);
        public bool isPlayer => (bool)this.GetValueOrDefault("isPlayer", false);

        public bool isRuntimeObject => (bool)this.GetValueOrDefault("isRuntimeObject", false);

        public Vector3 position => (Vector3)this.GetValueOrDefault("position");
        public Quaternion rotation => (Quaternion)this.GetValueOrDefault("rotation");

        public long? clientId => (long?)this.GetValueOrDefault("clientId", null);
        
        // This is mostly implemented to make visibility in the debugger much easier... especially when finding the right object in the overall state
        public override string ToString()
        {
            return $"{this.GetType().Name} - type: {type} , id: {id} , clientId: {clientId}";
        }
    }
}
