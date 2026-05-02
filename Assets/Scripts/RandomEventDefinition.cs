using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RandomEvent", menuName = "Little Planet/Random Event")]
public sealed class RandomEventDefinition : ScriptableObject
{
    [SerializeField] private string eventName = "Random Event";
    [SerializeField, TextArea] private string description;
    [SerializeField] private EventType eventType;

    [Header("Temporary Planet Effects")]
    [SerializeField, Min(0f)] private float effectDurationSeconds = 10f;
    [SerializeField] private List<BuildingEffect> temporaryEffects = new();

    [Header("Area")]
    [SerializeField] private bool hasArea;
    [SerializeField] private bool areaMustStartOnHighland;
    [SerializeField, Range(1, 12)] private int areaRadius = 2;
    [SerializeField, Min(0f)] private float warningDurationSeconds = 3f;
    [SerializeField] private Color warningColor = new(1f, 0.1f, 0.05f, 0.7f);
    [SerializeField, Min(0f)] private float highlightSurfaceOffset = 0.06f;

    [Header("Area Impact")]
    [SerializeField] private bool destroyBuildingsInArea;
    [SerializeField] private bool resetTerraformingInArea;
    [SerializeField, Range(0f, 1f)] private float terraformingAfterImpact;

    [Header("Camera Shake")]
    [SerializeField] private bool shakeCamera;
    [SerializeField, Min(0f)] private float cameraShakeDurationSeconds = 1f;
    [SerializeField, Min(0f)] private float cameraShakeStrength = 0.08f;
    [SerializeField, Min(0.1f)] private float cameraShakeFrequency = 4f;

    public string EventName => string.IsNullOrWhiteSpace(eventName) ? name : eventName;
    public string Description => description;
    public EventType EventType => eventType;
    public float EffectDurationSeconds => effectDurationSeconds;
    public IReadOnlyList<BuildingEffect> TemporaryEffects => temporaryEffects;
    public bool HasArea => hasArea;
    public bool AreaMustStartOnHighland => areaMustStartOnHighland;
    public int AreaRadius => areaRadius;
    public float WarningDurationSeconds => warningDurationSeconds;
    public Color WarningColor => warningColor;
    public float HighlightSurfaceOffset => highlightSurfaceOffset;
    public bool DestroyBuildingsInArea => destroyBuildingsInArea;
    public bool ResetTerraformingInArea => resetTerraformingInArea;
    public float TerraformingAfterImpact => terraformingAfterImpact;
    public bool ShakeCamera => shakeCamera;
    public float CameraShakeDurationSeconds => cameraShakeDurationSeconds;
    public float CameraShakeStrength => cameraShakeStrength;
    public float CameraShakeFrequency => cameraShakeFrequency;
}
