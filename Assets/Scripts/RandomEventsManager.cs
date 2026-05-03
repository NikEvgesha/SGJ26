using System;
using System.Collections;
using System.Collections.Generic;
using LittlePlanet.HybridTerraform;
using LittlePlanet.PlanetSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[Serializable]
public sealed class WeightedRandomEvent
{
    public RandomEventDefinition eventDefinition;
    [Min(0f)] public float weight = 1f;
}

public sealed class RandomEventsManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Planet planet;
    [SerializeField] private PlanetCameraController cameraController;
    [SerializeField] private PlanetFlyTerraformSkill flySkill;
    [SerializeField] private BuildPanel buildPanel;
    [SerializeField] private BuildingEffectsController buildingEffectsController;
    [SerializeField] private CanvasGroup eventCaution;
    [SerializeField] private TMP_Text eventNameText;
    [SerializeField] private TMP_Text eventTimerText;
    [SerializeField] private Button goToAreaButton;
    [SerializeField] private GameObject effectsRoot;
    [SerializeField] private GameObject temperaturePlusEffectObject;
    [SerializeField] private GameObject temperatureMinusEffectObject;
    [SerializeField] private GameObject atmospherePlusEffectObject;
    [SerializeField] private GameObject atmosphereMinusEffectObject;
    [SerializeField] private GameObject demolishEffectObject;
    [SerializeField] private Material areaHighlightMaterial;

    [Header("Events")]
    [SerializeField] private List<WeightedRandomEvent> events = new();
    [SerializeField, Min(1f)] private float eventIntervalSeconds = 45f;
    [SerializeField] private bool startEventsAutomatically = true;

    [Header("Area Focus")]
    [SerializeField, Min(0.1f)] private float focusCameraDistance = 8f;
    [SerializeField, Min(0f)] private float focusSurfacePadding = 2.5f;
    [SerializeField, Min(0.01f)] private float focusDurationSeconds = 0.75f;

    [Header("Caution Pulse")]
    [SerializeField, Min(0f)] private float pulseScale = 0.06f;
    [SerializeField, Min(0.1f)] private float pulseSpeed = 3f;

    [Header("Meteor Visual")]
    [SerializeField] private GameObject meteorPrefab;
    [SerializeField, Min(0.1f)] private float meteorFlightDuration = 1.25f;
    [SerializeField, Min(0f)] private float meteorStartHeight = 12f;
    [SerializeField, Min(0f)] private float meteorSideOffset = 6f;
    [SerializeField, Min(0f)] private float meteorImpactSurfaceOffset = 0.35f;
    [SerializeField, Min(0.05f)] private float meteorImpactEffectDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float meteorImpactEffectStartScale = 0.4f;
    [SerializeField, Min(0.01f)] private float meteorImpactEffectEndScale = 3.5f;
    [SerializeField] private Color meteorImpactEffectColor = new(1f, 0.35f, 0.05f, 0.75f);

    [Header("Volcano Visual")]
    [SerializeField] private GameObject volcanoPrefab;
    [SerializeField, Min(0f)] private float volcanoSurfaceOffset = 0.1f;

    private readonly List<Tile> _activeAreaTiles = new();
    private Coroutine _eventRoutine;
    private Coroutine _pulseRoutine;
    private GameObject _areaHighlightObject;
    private MeshFilter _areaHighlightMeshFilter;
    private MeshRenderer _areaHighlightRenderer;
    private Mesh _areaHighlightMesh;
    private Material _areaHighlightMaterialInstance;
    private RectTransform _eventCautionRect;
    private Vector3 _eventCautionBaseScale = Vector3.one;
    private Tile _activeAreaCenter;
    private GameObject _activeVolcanoVisual;

    private void Awake()
    {
        ResolveReferences();
        HideCaution();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindGoToAreaButton();
        if (startEventsAutomatically)
        {
            StartEvents();
        }
    }

    private void OnDisable()
    {
        UnbindGoToAreaButton();
        StopEvents();
        ClearAreaHighlight();
        StopPulse();
        HideCautionWithoutLookup();
        DespawnVolcanoVisual();
    }

    private void OnDestroy()
    {
        DespawnVolcanoVisual();
        ReleaseHighlightResources();
    }

    public void StartEvents()
    {
        if (_eventRoutine != null)
        {
            return;
        }

        _eventRoutine = StartCoroutine(EventLoop());
    }

    public void StopEvents()
    {
        if (_eventRoutine == null)
        {
            return;
        }

        StopCoroutine(_eventRoutine);
        _eventRoutine = null;
    }

    private IEnumerator EventLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(eventIntervalSeconds);
            var selectedEvent = SelectRandomEvent();
            if (selectedEvent != null)
            {
                yield return RunEvent(selectedEvent);
            }
        }
    }

    private IEnumerator RunEvent(RandomEventDefinition eventDefinition)
    {
        ResolveReferences();
        _activeAreaCenter = null;
        _activeAreaTiles.Clear();
        ClearAreaHighlight();

        if (eventDefinition.HasArea && planet != null)
        {
            _activeAreaCenter = SelectAreaCenter(eventDefinition);
            if (_activeAreaCenter != null)
            {
                _activeAreaTiles.AddRange(planet.CollectTileArea(_activeAreaCenter, eventDefinition.AreaRadius));
                ShowAreaHighlight(_activeAreaTiles, eventDefinition.WarningColor, eventDefinition.HighlightSurfaceOffset);
            }
        }

        TrySpawnVolcanoVisual(eventDefinition);
        ShowCaution(eventDefinition);
        var warningDuration = Mathf.Max(0f, eventDefinition.WarningDurationSeconds);
        if (warningDuration > 0f)
        {
            var remainingSeconds = warningDuration;
            UpdateCautionTimer(remainingSeconds);
            while (remainingSeconds > 0f)
            {
                remainingSeconds -= Time.deltaTime;
                UpdateCautionTimer(remainingSeconds);
                yield return null;
            }
        }
        else
        {
            UpdateCautionTimer(0f);
        }

        if (eventDefinition.EventType == EventType.Meteor && _activeAreaCenter != null)
        {
            yield return PlayMeteorVisual();
        }

        yield return PlayEventEndShake(eventDefinition);
        DespawnVolcanoAfterShake(eventDefinition);
        ApplyEventImpact(eventDefinition);
        ClearAreaHighlight();
        HideCaution();
    }

    private IEnumerator PlayMeteorVisual()
    {
        if (planet == null)
        {
            yield break;
        }

        var impactTile = GetAreaImpactTile();
        if (impactTile == null)
        {
            yield break;
        }

        var impactPosition = planet.GetTileWorldSurfaceCenter(impactTile, meteorImpactSurfaceOffset);
        var up = (impactPosition - planet.transform.position).normalized;
        if (up.sqrMagnitude <= 0.000001f)
        {
            up = Vector3.up;
        }

        var tangent = Vector3.Cross(up, Vector3.up);
        if (tangent.sqrMagnitude <= 0.000001f)
        {
            tangent = Vector3.Cross(up, Vector3.right);
        }

        tangent.Normalize();
        var startPosition = impactPosition + up * meteorStartHeight + tangent * meteorSideOffset;
        var meteorInstance = meteorPrefab != null
            ? Instantiate(meteorPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        meteorInstance.name = "Meteor_EventVisual";
        meteorInstance.transform.position = startPosition;
        meteorInstance.transform.rotation = Quaternion.LookRotation((impactPosition - startPosition).normalized, up);

        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, meteorFlightDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            meteorInstance.transform.position = Vector3.Lerp(startPosition, impactPosition, t);
            var flightDirection = impactPosition - meteorInstance.transform.position;
            if (flightDirection.sqrMagnitude > 0.000001f)
            {
                meteorInstance.transform.rotation = Quaternion.LookRotation(flightDirection.normalized, up);
            }
            yield return null;
        }

        Destroy(meteorInstance);
        yield return PlayMeteorImpactEffect(impactPosition, up);
    }

    private IEnumerator PlayMeteorImpactEffect(Vector3 impactPosition, Vector3 up)
    {
        var effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = "Meteor_ImpactEffect";
        effect.transform.position = impactPosition + up * 0.05f;
        effect.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
        effect.transform.localScale = Vector3.one * meteorImpactEffectStartScale;

        var collider = effect.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        var renderer = effect.GetComponent<Renderer>();
        Material material = null;
        if (renderer != null)
        {
            var shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Standard");
            material = new Material(shader) { color = meteorImpactEffectColor };
            renderer.sharedMaterial = material;
        }

        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, meteorImpactEffectDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var scale = Mathf.Lerp(meteorImpactEffectStartScale, meteorImpactEffectEndScale, t);
            effect.transform.localScale = Vector3.one * scale;

            if (material != null)
            {
                var color = meteorImpactEffectColor;
                color.a *= 1f - t;
                material.color = color;
            }

            yield return null;
        }

        Destroy(effect);
        if (material != null)
        {
            Destroy(material);
        }
    }

    private Tile GetAreaImpactTile()
    {
        if (_activeAreaTiles == null || _activeAreaTiles.Count == 0)
        {
            return _activeAreaCenter;
        }

        var averageDirection = Vector3.zero;
        for (var i = 0; i < _activeAreaTiles.Count; i++)
        {
            var tile = _activeAreaTiles[i];
            if (tile == null)
            {
                continue;
            }

            averageDirection += tile.Center.normalized;
        }

        if (averageDirection.sqrMagnitude <= 0.000001f)
        {
            return _activeAreaCenter ?? _activeAreaTiles[0];
        }

        averageDirection.Normalize();
        Tile bestTile = null;
        var bestDot = float.NegativeInfinity;
        for (var i = 0; i < _activeAreaTiles.Count; i++)
        {
            var tile = _activeAreaTiles[i];
            if (tile == null)
            {
                continue;
            }

            var dot = Vector3.Dot(tile.Center.normalized, averageDirection);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestTile = tile;
            }
        }

        return bestTile ?? _activeAreaCenter;
    }

    private RandomEventDefinition SelectRandomEvent()
    {
        var totalWeight = 0f;
        for (var i = 0; i < events.Count; i++)
        {
            var entry = events[i];
            if (entry?.eventDefinition != null && entry.weight > 0f)
            {
                totalWeight += entry.weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        var roll = UnityEngine.Random.Range(0f, totalWeight);
        for (var i = 0; i < events.Count; i++)
        {
            var entry = events[i];
            if (entry?.eventDefinition == null || entry.weight <= 0f)
            {
                continue;
            }

            roll -= entry.weight;
            if (roll <= 0f)
            {
                return entry.eventDefinition;
            }
        }

        return null;
    }

    private Tile SelectAreaCenter(RandomEventDefinition eventDefinition)
    {
        if (planet == null || eventDefinition == null)
        {
            return null;
        }

        if (!eventDefinition.AreaMustStartOnHighland)
        {
            return planet.GetRandomTile();
        }

        var highlandTile = planet.GetRandomHighlandTile();
        return highlandTile ?? planet.GetRandomTile();
    }

    private void ApplyEventImpact(RandomEventDefinition eventDefinition)
    {
        if (buildingEffectsController != null && eventDefinition.TemporaryEffects.Count > 0 && eventDefinition.EffectDurationSeconds > 0f)
        {
            buildingEffectsController.RegisterTemporaryEffects(eventDefinition.TemporaryEffects, eventDefinition.EffectDurationSeconds);
        }

        if (!eventDefinition.HasArea || _activeAreaTiles.Count == 0)
        {
            return;
        }

        if (eventDefinition.DestroyBuildingsInArea && buildPanel != null)
        {
            buildPanel.DestroyBuildingsOnTiles(_activeAreaTiles);
        }

        if (eventDefinition.ResetTerraformingInArea && planet != null)
        {
            planet.SetTilesTerraforming(_activeAreaTiles, eventDefinition.TerraformingAfterImpact);
        }
    }

    private void TrySpawnVolcanoVisual(RandomEventDefinition eventDefinition)
    {
        if (eventDefinition == null
            || eventDefinition.EventType != EventType.Volcano
            || planet == null)
        {
            return;
        }

        DespawnVolcanoVisual();

        if (volcanoPrefab == null)
        {
            return;
        }

        var spawnTile = _activeAreaCenter ?? GetAreaImpactTile() ?? SelectAreaCenter(eventDefinition);
        if (spawnTile == null)
        {
            return;
        }

        if (_activeAreaCenter == null)
        {
            _activeAreaCenter = spawnTile;
        }

        if (eventDefinition.HasArea && _activeAreaTiles.Count == 0)
        {
            _activeAreaTiles.AddRange(planet.CollectTileArea(spawnTile, eventDefinition.AreaRadius));
        }

        var spawnPosition = planet.GetTileWorldSurfaceCenter(spawnTile, volcanoSurfaceOffset);
        var up = (spawnPosition - planet.transform.position).normalized;
        if (up.sqrMagnitude <= 0.000001f)
        {
            up = Vector3.up;
        }

        var rotation = Quaternion.FromToRotation(Vector3.up, up);
        _activeVolcanoVisual = Instantiate(volcanoPrefab, spawnPosition, rotation, planet.transform);
        _activeVolcanoVisual.name = "Volcano_EventVisual";
    }

    private void DespawnVolcanoAfterShake(RandomEventDefinition eventDefinition)
    {
        if (eventDefinition == null || eventDefinition.EventType != EventType.Volcano)
        {
            return;
        }

        DespawnVolcanoVisual();
    }

    private void DespawnVolcanoVisual()
    {
        if (_activeVolcanoVisual != null)
        {
            Destroy(_activeVolcanoVisual);
            _activeVolcanoVisual = null;
        }
    }

    private void GoToArea()
    {
        if (_activeAreaCenter == null || planet == null)
        {
            return;
        }

        flySkill?.CancelSkillAndStartCooldown();
        buildPanel?.CancelBuildMode();
        var safeDistance = Mathf.Max(focusCameraDistance, planet.Radius + focusSurfacePadding, planet.CurrentWaterRadius + focusSurfacePadding);
        cameraController?.FocusOnLocalDirection(_activeAreaCenter.Center, safeDistance, focusDurationSeconds);
    }

    private void ShowCaution(RandomEventDefinition eventDefinition)
    {
        EnsureCautionReferences();
        var showGoToArea = eventDefinition.HasArea && _activeAreaCenter != null;
        if (eventNameText != null)
        {
            eventNameText.text = eventDefinition.EventName;
        }

        UpdateCautionTimer(eventDefinition != null ? eventDefinition.WarningDurationSeconds : 0f);

        UpdateCautionEffects(eventDefinition);

        if (goToAreaButton != null)
        {
            goToAreaButton.gameObject.SetActive(showGoToArea);
        }

        if (eventCaution != null)
        {
            eventCaution.alpha = 1f;
            eventCaution.interactable = true;
            eventCaution.blocksRaycasts = true;
        }

        StartPulse();
    }

    private void HideCaution()
    {
        EnsureCautionReferences();
        HideCautionWithoutLookup();
    }

    private void HideCautionWithoutLookup()
    {
        if (eventCaution != null)
        {
            eventCaution.alpha = 0f;
            eventCaution.interactable = false;
            eventCaution.blocksRaycasts = false;
        }

        if (goToAreaButton != null)
        {
            goToAreaButton.gameObject.SetActive(false);
        }

        HideAllCautionEffects();
        UpdateCautionTimer(0f);

        StopPulse();
    }

    private void UpdateCautionTimer(float remainingSeconds)
    {
        if (eventTimerText == null)
        {
            return;
        }

        var clampedSeconds = Mathf.Max(0f, remainingSeconds);
        var totalSeconds = Mathf.CeilToInt(clampedSeconds);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        eventTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateCautionEffects(RandomEventDefinition eventDefinition)
    {
        var hasTemperaturePlus = false;
        var hasTemperatureMinus = false;
        var hasAtmospherePlus = false;
        var hasAtmosphereMinus = false;
        var showDemolish = eventDefinition != null && eventDefinition.DestroyBuildingsInArea;

        if (eventDefinition != null)
        {
            var temporaryEffects = eventDefinition.TemporaryEffects;
            if (temporaryEffects != null)
            {
                for (var i = 0; i < temporaryEffects.Count; i++)
                {
                    var effect = temporaryEffects[i];
                    switch (effect.type)
                    {
                        case TerraformingType.Temperature:
                            if (effect.value > 0f)
                            {
                                hasTemperaturePlus = true;
                            }
                            else if (effect.value < 0f)
                            {
                                hasTemperatureMinus = true;
                            }
                            break;
                        case TerraformingType.Atmosphere:
                            if (effect.value > 0f)
                            {
                                hasAtmospherePlus = true;
                            }
                            else if (effect.value < 0f)
                            {
                                hasAtmosphereMinus = true;
                            }
                            break;
                    }
                }
            }
        }

        SetEffectObjectActive(
            ref temperaturePlusEffectObject,
            hasTemperaturePlus,
            "Temperature_plus",
            "Temperature_Plus",
            "temperature_plus",
            "TemperaturePlus");

        SetEffectObjectActive(
            ref temperatureMinusEffectObject,
            hasTemperatureMinus,
            "Temperature_minus",
            "Temperature_Minus",
            "temperature_minus",
            "TemperatureMinus");

        SetEffectObjectActive(
            ref atmospherePlusEffectObject,
            hasAtmospherePlus,
            "Atmosphere_plus",
            "Atmosphere_Plus",
            "atmosphere_plus",
            "AtmospherePlus");

        SetEffectObjectActive(
            ref atmosphereMinusEffectObject,
            hasAtmosphereMinus,
            "Atmosphere_minus",
            "Atmosphere_Minus",
            "atmosphere_minus",
            "AtmosphereMinus");

        if (demolishEffectObject != null)
        {
            demolishEffectObject.SetActive(showDemolish);
        }

        if (effectsRoot != null)
        {
            effectsRoot.SetActive(
                hasTemperaturePlus
                || hasTemperatureMinus
                || hasAtmospherePlus
                || hasAtmosphereMinus
                || showDemolish);
        }
    }

    private void HideAllCautionEffects()
    {
        if (temperaturePlusEffectObject != null)
        {
            temperaturePlusEffectObject.SetActive(false);
        }

        if (temperatureMinusEffectObject != null)
        {
            temperatureMinusEffectObject.SetActive(false);
        }

        if (atmospherePlusEffectObject != null)
        {
            atmospherePlusEffectObject.SetActive(false);
        }

        if (atmosphereMinusEffectObject != null)
        {
            atmosphereMinusEffectObject.SetActive(false);
        }

        if (demolishEffectObject != null)
        {
            demolishEffectObject.SetActive(false);
        }

        if (effectsRoot != null)
        {
            effectsRoot.SetActive(false);
        }
    }

    private void SetEffectObjectActive(ref GameObject effectObject, bool isActive, params string[] candidateNames)
    {
        if (effectObject == null && eventCaution != null && candidateNames != null)
        {
            for (var i = 0; i < candidateNames.Length; i++)
            {
                var candidateName = candidateNames[i];
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    continue;
                }

                var transform = FindChildByName(eventCaution.transform, candidateName);
                if (transform == null)
                {
                    continue;
                }

                effectObject = transform.gameObject;
                break;
            }
        }

        if (effectObject != null)
        {
            effectObject.SetActive(isActive);
        }
    }

    private IEnumerator PlayEventEndShake(RandomEventDefinition eventDefinition)
    {
        if (eventDefinition == null
            || !eventDefinition.ShakeCamera
            || cameraController == null
            || (flySkill != null && flySkill.IsFlightModeActive))
        {
            yield break;
        }

        var shakeDuration = Mathf.Max(0f, eventDefinition.CameraShakeDurationSeconds);
        cameraController.ShakeCamera(
            shakeDuration,
            eventDefinition.CameraShakeStrength,
            eventDefinition.CameraShakeFrequency);

        if (shakeDuration > 0f)
        {
            yield return new WaitForSeconds(shakeDuration);
        }
    }

    private void StartPulse()
    {
        StopPulse();
        if (eventCaution == null)
        {
            return;
        }

        _eventCautionRect = eventCaution.transform as RectTransform;
        if (_eventCautionRect != null)
        {
            _eventCautionBaseScale = _eventCautionRect.localScale;
        }

        _pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private void StopPulse()
    {
        if (_pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
        }

        if (_eventCautionRect != null)
        {
            _eventCautionRect.localScale = _eventCautionBaseScale;
        }
    }

    private IEnumerator PulseRoutine()
    {
        while (eventCaution != null && eventCaution.alpha > 0f)
        {
            if (_eventCautionRect != null)
            {
                var scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
                _eventCautionRect.localScale = _eventCautionBaseScale * scale;
            }

            yield return null;
        }
    }

    private void ShowAreaHighlight(IReadOnlyList<Tile> tiles, Color color, float surfaceOffset)
    {
        if (planet == null || tiles == null || tiles.Count == 0)
        {
            return;
        }

        EnsureAreaHighlight();
        if (_areaHighlightMesh == null || _areaHighlightRenderer == null)
        {
            return;
        }

        var vertices = new List<Vector3>(tiles.Count * 7);
        var triangles = new List<int>(tiles.Count * 18);
        for (var i = 0; i < tiles.Count; i++)
        {
            AppendTileFan(tiles[i], vertices, triangles, surfaceOffset);
        }

        _areaHighlightMesh.Clear();
        if (vertices.Count > 65535)
        {
            _areaHighlightMesh.indexFormat = IndexFormat.UInt32;
        }

        _areaHighlightMesh.SetVertices(vertices);
        _areaHighlightMesh.SetTriangles(triangles, 0, true);
        _areaHighlightMesh.RecalculateBounds();
        _areaHighlightMesh.RecalculateNormals();
        _areaHighlightMaterialInstance.color = color;
        _areaHighlightRenderer.enabled = vertices.Count > 0;
    }

    private void AppendTileFan(Tile tile, List<Vector3> vertices, List<int> triangles, float surfaceOffset)
    {
        if (tile == null || tile.Corners == null || tile.Corners.Count < 3)
        {
            return;
        }

        var centerWorld = planet.GetTileWorldSurfacePoint(tile, tile.Center, surfaceOffset, true);
        var centerIndex = vertices.Count;
        vertices.Add(planet.transform.InverseTransformPoint(centerWorld));

        for (var i = 0; i < tile.Corners.Count; i++)
        {
            var cornerWorld = planet.GetTileWorldSurfacePoint(tile, tile.Corners[i], surfaceOffset, false);
            vertices.Add(planet.transform.InverseTransformPoint(cornerWorld));
        }

        for (var i = 0; i < tile.Corners.Count; i++)
        {
            triangles.Add(centerIndex);
            triangles.Add(centerIndex + i + 1);
            triangles.Add(centerIndex + ((i + 1) % tile.Corners.Count) + 1);
        }
    }

    private void ClearAreaHighlight()
    {
        if (_areaHighlightRenderer != null)
        {
            _areaHighlightRenderer.enabled = false;
        }

        if (_areaHighlightMesh != null)
        {
            _areaHighlightMesh.Clear();
        }
    }

    private void EnsureAreaHighlight()
    {
        if (_areaHighlightObject != null)
        {
            return;
        }

        _areaHighlightObject = new GameObject("RandomEventAreaHighlight");
        if (planet != null)
        {
            _areaHighlightObject.transform.SetParent(planet.transform, false);
        }

        _areaHighlightMeshFilter = _areaHighlightObject.AddComponent<MeshFilter>();
        _areaHighlightRenderer = _areaHighlightObject.AddComponent<MeshRenderer>();
        _areaHighlightRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _areaHighlightRenderer.receiveShadows = false;
        _areaHighlightMesh = new Mesh { name = "RandomEventAreaHighlightMesh" };
        _areaHighlightMeshFilter.sharedMesh = _areaHighlightMesh;

        var shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Standard");
        _areaHighlightMaterialInstance = areaHighlightMaterial != null
            ? new Material(areaHighlightMaterial)
            : new Material(shader);
        _areaHighlightMaterialInstance.name = "RandomEventAreaHighlightMaterial";
        if (_areaHighlightMaterialInstance.HasProperty("_Cull"))
        {
            _areaHighlightMaterialInstance.SetInt("_Cull", (int)CullMode.Off);
        }

        _areaHighlightRenderer.sharedMaterial = _areaHighlightMaterialInstance;
        _areaHighlightRenderer.enabled = false;
    }

    private void ReleaseHighlightResources()
    {
        if (_areaHighlightObject != null)
        {
            Destroy(_areaHighlightObject);
        }

        if (_areaHighlightMesh != null)
        {
            Destroy(_areaHighlightMesh);
        }

        if (_areaHighlightMaterialInstance != null)
        {
            Destroy(_areaHighlightMaterialInstance);
        }
    }

    private void ResolveReferences()
    {
        if (planet == null)
        {
            planet = FindFirstObjectByType<Planet>();
        }

        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<PlanetCameraController>();
        }

        if (flySkill == null)
        {
            flySkill = FindFirstObjectByType<PlanetFlyTerraformSkill>();
        }

        if (buildPanel == null)
        {
            buildPanel = FindFirstObjectByType<BuildPanel>(FindObjectsInactive.Include);
        }

        if (buildingEffectsController == null)
        {
            buildingEffectsController = FindFirstObjectByType<BuildingEffectsController>(FindObjectsInactive.Include);
        }

        EnsureCautionReferences();
    }

    private void EnsureCautionReferences()
    {
        if (eventCaution == null)
        {
            var cautionObject = GameObject.Find("EventCaution");
            if (cautionObject != null)
            {
                eventCaution = cautionObject.GetComponent<CanvasGroup>();
                if (eventCaution == null)
                {
                    eventCaution = cautionObject.AddComponent<CanvasGroup>();
                }
            }
        }

        if (eventCaution == null)
        {
            return;
        }

        var preferredEventNameText = FindText(eventCaution.transform, "Name") ?? FindText(eventCaution.transform, "name");
        if (preferredEventNameText != null
            && (eventNameText == null || string.Equals(eventNameText.gameObject.name, "Title", StringComparison.Ordinal)))
        {
            eventNameText = preferredEventNameText;
        }

        if (eventNameText == null)
        {
            eventNameText = eventCaution.GetComponentInChildren<TMP_Text>(true);
        }

        if (eventTimerText == null)
        {
            eventTimerText =
                FindText(eventCaution.transform, "Timer")
                ?? FindText(eventCaution.transform, "TimerLabel")
                ?? FindText(eventCaution.transform, "timer");

            if (eventTimerText == null)
            {
                var timerTransform = FindChildByName(eventCaution.transform, "Timer");
                if (timerTransform != null)
                {
                    eventTimerText = timerTransform.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        if (goToAreaButton == null)
        {
            var buttonTransform = FindChildByName(eventCaution.transform, "GoToArea");
            goToAreaButton = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        }

        if (effectsRoot == null)
        {
            var effectsTransform = FindChildByName(eventCaution.transform, "Effects");
            effectsRoot = effectsTransform != null ? effectsTransform.gameObject : null;
        }

        if (temperaturePlusEffectObject == null)
        {
            var temperaturePlusTransform =
                FindChildByName(eventCaution.transform, "Temperature_plus")
                ?? FindChildByName(eventCaution.transform, "Temperature_Plus")
                ?? FindChildByName(eventCaution.transform, "temperature_plus");
            temperaturePlusEffectObject = temperaturePlusTransform != null ? temperaturePlusTransform.gameObject : null;
        }

        if (temperatureMinusEffectObject == null)
        {
            var temperatureMinusTransform =
                FindChildByName(eventCaution.transform, "Temperature_minus")
                ?? FindChildByName(eventCaution.transform, "Temperature_Minus")
                ?? FindChildByName(eventCaution.transform, "temperature_minus");
            temperatureMinusEffectObject = temperatureMinusTransform != null ? temperatureMinusTransform.gameObject : null;
        }

        if (atmospherePlusEffectObject == null)
        {
            var atmospherePlusTransform =
                FindChildByName(eventCaution.transform, "Atmosphere_plus")
                ?? FindChildByName(eventCaution.transform, "Atmosphere_Plus")
                ?? FindChildByName(eventCaution.transform, "atmosphere_plus");
            atmospherePlusEffectObject = atmospherePlusTransform != null ? atmospherePlusTransform.gameObject : null;
        }

        if (atmosphereMinusEffectObject == null)
        {
            var atmosphereMinusTransform =
                FindChildByName(eventCaution.transform, "Atmosphere_minus")
                ?? FindChildByName(eventCaution.transform, "Atmosphere_Minus")
                ?? FindChildByName(eventCaution.transform, "atmosphere_minus");
            atmosphereMinusEffectObject = atmosphereMinusTransform != null ? atmosphereMinusTransform.gameObject : null;
        }

        if (demolishEffectObject == null)
        {
            var demolishTransform = FindChildByName(eventCaution.transform, "Demolish");
            demolishEffectObject = demolishTransform != null ? demolishTransform.gameObject : null;
        }
    }

    private void BindGoToAreaButton()
    {
        EnsureCautionReferences();
        if (goToAreaButton == null)
        {
            return;
        }

        goToAreaButton.onClick.RemoveListener(GoToArea);
        goToAreaButton.onClick.AddListener(GoToArea);
    }

    private void UnbindGoToAreaButton()
    {
        if (goToAreaButton != null)
        {
            goToAreaButton.onClick.RemoveListener(GoToArea);
        }
    }

    private static TMP_Text FindText(Transform root, string childName)
    {
        var child = FindChildByName(root, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        var children = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }
}
