using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RGThirdPersonDemo
{
    public class Projectile : MonoBehaviour
    {
        public float projectileSpeed = 10f;
        [Tooltip("Set a maximum time for the projectile to live in case it doesn't hit the target.")]
        public float maxFlightTime = 5f;
        [Tooltip("Set a radius within which the projectile registers a hit.")]
        public float hitRadius = 1.0f; 

        private EnemyController target;
        private float _flightTimer;
        private bool _initialized;
        private int _damage;
        
        private void Start()
        {
            _flightTimer = 0f;
        }

        public void Initialize(AttackAbility attack)
        {
            _initialized = true;
            _damage = attack.damage;
        }

        public void Fire(EnemyController target)
        {
            if (!_initialized)
            {
                return;
            }
            transform.SetParent(null, true);
            this.target = target;
            _flightTimer = 0f;
        }
        
        private void Update()
        {
            if (target == null)
            {
                // If the target is null, it means the projectile hasn't been fired yet or the target was destroyed.
                return;
            }

            _flightTimer += Time.deltaTime;

            if (_flightTimer >= maxFlightTime)
            {
                // Destroy the projectile if it has been flying for too long without hitting the target.
                Destroy(gameObject);
            }

            // Calculate the direction to the target.
            Vector3 directionToTarget = (target.GetCenterPosition() - transform.position).normalized;

            // Move the projectile in the direction of the target's last position.
            transform.position += directionToTarget * projectileSpeed * Time.deltaTime;

            // Check for a hit.
            if (Vector3.Distance(transform.position, target.GetCenterPosition()) <= hitRadius)
            {
                // Perform hit actions here (e.g., deal damage to the target).
                target.Hit(_damage);

                // Destroy the projectile after a hit.
                Destroy(gameObject);
            }
        }
        
    }
}


