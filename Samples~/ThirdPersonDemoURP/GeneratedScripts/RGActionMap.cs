/*
* This file has been automatically generated. Do not modify.
*/

using System;

namespace RegressionGames
{
    using UnityEngine;
    using RGThirdPersonDemo;

    public class RGActionMap : MonoBehaviour
    {
        private void Awake()
        {
            if (this.TryGetComponent<PlayerAttack>(out var _))
            {
                gameObject.AddComponent<RGAction_SelectAndAttackEnemy>();
            }

            if (this.TryGetComponent<PlayerInputControl>(out var _))
            {
                gameObject.AddComponent<RGAction_MoveInDirection>();
            }
        }
    }
}