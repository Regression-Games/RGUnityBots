using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RegressionGames.StateActionTypes
{
    // ReSharper disable InconsistentNaming
    [Serializable]
    public class RGStateEntity_Core : Dictionary<string, object>, IRGStateEntity
    {
        public bool GetIsPlayer()
        {
            return isPlayer;
        }

        public string GetEntityType()
        {
            return null;
        }

        // handle long to int conversion
        public int id => int.Parse(this["id"].ToString());
        public string scene => (string)this["scene"];
        public string name => (string)this["name"];
        public string tag => (string)this["tag"];
        public string pathInScene => (string)this["pathInScene"];
        public Vector3 position => (Vector3)this["position"];
        public Quaternion rotation => (Quaternion)this["rotation"];
        public bool isPlayer => (bool)this["isPlayer"];

        public long? clientId => (long?)this.GetValueOrDefault("clientId", null);

        private static readonly HashSet<string> _protectedKeys = new HashSet<string>
            { "id", "tag", "scene", "name", "pathInScene", "position", "rotation", "isPlayer", "clientId", "interactable" };
        
        public List<string> types => this.Keys.Where(k => !_protectedKeys.Contains(k)).ToList();

        // This is mostly implemented to make visibility in the debugger much easier... especially when finding the right object in the overall state
        public override string ToString()
        {
            return $"pathInScene: {pathInScene}, scene: {scene}, id: {id}, tag: {tag}, clientId: {clientId}, isPlayer: {isPlayer}";
        }
    }
}
