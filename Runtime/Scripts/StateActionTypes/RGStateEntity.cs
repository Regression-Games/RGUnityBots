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
        public int id => (int)this["id"];
        public string type => (string)this["type"];
        public bool isPlayer => (bool)this["isPlayer"];
        public bool isRuntimeObject => (bool)this["isRuntimeObject"];
        // TODO (REG-1303): These should be non-nullable and we should remove the option NOT to sync position and rotation
        public Vector3? position => (Vector3?)this["position"];
        public Quaternion? rotation => (Quaternion?)this["rotation"];
        public uint? clientId => (uint?)this["clientId"];
    }
}