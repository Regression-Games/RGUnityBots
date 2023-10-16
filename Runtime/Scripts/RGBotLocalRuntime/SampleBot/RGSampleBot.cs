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
            //Temporary test code
            try
            {
                var entities = rgObject.FindEntities();
                if (entities.Count > 0)
                {
                    var target = entities[new Random().Next(entities.Count)];

                    //TODO (REG-1302): If Actions were strongly typed we wouldn't need to build this weird map...
                    var action = new RGActionRequest("PerformSkill", new Dictionary<string, object>
                    {
                        { "skillId", new Random().Next(2) },
                        { "targetId", target["id"] },
                        { "xPosition", target.position.x },
                        { "yPosition", target.position.y },
                        { "zPosition", target.position.z }
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
