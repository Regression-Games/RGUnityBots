/// <auto-generated>
/// This file has been automatically generated. Do not modify.
/// </auto-generated>
using System;
using RegressionGames;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RGThirdPersonDemo
{
    public class RGStateEntity_EnemyController : RGStateEntityBase
    {
        public static readonly Type BehaviourType = typeof(EnemyController);
        public static readonly string EntityTypeName = "Enemy";
        public static readonly bool IsPlayer = false;
        public override string GetEntityType()
        {
            return EntityTypeName;
        }

        public override bool GetIsPlayer()
        {
            return IsPlayer;
        }

        public override void PopulateFromMonoBehaviour(MonoBehaviour monoBehaviour)
        {
            var behaviour = (EnemyController)monoBehaviour;
            this["CurrentHealth"] = behaviour.GetCurrentHp();
            this["MaxHealth"] = behaviour.GetTotalHp();
        }

        public int CurrentHealth => (int)int.Parse(this.GetField("CurrentHealth").ToString());
        public int MaxHealth => (int)int.Parse(this.GetField("MaxHealth").ToString());
    }
}