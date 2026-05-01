using TMPro;
using UnityEngine;

public class BuildingInfoTooltip : MonoBehaviour
{
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
        if (infoText != null)
        {
            infoText.text = $"{building.BuildingName}\n$ {building.Price}";
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (infoText == null)
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
}
