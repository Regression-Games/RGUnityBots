using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;

/*
 * A component that can be inherited to relay game state information to
 * Regression Games. Includes a few default pieces of information that can
 * be enabled from the editor when attached to an object.
 *
 * TODO (REG-1300): Can we use a generic type instead of a dictionary? That way users can
 *       debug and use the states within their own code?
 */
namespace RegressionGames.RGBotConfigs
{
    [RequireComponent(typeof(RGEntity))]
    public class RGState : MonoBehaviour
    {
        
        // we require each state to have an 'RGEntity' component
        protected RGEntity rgEntity
        {
            get { return GetComponent<RGEntity>(); }
        }
        
        /**
         * A function that is overriden to provide the custom state of this specific GameObject.
         * For example, you may want to retrieve and set the health of a player on the returned
         * object, or their inventory information
         */
        public virtual Dictionary<string, object> GetState()
        {
            return new Dictionary<string, object>();
        }

        /**
         * Returns the entire internal state for this object, which consists of the default
         * states tracked by RG, and the result of any overridden GetState implementation.
         */
        public RGStateEntity GetGameObjectState()
        {
            var state = new RGStateEntity()
            {
                ["id"] = rgEntity.transform.GetInstanceID(),
                ["type"] = rgEntity.objectType,
                ["isPlayer"] = rgEntity.isPlayer,
                ["isRuntimeObject"] = rgEntity.isRuntimeObject,
            };

            if (rgEntity.syncPosition) state["position"] = rgEntity.transform.position;
            if (rgEntity.syncRotation) state["rotation"] = rgEntity.transform.rotation;
            var dict = GetState();
            foreach (var entry in dict)
            {
                state.Add(entry.Key, entry.Value);
            }

            return state;
        }
    }
}