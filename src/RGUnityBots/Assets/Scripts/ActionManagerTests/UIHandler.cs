using UnityEngine;
using UnityEngine.UI;

namespace ActionManagerTests
{
    public class UIHandler : MonoBehaviour
    {

        public void OnBtnClick()
        {
            Debug.Log("Clicked button " + gameObject.name);
        }

        public void OnToggleChanged()
        {
            var toggle = GetComponent<Toggle>();
            Debug.Log(gameObject.name + " changed to " + toggle.isOn);
        }
        
    }
}