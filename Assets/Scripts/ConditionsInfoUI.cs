using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConditionsInfoUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text terraformingPercentText;
    [SerializeField] private Slider terraformingSlider;
    [SerializeField] private RectTransform heatAtmosphereInfoRoot;
    [SerializeField] private RectTransform circleRect;
    [SerializeField] private RectTransform indicatorRect;

    [Header("Auto Find")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private string terraformingValueObjectName = "TerraformProgress";
    [SerializeField] private string heatAtmosphereInfoObjectName = "HeatAtmosphereInfo";
    [SerializeField] private string circleObjectName = "container";
    [SerializeField] private string indicatorObjectName = "Indicator";

    [Header("Indicator")]
    [SerializeField, Min(0f)] private float indicatorPadding = 2f;
    [SerializeField] private Color indicatorColor = new(0.2f, 0.85f, 0.25f, 1f);
    [SerializeField] private Vector2 fallbackIndicatorSize = new(14f, 14f);

    private bool _initialized;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
    }

    public void SetTerraformingPercent(float percent)
    {
        EnsureReferences();
        if (terraformingPercentText == null && terraformingSlider == null)
        {
            return;
        }

        var clampedPercent = Mathf.Clamp(percent, 0f, 100f);
        if (terraformingPercentText != null)
        {
            terraformingPercentText.text = $"{Mathf.RoundToInt(clampedPercent)} %";
        }

        if (terraformingSlider != null)
        {
            terraformingSlider.SetValueWithoutNotify(clampedPercent / 100f);
        }
    }

    public void SetConditions(
        float humidity,
        float atmosphere,
        float minHumidity,
        float maxHumidity,
        float minAtmosphere,
        float maxAtmosphere)
    {
        EnsureReferences();
        if (indicatorRect == null || circleRect == null)
        {
            return;
        }

        var xNorm = NormalizeToSignedRange(atmosphere, minAtmosphere, maxAtmosphere);
        var yNorm = NormalizeToSignedRange(humidity, minHumidity, maxHumidity);
        var direction = new Vector2(xNorm, yNorm);
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        var radius = Mathf.Min(circleRect.rect.width, circleRect.rect.height) * 0.5f;
        var indicatorHalfExtent = Mathf.Max(indicatorRect.rect.width, indicatorRect.rect.height) * 0.5f;
        var maxDistance = Mathf.Max(0f, radius - indicatorPadding - indicatorHalfExtent);
        indicatorRect.anchoredPosition = direction * maxDistance;
    }

    private void EnsureReferences()
    {
        if (_initialized && !autoFindReferences)
        {
            return;
        }

        if (autoFindReferences)
        {
            if (terraformingPercentText == null)
            {
                terraformingPercentText = FindText(terraformingValueObjectName);
            }

            if (terraformingSlider == null)
            {
                terraformingSlider = FindSlider(terraformingValueObjectName);
            }

            if (heatAtmosphereInfoRoot == null)
            {
                heatAtmosphereInfoRoot = FindRect(heatAtmosphereInfoObjectName);
            }

            if (circleRect == null && heatAtmosphereInfoRoot != null)
            {
                circleRect = FindRectInParent(heatAtmosphereInfoRoot, circleObjectName) ?? heatAtmosphereInfoRoot;
            }
        }

        if (indicatorRect == null && circleRect != null)
        {
            indicatorRect = FindRectInParent(circleRect, indicatorObjectName);
            if (indicatorRect == null)
            {
                indicatorRect = CreateFallbackIndicator(circleRect);
            }
        }

        _initialized = true;
    }

    private TMP_Text FindText(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var rect = FindRect(objectName);
        if (rect == null)
        {
            return null;
        }

        return rect.GetComponent<TMP_Text>() ?? rect.GetComponentInChildren<TMP_Text>(true);
    }

    private Slider FindSlider(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var rect = FindRect(objectName);
        if (rect == null)
        {
            return null;
        }

        return rect.GetComponent<Slider>() ?? rect.GetComponentInChildren<Slider>(true);
    }

    private RectTransform FindRect(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var go = GameObject.Find(objectName);
        if (go == null)
        {
            return null;
        }

        return go.GetComponent<RectTransform>();
    }

    private static RectTransform FindRectInParent(RectTransform parent, string name)
    {
        if (parent == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var all = parent.GetComponentsInChildren<RectTransform>(true);
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] != null && string.Equals(all[i].name, name, System.StringComparison.Ordinal))
            {
                return all[i];
            }
        }

        return null;
    }

    private RectTransform CreateFallbackIndicator(RectTransform parent)
    {
        var go = new GameObject(indicatorObjectName, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = fallbackIndicatorSize;
        var image = go.GetComponent<Image>();
        image.color = indicatorColor;
        return rect;
    }

    private static float NormalizeToSignedRange(float value, float min, float max)
    {
        if (max <= min)
        {
            return 0f;
        }

        var t = Mathf.InverseLerp(min, max, value);
        return t * 2f - 1f;
    }
}
