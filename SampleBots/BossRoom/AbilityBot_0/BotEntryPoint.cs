using System;
using System.Collections.Generic;
using RegressionGames;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;
using UnityEngine;

namespace AbilityBot_0
{
    public class BotEntryPoint : RGUserBot
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
            var classIndex = new System.Random().Next(4);
            var classes = new string[] {"Mage", "Rogue", "Tank", "Archer"};
            rgObject.CharacterConfig = $"{{\"characterType\": \"{classes[classIndex]}\"}}";
        }

        public override void ProcessTick(RG rgObject)
        {
            try
            {
                var entities = rgObject.FindEntities();
                if (entities.Count > 0)
                {
                    // default to friendly ability
                    var skillId = 0;
                    var target = entities[new System.Random().Next(entities.Count)];
                    if ((int)target.GetValueOrDefault("team", 0) == 1)
                    {
                        // if enemy.. set ability to 1
                        skillId = 1;
                    }

                    // ensure target is not UI component
                    if (target.ContainsKey("position"))
                    {
                        var targetPosition = (Vector3)target["position"];
                        var action = new RGActionRequest("PerformSkill", new Dictionary<string, object>()
                        {
                            { "skillId",  skillId},
                            { "targetId", target["id"] },
                            { "xPosition", targetPosition.x },
                            { "yPosition", targetPosition.y },
                            { "zPosition", targetPosition.z },
                        });
                        rgObject.PerformAction(action);   
                    }
                }
                else
                {
                    RGDebug.LogWarning("No entities found...");
                }
            }
            catch (Exception ex)
            {
                RGDebug.LogError($"Error getting target position: {ex}");
            }
        }
    }
}