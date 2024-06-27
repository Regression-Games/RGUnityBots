using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.Models;
using Unity.Entities;
using UnityEngine;

namespace RegressionGames.StateRecorder.ECS.Models
{
    public class EntityStatus : ObjectStatus
    {
        private EntityStatus()
        {

        }

        public Entity Entity;


        // right now this resets on awake from InGameObjectFinder, but we may have to deal with dynamically re-parented transforms better at some point...
        private static readonly Dictionary<long, EntityStatus> EntitiesIveSeen = new(1000);

        public static void Reset()
        {
            EntitiesIveSeen.Clear();
        }

        public static EntityStatus GetOrCreateEntityStatus(Entity theEntity, EntityManager entityManager)
        {
            string tPath = null;

            var id = ((long)theEntity.Index)<<16 + theEntity.Version;

            if (EntitiesIveSeen.TryGetValue(id, out var status))
            {
                if (status.Path != null)
                {
                    tPath = status.Path;
                }
            }

            if (tPath == null)
            {
                if (status == null)
                {
                    status = new EntityStatus()
                    {
                        Id = id
                    };
                    EntitiesIveSeen[id] = status;
                    // update the cache with our result
                }

                tPath = BuildEntityPath(theEntity, entityManager);

                status.Path = tPath;
                status.NormalizedPath = tPath;
            }

            return status;
        }

        private static readonly ThreadLocal<StringBuilder> PathBuilder = new ThreadLocal<StringBuilder>(() => new(1000));

        private static string BuildEntityPath(Entity theEntity, EntityManager entityManager)
        {
            var pathBuilder = PathBuilder.Value;
            pathBuilder.Clear();
            pathBuilder.Append("Entity-");
            // Iterate all component types alpha to build up the entity 'type' name
            var componentTypes = entityManager.GetComponentTypes(theEntity);
            foreach (string s in componentTypes.Select(a=>a.GetType().Name.Substring(0,3)).OrderBy(a=>a))
            {
                pathBuilder.Append(s).Append("-");
            }
            return pathBuilder.ToString();
        }

        public override bool PositionHitsCollider(Vector3 position)
        {
            throw new System.NotImplementedException();
        }
    }
}
