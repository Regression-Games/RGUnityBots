using System;
using System.Collections.Generic;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace RegressionGames.StateActionTypes
{
    public abstract class RGActionBehaviour : MonoBehaviour
    {
        public abstract string GetActionName();

        public abstract void Invoke(RGActionRequest actionRequest);

    }
}
