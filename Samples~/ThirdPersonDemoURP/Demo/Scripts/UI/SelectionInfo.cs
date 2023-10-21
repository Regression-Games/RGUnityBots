using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RGThirdPersonDemo
{
    public class SelectionInfo : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image hpImage;

        private bool _isEnemySelected;
        private EnemyController _selectedEnemy;
        
        // Start is called before the first frame update
        void Start()
        {

        }

        private void Update()
        {
            if (!_isEnemySelected)
            {
                return;
            }

            hpImage.fillAmount = (float)_selectedEnemy.GetCurrentHp() / (float)_selectedEnemy.GetTotalHp();
        }

        // Show the selection UI and set its info to the current selection 
        public void SelectEnemy(EnemyController enemyController)
        {
            _isEnemySelected = true;
            _selectedEnemy = enemyController;
            nameText.text = enemyController.GetName();
            iconImage.sprite = enemyController.GetIcon();
            gameObject.ActivateChildren();
        }

        // Cleanup and hide selection UI
        public void Deselect()
        {
            _isEnemySelected = false;
            _selectedEnemy = null;
            gameObject.DeactivateChildren();
        }
    }
}