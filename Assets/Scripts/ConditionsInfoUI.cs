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
    [SerializeField] private RectTransform indicatorArrowRect;
    [SerializeField] private BuildingEffectsController effectsController;

    [Header("Auto Find")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private string terraformingValueObjectName = "TerraformProgress";
    [SerializeField] private string heatAtmosphereInfoObjectName = "HeatAtmosphereInfo";
    [SerializeField] private string circleObjectName = "container";
    [SerializeField] private string indicatorObjectName = "Indicator";
    [SerializeField] private string indicatorArrowObjectName = "Arrow";

    [Header("Indicator")]
    [SerializeField, Min(0f)] private float indicatorPadding = 2f;
    [SerializeField, Min(0.01f)] private float indicatorMoveDuration = 1f;
    [SerializeField] private Color indicatorColor = new(0.2f, 0.85f, 0.25f, 1f);
    [SerializeField] private Vector2 fallbackIndicatorSize = new(14f, 14f);
    [SerializeField] private float arrowAngleOffset = -90f;
    [SerializeField, Min(0f)] private float arrowOrbitRadius = 12f;
    [SerializeField, Min(0f)] private float arrowEffectThreshold = 0.0001f;

    private bool _initialized;
    private bool _hasIndicatorTarget;
    private Vector3 _indicatorMoveStartWorldPosition;
    private Vector3 _targetIndicatorWorldPosition;
    private float _indicatorMoveElapsed;
    private Vector2 _lastArrowDirection = Vector2.up;
    private Vector2 _lastArrowEffect = new(float.NaN, float.NaN);
    private bool _isArrowVisible = true;
    private CanvasGroup _indicatorArrowCanvasGroup;
    private Graphic[] _indicatorArrowGraphics;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
    }

    private void Update()
    {
        UpdateIndicatorVisual();
        UpdateArrowFromEffects();
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
        var indicatorHalfWidth = indicatorRect.rect.width * 0.5f;
        var indicatorHalfHeight = indicatorRect.rect.height * 0.5f;
        var maxX = Mathf.Max(0f, circleRect.rect.width * 0.5f - indicatorPadding - indicatorHalfWidth);
        var maxY = Mathf.Max(0f, circleRect.rect.height * 0.5f - indicatorPadding - indicatorHalfHeight);
        var localTarget = circleRect.rect.center + new Vector2(
            Mathf.Clamp(xNorm, -1f, 1f) * maxX,
            Mathf.Clamp(yNorm, -1f, 1f) * maxY);
        var nextTarget = circleRect.TransformPoint(localTarget);
        if (!_hasIndicatorTarget)
        {
            _hasIndicatorTarget = true;
            _targetIndicatorWorldPosition = nextTarget;
            _indicatorMoveStartWorldPosition = nextTarget;
            _indicatorMoveElapsed = indicatorMoveDuration;
            indicatorRect.position = nextTarget;
            return;
        }

        if ((nextTarget - _targetIndicatorWorldPosition).sqrMagnitude > 0.0001f)
        {
            _indicatorMoveStartWorldPosition = indicatorRect.position;
            _targetIndicatorWorldPosition = nextTarget;
            _indicatorMoveElapsed = 0f;
        }
    }

    private void UpdateIndicatorVisual()
    {
        if (!_hasIndicatorTarget || indicatorRect == null)
        {
            return;
        }

        var currentPosition = indicatorRect.position;
        var toTarget = _targetIndicatorWorldPosition - currentPosition;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            indicatorRect.position = _targetIndicatorWorldPosition;
            return;
        }

        _indicatorMoveElapsed += Time.deltaTime;
        var t = Mathf.Clamp01(_indicatorMoveElapsed / Mathf.Max(0.01f, indicatorMoveDuration));
        indicatorRect.position = Vector3.Lerp(_indicatorMoveStartWorldPosition, _targetIndicatorWorldPosition, t);
    }

    private Vector2 GetIndicatorLocalDirection(Vector3 worldDirection)
    {
        if (indicatorArrowRect == null)
        {
            return new Vector2(worldDirection.x, worldDirection.y);
        }

        var parent = indicatorArrowRect.parent;
        if (parent == null)
        {
            return new Vector2(worldDirection.x, worldDirection.y);
        }

        var localDirection = parent.InverseTransformVector(worldDirection);
        return new Vector2(localDirection.x, localDirection.y);
    }

    private void UpdateArrowFromEffects()
    {
        if (effectsController == null)
        {
            SetArrowVisible(false);
            _lastArrowEffect = new Vector2(float.NaN, float.NaN);
            return;
        }

        var effectDirection = new Vector2(
            effectsController.AtmospherePerTick,
            effectsController.TemperaturePerTick);
        if ((effectDirection - _lastArrowEffect).sqrMagnitude <= 0.000001f)
        {
            return;
        }

        _lastArrowEffect = effectDirection;
        SetArrowDirection(effectDirection);
    }

    private void SetArrowDirection(Vector2 direction)
    {
        if (indicatorArrowRect == null)
        {
            return;
        }

        if (direction.sqrMagnitude <= arrowEffectThreshold * arrowEffectThreshold)
        {
            SetArrowVisible(false);
            return;
        }

        SetArrowVisible(true);
        _lastArrowDirection = direction.normalized;

        var angle = Mathf.Atan2(_lastArrowDirection.y, _lastArrowDirection.x) * Mathf.Rad2Deg + arrowAngleOffset;
        indicatorArrowRect.anchoredPosition = _lastArrowDirection * arrowOrbitRadius;
        indicatorArrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void SetArrowVisible(bool isVisible)
    {
        if (indicatorArrowRect == null || _isArrowVisible == isVisible)
        {
            return;
        }

        _isArrowVisible = isVisible;
        if (_indicatorArrowCanvasGroup != null)
        {
            _indicatorArrowCanvasGroup.alpha = isVisible ? 1f : 0f;
            return;
        }

        if (_indicatorArrowGraphics == null)
        {
            CacheArrowReferences();
        }

        if (_indicatorArrowGraphics == null)
        {
            return;
        }

        for (var i = 0; i < _indicatorArrowGraphics.Length; i++)
        {
            if (_indicatorArrowGraphics[i] != null)
            {
                _indicatorArrowGraphics[i].enabled = isVisible;
            }
        }
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

        if (indicatorArrowRect == null && indicatorRect != null)
        {
            indicatorArrowRect = FindRectInParent(indicatorRect, indicatorArrowObjectName);
        }

        if (indicatorArrowRect != null && _indicatorArrowCanvasGroup == null && _indicatorArrowGraphics == null)
        {
            CacheArrowReferences();
        }

        if (effectsController == null)
        {
            effectsController = FindFirstObjectByType<BuildingEffectsController>(FindObjectsInactive.Include);
        }

        _initialized = true;
    }

    private void CacheArrowReferences()
    {
        _indicatorArrowCanvasGroup = null;
        _indicatorArrowGraphics = null;
        if (indicatorArrowRect == null)
        {
            return;
        }

        _indicatorArrowCanvasGroup = indicatorArrowRect.GetComponent<CanvasGroup>();
        if (_indicatorArrowCanvasGroup == null)
        {
            _indicatorArrowGraphics = indicatorArrowRect.GetComponentsInChildren<Graphic>(true);
        }
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
