using System;
using System.Collections.Generic;
using System.Linq;
using RegressionGames;
using RegressionGames.RGBotLocalRuntime;
using RegressionGames.StateActionTypes;
using UnityEngine;
using Random = System.Random;

namespace MenuAbilityBot_1
{
    public class BotEntryPoint : RGUserBot
    {
        private bool _playedGame;

        // flags for clicking the buttons we need to click to start the game
        private readonly Dictionary<string, bool> _stateFlags = new()
        {
            { "RGHostButton", false },
            { "StartWithRGButton", false },
            { "SelectProfileButton", false },
            { "ProfileMenuButton", false },
            { "ReadyButton", false },
            { "Seat7Button", false },
            { "CheatsCancelButton", false },
            { "GameHUDStartButton", false }
        };

        protected override bool GetIsSpawnable()
        {
            return false;
        }

        protected override RGBotLifecycle GetBotLifecycle()
        {
            return RGBotLifecycle.PERSISTENT;
        }

        public override void ConfigureBot(RG rgObject)
        {
            var classIndex = new Random().Next(4);
            var classes = new[] { "Mage", "Rogue", "Tank", "Archer" };
            rgObject.CharacterConfig = $"{{\"characterType\": \"{classes[classIndex]}\"}}";
        }

        public override void ProcessTick(RG rgObject)
        {
            switch (rgObject.GetSceneName())
            {
                case "MainMenu":

                    if (_playedGame)
                    {
                        rgObject.Complete();
                    }
                    else
                    {
                        var hostButton = rgObject.GetInteractableButton("RGHostButton");
                        if (hostButton != null && _stateFlags["StartWithRGButton"] && !_stateFlags["RGHostButton"])
                        {
                            rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                            {
                                { "targetId", hostButton.id }
                            }));
                            _stateFlags["RGHostButton"] = true;
                        }

                        var startButton = rgObject.GetInteractableButton("StartWithRGButton");
                        if (startButton != null && _stateFlags["SelectProfileButton"] &&
                            !_stateFlags["StartWithRGButton"])
                        {
                            rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                            {
                                { "targetId", startButton.id }
                            }));
                            _stateFlags["StartWithRGButton"] = true;
                        }

                        var selectProfileButton = rgObject.GetInteractableButton("SelectProfileButton");
                        if (selectProfileButton != null && _stateFlags["ProfileMenuButton"] &&
                            !_stateFlags["SelectProfileButton"])
                        {
                            rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                            {
                                { "targetId", selectProfileButton.id }
                            }));
                            _stateFlags["SelectProfileButton"] = true;
                        }

                        var profileMenuButton = rgObject.GetInteractableButton("ProfileMenuButton");
                        if (profileMenuButton != null && !_stateFlags["ProfileMenuButton"])
                        {
                            rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                            {
                                { "targetId", profileMenuButton.id }
                            }));
                            _stateFlags["ProfileMenuButton"] = true;
                        }
                    }

                    break;
                case "CharSelect":
                    if (_playedGame)
                    {
                        rgObject.Complete();
                    }
                    else
                    {
                        var readyButton = rgObject.GetInteractableButton("ReadyButton");
                        if (readyButton != null && _stateFlags["Seat7Button"] && !_stateFlags["ReadyButton"])
                        {
                            rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                            {
                                { "targetId", readyButton.id }
                            }));
                            _stateFlags["ReadyButton"] = true;
                        }

                        var seat7Button = rgObject.GetInteractableButton("Seat7Button");
                        if (seat7Button != null && !_stateFlags["Seat7Button"])
                        {
                            rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                            {
                                { "targetId", seat7Button.id }
                            }));
                            _stateFlags["Seat7Button"] = true;
                        }
                    }

                    break;
                case "BossRoom":
                    _playedGame = true;

                    var GameHUDStartButton = rgObject.GetInteractableButton("GameHUDStartButton");
                    if (GameHUDStartButton != null && _stateFlags["CheatsCancelButton"] &&
                        !_stateFlags["GameHUDStartButton"])
                    {
                        rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                        {
                            { "targetId", GameHUDStartButton.id }
                        }));
                        _stateFlags["GameHUDStartButton"] = true;
                    }

                    var CheatsCancelButton = rgObject.GetInteractableButton("CheatsCancelButton");
                    if (CheatsCancelButton != null && !_stateFlags["CheatsCancelButton"])
                    {
                        rgObject.PerformAction(new RGActionRequest("ClickButton", new Dictionary<string, object>
                        {
                            { "targetId", CheatsCancelButton.id }
                        }));
                        _stateFlags["CheatsCancelButton"] = true;
                    }

                    if (_stateFlags["CheatsCancelButton"] && _stateFlags["GameHUDStartButton"])
                        try
                        {
                            var entities = rgObject.FindEntities();
                            if (entities.Count > 0)
                            {
                                var target = entities[new Random().Next(entities.Count)];

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

                    break;
                case "PostGame":
                default:
                    // teardown myself
                    rgObject.Complete();
                    break;
            }
        }
        
    }
}
