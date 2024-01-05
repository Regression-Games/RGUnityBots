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

    public sealed class RGStateEntity_Empty : RGStateEntityBase
    {
        public override bool GetIsPlayer()
        {
            return false;
        }

        public override string GetEntityType()
        {
            return null;
        }

        public override void PopulateFromMonoBehaviour(MonoBehaviour monoBehaviour)
        {
            // no op
        }
    }
}
