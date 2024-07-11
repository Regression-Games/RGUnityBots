using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace RegressionGames.StateRecorder
{
    public interface IEntitySelector
    {
        public List<Entity> SelectEntities(EntityManager entityManager);

        public (Vector3?, Quaternion?)? SelectPositionAndRotationForEntity(Entity entity, EntityManager entityManager);

        public (Bounds?, float, Bounds?)? SelectBounds(Entity entity, EntityManager entityManager);
    }
}
