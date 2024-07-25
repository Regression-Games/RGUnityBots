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

        public void OnSliderValueChanged()
        {
            var slider = GetComponent<Slider>();
            string posName;
            if (slider.value < 0.01f)
            {
                posName = "start";
            } else if (slider.value <= 0.5f)
            {
                posName = "first half";
            } else if (slider.value < 0.99f)
            {
                posName = "second half";
            }
            else if (slider.value >= 0.99f)
            {
                posName = "end";
            }
            else
            {
                posName = "";
            }
            Debug.Log(gameObject.name + " changed to " + posName);
        }
    }
}