using RegressionGames;
using UnityEngine;

namespace RGThirdPersonDemo
{
    public class PlayerInputControl : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Bot Settings")] public bool isBot = true;

        private void Update()
        {
            if (!isBot)
            {
                MoveInput(new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")));
                JumpInput(Input.GetButtonDown("Jump"));
            }
        }

        [RGAction("MoveInDirection")]
        public void MoveInput(Vector2 newMoveDirection)
        {
            move = newMoveDirection;
        }
        
        public void JumpInput(bool newJumpState)
        {
            jump = newJumpState;
        }
    }
}