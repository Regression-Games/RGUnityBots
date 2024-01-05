using System;
using System.Collections;
using System.Collections.Generic;
using RegressionGames;
using UnityEngine;
using RGThirdPersonDemo.Events;
using UnityEngine.EventSystems;

namespace RGThirdPersonDemo
{
    [RGStateType("Enemy")]
    public class EnemyController : MonoBehaviour, ISelectable
    {
        [SerializeField] private EnemyEntity enemyInfo;
        [SerializeField] private EnemyEvent onEnemySelected;
        [SerializeField] private EnemyEvent onEnemyDeselected;
        [SerializeField] private DamageText enemyDamageText;
        
        private Animator _animator;
        private CheckCameraVisibility _checkCameraVisibility;
        private Transform _player;
        
        private int _currentHp;
        private bool _isSelected = false;
        private bool _isMouseOver = false;

        private void Awake()
        {
            _checkCameraVisibility = GetComponent<CheckCameraVisibility>();
            if (_checkCameraVisibility != null)
            {
                _checkCameraVisibility.onBecameInvisible.AddListener(OnExitCameraView);
            }

            _animator = GetComponent<Animator>();
            _player = GameObject.FindWithTag("Player").transform;
        }

        private void Start()
        {
            _currentHp = enemyInfo.hp;
        }

        private void OnExitCameraView()
        {
            if (_isSelected)
            {
                Deselect();
            }
        }
        
        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.IsPointerOverGameObject())
                    return;
                
                if (_isSelected && !_isMouseOver)
                {
                    // Deselect the object if it's already selected and the mouse click is away from the object.
                    Deselect();
                }
                else if (_isMouseOver)
                {
                    // Select the object if it's not already selected and the mouse click is over the object.
                    Select();
                }
            }

            if (_isSelected)
            {
                RotateTowardsTarget(_player);
            }
        }

        void OnMouseEnter()
        {
            _isMouseOver = true;
        }

        void OnMouseExit()
        {
            _isMouseOver = false;
        }
        
        public void Select()
        {
            _isSelected = true;
            onEnemySelected.Trigger(this);
        }

        public void Deselect()
        {
            _isSelected = false;
            onEnemyDeselected.Trigger(this);
        }

        public void Hit(int damage)
        {
            _animator.Play("Hit");
            enemyDamageText.SetDamageText(Mathf.Round(damage).ToString());
            _currentHp -= damage;
        }
        
        /*
         * Gets the enemy's name from its assigned info
         */
        public string GetName()
        {
            if (!enemyInfo)
            {
                Debug.LogWarning($"{gameObject.name} is missing entity info. Please check the inspector and assign missing fields");
                return default;
            }
            return enemyInfo.enemyName;
        }

        /*
         * Gets the enemy's icon from its assigned info
         */
        public Sprite GetIcon()
        {
            if (!enemyInfo)
            {
                Debug.LogWarning($"{gameObject.name} is missing entity info. Please check the inspector and assign missing fields");
                return default;
            }
            return enemyInfo.icon;
        }

        /*
         * Gets the enemies current hp
         */
        [RGState("CurrentHealth")]
        public int GetCurrentHp()
        {
            return _currentHp;
        }
        
        /*
         * Gets the enemy's total HP from its assigned info
         */
        [RGState("MaxHealth")]
        public int GetTotalHp()
        {
            if (!enemyInfo)
            {
                Debug.LogWarning($"{gameObject.name} is missing entity info. Please check the inspector and assign missing fields");
                return default;
            }
            return enemyInfo.hp;
        }

        /*
         * Gets the enemy's 'center' position, which may differ from the transform's root
         */
        public Vector3 GetCenterPosition()
        {
            Vector3 position = transform.position;
            position.y = 1f;
            return position;
        }
        
        /*
         * The enemy will look at the player if is currently selectly
         */
        private void RotateTowardsTarget(Transform target)
        {
            float rotationSpeed = 100f;
            if (target != null)
            {
                Vector3 targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
                Vector3 directionToTarget = targetPosition - transform.position;

                if (directionToTarget != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                }
            }
        }
    }
}