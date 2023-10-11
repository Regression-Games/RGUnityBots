using System.Collections;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.RGBotConfigs
{
    public interface IRGState
    {
        public RGStateEntity GetGameObjectState();
    }
}