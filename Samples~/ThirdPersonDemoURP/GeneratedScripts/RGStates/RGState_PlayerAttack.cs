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

namespace RGThirdPersonDemo
{
    public class RGStateEntity_PlayerAttack : RGStateEntity<RGState_PlayerAttack>
    {
        public bool IsAttacking => (bool)this.GetValueOrDefault("IsAttacking");
    }

    public class RGState_PlayerAttack : RGState
    {
        private PlayerAttack myComponent;
        public void Start()
        {
            myComponent = this.GetComponent<PlayerAttack>();
            if (myComponent == null)
            {
                RGDebug.LogError("PlayerAttack not found");
            }
        }

        protected override Dictionary<string, object> GetState()
        {
            var state = new Dictionary<string, object>();
            state.Add("IsAttacking", myComponent.IsAttacking());
            return state;
        }
    }
}