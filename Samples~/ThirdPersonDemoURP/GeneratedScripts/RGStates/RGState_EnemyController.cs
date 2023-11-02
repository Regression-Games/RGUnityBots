/*
* This file has been automatically generated. Do not modify.
*/

using System;
using System.Collections.Generic;
using RegressionGames;
using RegressionGames.RGBotConfigs;
using RegressionGames.StateActionTypes;
using UnityEngine;
using RGThirdPersonDemo;

namespace RegressionGames.RGBotConfigs
{
    public class RGStateEntity_EnemyController : RGStateEntity<RGState_EnemyController>
    {
        public int currentHealth => (int)int.Parse(this.GetValueOrDefault("currentHealth").ToString());
        public int maxHealth => (int)int.Parse(this.GetValueOrDefault("maxHealth").ToString());
    }

    public class RGState_EnemyController : RGState
    {
        private EnemyController myComponent;
        public void Start()
        {
            myComponent = this.GetComponent<EnemyController>();
            if (myComponent == null)
            {
                RGDebug.LogError("EnemyController not found");
            }
        }

        protected override Dictionary<string, object> GetState()
        {
            var state = new Dictionary<string, object>();
            state.Add("currentHealth", myComponent.GetCurrentHp());
            state.Add("maxHealth", myComponent.GetTotalHp());
            return state;
        }
    }
}