using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo
{
    [CreateAssetMenu(menuName = "Ability/Attack")]
    public class AttackAbility : ScriptableObject
    {
        public string abilityName;
        
        [Header("Casting Settings")]
        [Tooltip("Icon to appear next to the next casting progress bar")]
        public Sprite abilityIcon;
        [Tooltip("Cast time in seconds")]
        public float castTime;
        [Tooltip("Cooldown time in seconds")]
        public float cooldownTime;
        [Tooltip("Color of the casting progress bar")]
        public Color castColor;
        [Tooltip("The name of the animation to play")]
        public string animation;
        
        [Header("Projectile Settings")]
        [Tooltip("Prefab created after cast is complete")]
        public GameObject projectile;
        [Tooltip("Damage caused on hit")]
        public int damage;
    }
}