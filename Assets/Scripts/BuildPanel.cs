using System.Collections.Generic;
using LittlePlanet.PlanetSystem;
using LittlePlanet.RuntimeInput;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class BuildPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Planet planet;
    [SerializeField] private Button buildButton;
    [SerializeField] private BuildingUISlot slotPrefab;
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private BuildingInfoTooltip buildingInfo;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private BuildingEffectsController buildingEffectsController;
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Transform placedBuildingsRoot;

    [Header("Buildings")]
    [SerializeField] private List<Building> buildings = new();

    [Header("Placement")]
    [SerializeField, Min(0f)] private float placementSurfaceOffset = 0.25f;
    [SerializeField, Min(0f)] private float previewSurfaceOffset = 0.035f;
    [SerializeField] private Color canBuildColor = new(0.1f, 0.85f, 1f, 0.75f);
    [SerializeField] private Color blockedColor = new(1f, 0.12f, 0.08f, 0.75f);
    [SerializeField] private Material previewMaterial;
    [SerializeField] private bool startOpen;
    [SerializeField, Min(0.05f)] private float waterDestroyCheckInterval = 0.25f;

    private readonly List<BuildingUISlot> _slots = new();
    private readonly List<PlacedBuildingEntry> _placedBuildings = new();
    private readonly HashSet<int> _occupiedTileIndices = new();
    private BuildingUISlot _selectedSlot;
    private Building _selectedBuilding;
    private GameObject _previewObject;
    private MeshFilter _previewMeshFilter;
    private MeshRenderer _previewMeshRenderer;
    private Mesh _previewMesh;
    private Material _previewMaterialInstance;
    private Tile _hoveredTile;
    private bool _hoveredTileCanBuild;
    private bool _isOpen;
    private bool _planetGenerationBound;
    private int _lastSlotClickFrame = -1;
    private float _nextWaterDestroyCheckTime;

    private sealed class PlacedBuildingEntry
    {
        public Building Building;
        public Tile Tile;
    }

    private void Awake()
    {
        ResolveReferences();
        BuildSlots();
        SetOpen(startOpen);
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindPlanetEvents();
        BindBuildButton();
    }

    private void OnDisable()
    {
        UnbindPlanetEvents();
        UnbindBuildButton();
        DeactivateBuildMode();
    }

    private void OnDestroy()
    {
        ReleasePreviewResources();
    }

    private void Update()
    {
        CheckPlacedBuildingsWaterContact();

        if (!_isOpen || _selectedBuilding == null)
        {
            HidePreview();
            return;
        }

        UpdatePlacementPreview();
    }

    public void Toggle()
    {
        SetOpen(!_isOpen);
    }

    public void SelectBuilding(BuildingUISlot slot)
    {
        if (slot == null || slot.Building == null)
        {
            return;
        }

        _selectedSlot = slot;
        _selectedBuilding = slot.Building;
        _lastSlotClickFrame = Time.frameCount;
        for (var i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetSelected(_slots[i] == _selectedSlot);
        }

        if (planet != null)
        {
            planet.ClickTintEnabled = false;
        }
    }

    public void ShowTooltip(BuildingUISlot slot)
    {
        if (slot == null || buildingInfo == null)
        {
            return;
        }

        buildingInfo.Show(slot.Building);
    }

    public void HideTooltip(BuildingUISlot slot)
    {
        if (buildingInfo != null)
        {
            buildingInfo.Hide();
        }
    }

    private void SetOpen(bool isOpen)
    {
        _isOpen = isOpen;
        EnsureCanvasGroup();
        canvasGroup.alpha = isOpen ? 1f : 0f;
        canvasGroup.interactable = isOpen;
        canvasGroup.blocksRaycasts = isOpen;

        if (!isOpen)
        {
            DeactivateBuildMode();
        }
    }

    private void DeactivateBuildMode()
    {
        _selectedBuilding = null;
        _selectedSlot = null;
        _hoveredTile = null;
        _hoveredTileCanBuild = false;
        HidePreview();
        if (buildingInfo != null)
        {
            buildingInfo.Hide();
        }

        for (var i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetSelected(false);
        }

        if (planet != null)
        {
            planet.ClickTintEnabled = true;
        }
    }

    private void BuildSlots()
    {
        if (slotPrefab == null || slotsRoot == null)
        {
            return;
        }

        for (var i = slotsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(slotsRoot.GetChild(i).gameObject);
        }

        _slots.Clear();
        for (var i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null)
            {
                continue;
            }

            var slot = Instantiate(slotPrefab, slotsRoot);
            slot.Initialize(this, building);
            _slots.Add(slot);
        }
    }

    private void UpdatePlacementPreview()
    {
        if (planet == null)
        {
            HidePreview();
            return;
        }

        if (Time.frameCount == _lastSlotClickFrame || IsPointerOverBuildPanel() || UiInputRuntime.IsPointerOverInteractiveUi())
        {
            _hoveredTile = null;
            HidePreview();
            return;
        }

        if (!planet.TryGetTileAtPointer(out var tile, out _))
        {
            _hoveredTile = null;
            HidePreview();
            return;
        }

        _hoveredTile = tile;
        _hoveredTileCanBuild = CanBuildOnTile(tile);
        ShowPreview(tile, _hoveredTileCanBuild ? canBuildColor : blockedColor);

        if (_hoveredTileCanBuild && InputCompat.WasLeftMousePressedThisFrame())
        {
            TryBuildOnTile(tile);
        }
    }

    private bool CanBuildOnTile(Tile tile)
    {
        return tile != null
            && _selectedBuilding != null
            && planet != null
            && !planet.IsHighlandTile(tile)
            && !planet.IsTileWaterAffected(tile)
            && !_occupiedTileIndices.Contains(tile.Index)
            && planet.Currency.Amount >= _selectedBuilding.Price;
    }

    private void TryBuildOnTile(Tile tile)
    {
        if (!CanBuildOnTile(tile) || !planet.Currency.TrySpend(_selectedBuilding.Price))
        {
            return;
        }

        EnsurePlacedBuildingsRoot();
        var instance = Instantiate(_selectedBuilding, placedBuildingsRoot);
        PositionBuilding(instance.transform, tile);
        _placedBuildings.Add(new PlacedBuildingEntry
        {
            Building = instance,
            Tile = tile
        });
        _occupiedTileIndices.Add(tile.Index);
        buildingEffectsController?.RegisterEffects(_selectedBuilding.Effects);
    }

    private void PositionBuilding(Transform target, Tile tile)
    {
        var position = planet.GetTileWorldSurfaceCenter(tile, placementSurfaceOffset);
        target.position = position;

        var up = (position - planet.transform.position).normalized;
        if (up.sqrMagnitude > 0.000001f)
        {
            target.rotation = Quaternion.FromToRotation(Vector3.up, up);
        }
    }

    private void ShowPreview(Tile tile, Color color)
    {
        EnsurePreview();
        if (_previewMesh == null || _previewMeshRenderer == null)
        {
            return;
        }

        BuildPreviewMesh(tile);
        _previewMeshRenderer.sharedMaterial.color = color;
        _previewObject.SetActive(true);
    }

    private void HidePreview()
    {
        if (_previewObject != null)
        {
            _previewObject.SetActive(false);
        }
    }

    private void BuildPreviewMesh(Tile tile)
    {
        var corners = tile.Corners;
        var vertices = new List<Vector3>(corners.Count + 1);
        var triangles = new List<int>(corners.Count * 3);

        var centerWorld = planet.GetTileWorldSurfacePoint(tile, tile.Center, previewSurfaceOffset, true);
        vertices.Add(_previewObject.transform.InverseTransformPoint(centerWorld));

        for (var i = 0; i < corners.Count; i++)
        {
            var cornerWorld = planet.GetTileWorldSurfacePoint(tile, corners[i], previewSurfaceOffset, false);
            vertices.Add(_previewObject.transform.InverseTransformPoint(cornerWorld));
        }

        for (var i = 0; i < corners.Count; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(((i + 1) % corners.Count) + 1);
        }

        _previewMesh.Clear();
        _previewMesh.SetVertices(vertices);
        _previewMesh.SetTriangles(triangles, 0, true);
        _previewMesh.RecalculateNormals();
        _previewMesh.RecalculateBounds();
    }

    private void EnsurePreview()
    {
        if (_previewObject != null)
        {
            return;
        }

        _previewObject = new GameObject("BuildingPlacementPreview");
        _previewObject.transform.SetParent(planet != null ? planet.transform : null, false);
        _previewMeshFilter = _previewObject.AddComponent<MeshFilter>();
        _previewMeshRenderer = _previewObject.AddComponent<MeshRenderer>();
        _previewMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _previewMeshRenderer.receiveShadows = false;
        _previewMesh = new Mesh { name = "BuildingPlacementPreviewMesh" };
        _previewMeshFilter.sharedMesh = _previewMesh;

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");
        _previewMaterialInstance = previewMaterial != null
            ? new Material(previewMaterial)
            : new Material(shader);
        _previewMaterialInstance.name = "BuildingPlacementPreviewMaterial";
        _previewMaterialInstance.color = canBuildColor;
        if (_previewMaterialInstance.HasProperty("_Cull"))
        {
            _previewMaterialInstance.SetInt("_Cull", (int)CullMode.Off);
        }

        _previewMeshRenderer.sharedMaterial = _previewMaterialInstance;
        _previewObject.SetActive(false);
    }

    private void ReleasePreviewResources()
    {
        if (_previewObject != null)
        {
            Destroy(_previewObject);
        }

        if (_previewMesh != null)
        {
            Destroy(_previewMesh);
        }

        if (_previewMaterialInstance != null)
        {
            Destroy(_previewMaterialInstance);
        }
    }

    private void ResolveReferences()
    {
        if (planet == null)
        {
            planet = FindFirstObjectByType<Planet>();
        }

        if (slotsRoot == null)
        {
            slotsRoot = transform;
        }

        if (buildingInfo == null)
        {
            buildingInfo = FindFirstObjectByType<BuildingInfoTooltip>(FindObjectsInactive.Include);
        }

        if (buildingEffectsController == null)
        {
            buildingEffectsController = GetComponent<BuildingEffectsController>();
        }

        if (buildingEffectsController == null)
        {
            buildingEffectsController = gameObject.AddComponent<BuildingEffectsController>();
        }

        EnsureCanvasGroup();
        if (interactionCamera == null)
        {
            interactionCamera = Camera.main;
        }
    }

    private bool IsPointerOverBuildPanel()
    {
        if (!_isOpen || transform is not RectTransform rectTransform)
        {
            return false;
        }

        var canvas = GetComponentInParent<Canvas>();
        var uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(
            rectTransform,
            InputCompat.GetMousePosition(),
            uiCamera);
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void BindPlanetEvents()
    {
        if (_planetGenerationBound || planet == null)
        {
            return;
        }

        planet.PlanetGenerated += HandlePlanetGenerated;
        _planetGenerationBound = true;
    }

    private void UnbindPlanetEvents()
    {
        if (!_planetGenerationBound || planet == null)
        {
            return;
        }

        planet.PlanetGenerated -= HandlePlanetGenerated;
        _planetGenerationBound = false;
    }

    private void HandlePlanetGenerated()
    {
        ClearPlacedBuildings();
        DeactivateBuildMode();
    }

    private void ClearPlacedBuildings()
    {
        for (var i = _placedBuildings.Count - 1; i >= 0; i--)
        {
            var entry = _placedBuildings[i];
            if (entry == null || entry.Building == null)
            {
                continue;
            }

            Destroy(entry.Building.gameObject);
        }

        _placedBuildings.Clear();
        _occupiedTileIndices.Clear();
        if (buildingEffectsController != null)
        {
            buildingEffectsController.ClearEffects();
        }
    }

    private void CheckPlacedBuildingsWaterContact()
    {
        if (planet == null || _placedBuildings.Count == 0 || Time.time < _nextWaterDestroyCheckTime)
        {
            return;
        }

        _nextWaterDestroyCheckTime = Time.time + waterDestroyCheckInterval;
        for (var i = _placedBuildings.Count - 1; i >= 0; i--)
        {
            var entry = _placedBuildings[i];
            if (entry == null || entry.Building == null || entry.Tile == null)
            {
                RemovePlacedBuildingAt(i, unregisterEffects: false);
                continue;
            }

            if (planet.IsTileWaterAffected(entry.Tile))
            {
                RemovePlacedBuildingAt(i, unregisterEffects: true);
            }
        }
    }

    private void RemovePlacedBuildingAt(int index, bool unregisterEffects)
    {
        if (index < 0 || index >= _placedBuildings.Count)
        {
            return;
        }

        var entry = _placedBuildings[index];
        if (entry != null)
        {
            if (entry.Tile != null)
            {
                _occupiedTileIndices.Remove(entry.Tile.Index);
            }

            if (unregisterEffects && entry.Building != null && buildingEffectsController != null)
            {
                buildingEffectsController.UnregisterEffects(entry.Building.Effects);
            }

            if (entry.Building != null)
            {
                Destroy(entry.Building.gameObject);
            }
        }

        _placedBuildings.RemoveAt(index);
    }

    private void EnsurePlacedBuildingsRoot()
    {
        if (placedBuildingsRoot != null)
        {
            return;
        }

        var root = new GameObject("PlacedBuildings");
        placedBuildingsRoot = root.transform;
        if (planet != null)
        {
            placedBuildingsRoot.SetParent(planet.transform, false);
        }
    }

    private void BindBuildButton()
    {
        if (buildButton == null)
        {
            var buttonObject = GameObject.Find("BuildButton");
            if (buttonObject != null)
            {
                buildButton = buttonObject.GetComponent<Button>();
            }
        }

        if (buildButton == null)
        {
            return;
        }

        buildButton.onClick.RemoveListener(Toggle);
        buildButton.onClick.AddListener(Toggle);
    }

    private void UnbindBuildButton()
    {
        if (buildButton != null)
        {
            buildButton.onClick.RemoveListener(Toggle);
        }
    }
}
