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
            var indicator = FindChildRecursive(transform, "ActiveIndicator");
            if (indicator != null)
            {
                activeIndicator = indicator.gameObject;
            }
        }

        if (iconImage == null)
        {
            var icon = FindChildRecursive(transform, "Icon");
            if (icon != null)
            {
                iconImage = icon.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                iconImage = FindFirstUsableIconImage();
            }
        }
    }

    private Image FindFirstUsableIconImage()
    {
        var images = GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var image = images[i];
            if (image == null || image.gameObject == activeIndicator)
            {
                continue;
            }

            return image;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            var nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
