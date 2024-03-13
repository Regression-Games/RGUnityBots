using System;
using RegressionGames;
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
    public class QuickStartBot : MonoBehaviour, IRGBot
    {

        private Vector3? _destination;
        private readonly Random _random = new ();

        public void Update()
        {
            ProcessTick();
        }

        public void ProcessTick()
        {
            var playerAttack = GetComponent<PlayerAttack>();

            if (playerAttack == null)
            {
                RGDebug.LogError("QuickStartBot spawned without selecting a prefab");
                Destroy(this.gameObject);
                return;
            }

            // If we are attacking, just wait, and don't move
            if (playerAttack.IsAttacking())
            {
                return;
            }

            var myPosition = transform.position;

            var playerInputControl = GetComponent<PlayerInputControl>();

            // If we have no destination, or we have reached our destination, choose a new one
            if (_destination == null || Vector3.Distance(myPosition, (Vector3) _destination) < 1)
            {
                // Choose a random position within a box around the enemy
                var x = _random.Next(-2, 6);
                var z = _random.Next(2, 9);
                _destination = new Vector3(x, 0, z);

                // Also, shoot the enemy while we are waiting
                var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                if (enemies.Length > 0)
                {
                    playerInputControl.MoveInput(new Vector2(0, 0));
                    playerAttack.SelectAndAttackEnemy(enemies[0], 0);
                    return;
                }
            }

            // Get the direction from the bot position to this new position
            var direction = (Vector3) _destination - new Vector3(myPosition.x, 0, myPosition.z);

            // Move to the desired location
            playerInputControl.MoveInput(new Vector2(direction.x, direction.z));
        }

        void OnDrawGizmos()
        {
            if (_destination != null)
            {
                // Draw some debug lines to help us see where we are going. Turn on Gizmos in the editor to see this.
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere((Vector3)_destination, 0.5f);
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, (Vector3)_destination);
            }
        }
    }
}
