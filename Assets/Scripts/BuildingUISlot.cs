using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingUISlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject activeIndicator;

    private BuildPanel _owner;
    private Building _building;

    public Building Building => _building;

    private void Awake()
    {
        ResolveReferences();
        SetActiveIndicator(false);
    }

    public void Initialize(BuildPanel owner, Building building)
    {
        _owner = owner;
        _building = building;

        ResolveReferences();
        if (iconImage != null)
        {
            iconImage.sprite = building != null ? building.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        SetActiveIndicator(false);
    }

    public void SetSelected(bool isSelected)
    {
        SetActiveIndicator(isSelected);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _owner?.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _owner?.HideTooltip(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _owner?.SelectBuilding(this);
    }

    private void SetActiveIndicator(bool isActive)
    {
        if (activeIndicator != null)
        {
            activeIndicator.SetActive(isActive);
        }
    }

    private void ResolveReferences()
    {
        if (activeIndicator == null)
        {
            var indicator = transform.Find("ActiveIndicator");
            if (indicator != null)
            {
                activeIndicator = indicator.gameObject;
            }
        }

        if (iconImage == null)
        {
            var icon = transform.Find("Panel/Icon") ?? transform.Find("Icon");
            if (icon != null)
            {
                iconImage = icon.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>(true);
            }
        }
    }
}
