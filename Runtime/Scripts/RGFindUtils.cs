using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RGFindUtils
{
    private static readonly RGFindUtils instance = new RGFindUtils();
    static RGFindUtils() {}
    private RGFindUtils() {}
    public static RGFindUtils Instance => instance;

    private Dictionary<int, Object> objectCache = new Dictionary<int, Object>();

    /**
     * Finds an object by instance ID, using a type parameter for narrowing down
     * the number of objects to search for in the scene.
     */
    public T FindOneByInstanceId<T>(int instanceId) where T: MonoBehaviour
    {
        // If the requested object is not in the cache, refresh the cache
        if (!objectCache.ContainsKey(instanceId))
            UpdateCache<T>();

        return objectCache.GetValueOrDefault(instanceId, null) as T;
    }

    private void UpdateCache<T>() where T : MonoBehaviour
    {
        var gameObjects = Object.FindObjectsOfType<T>();
        foreach (var obj in gameObjects)
        {
            objectCache[obj.transform.GetInstanceID()] = obj;
        }
    }
}
