using System.Collections;
using System.Collections.Generic;
using RegressionGames;
using UnityEditor;
using UnityEngine;

namespace RGThirdPersonDemo
{
    [RequireComponent((typeof(PlayerMovement)))]
    [RequireComponent(typeof(Animator))]
    [RGStateType(true)]
    public class PlayerAttack : MonoBehaviour
    {
        [SerializeField] private Transform _projectileRoot;
        
        private PlayerMovement _playerMovement;
        private Animator _animator;
        private EnemyController _selectedEnemy;
        private Projectile _projectile;
        private IEnumerator _attackEndCoroutine;
        private bool _hasProjectile;
        private string _attackAnimation;

        public List<AttackAbility> abilities = new ();
        
        void Awake()
        {
            _animator = GetComponent<Animator>();
            _playerMovement = GetComponent<PlayerMovement>();
        }

        /*
         * Begins an attack with the given attack ability info
         */
        public void Attack(AttackAbility attack)
        {
            if (_selectedEnemy == null)
            {
                Debug.LogWarning("Unable to attack without enemy selected");
                return;
            }
            
            // rotate to target
            _playerMovement.LookAtTransform(_selectedEnemy.transform);
            
            // animate
            if (!string.IsNullOrEmpty(attack.animation))
            {
                _attackAnimation = attack.animation;
                _animator.SetBool(_attackAnimation, true);
            }

            // create projectile
            if (attack.projectile != null)
            {
                GameObject projectileGO = Instantiate(attack.projectile, _projectileRoot);
                _hasProjectile = projectileGO.TryGetComponent(out _projectile);
                if (_hasProjectile)
                {
                    _projectile.Initialize(attack);
                }
                projectileGO.transform.localPosition = Vector3.zero;
            }

            _attackEndCoroutine = TriggerAttackEnd(attack);
            StartCoroutine(_attackEndCoroutine);
        }

        /*
         * Cancels the current attack
         */
        public void CancelAttack()
        {
            // stop attack end coroutine
            if (_attackEndCoroutine != null)
            {
                StopCoroutine(_attackEndCoroutine);
                _attackEndCoroutine = null;
            }
            
            // remove any pending projectiles
            if (_hasProjectile && _projectile != null)
            {
                Destroy(_projectile.gameObject);
                _hasProjectile = false;
            }
            
            // run end-of-attack logic
            EndAttack();
        }
        
        /*
         * Triggered from the animator. Animation event used to precisely match animation to projectile launch
         * without relying on frame-counting
         */
        public void TriggerProjectile()
        {
            if (!_hasProjectile || _selectedEnemy == null)
            {
                return;
            }
            _projectile.Fire(_selectedEnemy);
        }
        
        public void EndAttack()
        {
            if (!string.IsNullOrEmpty(_attackAnimation))
            {
                Debug.Log("Cancelling attack animation");
                _animator.SetBool(_attackAnimation, false);
            }
        }
        
        public void SelectEnemy(EnemyController enemyController)
        {
            _selectedEnemy = enemyController;
            _animator.SetBool("Enemy Selected", true);
        }

        public EnemyController GetSelectedEnemy()
        {
            return _selectedEnemy;
        }

        public void Deselect()
        {
            _selectedEnemy = null;
            _animator.SetBool("Enemy Selected", false);
        }

        // Trigger the attack end, after the given cast time
        private IEnumerator TriggerAttackEnd(AttackAbility attack)
        {
            yield return new WaitForSeconds(attack.castTime);
            EndAttack();
            _attackEndCoroutine = null;
            _hasProjectile = false;
        }

        [RGAction]
        public void SelectAndAttackEnemy(int enemyId, int ability)
        {
            Debug.Log("Attack enemy with id " + enemyId);
            var enemy = RGFindUtils.Instance.FindOneByInstanceId<EnemyController>(enemyId);
            SelectEnemy(enemy);
            Attack(abilities[ability]);
        }

        [RGState]
        public bool IsAttacking()
        {
            return !string.IsNullOrEmpty(_attackAnimation) && _animator.GetBool(_attackAnimation);
        }
        
    }
}