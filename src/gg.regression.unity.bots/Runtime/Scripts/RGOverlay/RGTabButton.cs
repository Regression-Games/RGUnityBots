using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RegressionGames
{
    /**
     * <summary>
     * A button that is intended to be used with other buttons of the same type. These buttons should be used to toggle
     * visible content or settings
     * </summary>
     */
    public class RGTabButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        // is this button currently selected
        public bool isActive;

        public UnityEvent onClick;

        private Image _background;

        private TMP_Text _label;

        private Color _fromColor;

        private Color _toColor;

        private float _colorChangeAlpha;

        private bool _isChangingColor;

        private static readonly Color DefaultColor = new (1, 1, 1, 0.01f);

        private static readonly Color HoverColor = new (1, 1, 1, 0.05f);

        private static readonly Color ActiveColor = Color.white;

        /**
         * <summary>
         * Ensure that the required fields are set, and set the active state
         * </summary>
         */
        public void Start()
        {
            var background = GetComponent<Image>();
            if (background != null)
            {
                _background = background;
            }
            else
            {
                Debug.LogError("RGTabButton is missing its background");
            }

            var label = GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                _label = label;
            }
            else
            {
                Debug.LogError("RGTabButton is missing its label");
            }
            
            _background.color = isActive ? ActiveColor : DefaultColor;
            _label.color = isActive ? Color.black : ActiveColor;;
        }

        /**
         * <summary>
         * When this button is changing color, update the color lerp process
         * </summary>
         */
        public void Update()
        {
            if (!_isChangingColor)
            {
                return;
            }
            
            // change the colour over 0.1 second
            _colorChangeAlpha += Time.deltaTime * 10.0f;
            _background.color = Color.Lerp(_fromColor, _toColor, _colorChangeAlpha);

            if (_colorChangeAlpha >= 1.0f)
            {
                _colorChangeAlpha = 0;
                _isChangingColor = false;
            }
        }

        /**
         * <summary>
         * Show the hover state when the user's pointer enters this button
         * </summary>
         */
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isActive)
            {
                return;
            }
            
            StartColorChange(HoverColor);
        }

        /**
         * <summary>
         * Revert the hover state when the user's pointer exits this button
         * </summary>
         */
        public void OnPointerExit(PointerEventData eventData)
        {
            if (isActive)
            {
                return;
            }
            
            StartColorChange(DefaultColor);
        }

        /**
         * <summary>
         * Set this button as the currently selected one, and execute the click action assigned
         * </summary>
         */
        public void OnPointerDown(PointerEventData eventData)
        {
            if (isActive)
            {
                return;
            }
            
            onClick.Invoke();
            SetAsCurrent(true);
        }

        /**
         * <summary>
         * If this button is set to be active:
         * - Set the button to be selected (active state)
         * - Find any other sibling RGTabButtons and set them to the default state
         * If this button is set to not be active:
         * - Set the button to the default state
         * </summary>
         * <para name="setIsActive">Whether this button should change to the active state or not</para>
         */
        private void SetAsCurrent(bool setIsActive)
        {
            var nextColor = setIsActive ? ActiveColor : DefaultColor;
            StartColorChange(nextColor);
            _label.color = setIsActive ? Color.black : ActiveColor;
            
            if (!setIsActive)
            {
                isActive = false;
                return;
            }
            
            var parentTransform = transform.parent;
            if (parentTransform == null)
            {
                isActive = true;
                return;
            }
            
            foreach (Transform sibling in parentTransform)
            {
                var tabButton = sibling.gameObject.GetComponent<RGTabButton>();
                if (tabButton != null && tabButton.isActive)
                {
                    tabButton.SetAsCurrent(false);
                }
            }

            isActive = setIsActive;
        }

        private void StartColorChange(Color nextColor)
        {
            _fromColor = _background.color;
            _toColor = nextColor;
            _isChangingColor = true;
            _colorChangeAlpha = 0;
        }
    }
}
