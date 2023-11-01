using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RegressionGames.RGBotConfigs;
using UnityEngine;

namespace RegressionGames.StateActionTypes
{
    public interface IRGStateEntity : IDictionary<string, object>
    {
        public int id => 0;
        public string type => null;
        public bool isPlayer => false;
        public bool isRuntimeObject => false;
        public Vector3 position => Vector3.zero;
        public Quaternion rotation => Quaternion.identity;
        public long? clientId => null;
        
        /**
         * <summary>Retrieve the value of the named field as the specified type or default value
         * Note, some types in Unity can only be constructed on the Main thread.
         * Because of that fact, this method may return the default in cases where you wouldn't expect it to.
         * (Example: Collider2D)</summary>
         */
        [CanBeNull]
        public T GetField<T>(string fieldName, T defaultValue)
        {
            return DictionaryExtensions.GetField<T>(this, fieldName, defaultValue);
        }

        /**
         * <summary>Retrieve the value of the named field as the specified type or null if it does not exist.
         * Note, some types in Unity can only be constructed on the Main thread.
         * Because of that fact, this method may return null in cases where you wouldn't expect it to.
         * (Example: Collider2D)</summary>
         */
        [CanBeNull]
        public T GetField<T>(string fieldName)
        {
            return DictionaryExtensions.GetField<T>(this, fieldName);
        }
        
        /**
         * <summary>Retrieve the value of the named field or null if the field does not exist.</summary>
         */
        [CanBeNull]
        public object GetField(string fieldName)
        {
            return DictionaryExtensions.GetField(this, fieldName);
        }

        // This is mostly implemented to make visibility in the debugger much easier... especially when finding the right object in the overall state
        public override string ToString()
        {
            return $"{this.GetType().Name} - type: {type} , id: {id} , clientId: {clientId}";
        }
    }

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
    }
    
    public static class DictionaryExtensions
    {
        /**
         * <summary>Retrieve the value of the named field as the specified type or null if it does not exist.
         * Note, some types in Unity can only be constructed on the Main thread.
         * Because of that fact, this method may return null or the default for the requested type in cases where you wouldn't expect it to.
         * (Example: Collider2D)</summary>
         */
        [CanBeNull]
        public static T GetField<T>(this IDictionary<string, object> dictionary, string fieldName)
        {
            return GetField<T>(dictionary, fieldName, default(T));
        }
        
        /**
         * <summary>Retrieve the value of the named field as the specified type or null if it does not exist.
         * Note, some types in Unity can only be constructed on the Main thread.
         * Because of that fact, this method may return null or the default for the requested type in cases where you wouldn't expect it to.
         * (Example: Collider2D)</summary>
         */
        [CanBeNull]
        public static T GetField<T>(this IDictionary<string, object> dictionary, string fieldName, T defaultValue)
        {
            try
            {
                if (dictionary.TryGetValue(fieldName, out var result))
                {
                    if (result.GetType() == typeof(T))
                    {
                        return (T)result;
                    }

                    // use json here to cheat converting this object from dictionary to the real type they requested
                    return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(result, Formatting.None,
                        new JsonSerializerSettings
                        {
                            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                        }));
                    
                }
            }
            catch (Exception e)
            {
                // ignored
            }

            return defaultValue;
        }
        
        /**
         * <summary>Retrieve the value of the named field or null if the field does not exist.</summary>
         */
        [CanBeNull]
        public static object GetField(this IDictionary<string, object> dictionary, string fieldName)
        {
            return dictionary.TryGetValue(fieldName, out var result) ? result : null;
        }
    }
}
