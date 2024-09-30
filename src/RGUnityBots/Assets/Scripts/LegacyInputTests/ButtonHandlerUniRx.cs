using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace LegacyInputTests
{
    public class ButtonHandlerUniRx : MonoBehaviour
    {
        public GameObject clickedIndicator;
        private void Start()
        {
            GetComponent<Button>().OnClickAsObservable().Subscribe(_ =>
            {
                Debug.Log($"{gameObject.name} OnClickAsObservable()");
                clickedIndicator.SetActive(true);
            });
        }
    }
}
