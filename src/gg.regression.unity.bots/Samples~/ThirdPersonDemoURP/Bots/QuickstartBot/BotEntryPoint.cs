using RegressionGames;
using RegressionGames.RGBotLocalRuntime;
using RGThirdPersonDemo;
using UnityEngine;
using Random = System.Random;

namespace QuickstartBot
{
    
    /**
     * This bot implements a simple kiting strategy:
     *  - Check if the bot has fully spawned; if not, exit the script.
     *  - If the bot is attacking, make it wait and not move.
     *  - If the bot has no destination or has reached its destination:
     *     - Choose a random position within a specified box around the enemy.
     *     - Set this new position as the destination.
     *     - If there are enemies nearby:
     *         - Stop moving.
     *         - Select and attack the first enemy in the list, and then terminate this turn.
     *  - Calculate the direction from the bots current position to the destination.
     *  - Move the bot in the calculated direction using game actions.
     */
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
            // No config needed
        }

        private Vector3? _destination;
        private readonly Random _random = new ();

        public override void ProcessTick(RG rgObject)
        {
            
            // Get this bot
            var thisBot = rgObject.GetMyPlayer();
            if (thisBot == null) return; // We have not fully spawned yet, so wait

            // If we are attacking, just wait, and don't move
            if (thisBot.GetField<RGStateEntity_PlayerAttack>("PlayerAttack")?.IsAttacking == true) return;
            
            // If we have no destination, or we have reached our destination, choose a new one
            if (_destination == null || Vector3.Distance(thisBot.position, (Vector3) _destination) < 1)
            {
                // Choose a random position within a box around the enemy
                var x = _random.Next(-2, 6);
                var z = _random.Next(2, 9);
                _destination = new Vector3(x, 0, z);
                
                // Also, shoot the enemy while we are waiting
                var enemies = rgObject.FindEntities("Enemy");
                if (enemies.Count > 0)
                {
                    rgObject.PerformAction(new RGActionRequest_PlayerInputControl_MoveInDirection(new Vector2(0, 0)));
                    rgObject.PerformAction(new RGActionRequest_PlayerAttack_SelectAndAttackEnemy(enemies[0].id, 0));
                    return;
                }
            }
            
            // Draw some debug lines to help us see where we are going. Turn on Gizmos in the editor to see this.
            RGGizmos.CreateSphere((Vector3) _destination, Color.yellow, 0.5f, true, "Destination Point");
            RGGizmos.CreateLine(thisBot.id, (Vector3) _destination, Color.red, "Destination Point");

            // Get the direction from the bot position to this new position
            var direction = (Vector3) _destination - new Vector3(thisBot.position.x, 0, thisBot.position.z);
            
            // Move to the desired location
            rgObject.PerformAction(new RGActionRequest_PlayerInputControl_MoveInDirection(new Vector2(direction.x, direction.z)));
        }
    }
}
