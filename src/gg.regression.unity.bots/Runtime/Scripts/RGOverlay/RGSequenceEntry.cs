using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/**
 * <summary>Displays the high-level information for a Sequence</summary>
 */
public class RGSequenceEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string name;

    public string description;

    public DateTime lastModified;

    /**
     * UI component fields
     */
    [SerializeField]
    public TMP_Text nameComponent;

    [SerializeField]
    public TMP_Text descriptionComponent;

    [SerializeField]
    public TMP_Text lastModifiedComponent;

    /**
     * Hover mangement. Assign a UI component that will be shown by default,
     * and another UI component that will hide by default (ie: be shown only when
     * the Sequence is hovered)
     */
    [SerializeField]
    public Component showByDefault;

    [SerializeField]
    public Component hideByDefault;

    void Start()
    {
        if (nameComponent != null)
        {
            nameComponent.text = name;
        }

        if (descriptionComponent != null)
        {
            descriptionComponent.text = description;
        }

        if (lastModifiedComponent != null)
        {
            lastModifiedComponent.text = lastModified.ToString("D");
        }

        if (showByDefault != null)
        {
            showByDefault.gameObject.SetActive(true);
        }

        if (hideByDefault != null)
        {
            hideByDefault.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (showByDefault != null && hideByDefault != null)
        {
            showByDefault.gameObject.SetActive(false);
            hideByDefault.gameObject.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (showByDefault != null && hideByDefault != null)
        {
            showByDefault.gameObject.SetActive(true);
            hideByDefault.gameObject.SetActive(false);
        }
    }
}
