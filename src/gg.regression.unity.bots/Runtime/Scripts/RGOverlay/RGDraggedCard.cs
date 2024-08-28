using TMPro;
using UnityEngine;

namespace RegressionGames
{
    public class RGDraggedCard : MonoBehaviour
    {
        public string draggedCardName;

        [SerializeField] public TMP_Text namePrefab;

        public void Start()
        {
            if (namePrefab != null)
            {
                namePrefab.text = draggedCardName;
            }
        }
    }
}