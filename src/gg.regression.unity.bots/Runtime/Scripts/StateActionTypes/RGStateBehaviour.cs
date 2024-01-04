using UnityEngine;

namespace RegressionGames.StateActionTypes
{
    public interface IRGStateBehaviour
    {
        public void PopulateStateEntity(RGStateEntity_Core gameObjectCoreState, out bool isPlayer);
    }
    
    public abstract class RGStateBehaviour<T> : MonoBehaviour , IRGStateBehaviour where T : IRGStateEntity
    {
        // cache this so we don't create every tick
        private T _myStateEntity;
        
        public void PopulateStateEntity(RGStateEntity_Core gameObjectCoreState, out bool isPlayer)
        {
            var typeName = GetType().Name;
            _myStateEntity ??= CreateStateEntityInstance();

            var entityTypeName = _myStateEntity.GetEntityType();
            if (!string.IsNullOrEmpty(entityTypeName))
            {
                typeName = entityTypeName;
            }

            var state = _myStateEntity;
            if (gameObjectCoreState.TryGetValue(typeName, out var stateObject))
            {
                state = (T)stateObject;
            }
            else
            {
                gameObjectCoreState[typeName] = state;
            }

            isPlayer = state.GetIsPlayer();
            PopulateStateEntity(state);
        }

        protected abstract T CreateStateEntityInstance();

        protected abstract void PopulateStateEntity(T stateEntity);
    }
}
