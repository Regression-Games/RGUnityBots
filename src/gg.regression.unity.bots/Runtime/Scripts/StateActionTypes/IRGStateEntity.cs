using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace RegressionGames.StateActionTypes
{
    // ReSharper disable once InconsistentNaming
    /**
     * <summary>Any Custom RGStateEntity implementations should implement this interface.</summary>
     */
    public interface IRGStateEntity : IDictionary<string, object>
    {
        public string GetEntityType();

        public bool GetIsPlayer();
        
        /**
         * <summary>Retrieve the value of the named field as the specified type or default value
         * Note, some types in Unity can only be constructed on the Main thread.
         * Because of that fact, this method may return the default in cases where you wouldn't expect it to.
         * (Example: Collider2D)</summary>
         */
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
        public T GetField<T>(string fieldName)
        {
            return DictionaryExtensions.GetField<T>(this, fieldName);
        }
        
        /**
         * <summary>Retrieve the value of the named field or null if the field does not exist.</summary>
         */
        public object GetField(string fieldName)
        {
            return DictionaryExtensions.GetField(this, fieldName);
        }
    }
}
