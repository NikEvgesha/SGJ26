using TMPro;
using UnityEngine;

public class TileInfoUI : MonoBehaviour
{
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private TMP_Text greenPercentText;

    private Camera _lookCamera;
    private bool _isVisible;

    private void Awake()
    {
        rootRect ??= transform as RectTransform;
        EnsureTextComponent();

        HideInfo();
    }

    private void LateUpdate()
    {
        if (!_isVisible)
        {
            return;
        }

        FaceCamera();
    }

    public void ShowInfo(Vector3 worldPosition, float greenPercent, Camera camera)
    {
        rootRect ??= transform as RectTransform;
        EnsureTextComponent();
        _lookCamera = camera;
        if (rootRect != null)
        {
            rootRect.position = worldPosition;
        }

        if (greenPercentText != null)
        {
            greenPercentText.text = $"{Mathf.RoundToInt(greenPercent)} %";
        }

        SetVisible(true);
        FaceCamera();
    }

    public void HideInfo()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }
    }

    private void FaceCamera()
    {
        if (_lookCamera == null || rootRect == null)
        {
            return;
        }

        var direction = rootRect.position - _lookCamera.transform.position;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        rootRect.rotation = Quaternion.LookRotation(direction.normalized, _lookCamera.transform.up);
    }

    private void EnsureTextComponent()
    {
        if (greenPercentText != null)
        {
            return;
        }

        greenPercentText = GetComponentInChildren<TMP_Text>(true);
        if (greenPercentText != null)
        {
            return;
        }

        if (rootRect == null)
        {
            rootRect = transform as RectTransform;
        }

        if (rootRect == null)
        {
            return;
        }

        var textObject = new GameObject("GreenPercent");
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.SetParent(rootRect, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(220f, 72f);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "0 %";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 36f;
        text.color = Color.white;
        greenPercentText = text;
    }
}
