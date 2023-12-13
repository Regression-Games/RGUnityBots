using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RegressionGames
{
public static class DictionaryExtensions
    {
        /**
         * <summary>Retrieve the value of the named field as the specified type or null if it does not exist.
         * Note, some types in Unity can only be constructed on the Main thread.
         * Because of that fact, this method may return null or the default for the requested type in cases where you wouldn't expect it to.
         * (Example: Collider2D)</summary>
         */
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
            catch (Exception)
            {
                // ignored
            }

            return defaultValue;
        }

        /**
         * <summary>Retrieve the value of the named field or null if the field does not exist.</summary>
         */
        public static object GetField(this IDictionary<string, object> dictionary, string fieldName)
        {
            return dictionary.TryGetValue(fieldName, out var result) ? result : null;
        }
    }
}
