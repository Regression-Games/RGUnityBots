using TMPro;
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

        private string SliderPositionName(float value)
        {
            if (value < 0.01f)
            {
                return "start";
            } else if (value <= 0.5f)
            {
                return "first half";
            } else if (value < 0.99f)
            {
                return "second half";
            }
            else if (value >= 0.99f)
            {
                return "end";
            }
            else
            {
                return "";
            }
        }

        public void OnSliderValueChanged()
        {
            var slider = GetComponent<Slider>();
            string posName = SliderPositionName(slider.value);
            Debug.Log(gameObject.name + " changed to " + posName);
        }

        public void OnScrollbarValueChanged()
        {
            var scrollbar = GetComponent<Scrollbar>();
            string posName = SliderPositionName(scrollbar.value);
            Debug.Log(gameObject.name + " changed to " + posName);
        }

        public void OnDropdownValueChanged()
        {
            Debug.Log(gameObject.name + " value changed");
        }

        public void OnInputFieldSubmit()
        {
            InputField field = gameObject.GetComponent<InputField>();
            if (field != null)
            {
                Debug.Log(gameObject.name + " submitted with text " + field.text);
            }
        }

        public void OnTMPInputFieldEndEdit()
        {
            TMP_InputField field = gameObject.GetComponent<TMP_InputField>();
            if (field != null)
            {
                Debug.Log(gameObject.name + " submitted with text " + field.text);
            }
        }
    }
}