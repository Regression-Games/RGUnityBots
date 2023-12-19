using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace RegressionGames.StateActionTypes
{
    public abstract class RGActionBehaviour : MonoBehaviour
    {
        public abstract string GetActionName();
        
        public virtual string GetEntityType()
        {
            return GetType().Name;
        }

        public abstract void Invoke(RGActionRequest actionRequest);

    }
}
