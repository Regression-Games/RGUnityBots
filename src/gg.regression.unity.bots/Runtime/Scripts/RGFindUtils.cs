using System.Collections.Generic;
using System.Linq;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames
{
    public sealed class RGFindUtils
    {
        private RGFindUtils()
        {
        }

        public static RGFindUtils Instance { get; } = new RGFindUtils();

        private readonly Dictionary<int, GameObject> _objectCache = new Dictionary<int, GameObject>();

        /**
         * Finds an object by instance ID, using a type parameter for narrowing down
         * the number of objects to search for in the scene.
         */
        public GameObject FindOneByInstanceId(int instanceId) 
        {
            // If the requested object is not in the cache, refresh the cache
            if (!_objectCache.ContainsKey(instanceId))
            {
                UpdateCache();
            }
            return _objectCache.GetValueOrDefault(instanceId, null);
        }

        private void UpdateCache()
        {
            var gameObjects = FindStatefulBehaviours().Select(v => v.gameObject).Distinct();
            foreach (var obj in gameObjects)
            {
                _objectCache[obj.transform.GetInstanceID()] = obj;
            }
        }
        
        /**
         * <summary>WARNING:  Call this no more than once per tick, it evaluates every behaviour in the scene.  VERY EXPENSIVE</summary>
         */
        public IEnumerable<MonoBehaviour> FindStatefulBehaviours()
        {
            var statefulBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(v => BehavioursWithStateOrActions.GetRGStateEntityMappingForBehaviour(v) != null || v is IRGStateBehaviour);
            return statefulBehaviours;
        }
    }
}