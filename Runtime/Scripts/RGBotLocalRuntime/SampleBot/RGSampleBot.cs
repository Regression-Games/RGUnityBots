using System;
using System.Collections.Generic;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace RegressionGames.RGBotLocalRuntime.SampleBot
{
    public class RGSampleBot : RGUserBot
    {
        protected override long GetBotId()
        {
            return 888877775555L;
        }

        protected override string GetBotName()
        {
            return "TTLocalBot";
        }

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
            var classIndex = new System.Random().Next(4);
            var classes = new string[] {"Mage", "Rogue", "Tank", "Archer"};
            rgObject.CharacterConfig = $"{{\"characterType\": \"{classes[classIndex]}\"}}";
        }

        public override void ProcessTick(RG rgObject)
        {
            //Temporary test code
            try
            {
                var entities = rgObject.FindEntities();
                if (entities.Count > 0)
                {
                    var target = entities[new System.Random().Next(entities.Count)];

                    var targetPosition = target.position ?? Vector3.zero;
                    //TODO: If Actions were strongly typed we wouldn't need to build this weird map...
                    var action = new RGActionRequest("PerformSkill", new Dictionary<string, object>()
                    {
                        { "skillId", new System.Random().Next(2) },
                        { "targetId", target["id"] },
                        { "xPosition", targetPosition.x },
                        { "yPosition", targetPosition.y },
                        { "zPosition", targetPosition.z },
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
                RGDebug.LogError($"Error getting target position: {ex}");
            }
        }
    }
}