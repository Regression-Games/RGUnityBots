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
    public class RGAction_MoveInDirection : RGAction
    {
        public void Start()
        {
            AddMethod("MoveInDirection", new Action<Vector2>(GetComponent<PlayerInputControl>().MoveInput));
        }

        public override string GetActionName()
        {
            return "MoveInDirection";
        }

        public override void StartAction(Dictionary<string, object> input)
        {
            Vector2 newMoveDirection = default;
            if (input.TryGetValue("newMoveDirection", out var newMoveDirectionInput))
            {
                try
                {
                    if (newMoveDirectionInput is Vector2)
                    {
                        newMoveDirection = (Vector2)newMoveDirectionInput;
                    }
                    else
                    {
                        newMoveDirection = RGSerialization.Deserialize_Vector2(newMoveDirectionInput.ToString());
                    }
                }
                catch (Exception ex)
                {
                    RGDebug.LogError($"Failed to parse 'newMoveDirection' - {ex}");
                }
            }
            else
            {
                RGDebug.LogError("No parameter 'newMoveDirection' found");
                return;
            }

            Invoke("MoveInDirection", newMoveDirection);
        }
    }

    public class RGActionRequest_MoveInDirection : RGActionRequest
    {
        public RGActionRequest_MoveInDirection(Vector2 newMoveDirection)
        {
            action = "MoveInDirection";
            Input = new ()
            {{"newMoveDirection", newMoveDirection}, };
        }
    }
}