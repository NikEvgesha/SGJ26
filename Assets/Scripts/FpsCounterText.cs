using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class FpsCounterText : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField] private string prefix = "FPS: ";

    private int _frames;
    private float _elapsedTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        refreshInterval = Mathf.Max(0.05f, refreshInterval);
        ResolveReferences();
    }

    private void Update()
    {
        _frames++;
        _elapsedTime += Time.unscaledDeltaTime;

        if (_elapsedTime < refreshInterval)
        {
            return;
        }

        var fps = _frames / _elapsedTime;
        targetText.text = $"{prefix}{Mathf.RoundToInt(fps)}";
        _frames = 0;
        _elapsedTime = 0f;
    }

    private void ResolveReferences()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }
}
