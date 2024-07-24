using UnityEngine;

namespace ActionManagerTests
{
    public class ButtonHandler : MonoBehaviour
    {

        public void OnBtnClick()
        {
            Debug.Log("Clicked button " + gameObject.name);
        }
        
    }
}