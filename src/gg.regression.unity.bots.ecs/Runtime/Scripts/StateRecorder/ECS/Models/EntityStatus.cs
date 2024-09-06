using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using RegressionGames.StateRecorder.Models;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RegressionGames.StateRecorder.ECS.Models
{
    public class EntityStatus : ObjectStatus
    {

        public Entity Entity;

        public EntityManager EntityManager;

        public List<IComponentData> ComponentData = new();

        private EntityStatus()
        {
        }


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
                    long? parentId = null;
                    if (entityManager.HasComponent<Parent>(theEntity))
                    {
                        var theParent = entityManager.GetComponentData<Parent>(theEntity).Value;
                        parentId = ((long)theParent.Index) << 16 + theParent.Version;
                    }

                    status = new EntityStatus()
                    {
                        Id = id,
                        ParentId = parentId
                    };
                    EntitiesIveSeen[id] = status;

                    if (entityManager.HasComponent<SceneReference>(theEntity))
                    {
                        var sceneReference = entityManager.GetComponentData<SceneReference>(theEntity);
                        //TODO (reg-1832) : Lookup actual scene name ?
                        status.Scene = "{guid:\"" + sceneReference.SceneGUID + "\"}";
                    }

                    // todo (reg-1832) - populate layer name from RenderFilterSettings
                    //if (entityManager.HasComponent<RenderFilterSettings>(theEntity))
                    {
                        //var renderFilterSettings = entityManager.GetComponentData<RenderFilterSettings>(theEntity);
                        //status.LayerName = LayerMask.LayerToName(renderFilterSettings.Layer);
                    }
                }

                tPath = BuildEntityPath(theEntity, entityManager);

                status.Path = tPath;
                status.NormalizedPath = tPath;
            }

            status.Entity = theEntity;
            status.EntityManager = entityManager;

            return status;
        }

        private static readonly ThreadLocal<StringBuilder> PathBuilder = new(() => new(1000));

        private static string BuildEntityPath(Entity theEntity, EntityManager entityManager)
        {
            var pathBuilder = PathBuilder.Value;
            pathBuilder.Clear();
            pathBuilder.Append("Entity-");
            var entityArchetype = entityManager.GetChunk(theEntity).Archetype;
            pathBuilder.Append(entityArchetype.ToString());
            return pathBuilder.ToString();
        }

        public override bool PositionHitsCollider(Vector3 position)
        {
            //TODO (REG-1832): Somehow implement me even though we can't easily / efficiently query this for collider components using the entitymanager
            return false;
        }

    }
}
