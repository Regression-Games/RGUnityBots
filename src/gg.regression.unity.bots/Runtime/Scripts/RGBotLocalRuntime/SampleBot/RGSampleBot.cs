using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;
using Random = System.Random;

namespace RegressionGames.RGBotLocalRuntime.SampleBot
{
    public class RGSampleBot : RGUserBot
    {
        protected override bool GetIsSpawnable()
        {
            return true;
        }

        protected override RGBotLifecycle GetBotLifecycle()
        {
            return RGBotLifecycle.MANAGED;
        }

        public override void ConfigureBot(RG rgObject)
        {
            var classIndex = new Random().Next(4);
            var classes = new[] { "Mage", "Rogue", "Tank", "Archer" };
            rgObject.CharacterConfig = new Dictionary<string, object>
            {
                { "characterType", $"{classes[classIndex]}" }
            };
        }

        public override void ProcessTick(RG rgObject)
        {

            var myPlayers = rgObject.GetMyPlayers();
            if (myPlayers.Count == 0) return;
            var thisEntity = myPlayers[0];

            //Temporary test code
            try
            {
                var entities = rgObject.FindEntities();
                if (entities.Count > 0)
                {
                    var target = entities[new Random().Next(entities.Count)];

                    RGGizmos.CreateLine(thisEntity.id, target.position, Color.red, "TargetEnemy");
                    RGGizmos.CreateSphere(target.id, Color.blue, 0.7f, false, "TargetEnemy");

                    var chosenAbility = new Random().Next(2);
                    RGGizmos.CreateText(thisEntity.id, $"Ability {chosenAbility} on enemy {target["id"]}");

                    var targetPosition = (Vector3)target["position"];
                    //TODO (REG-1302): If Actions were strongly typed we wouldn't need to build this weird map...
                    var action = new RGActionRequest("PerformSkill", new Dictionary<string, object>()
                    {
                        { "skillId", chosenAbility },
                        { "targetId", target["id"] },
                        { "xPosition", targetPosition.x },
                        { "yPosition", targetPosition.y },
                        { "zPosition", targetPosition.z }
                    });
                    rgObject.PerformAction(action);
                }
                else
                {
                    RGDebug.LogWarning("No players found...");
                }
            }
            catch (Exception ex)
            {
                RGDebug.LogException(ex, $"Error getting target position.");
            }
        }
    }
}
