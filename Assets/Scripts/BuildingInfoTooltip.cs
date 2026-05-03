using TMPro;
using UnityEngine;

public class BuildingInfoTooltip : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    public void Show(Building building)
    {
        if (building == null)
        {
            Hide();
            return;
        }

        ResolveReferences();
        if (nameText != null)
        {
            nameText.text = building.BuildingName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = building.Description;
        }

        if (priceText != null)
        {
            priceText.text = building.Price.ToString();
        }

        var hasStructuredFields = nameText != null || descriptionText != null || priceText != null;
        if (!hasStructuredFields && infoText != null)
        {
            infoText.text = $"{building.BuildingName}\n{building.Description}\n{building.Price}";
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (nameText == null)
        {
            nameText = FindTextByName("Name");
        }

        if (descriptionText == null)
        {
            descriptionText = FindTextByName("Description");
        }

        if (priceText == null)
        {
            priceText = FindTextByName("price");
        }

        if (infoText == null && nameText == null && descriptionText == null && priceText == null)
        {
            infoText = GetComponentInChildren<TMP_Text>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private TMP_Text FindTextByName(string childName)
    {
        if (string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        var transforms = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var child = transforms[i];
            if (child == null || !string.Equals(child.name, childName, System.StringComparison.Ordinal))
            {
                continue;
            }

            return child.GetComponent<TMP_Text>();
        }

        return null;
    }
}
