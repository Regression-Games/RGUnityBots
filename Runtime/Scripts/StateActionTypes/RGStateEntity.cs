using System.Collections.Generic;
using UnityEngine;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable InconsistentNaming
    /**
     * Easily expose the most commonly used fields of entities
     * in the game state.  Allows bot code to avoid Dictionary lookup syntax
     * for these commonly used fields.
     */
    public class RGStateEntity : Dictionary<string, object>
    {
        public long id => (long)this.GetValueOrDefault("id", 0L);
        public string type => (string)this.GetValueOrDefault("type", null);
        public bool isPlayer => (bool)this.GetValueOrDefault("isPlayer", false);
        public bool isRuntimeObject => (bool)this.GetValueOrDefault("isRuntimeObject", false);
        // TODO (REG-1303): These should be non-nullable and we should remove the option NOT to sync position and rotation
        public Vector3? position => (Vector3?)this.GetValueOrDefault("position", null);
        public Quaternion? rotation => (Quaternion?)this.GetValueOrDefault("rotation", null);
        public long? clientId => (long?)this.GetValueOrDefault("clientId", null);
    }
}