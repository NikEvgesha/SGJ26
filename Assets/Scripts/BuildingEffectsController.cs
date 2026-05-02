using System.Collections.Generic;
using LittlePlanet.PlanetSystem;
using UnityEngine;

public sealed class BuildingEffectsController : MonoBehaviour
{
    [SerializeField] private Planet planet;
    [SerializeField, Min(0.1f)] private float tickIntervalSeconds = 1f;

    private float _temperaturePerTick;
    private float _atmospherePerTick;
    private float _nextTickTime;

    public float TemperaturePerTick => _temperaturePerTick;
    public float AtmospherePerTick => _atmospherePerTick;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (planet == null)
        {
            ResolveReferences();
            if (planet == null)
            {
                return;
            }
        }

        if (Time.time < _nextTickTime)
        {
            return;
        }

        _nextTickTime = Time.time + tickIntervalSeconds;
        ApplyTick();
    }

    public void RegisterEffects(IReadOnlyList<BuildingEffect> effects)
    {
        AddEffects(effects, 1f);
    }

    public void RegisterTemporaryEffects(IReadOnlyList<BuildingEffect> effects, float durationSeconds)
    {
        if (effects == null || durationSeconds <= 0f)
        {
            return;
        }

        RegisterEffects(effects);
        StartCoroutine(UnregisterEffectsAfterDelay(effects, durationSeconds));
    }

    public void UnregisterEffects(IReadOnlyList<BuildingEffect> effects)
    {
        AddEffects(effects, -1f);
    }

    public void ClearEffects()
    {
        _temperaturePerTick = 0f;
        _atmospherePerTick = 0f;
        _nextTickTime = Time.time + tickIntervalSeconds;
    }

    private void ApplyTick()
    {
        if (Mathf.Abs(_temperaturePerTick) > 0.0001f)
        {
            planet.AddTemperature(_temperaturePerTick);
        }

        if (Mathf.Abs(_atmospherePerTick) > 0.0001f)
        {
            planet.AddAtmosphere(_atmospherePerTick);
        }
    }

    private void AddEffects(IReadOnlyList<BuildingEffect> effects, float sign)
    {
        if (effects == null)
        {
            return;
        }

        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            switch (effect.type)
            {
                case TerraformingType.Temperature:
                    _temperaturePerTick += effect.value * sign;
                    break;
                case TerraformingType.Atmosphere:
                    _atmospherePerTick += effect.value * sign;
                    break;
            }
        }
    }

    private System.Collections.IEnumerator UnregisterEffectsAfterDelay(IReadOnlyList<BuildingEffect> effects, float durationSeconds)
    {
        yield return new WaitForSeconds(durationSeconds);
        UnregisterEffects(effects);
    }

    private void ResolveReferences()
    {
        if (planet == null)
        {
            planet = FindFirstObjectByType<Planet>();
        }
    }
}
