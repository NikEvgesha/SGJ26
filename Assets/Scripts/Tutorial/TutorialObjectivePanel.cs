using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialObjectivePanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private Button collapseButton;
    [SerializeField] private TMP_Text collapseButtonLabel;
    [SerializeField] private RectTransform contentRoot;

    private bool _isCollapsed;
    private bool _isBuilding;
    private bool _isBuilt;

    public static TutorialObjectivePanel CreateOrFind(Transform uiRoot, TutorialObjectivePanel prefab = null)
    {
        var existing = FindFirstObjectByType<TutorialObjectivePanel>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.EnsureBuilt();
            return existing;
        }

        if (uiRoot == null)
        {
            return null;
        }

        TutorialObjectivePanel panel;
        if (prefab != null)
        {
            panel = Instantiate(prefab, uiRoot, false);
            panel.name = prefab.name;
            panel.EnsureBuilt();
            return panel;
        }

        var root = new GameObject("TutorialObjectivePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(TutorialObjectivePanel));
        panel = root.GetComponent<TutorialObjectivePanel>();
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(uiRoot, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -16f);
        rect.sizeDelta = new Vector2(420f, 180f);

        var image = root.GetComponent<Image>();
        image.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);

        panel.EnsureBuilt();
        return panel;
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnEnable()
    {
        EnsureBuilt();
    }

    public void SetVisible(bool isVisible)
    {
        EnsureBuilt();
        if (canvasGroup == null)
        {
            gameObject.SetActive(isVisible);
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    public void SetObjective(string text)
    {
        EnsureBuilt();
        if (objectiveText != null)
        {
            objectiveText.text = text ?? string.Empty;
        }
    }

    public void ExpandForStepChange()
    {
        SetCollapsed(false);
    }

    public void SetCollapsed(bool isCollapsed)
    {
        _isCollapsed = isCollapsed;
        EnsureBuilt();
        ApplyCollapsedState();
    }

    private void ApplyCollapsedState()
    {
        if (contentRoot != null)
        {
            contentRoot.gameObject.SetActive(!_isCollapsed);
        }

        if (collapseButtonLabel != null)
        {
            collapseButtonLabel.text = _isCollapsed ? ">" : "<";
        }
    }

    private void ToggleCollapsed()
    {
        SetCollapsed(!_isCollapsed);
    }

    private void EnsureBuilt()
    {
        if (_isBuilt || _isBuilding)
        {
            return;
        }

        _isBuilding = true;
        try
        {
            canvasGroup ??= GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            if (contentRoot == null)
            {
                contentRoot = EnsureChildRect(transform, "Content");
                contentRoot.anchorMin = new Vector2(0f, 0f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.offsetMin = new Vector2(12f, 12f);
                contentRoot.offsetMax = new Vector2(-54f, -12f);
            }

            if (collapseButton == null)
            {
                var buttonRoot = EnsureChildRect(transform, "CollapseButton");
                buttonRoot.anchorMin = new Vector2(1f, 1f);
                buttonRoot.anchorMax = new Vector2(1f, 1f);
                buttonRoot.pivot = new Vector2(1f, 1f);
                buttonRoot.sizeDelta = new Vector2(34f, 34f);
                buttonRoot.anchoredPosition = new Vector2(-10f, -10f);

                var buttonImage = buttonRoot.GetComponent<Image>() ?? buttonRoot.gameObject.AddComponent<Image>();
                buttonImage.color = new Color(0.14f, 0.18f, 0.24f, 0.95f);

                collapseButton = buttonRoot.GetComponent<Button>() ?? buttonRoot.gameObject.AddComponent<Button>();
                collapseButton.targetGraphic = buttonImage;
            }

            if (collapseButtonLabel == null)
            {
                var labelRect = EnsureChildRect(collapseButton.transform, "Label");
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                collapseButtonLabel = labelRect.GetComponent<TMP_Text>() ?? labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                collapseButtonLabel.alignment = TextAlignmentOptions.Center;
                collapseButtonLabel.fontSize = 20f;
                collapseButtonLabel.color = Color.white;
                collapseButtonLabel.text = "<";
                TryApplySharedFont(collapseButtonLabel);
            }

            if (objectiveText == null)
            {
                var textRect = EnsureChildRect(contentRoot, "ObjectiveText");
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                objectiveText = textRect.GetComponent<TMP_Text>() ?? textRect.gameObject.AddComponent<TextMeshProUGUI>();
                objectiveText.alignment = TextAlignmentOptions.TopLeft;
                objectiveText.enableWordWrapping = true;
                objectiveText.fontSize = 26f;
                objectiveText.color = Color.white;
                objectiveText.text = string.Empty;
                TryApplySharedFont(objectiveText);
            }

            collapseButton.onClick.RemoveListener(ToggleCollapsed);
            collapseButton.onClick.AddListener(ToggleCollapsed);
            ApplyCollapsedState();
            _isBuilt = true;
        }
        finally
        {
            _isBuilding = false;
        }
    }

    private static RectTransform EnsureChildRect(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child != null)
        {
            return child as RectTransform;
        }

        var go = new GameObject(childName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static void TryApplySharedFont(TMP_Text text)
    {
        if (text == null || text.font != null)
        {
            return;
        }

        var anyText = FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        if (anyText != null && anyText.font != null)
        {
            text.font = anyText.font;
        }
    }
}
