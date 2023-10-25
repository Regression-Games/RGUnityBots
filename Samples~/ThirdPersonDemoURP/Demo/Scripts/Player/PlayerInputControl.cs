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

        private void Update()
        {
            MoveInput(new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")));
            JumpInput(Input.GetButtonDown("Jump"));
        }

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