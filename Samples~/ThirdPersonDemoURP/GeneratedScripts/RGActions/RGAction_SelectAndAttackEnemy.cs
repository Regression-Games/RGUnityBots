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

namespace RegressionGames
{
    public class RGAction_SelectAndAttackEnemy : RGAction
    {
        public void Start()
        {
            AddMethod("SelectAndAttackEnemy", new Action<int, int>(GetComponent<PlayerAttack>().SelectAndAttackEnemy));
        }

        public override string GetActionName()
        {
            return "SelectAndAttackEnemy";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            int enemyId = default;
            if (input.TryGetValue("enemyId", out var enemyIdInput))
            {
                try
                {
                    int.TryParse(enemyIdInput.ToString(), out enemyId);
                }
                catch (Exception ex)
                {
                    RGDebug.LogError($"Failed to parse 'enemyId' - {ex}");
                }
            }
            else
            {
                RGDebug.LogError("No parameter 'enemyId' found");
                return;
            }

            int ability = default;
            if (input.TryGetValue("ability", out var abilityInput))
            {
                try
                {
                    int.TryParse(abilityInput.ToString(), out ability);
                }
                catch (Exception ex)
                {
                    RGDebug.LogError($"Failed to parse 'ability' - {ex}");
                }
            }
            else
            {
                RGDebug.LogError("No parameter 'ability' found");
                return;
            }

            Invoke("SelectAndAttackEnemy", enemyId, ability);
        }
    }

    public class RGActionRequest_SelectAndAttackEnemy : RGActionRequest
    {
        public RGActionRequest_SelectAndAttackEnemy(int enemyId, int ability)
        {
            action = "SelectAndAttackEnemy";
            Input = new ()
            {{"enemyId", enemyId}, {"ability", ability}, };
        }
    }
}