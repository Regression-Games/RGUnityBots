using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable once CheckNamespace
// ReSharper disable InconsistentNaming
namespace RegressionGames.StateActionTypes
{
    [Serializable]
    public abstract class RGStateEntityBase : Dictionary<string, object>, IRGStateEntity
    {
        public abstract bool GetIsPlayer();

        public abstract string GetEntityType();

        public abstract void PopulateFromMonoBehaviour(MonoBehaviour monoBehaviour);

    }
}
