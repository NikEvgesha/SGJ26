using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LittlePlanet.PlanetSystem
{
    public sealed class CurrencyWallet
    {
        public const int DefaultInitialAmount = 1000;

        public CurrencyWallet(int initialAmount = DefaultInitialAmount)
        {
            Amount = Math.Max(0, initialAmount);
        }

        public int Amount { get; private set; }

        public event Action<int> AmountChanged;

        public void SetAmount(int amount)
        {
            var clamped = Math.Max(0, amount);
            if (Amount == clamped)
            {
                return;
            }

            Amount = clamped;
            AmountChanged?.Invoke(Amount);
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SetAmount(Amount + amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Amount < amount)
            {
                return false;
            }

            SetAmount(Amount - amount);
            return true;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class Planet : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField, Min(0.5f)] private float radius = 5f;
        [SerializeField, Range(1, 5)] private int subdivisions = 2;
        [SerializeField] private bool scaleTileDensityWithRadius = true;
        [SerializeField, Min(0.5f)] private float densityReferenceRadius = 5f;
        [SerializeField, Range(1, 8)] private int maxSubdivisions = 8;
        [SerializeField] private float tileElevation = 0.02f;
        [SerializeField] private bool generateOnStart = true;
        [SerializeField] private bool regenerateOnValidate = true;

        [Header("Relief")]
        [SerializeField] private bool enableRelief = true;
        [SerializeField, Min(0f)] private float reliefHeight = 0.35f;
        [SerializeField, Min(0.01f)] private float reliefFrequency = 3f;
        [SerializeField, Range(1, 8)] private int reliefOctaves = 4;
        [SerializeField, Range(1f, 4f)] private float reliefLacunarity = 2f;
        [SerializeField, Range(0.1f, 1f)] private float reliefPersistence = 0.5f;
        [SerializeField] private int reliefSeed = 12345;
        [SerializeField] private bool ridgedRelief;

        [Header("Rendering")]
        [SerializeField] private Material tileMaterial;
        [SerializeField] private Color pentagonColor = new(0.95f, 0.7f, 0.25f, 1f);
        [SerializeField] private Color hexagonColor = new(0.35f, 0.7f, 0.95f, 1f);
        [SerializeField] private Color highTileColor = new(0.55f, 0.55f, 0.55f, 1f);

        [Header("Peaks")]
        [SerializeField, Min(0f)] private float highTileElevationThreshold = 1f;
        [SerializeField] private bool enableHighlandMountains = true;
        [SerializeField, Min(0f)] private float highlandMountainHeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float highlandMountainEdgeFalloff = 0.55f;
        [SerializeField, Min(0.01f)] private float highlandMountainNoiseFrequency = 6f;
        [SerializeField, Range(0.5f, 4f)] private float highlandMountainSharpness = 1.8f;
        [SerializeField] private int highlandMountainSeed = 24680;
        [SerializeField] private bool enableHighlandSnowCaps = true;
        [SerializeField] private Color highlandSnowCapColor = new(0.97f, 0.97f, 0.97f, 1f);
        [SerializeField, Range(0.05f, 0.9f)] private float highlandSnowCapInset = 0.28f;
        [SerializeField, Min(0f)] private float highlandSnowCapSurfaceOffset = 0.004f;

        [Header("Interaction")]
        [SerializeField, Min(0f)] private float highlightSurfaceOffset = 0.01f;
        [SerializeField, Min(0.05f)] private float clickTintDuration = 1.6f;
        [SerializeField, Range(1, 8)] private int clickRadius = 1;
        [SerializeField, Range(0.05f, 1f)] private float clickPower = 0.05f;
        [SerializeField, Range(0.01f, 0.5f)] private float clickPowerStep = 0.05f;
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.1f)] private float clickRaycastDistance = 500f;
        [SerializeField] private LayerMask clickableLayers = ~0;
        [SerializeField, Min(0f)] private float tileInfoHeightOffset = 0.2f;
        [SerializeField] private TileInfoUI tileInfoPrefab;
        [SerializeField] private Button showInfoButton;
        [SerializeField] private bool autoFindShowInfoButton = true;
        [SerializeField] private string showInfoButtonObjectName = "ShowInfo";

        [Header("Water")]
        [SerializeField] private bool manageWater = true;
        [SerializeField] private Transform waterSphere;
        [SerializeField] private string waterObjectName = "Water";
        [SerializeField, Range(0f, 3f)] private float maxWaterRadiusRatio = 1.8f;
        [SerializeField] private bool useReliefBasedInitialWaterRadius = true;
        [SerializeField, Min(0f)] private float initialWaterBelowReliefOffset = 0.05f;
        [SerializeField, Range(0f, 1f)] private float initialWaterFillNormalized = 0f;
        [SerializeField, Range(0.001f, 1f)] private float addWaterStepNormalized = 0.12f;
        [SerializeField] private Color waterContactTileColor = new(0.2f, 0.85f, 1f, 0.9f);
        [SerializeField, Min(0f)] private float waterContactSurfaceOffset = 0.008f;
        [SerializeField] private Button addWaterButton;
        [SerializeField] private bool autoFindAddWaterButton = true;
        [SerializeField] private string addWaterButtonObjectName = "AddWater";
        [SerializeField] private Button regenerateButton;
        [SerializeField] private bool autoFindRegenerateButton = true;
        [SerializeField] private string regenerateButtonObjectName = "Regenerate";
        [SerializeField] private Button addClickPowerButton;
        [SerializeField] private bool autoFindAddClickPowerButton = true;
        [SerializeField] private string addClickPowerButtonObjectName = "AddClickPower";
        [SerializeField] private Button addClickRadiusButton;
        [SerializeField] private bool autoFindAddClickRadiusButton = true;
        [SerializeField] private string addClickRadiusButtonObjectName = "AddClickRadius";

        [Header("HUD")]
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private TMP_Text oceanIndexText;
        [SerializeField] private bool autoFindHudTexts = true;
        [SerializeField] private string currencyPanelObjectName = "Currency";
        [SerializeField] private string currencyAmountObjectName = "Amount";
        [SerializeField] private string oceanIndexObjectName = "OceanIndex";

        [Header("Conditions")]
        [SerializeField] private ConditionsInfoUI conditionsInfoUI;
        [SerializeField] private bool autoFindConditionsInfoUI = true;
        [SerializeField] private string conditionsInfoObjectName = "TerraformingIndex";
        [SerializeField] private float minHumidity = 0f;
        [SerializeField] private float maxHumidity = 100f;
        [SerializeField] private float minAtmosphere = 0f;
        [SerializeField] private float maxAtmosphere = 100f;

        private readonly List<Tile> _tiles = new();

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;

        private Transform _highlightRoot;
        private MeshFilter _highlightMeshFilter;
        private MeshRenderer _highlightMeshRenderer;
        private Transform _waterContactRoot;
        private MeshFilter _waterContactMeshFilter;
        private MeshRenderer _waterContactMeshRenderer;
        private Transform _snowCapsRoot;
        private MeshFilter _snowCapsMeshFilter;
        private MeshRenderer _snowCapsMeshRenderer;

        private Mesh _renderMesh;
        private Mesh _collisionMesh;
        private Mesh _highlightMesh;
        private Mesh _waterContactMesh;
        private Mesh _snowCapsMesh;

        private Material _pentagonMaterial;
        private Material _hexagonMaterial;
        private Material _highTileMaterial;
        private Material _clickTintMaterial;
        private Material _waterContactMaterial;
        private Material _snowCapsMaterial;

        private int[] _triangleToTile;
        private float _generatedSurfaceMaxRadius;
        private float _generatedSurfaceMinRadius;
        private bool[] _isHighlandTile;
        private float[] _tileMinSurfaceRadius;
        private float[] _tileMaxSurfaceRadius;
        private bool[] _waterTouchedTiles;
        private float[] _tileTintCurrent;
        private float[] _tileTintTarget;
        private int[] _highlightTileVertexStarts;
        private int[] _highlightTileVertexCounts;
        private bool[] _highlightTileVisible;
        private Color32[] _highlightVertexColors;
        private bool _highlightMeshGeometryBuilt;
        private float _highlightGeometryOffset = float.NaN;
        private Color _highlightBaseColor = new(0f, 0f, 0f, 1f);
        private int _highlightVisibleTileCount;
        private bool _clickTintDirty;
        private readonly List<ClickTintWave> _activeClickTintWaves = new();
        private readonly List<int> _changedTintTiles = new(256);
        private readonly List<int> _activeTintTiles = new(512);
        private bool[] _isTintTileActive;
        private float _currentWaterRadius;
        private bool _waterInitialized;
        private bool _isAddWaterButtonBound;
        private bool _isRegenerateButtonBound;
        private bool _isAddClickPowerButtonBound;
        private bool _isAddClickRadiusButtonBound;
        private bool _isShowInfoButtonBound;
        private Button _boundAddWaterButton;
        private Button _boundRegenerateButton;
        private Button _boundAddClickPowerButton;
        private Button _boundAddClickRadiusButton;
        private Button _boundShowInfoButton;
        private MeshRenderer _waterRenderer;
        private Collider _waterCollider;
        private bool _isTileInfoMode;
        private TileInfoUI _tileInfoInstance;
        private bool _tileInfoOwnedInstance;
        private readonly CurrencyWallet _currencyWallet = new(1000);
        private float _humidity;
        private float _atmosphere;

        public IReadOnlyList<Tile> Tiles => _tiles;
        public event Action<Tile> TileClicked;
        public float Radius => radius;
        public CurrencyWallet Currency => _currencyWallet;

        private sealed class ClickTintWave
        {
            public ClickTintWave(int[][] rings, float[] stageInfluences)
            {
                Rings = rings;
                StageInfluences = stageInfluences;
            }

            public int[][] Rings { get; }
            public float[] StageInfluences { get; }
            public int Stage { get; set; }
            public bool StageApplied { get; set; }
            public int[] StageTileIndices { get; set; }
            public float[] StageRequiredLevels { get; set; }
        }

        private void Awake()
        {
            EnsureCoreComponents();
            EnsureHighlightComponents();
            EnsureWaterContactComponents();
            EnsureSnowCapComponents();
        }

        private void Start()
        {
            if (generateOnStart)
            {
                Generate();
            }

            InitializeWaterIfNeeded(forceReset: true);
            _currencyWallet.AmountChanged += HandleCurrencyAmountChanged;
            EnsureHudReferences();
            EnsureConditionsInfoUiReference();
            ResetConditionsToMinimum();
            HandleCurrencyAmountChanged(_currencyWallet.Amount);
            UpdateOceanIndexUi();
            UpdateConditionsInfoUi();
            BindAddWaterButtonIfNeeded();
            BindRegenerateButtonIfNeeded();
            BindAddClickPowerButtonIfNeeded();
            BindAddClickRadiusButtonIfNeeded();
            BindShowInfoButtonIfNeeded();
        }

        private void Update()
        {
            TryHandleTileClick();
            UpdateClickTintAnimation();
            UpdateTileInfoHover();
            EnsureHudReferences();
            UpdateOceanIndexUi();
            EnsureConditionsInfoUiReference();
            UpdateConditionsInfoUi();
            BindAddWaterButtonIfNeeded();
            BindRegenerateButtonIfNeeded();
            BindAddClickPowerButtonIfNeeded();
            BindAddClickRadiusButtonIfNeeded();
            BindShowInfoButtonIfNeeded();
        }

        private void OnDestroy()
        {
            _currencyWallet.AmountChanged -= HandleCurrencyAmountChanged;
            UnbindAddWaterButton();
            UnbindRegenerateButton();
            UnbindAddClickPowerButton();
            UnbindAddClickRadiusButton();
            UnbindShowInfoButton();
            ReleaseRuntimeResources();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.5f, radius);
            densityReferenceRadius = Mathf.Max(0.5f, densityReferenceRadius);
            maxSubdivisions = Mathf.Max(1, maxSubdivisions);
            clickRaycastDistance = Mathf.Max(0.1f, clickRaycastDistance);
            highlightSurfaceOffset = Mathf.Max(0f, highlightSurfaceOffset);
            clickTintDuration = Mathf.Max(0.05f, clickTintDuration);
            clickRadius = Mathf.Clamp(clickRadius, 1, 8);
            clickPower = Mathf.Clamp(clickPower, 0.05f, 1f);
            clickPowerStep = Mathf.Clamp(clickPowerStep, 0.01f, 0.5f);
            tileInfoHeightOffset = Mathf.Max(0f, tileInfoHeightOffset);
            if (maxHumidity < minHumidity)
            {
                maxHumidity = minHumidity;
            }

            if (maxAtmosphere < minAtmosphere)
            {
                maxAtmosphere = minAtmosphere;
            }
            highTileElevationThreshold = Mathf.Max(0f, highTileElevationThreshold);
            highlandMountainHeight = Mathf.Max(0f, highlandMountainHeight);
            highlandMountainEdgeFalloff = Mathf.Clamp01(highlandMountainEdgeFalloff);
            highlandMountainNoiseFrequency = Mathf.Max(0.01f, highlandMountainNoiseFrequency);
            highlandMountainSharpness = Mathf.Clamp(highlandMountainSharpness, 0.5f, 4f);
            highlandSnowCapInset = Mathf.Clamp(highlandSnowCapInset, 0.05f, 0.9f);
            highlandSnowCapSurfaceOffset = Mathf.Max(0f, highlandSnowCapSurfaceOffset);
            reliefFrequency = Mathf.Max(0.01f, reliefFrequency);
            reliefOctaves = Mathf.Clamp(reliefOctaves, 1, 8);
            reliefLacunarity = Mathf.Clamp(reliefLacunarity, 1f, 4f);
            reliefPersistence = Mathf.Clamp(reliefPersistence, 0.1f, 1f);
            maxWaterRadiusRatio = Mathf.Clamp(maxWaterRadiusRatio, 0f, 3f);
            initialWaterBelowReliefOffset = Mathf.Max(0f, initialWaterBelowReliefOffset);
            initialWaterFillNormalized = Mathf.Clamp01(initialWaterFillNormalized);
            addWaterStepNormalized = Mathf.Clamp(addWaterStepNormalized, 0.001f, 1f);
            waterContactSurfaceOffset = Mathf.Max(0f, waterContactSurfaceOffset);

            if (!regenerateOnValidate || Application.isPlaying)
            {
                return;
            }

            Generate();
        }

        [ContextMenu("Generate Planet")]
        public void Generate()
        {
            EnsureCoreComponents();
            EnsureHighlightComponents();
            EnsureWaterContactComponents();
            EnsureSnowCapComponents();
            RandomizeGenerationSeeds();
            ClearGeneratedData();

            var effectiveSubdivisions = GetEffectiveSubdivisions();
            var sphereData = IcosphereBuilder.Build(effectiveSubdivisions, radius);
            var descriptors = TileTopologyBuilder.Build(sphereData.Vertices, sphereData.Triangles, radius);

            BuildTileData(descriptors);
            BuildRuntimeMaterials();
            BuildMeshes(descriptors);
            BuildSnowCapMesh();
            ClearHighlightMesh();
            ResetConditionsToMinimum();
            InitializeWaterIfNeeded(forceReset: true);
            UpdateConditionsInfoUi();
        }

        private void RandomizeGenerationSeeds()
        {
            reliefSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            highlandMountainSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        public void ApplyToNeighbors(Tile sourceTile, Action<Tile> action)
        {
            if (sourceTile == null)
            {
                return;
            }

            sourceTile.ApplyToNeighbors(action);
        }

        public void AddWater()
        {
            if (!manageWater)
            {
                return;
            }

            InitializeWaterIfNeeded(forceReset: false);
            var maxWaterRadius = GetMaxWaterRadius();
            if (maxWaterRadius <= 0f)
            {
                return;
            }

            var step = maxWaterRadius * addWaterStepNormalized;
            _currentWaterRadius = Mathf.Min(maxWaterRadius, _currentWaterRadius + step);
            ApplyWaterRadius();
        }

        public void AddClickPower()
        {
            clickPower = Mathf.Clamp(clickPower + clickPowerStep, 0.05f, 1f);
        }

        public void AddClickRadius()
        {
            clickRadius = Mathf.Clamp(clickRadius + 1, 1, 8);
        }

        public void SetHumidity(float value)
        {
            _humidity = Mathf.Clamp(value, minHumidity, maxHumidity);
            UpdateConditionsInfoUi();
        }

        public void AddHumidity(float delta)
        {
            SetHumidity(_humidity + delta);
        }

        public void SetAtmosphere(float value)
        {
            _atmosphere = Mathf.Clamp(value, minAtmosphere, maxAtmosphere);
            UpdateConditionsInfoUi();
        }

        public void AddAtmosphere(float delta)
        {
            SetAtmosphere(_atmosphere + delta);
        }

        public void ToggleTileInfoMode()
        {
            _isTileInfoMode = !_isTileInfoMode;
            if (!_isTileInfoMode)
            {
                HideTileInfo();
            }
            else
            {
                EnsureTileInfoInstance();
            }
        }

        private void BuildTileData(IReadOnlyList<TileTopologyBuilder.TileDescriptor> descriptors)
        {
            var indexedTiles = new Tile[descriptors.Count];
            _isHighlandTile = new bool[descriptors.Count];
            var baselineRadius = radius + tileElevation;

            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                var tile = new Tile(descriptor.Index, descriptor.Shape, descriptor.Center, descriptor.Corners);
                tile.Clicked += HandleTileClicked;

                var centerSurfaceRadius = GetSurfacePoint(descriptor.Center.normalized, tileElevation).magnitude;
                var protrusion = centerSurfaceRadius - baselineRadius;
                _isHighlandTile[descriptor.Index] = protrusion >= highTileElevationThreshold;

                _tiles.Add(tile);
                indexedTiles[descriptor.Index] = tile;
            }

            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                var neighbors = new List<Tile>(descriptor.NeighborIndices.Length);

                for (var j = 0; j < descriptor.NeighborIndices.Length; j++)
                {
                    neighbors.Add(indexedTiles[descriptor.NeighborIndices[j]]);
                }

                indexedTiles[descriptor.Index].SetNeighbors(neighbors);
            }

            RebuildTileSurfaceRadiusRanges();
        }

        private void BuildMeshes(IReadOnlyList<TileTopologyBuilder.TileDescriptor> descriptors)
        {
            var vertices = new List<Vector3>(descriptors.Count * 7);
            var uvs = new List<Vector2>(descriptors.Count * 7);
            var pentagonTriangles = new List<int>(descriptors.Count * 15);
            var hexagonTriangles = new List<int>(descriptors.Count * 18);
            var highlandTriangles = new List<int>(descriptors.Count * 18);
            var colliderTriangles = new List<int>(descriptors.Count * 18);
            var triangleToTile = new List<int>(descriptors.Count * 6);
            var surfaceMaxRadius = 0f;
            var surfaceMinRadius = float.PositiveInfinity;

            for (var i = 0; i < descriptors.Count; i++)
            {
                var descriptor = descriptors[i];
                var corners = descriptor.Corners;
                if (corners.Length < 3)
                {
                    continue;
                }

                var center = GetTileSurfacePoint(descriptor.Index, descriptor.Center.normalized, tileElevation, true);
                var centerIndex = vertices.Count;
                vertices.Add(center);
                uvs.Add(CreateSphericalUv(center));
                surfaceMaxRadius = Mathf.Max(surfaceMaxRadius, center.magnitude);
                surfaceMinRadius = Mathf.Min(surfaceMinRadius, center.magnitude);

                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var corner = corners[cornerIndex];
                    var elevatedCorner = GetTileSurfacePoint(descriptor.Index, corner.normalized, tileElevation, false);
                    vertices.Add(elevatedCorner);
                    uvs.Add(CreateSphericalUv(elevatedCorner));
                    surfaceMaxRadius = Mathf.Max(surfaceMaxRadius, elevatedCorner.magnitude);
                    surfaceMinRadius = Mathf.Min(surfaceMinRadius, elevatedCorner.magnitude);
                }

                var winding = DetermineWinding(vertices[centerIndex], vertices[centerIndex + 1], vertices[centerIndex + 2], descriptor.Center.normalized);
                List<int> targetTriangles;
                var isHighland = _isHighlandTile != null
                                 && descriptor.Index >= 0
                                 && descriptor.Index < _isHighlandTile.Length
                                 && _isHighlandTile[descriptor.Index];

                if (isHighland)
                {
                    targetTriangles = highlandTriangles;
                }
                else
                {
                    targetTriangles = descriptor.Shape == PlanetTileShape.Pentagon ? pentagonTriangles : hexagonTriangles;
                }

                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var current = centerIndex + cornerIndex + 1;
                    var next = centerIndex + ((cornerIndex + 1) % corners.Length) + 1;

                    var a = centerIndex;
                    var b = winding > 0f ? current : next;
                    var c = winding > 0f ? next : current;

                    targetTriangles.Add(a);
                    targetTriangles.Add(b);
                    targetTriangles.Add(c);

                    colliderTriangles.Add(a);
                    colliderTriangles.Add(b);
                    colliderTriangles.Add(c);

                    triangleToTile.Add(descriptor.Index);
                }
            }

            _renderMesh = new Mesh { name = "Planet_RenderMesh" };
            _collisionMesh = new Mesh { name = "Planet_CollisionMesh" };

            if (vertices.Count > 65535)
            {
                _renderMesh.indexFormat = IndexFormat.UInt32;
                _collisionMesh.indexFormat = IndexFormat.UInt32;
            }

            _renderMesh.SetVertices(vertices);
            _renderMesh.SetUVs(0, uvs);
            _renderMesh.subMeshCount = 3;
            _renderMesh.SetTriangles(pentagonTriangles, 0, true);
            _renderMesh.SetTriangles(hexagonTriangles, 1, true);
            _renderMesh.SetTriangles(highlandTriangles, 2, true);
            _renderMesh.RecalculateNormals();
            _renderMesh.RecalculateBounds();

            _collisionMesh.SetVertices(vertices);
            _collisionMesh.SetUVs(0, uvs);
            _collisionMesh.SetTriangles(colliderTriangles, 0, true);
            _collisionMesh.RecalculateNormals();
            _collisionMesh.RecalculateBounds();

            _meshFilter.sharedMesh = _renderMesh;
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _collisionMesh;

            _triangleToTile = triangleToTile.ToArray();
            _generatedSurfaceMaxRadius = surfaceMaxRadius;
            _generatedSurfaceMinRadius = float.IsPositiveInfinity(surfaceMinRadius) ? 0f : surfaceMinRadius;
        }

        private void RebuildTileSurfaceRadiusRanges()
        {
            if (_tiles.Count == 0)
            {
                _tileMinSurfaceRadius = null;
                _tileMaxSurfaceRadius = null;
                return;
            }

            _tileMinSurfaceRadius = new float[_tiles.Count];
            _tileMaxSurfaceRadius = new float[_tiles.Count];

            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var minRadius = float.PositiveInfinity;
                var maxRadius = 0f;

                var centerRadius = GetTileSurfacePoint(tile.Index, tile.Center.normalized, tileElevation, true).magnitude;
                minRadius = Mathf.Min(minRadius, centerRadius);
                maxRadius = Mathf.Max(maxRadius, centerRadius);

                var corners = tile.Corners;
                for (var cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
                {
                    var cornerRadius = GetTileSurfacePoint(tile.Index, corners[cornerIndex].normalized, tileElevation, false).magnitude;
                    minRadius = Mathf.Min(minRadius, cornerRadius);
                    maxRadius = Mathf.Max(maxRadius, cornerRadius);
                }

                _tileMinSurfaceRadius[i] = float.IsPositiveInfinity(minRadius) ? 0f : minRadius;
                _tileMaxSurfaceRadius[i] = maxRadius;
            }
        }

        private void BuildSnowCapMesh()
        {
            if (_snowCapsMesh == null || _snowCapsMeshFilter == null || _snowCapsMeshRenderer == null)
            {
                return;
            }

            _snowCapsMesh.Clear();
            if (!enableHighlandSnowCaps || _tiles.Count == 0 || _isHighlandTile == null)
            {
                _snowCapsMeshFilter.sharedMesh = _snowCapsMesh;
                _snowCapsMeshRenderer.enabled = false;
                return;
            }

            var vertices = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var triangles = new List<int>(384);
            var inset = Mathf.Clamp(highlandSnowCapInset, 0.05f, 0.9f);

            for (var i = 0; i < _tiles.Count; i++)
            {
                if (i >= _isHighlandTile.Length || !_isHighlandTile[i])
                {
                    continue;
                }

                var tile = _tiles[i];
                var corners = tile.Corners;
                if (corners == null || corners.Count < 3)
                {
                    continue;
                }

                var centerDir = tile.Center.normalized;
                var center = GetTileSurfacePoint(tile.Index, centerDir, tileElevation + highlandSnowCapSurfaceOffset, true);
                var centerIndex = vertices.Count;
                vertices.Add(center);
                uvs.Add(CreateSphericalUv(center));

                for (var cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
                {
                    var cornerDir = corners[cornerIndex].normalized;
                    var innerDir = Vector3.Slerp(centerDir, cornerDir, inset).normalized;
                    var inner = GetTileSurfacePoint(tile.Index, innerDir, tileElevation + highlandSnowCapSurfaceOffset, false);
                    vertices.Add(inner);
                    uvs.Add(CreateSphericalUv(inner));
                }

                var winding = DetermineWinding(vertices[centerIndex], vertices[centerIndex + 1], vertices[centerIndex + 2], centerDir);
                for (var cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
                {
                    var current = centerIndex + cornerIndex + 1;
                    var next = centerIndex + ((cornerIndex + 1) % corners.Count) + 1;
                    triangles.Add(centerIndex);
                    triangles.Add(winding > 0f ? current : next);
                    triangles.Add(winding > 0f ? next : current);
                }
            }

            if (vertices.Count == 0)
            {
                _snowCapsMeshFilter.sharedMesh = _snowCapsMesh;
                _snowCapsMeshRenderer.enabled = false;
                return;
            }

            if (vertices.Count > 65535)
            {
                _snowCapsMesh.indexFormat = IndexFormat.UInt32;
            }

            _snowCapsMesh.SetVertices(vertices);
            _snowCapsMesh.SetUVs(0, uvs);
            _snowCapsMesh.SetTriangles(triangles, 0, true);
            _snowCapsMesh.RecalculateNormals();
            _snowCapsMesh.RecalculateBounds();
            _snowCapsMeshFilter.sharedMesh = _snowCapsMesh;
            _snowCapsMeshRenderer.enabled = true;
        }

        private void HandleTileClicked(Tile tile)
        {
            if (tile == null)
            {
                return;
            }

            TileClicked?.Invoke(tile);
            AddClickTintInfluence(tile);
        }

        private void AddClickTintInfluence(Tile selectedTile)
        {
            if (selectedTile == null || _tiles.Count == 0)
            {
                return;
            }

            EnsureClickTintArrays();
            if (_tileTintCurrent == null || _tileTintTarget == null)
            {
                return;
            }

            var effectiveRadius = Mathf.Clamp(clickRadius, 1, 8);
            var ringCount = effectiveRadius;
            var rings = new int[ringCount][];
            var stageInfluences = new float[ringCount];

            var visited = new HashSet<int>();
            var currentFrontier = new List<int>(1) { selectedTile.Index };
            visited.Add(selectedTile.Index);

            for (var depth = 0; depth < ringCount; depth++)
            {
                rings[depth] = currentFrontier.ToArray();
                stageInfluences[depth] = clickPower * GetRingWeight(depth, ringCount);

                if (depth == ringCount - 1)
                {
                    break;
                }

                var nextFrontier = new List<int>(currentFrontier.Count * 2 + 4);
                for (var i = 0; i < currentFrontier.Count; i++)
                {
                    var index = currentFrontier[i];
                    if (index < 0 || index >= _tiles.Count)
                    {
                        continue;
                    }

                    var neighbors = _tiles[index].Neighbors;
                    for (var j = 0; j < neighbors.Count; j++)
                    {
                        var neighbor = neighbors[j];
                        if (neighbor == null || !visited.Add(neighbor.Index))
                        {
                            continue;
                        }

                        nextFrontier.Add(neighbor.Index);
                    }
                }

                currentFrontier = nextFrontier;
            }

            _activeClickTintWaves.Add(new ClickTintWave(rings, stageInfluences));
        }

        private void UpdateClickTintAnimation()
        {
            if (_tileTintCurrent == null || _tileTintTarget == null)
            {
                return;
            }

            _changedTintTiles.Clear();
            ProcessAllClickTintWaves();

            if (_changedTintTiles.Count > 0 || _clickTintDirty || !_highlightMeshGeometryBuilt)
            {
                BuildHighlightMesh();
                _clickTintDirty = false;
            }
        }

        private void ProcessAllClickTintWaves()
        {
            for (var waveIndex = _activeClickTintWaves.Count - 1; waveIndex >= 0; waveIndex--)
            {
                var wave = _activeClickTintWaves[waveIndex];
                ProcessClickTintWave(wave);

                if (wave.Stage >= wave.Rings.Length)
                {
                    _activeClickTintWaves.RemoveAt(waveIndex);
                }
            }
        }

        private void ProcessClickTintWave(ClickTintWave wave)
        {
            if (wave == null)
            {
                return;
            }

            while (wave.Stage < wave.Rings.Length)
            {
                if (!wave.StageApplied)
                {
                    var stageInfluence = wave.Stage >= 0 && wave.Stage < wave.StageInfluences.Length
                        ? wave.StageInfluences[wave.Stage]
                        : 0f;
                    var stageTiles = wave.Rings[wave.Stage];
                    wave.StageTileIndices = stageTiles;
                    wave.StageRequiredLevels = new float[stageTiles.Length];

                    for (var i = 0; i < stageTiles.Length; i++)
                    {
                        var tileIndex = stageTiles[i];
                        ApplyTintInfluenceByIndex(tileIndex, stageInfluence);
                        wave.StageRequiredLevels[i] = (tileIndex >= 0 && tileIndex < _tileTintTarget.Length)
                            ? _tileTintTarget[tileIndex]
                            : 0f;
                    }

                    wave.StageApplied = true;
                }

                if (!IsStageReached(wave))
                {
                    return;
                }

                wave.Stage++;
                wave.StageApplied = false;
                wave.StageTileIndices = null;
                wave.StageRequiredLevels = null;
            }
        }

        private bool IsStageReached(ClickTintWave wave)
        {
            if (wave?.StageTileIndices == null || wave.StageRequiredLevels == null)
            {
                return true;
            }

            const float epsilon = 0.001f;
            for (var i = 0; i < wave.StageTileIndices.Length; i++)
            {
                var index = wave.StageTileIndices[i];
                if (index < 0 || index >= _tileTintCurrent.Length)
                {
                    continue;
                }

                if (_tileTintCurrent[index] + epsilon < wave.StageRequiredLevels[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static float GetRingWeight(int depth, int ringCount)
        {
            if (ringCount <= 1)
            {
                return 1f;
            }

            var numerator = ringCount - depth;
            return Mathf.Clamp01((float)numerator / ringCount);
        }

        private void BuildHighlightMesh()
        {
            if (_highlightMesh == null || _highlightMeshFilter == null || _highlightMeshRenderer == null)
            {
                ClearHighlightMesh();
                return;
            }

            if (_tileTintCurrent == null || _tileTintCurrent.Length == 0)
            {
                ClearHighlightMesh();
                return;
            }

            if (!EnsureHighlightMeshGeometry())
            {
                ClearHighlightMesh();
                return;
            }

            var baseColor = GetWaterContactColor();
            var colorChanged = !AreColorsClose(baseColor, _highlightBaseColor);
            if (colorChanged)
            {
                _highlightBaseColor = baseColor;
                _clickTintDirty = true;
            }

            if (_clickTintDirty || colorChanged)
            {
                _highlightVisibleTileCount = 0;
                if (_highlightTileVisible != null)
                {
                    Array.Clear(_highlightTileVisible, 0, _highlightTileVisible.Length);
                }

                for (var i = 0; i < _tiles.Count; i++)
                {
                    UpdateHighlightTileColor(i, baseColor);
                }
            }
            else
            {
                for (var i = 0; i < _changedTintTiles.Count; i++)
                {
                    UpdateHighlightTileColor(_changedTintTiles[i], baseColor);
                }
            }

            if (_highlightVertexColors == null || _highlightVertexColors.Length == 0)
            {
                _highlightMeshRenderer.enabled = false;
                return;
            }

            _highlightMesh.SetColors(_highlightVertexColors);
            _highlightMeshRenderer.enabled = _highlightVisibleTileCount > 0;
        }

        private bool EnsureHighlightMeshGeometry()
        {
            if (_highlightMesh == null)
            {
                return false;
            }

            var needsRebuild = !_highlightMeshGeometryBuilt
                               || _highlightTileVertexStarts == null
                               || _highlightTileVertexCounts == null
                               || _highlightTileVisible == null
                               || _highlightVertexColors == null
                               || _highlightTileVertexStarts.Length != _tiles.Count
                               || _highlightTileVertexCounts.Length != _tiles.Count
                               || _highlightTileVisible.Length != _tiles.Count
                               || !Mathf.Approximately(_highlightGeometryOffset, highlightSurfaceOffset);

            if (!needsRebuild)
            {
                return true;
            }

            var vertices = new List<Vector3>(_tiles.Count * 7);
            var uvs = new List<Vector2>(_tiles.Count * 7);
            var colors = new List<Color32>(_tiles.Count * 7);
            var triangles = new List<int>(_tiles.Count * 18);
            var tileStarts = new int[_tiles.Count];
            var tileCounts = new int[_tiles.Count];

            var baseColor = GetWaterContactColor();
            for (var i = 0; i < _tiles.Count; i++)
            {
                tileStarts[i] = vertices.Count;
                AppendTileFanWithColor(_tiles[i], triangles, vertices, uvs, colors, highlightSurfaceOffset, 0f, baseColor);
                tileCounts[i] = vertices.Count - tileStarts[i];
            }

            _highlightMesh.Clear();
            if (vertices.Count > 65535)
            {
                _highlightMesh.indexFormat = IndexFormat.UInt32;
            }

            if (vertices.Count == 0)
            {
                _highlightTileVertexStarts = tileStarts;
                _highlightTileVertexCounts = tileCounts;
                _highlightTileVisible = new bool[_tiles.Count];
                _highlightVertexColors = Array.Empty<Color32>();
                _highlightMeshGeometryBuilt = true;
                _highlightGeometryOffset = highlightSurfaceOffset;
                _highlightVisibleTileCount = 0;
                _highlightMeshFilter.sharedMesh = _highlightMesh;
                _highlightMeshRenderer.enabled = false;
                return false;
            }

            _highlightMesh.SetVertices(vertices);
            _highlightMesh.SetUVs(0, uvs);
            _highlightMesh.SetColors(colors);
            _highlightMesh.subMeshCount = 1;
            _highlightMesh.SetTriangles(triangles, 0, true);
            _highlightMesh.RecalculateNormals();
            _highlightMesh.RecalculateBounds();
            _highlightMesh.MarkDynamic();

            _highlightMeshFilter.sharedMesh = _highlightMesh;
            _highlightMeshRenderer.enabled = false;

            _highlightTileVertexStarts = tileStarts;
            _highlightTileVertexCounts = tileCounts;
            _highlightTileVisible = new bool[_tiles.Count];
            _highlightVertexColors = colors.ToArray();
            _highlightMeshGeometryBuilt = true;
            _highlightGeometryOffset = highlightSurfaceOffset;
            _highlightBaseColor = baseColor;
            _highlightVisibleTileCount = 0;
            return true;
        }

        private void UpdateHighlightTileColor(int tileIndex, Color baseColor)
        {
            if (_tileTintCurrent == null
                || _highlightTileVertexStarts == null
                || _highlightTileVertexCounts == null
                || _highlightTileVisible == null
                || _highlightVertexColors == null)
            {
                return;
            }

            if (tileIndex < 0 || tileIndex >= _tileTintCurrent.Length)
            {
                return;
            }

            var start = _highlightTileVertexStarts[tileIndex];
            var count = _highlightTileVertexCounts[tileIndex];
            if (start < 0 || count <= 0 || start + count > _highlightVertexColors.Length)
            {
                return;
            }

            var intensity = Mathf.Clamp01(_tileTintCurrent[tileIndex]);
            var visible = intensity > 0.001f;
            var wasVisible = _highlightTileVisible[tileIndex];
            if (visible != wasVisible)
            {
                _highlightVisibleTileCount += visible ? 1 : -1;
                _highlightTileVisible[tileIndex] = visible;
            }

            var tint = ToColor32(baseColor, visible ? intensity : 0f);
            for (var i = 0; i < count; i++)
            {
                _highlightVertexColors[start + i] = tint;
            }
        }

        private Color GetWaterContactColor()
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear
                ? waterContactTileColor.linear
                : waterContactTileColor;
        }

        private static bool AreColorsClose(Color a, Color b)
        {
            const float epsilon = 0.0001f;
            return Mathf.Abs(a.r - b.r) <= epsilon
                   && Mathf.Abs(a.g - b.g) <= epsilon
                   && Mathf.Abs(a.b - b.b) <= epsilon
                   && Mathf.Abs(a.a - b.a) <= epsilon;
        }

        private static Color32 ToColor32(Color baseColor, float alpha01)
        {
            var r = Mathf.Clamp01(baseColor.r);
            var g = Mathf.Clamp01(baseColor.g);
            var b = Mathf.Clamp01(baseColor.b);
            var a = Mathf.Clamp01(alpha01);
            return new Color32(
                (byte)Mathf.RoundToInt(r * 255f),
                (byte)Mathf.RoundToInt(g * 255f),
                (byte)Mathf.RoundToInt(b * 255f),
                (byte)Mathf.RoundToInt(a * 255f));
        }

        private void AppendTileFan(Tile tile, List<int> targetTriangles, List<Vector3> vertices, List<Vector2> uvs, float surfaceOffset)
        {
            if (tile == null)
            {
                return;
            }

            var corners = tile.Corners;
            if (corners == null || corners.Count < 3)
            {
                return;
            }

            var center = GetTileSurfacePoint(tile.Index, tile.Center.normalized, tileElevation + surfaceOffset, true);
            var centerIndex = vertices.Count;
            vertices.Add(center);
            uvs.Add(CreateSphericalUv(center));

            for (var i = 0; i < corners.Count; i++)
            {
                var corner = GetTileSurfacePoint(tile.Index, corners[i].normalized, tileElevation + surfaceOffset, false);
                vertices.Add(corner);
                uvs.Add(CreateSphericalUv(corner));
            }

            var winding = DetermineWinding(vertices[centerIndex], vertices[centerIndex + 1], vertices[centerIndex + 2], tile.Center.normalized);
            for (var i = 0; i < corners.Count; i++)
            {
                var current = centerIndex + i + 1;
                var next = centerIndex + ((i + 1) % corners.Count) + 1;

                targetTriangles.Add(centerIndex);
                targetTriangles.Add(winding > 0f ? current : next);
                targetTriangles.Add(winding > 0f ? next : current);
            }
        }

        private void AppendTileFanWithColor(
            Tile tile,
            List<int> targetTriangles,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color32> colors,
            float surfaceOffset,
            float intensity,
            Color baseColor)
        {
            if (tile == null)
            {
                return;
            }

            var corners = tile.Corners;
            if (corners == null || corners.Count < 3)
            {
                return;
            }

            var tint = ToColor32(baseColor, Mathf.Clamp01(intensity));

            var center = GetTileSurfacePoint(tile.Index, tile.Center.normalized, tileElevation + surfaceOffset, true);
            var centerIndex = vertices.Count;
            vertices.Add(center);
            uvs.Add(CreateSphericalUv(center));
            colors.Add(tint);

            for (var i = 0; i < corners.Count; i++)
            {
                var corner = GetTileSurfacePoint(tile.Index, corners[i].normalized, tileElevation + surfaceOffset, false);
                vertices.Add(corner);
                uvs.Add(CreateSphericalUv(corner));
                colors.Add(tint);
            }

            var winding = DetermineWinding(vertices[centerIndex], vertices[centerIndex + 1], vertices[centerIndex + 2], tile.Center.normalized);
            for (var i = 0; i < corners.Count; i++)
            {
                var current = centerIndex + i + 1;
                var next = centerIndex + ((i + 1) % corners.Count) + 1;

                targetTriangles.Add(centerIndex);
                targetTriangles.Add(winding > 0f ? current : next);
                targetTriangles.Add(winding > 0f ? next : current);
            }
        }

        private void EnsureClickTintArrays()
        {
            if (_tiles.Count == 0)
            {
                _tileTintCurrent = null;
                _tileTintTarget = null;
                _isTintTileActive = null;
                _activeTintTiles.Clear();
                return;
            }

            if (_tileTintCurrent != null
                && _tileTintTarget != null
                && _isTintTileActive != null
                && _tileTintCurrent.Length == _tiles.Count
                && _tileTintTarget.Length == _tiles.Count
                && _isTintTileActive.Length == _tiles.Count)
            {
                return;
            }

            var previousCurrent = _tileTintCurrent;
            var previousTarget = _tileTintTarget;

            _tileTintCurrent = new float[_tiles.Count];
            _tileTintTarget = new float[_tiles.Count];
            _isTintTileActive = new bool[_tiles.Count];
            _activeTintTiles.Clear();

            if (previousCurrent == null || previousTarget == null)
            {
                return;
            }

            var copyLength = Mathf.Min(_tileTintCurrent.Length, previousCurrent.Length);
            Array.Copy(previousCurrent, _tileTintCurrent, copyLength);
            Array.Copy(previousTarget, _tileTintTarget, copyLength);
            RebuildActiveTintTiles();
        }

        private void ApplyTintInfluenceByIndex(int tileIndex, float influence)
        {
            if (_tileTintCurrent == null || _tileTintTarget == null)
            {
                return;
            }

            if (tileIndex < 0 || tileIndex >= _tileTintTarget.Length)
            {
                return;
            }

            if (_isHighlandTile != null && tileIndex < _isHighlandTile.Length && _isHighlandTile[tileIndex])
            {
                return;
            }

            var previous = _tileTintTarget[tileIndex];
            var next = Mathf.Clamp01(previous + influence);
            if (Mathf.Abs(next - previous) <= 0.0001f)
            {
                return;
            }

            _tileTintTarget[tileIndex] = next;
            _tileTintCurrent[tileIndex] = next;
            _changedTintTiles.Add(tileIndex);
            _clickTintDirty = true;
        }

        private void RegisterTintTileIfActive(int tileIndex)
        {
            if (_tileTintCurrent == null || _tileTintTarget == null || _isTintTileActive == null)
            {
                return;
            }

            if (tileIndex < 0 || tileIndex >= _tileTintCurrent.Length || tileIndex >= _isTintTileActive.Length)
            {
                return;
            }

            const float epsilon = 0.0001f;
            if (_tileTintCurrent[tileIndex] + epsilon >= _tileTintTarget[tileIndex] || _isTintTileActive[tileIndex])
            {
                return;
            }

            _isTintTileActive[tileIndex] = true;
            _activeTintTiles.Add(tileIndex);
        }

        private void RemoveActiveTintTileAt(int activeListIndex)
        {
            if (activeListIndex < 0 || activeListIndex >= _activeTintTiles.Count)
            {
                return;
            }

            var tileIndex = _activeTintTiles[activeListIndex];
            if (_isTintTileActive != null && tileIndex >= 0 && tileIndex < _isTintTileActive.Length)
            {
                _isTintTileActive[tileIndex] = false;
            }

            var lastIndex = _activeTintTiles.Count - 1;
            if (activeListIndex != lastIndex)
            {
                _activeTintTiles[activeListIndex] = _activeTintTiles[lastIndex];
            }

            _activeTintTiles.RemoveAt(lastIndex);
        }

        private void RebuildActiveTintTiles()
        {
            _activeTintTiles.Clear();
            if (_tileTintCurrent == null || _tileTintTarget == null || _isTintTileActive == null)
            {
                return;
            }

            Array.Clear(_isTintTileActive, 0, _isTintTileActive.Length);

            const float epsilon = 0.0001f;
            var length = Mathf.Min(_tileTintCurrent.Length, _tileTintTarget.Length);
            for (var i = 0; i < length; i++)
            {
                if (_tileTintCurrent[i] + epsilon >= _tileTintTarget[i])
                {
                    continue;
                }

                _isTintTileActive[i] = true;
                _activeTintTiles.Add(i);
            }
        }

        private void ClearHighlightMesh()
        {
            if (_highlightMesh == null)
            {
                return;
            }

            _highlightMesh.Clear();
            _highlightMeshGeometryBuilt = false;
            _highlightTileVertexStarts = null;
            _highlightTileVertexCounts = null;
            _highlightTileVisible = null;
            _highlightVertexColors = null;
            _highlightVisibleTileCount = 0;
            _changedTintTiles.Clear();
            if (_highlightMeshFilter != null)
            {
                _highlightMeshFilter.sharedMesh = _highlightMesh;
            }

            if (_highlightMeshRenderer != null)
            {
                _highlightMeshRenderer.enabled = false;
            }
        }

        private void UpdateWaterContactMesh()
        {
            if (_waterContactMesh == null || _waterContactMeshFilter == null || _waterContactMeshRenderer == null)
            {
                return;
            }

            if (!manageWater || _currentWaterRadius <= 0f || _tiles.Count == 0 || _tileMinSurfaceRadius == null || _tileMaxSurfaceRadius == null)
            {
                ClearWaterContactMesh();
                return;
            }

            if (_waterTouchedTiles == null || _waterTouchedTiles.Length != _tiles.Count)
            {
                _waterTouchedTiles = new bool[_tiles.Count];
            }

            var vertices = new List<Vector3>(128);
            var uvs = new List<Vector2>(128);
            var triangles = new List<int>(256);
            const float contactTolerance = 0.001f;
            var tintChangedByWater = false;
            EnsureClickTintArrays();

            for (var i = 0; i < _tiles.Count; i++)
            {
                if (i >= _tileMinSurfaceRadius.Length || i >= _tileMaxSurfaceRadius.Length)
                {
                    break;
                }

                var minRadius = _tileMinSurfaceRadius[i];
                var maxRadius = _tileMaxSurfaceRadius[i];
                var inContact = !(_currentWaterRadius + contactTolerance < minRadius || _currentWaterRadius - contactTolerance > maxRadius);
                var underWater = _currentWaterRadius + contactTolerance >= maxRadius;
                if (inContact)
                {
                    _waterTouchedTiles[i] = true;
                }

                var shouldAffectTile = _waterTouchedTiles[i] || inContact || underWater;
                if (!shouldAffectTile)
                {
                    continue;
                }

                tintChangedByWater |= SetTileTintToFull(i);
                AppendTileFan(_tiles[i], triangles, vertices, uvs, waterContactSurfaceOffset);
            }

            if (tintChangedByWater)
            {
                _clickTintDirty = true;
            }

            _waterContactMesh.Clear();
            if (vertices.Count == 0)
            {
                _waterContactMeshFilter.sharedMesh = _waterContactMesh;
                _waterContactMeshRenderer.enabled = false;
                return;
            }

            if (vertices.Count > 65535)
            {
                _waterContactMesh.indexFormat = IndexFormat.UInt32;
            }

            _waterContactMesh.SetVertices(vertices);
            _waterContactMesh.SetUVs(0, uvs);
            _waterContactMesh.SetTriangles(triangles, 0, true);
            _waterContactMesh.RecalculateNormals();
            _waterContactMesh.RecalculateBounds();

            _waterContactMeshFilter.sharedMesh = _waterContactMesh;
            _waterContactMeshRenderer.enabled = true;
        }

        private bool SetTileTintToFull(int tileIndex)
        {
            if (_tileTintCurrent == null || _tileTintTarget == null)
            {
                return false;
            }

            if (tileIndex < 0 || tileIndex >= _tileTintCurrent.Length || tileIndex >= _tileTintTarget.Length)
            {
                return false;
            }

            const float full = 1f;
            var changed = Mathf.Abs(_tileTintCurrent[tileIndex] - full) > 0.0001f
                          || Mathf.Abs(_tileTintTarget[tileIndex] - full) > 0.0001f;
            if (!changed)
            {
                return false;
            }

            _tileTintCurrent[tileIndex] = full;
            _tileTintTarget[tileIndex] = full;
            _changedTintTiles.Add(tileIndex);
            return true;
        }

        private void ClearWaterContactMesh()
        {
            if (_waterContactMesh == null)
            {
                return;
            }

            _waterContactMesh.Clear();
            if (_waterContactMeshFilter != null)
            {
                _waterContactMeshFilter.sharedMesh = _waterContactMesh;
            }

            if (_waterContactMeshRenderer != null)
            {
                _waterContactMeshRenderer.enabled = false;
            }
        }

        private void TryHandleTileClick()
        {
            if (_triangleToTile == null || _triangleToTile.Length == 0)
            {
                return;
            }

            if (!TryGetPointerDownPosition(out var pointerPosition))
            {
                return;
            }

            if (!TryRaycastTileAtPointer(pointerPosition, out var tileIndex, out _))
            {
                return;
            }

            _tiles[tileIndex].TriggerClick();
        }

        private void UpdateTileInfoHover()
        {
            if (!_isTileInfoMode)
            {
                return;
            }

            if (_tiles.Count == 0)
            {
                HideTileInfo();
                return;
            }

            if (!TryGetPointerPosition(out var pointerPosition))
            {
                HideTileInfo();
                return;
            }

            if (!TryRaycastTileAtPointer(pointerPosition, out var tileIndex, out _) || tileIndex < 0 || tileIndex >= _tiles.Count)
            {
                HideTileInfo();
                return;
            }

            var cameraToUse = ResolveInteractionCamera(pointerPosition);
            if (cameraToUse == null)
            {
                HideTileInfo();
                return;
            }

            var info = EnsureTileInfoInstance();
            if (info == null)
            {
                return;
            }

            var tint01 = (_tileTintCurrent != null && tileIndex < _tileTintCurrent.Length)
                ? Mathf.Clamp01(_tileTintCurrent[tileIndex])
                : 0f;
            var percent = tint01 * 100f;
            var tile = _tiles[tileIndex];
            var localPosition = GetTileSurfacePoint(tile.Index, tile.Center.normalized, tileElevation + tileInfoHeightOffset, true);
            var worldPosition = transform.TransformPoint(localPosition);
            info.ShowInfo(worldPosition, percent, cameraToUse);
        }

        private TileInfoUI EnsureTileInfoInstance()
        {
            if (_tileInfoInstance != null)
            {
                return _tileInfoInstance;
            }

            var existing = FindFirstObjectByType<TileInfoUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                _tileInfoInstance = existing;
                _tileInfoOwnedInstance = false;
                _tileInfoInstance.HideInfo();
                return _tileInfoInstance;
            }

            if (tileInfoPrefab != null)
            {
                _tileInfoInstance = Instantiate(tileInfoPrefab);
                _tileInfoOwnedInstance = true;
                _tileInfoInstance.HideInfo();
                return _tileInfoInstance;
            }

            var fallbackObject = new GameObject("TileInfo_Runtime");
            var rectTransform = fallbackObject.AddComponent<RectTransform>();
            rectTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            var canvas = fallbackObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            fallbackObject.AddComponent<CanvasScaler>();
            fallbackObject.AddComponent<GraphicRaycaster>();
            _tileInfoInstance = fallbackObject.AddComponent<TileInfoUI>();
            _tileInfoOwnedInstance = true;
            _tileInfoInstance.HideInfo();
            return _tileInfoInstance;
        }

        private void HideTileInfo()
        {
            if (_tileInfoInstance == null)
            {
                return;
            }

            _tileInfoInstance.HideInfo();
        }

        private void ReleaseTileInfoInstanceIfOwned()
        {
            if (_tileInfoInstance == null || !_tileInfoOwnedInstance)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_tileInfoInstance.gameObject);
            }
            else
            {
                DestroyImmediate(_tileInfoInstance.gameObject);
            }

            _tileInfoInstance = null;
            _tileInfoOwnedInstance = false;
        }

        private void EnsureHudReferences()
        {
            if (!autoFindHudTexts)
            {
                return;
            }

            if (currencyText == null)
            {
                currencyText = FindHudText(currencyAmountObjectName, currencyPanelObjectName);
            }

            if (oceanIndexText == null)
            {
                oceanIndexText = FindHudText(oceanIndexObjectName, oceanIndexObjectName);
            }
        }

        private void EnsureConditionsInfoUiReference()
        {
            if (conditionsInfoUI != null || !autoFindConditionsInfoUI || string.IsNullOrWhiteSpace(conditionsInfoObjectName))
            {
                return;
            }

            var target = GameObject.Find(conditionsInfoObjectName);
            if (target == null)
            {
                return;
            }

            conditionsInfoUI = target.GetComponent<ConditionsInfoUI>() ?? target.GetComponentInChildren<ConditionsInfoUI>(true);
        }

        private static TMP_Text FindHudText(string valueObjectName, string panelObjectName)
        {
            if (!string.IsNullOrWhiteSpace(valueObjectName))
            {
                var direct = GameObject.Find(valueObjectName);
                if (direct != null)
                {
                    var directText = direct.GetComponent<TMP_Text>() ?? direct.GetComponentInChildren<TMP_Text>(true);
                    if (directText != null)
                    {
                        return directText;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(panelObjectName))
            {
                return null;
            }

            var panel = GameObject.Find(panelObjectName);
            if (panel == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(valueObjectName))
            {
                var transforms = panel.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < transforms.Length; i++)
                {
                    var child = transforms[i];
                    if (!string.Equals(child.name, valueObjectName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var text = child.GetComponent<TMP_Text>() ?? child.GetComponentInChildren<TMP_Text>(true);
                    if (text != null)
                    {
                        return text;
                    }
                }
            }

            return panel.GetComponentInChildren<TMP_Text>(true);
        }

        private void HandleCurrencyAmountChanged(int amount)
        {
            if (currencyText == null)
            {
                return;
            }

            currencyText.text = $"$ {amount}";
        }

        private void UpdateOceanIndexUi()
        {
            if (oceanIndexText == null)
            {
                return;
            }

            if (!manageWater)
            {
                oceanIndexText.text = "0 %";
                return;
            }

            var maxRadius = GetMaxWaterRadius();
            if (maxRadius <= 0.0001f)
            {
                oceanIndexText.text = "0 %";
                return;
            }

            var minRadius = GetMinWaterRadiusForOceanIndex(maxRadius);
            float percent;
            if (maxRadius - minRadius <= 0.0001f)
            {
                percent = _currentWaterRadius >= maxRadius ? 100f : 0f;
            }
            else
            {
                percent = Mathf.InverseLerp(minRadius, maxRadius, _currentWaterRadius) * 100f;
            }

            oceanIndexText.text = $"{Mathf.RoundToInt(Mathf.Clamp(percent, 0f, 100f))} %";
        }

        private float GetMinWaterRadiusForOceanIndex(float maxRadius)
        {
            if (maxRadius <= 0f)
            {
                return 0f;
            }

            if (useReliefBasedInitialWaterRadius)
            {
                return Mathf.Min(GetReliefBasedInitialWaterRadius(), maxRadius);
            }

            return Mathf.Clamp01(initialWaterFillNormalized) * maxRadius;
        }

        private void UpdateConditionsInfoUi()
        {
            if (conditionsInfoUI == null)
            {
                return;
            }

            conditionsInfoUI.SetTerraformingPercent(GetTerraformingPercent());
            conditionsInfoUI.SetConditions(
                _humidity,
                _atmosphere,
                minHumidity,
                maxHumidity,
                minAtmosphere,
                maxAtmosphere);
        }

        private void ResetConditionsToMinimum()
        {
            _humidity = minHumidity;
            _atmosphere = minAtmosphere;
        }

        public float GetTerraformingPercent()
        {
            if (_tileTintCurrent == null || _isHighlandTile == null || _tiles.Count == 0)
            {
                return 0f;
            }

            var tileCount = Mathf.Min(_tiles.Count, Mathf.Min(_tileTintCurrent.Length, _isHighlandTile.Length));
            if (tileCount <= 0)
            {
                return 0f;
            }

            var tintSum = 0f;
            var terraformingTileCount = 0;
            for (var i = 0; i < tileCount; i++)
            {
                if (_isHighlandTile[i])
                {
                    continue;
                }

                terraformingTileCount++;
                tintSum += Mathf.Clamp01(_tileTintCurrent[i]);
            }

            if (terraformingTileCount <= 0)
            {
                return 0f;
            }

            return tintSum / terraformingTileCount * 100f;
        }

        private bool TryRaycastTileAtPointer(Vector2 pointerPosition, out int tileIndex, out RaycastHit hit)
        {
            tileIndex = -1;
            hit = default;

            if (_triangleToTile == null || _triangleToTile.Length == 0)
            {
                return false;
            }

            var cameraToUse = ResolveInteractionCamera(pointerPosition);
            if (cameraToUse == null)
            {
                return false;
            }

            var ray = cameraToUse.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out hit, clickRaycastDistance, clickableLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.collider != _meshCollider)
            {
                return false;
            }

            var triangleIndex = hit.triangleIndex;
            if (triangleIndex < 0 || triangleIndex >= _triangleToTile.Length)
            {
                return false;
            }

            tileIndex = _triangleToTile[triangleIndex];
            return tileIndex >= 0 && tileIndex < _tiles.Count;
        }

        private static bool TryGetPointerDownPosition(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                pointerPosition = Input.mousePosition;
                return true;
            }

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == UnityEngine.TouchPhase.Began)
                {
                    pointerPosition = touch.position;
                    return true;
                }
            }
#endif

            pointerPosition = default;
            return false;
        }

        private static bool TryGetPointerPosition(out Vector2 pointerPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            pointerPosition = Input.mousePosition;
            if (Input.touchCount > 0)
            {
                pointerPosition = Input.GetTouch(0).position;
            }

            return true;
#else
            pointerPosition = default;
            return false;
#endif
        }

        private Camera ResolveInteractionCamera(Vector2 pointerPosition)
        {
            if (interactionCamera != null && interactionCamera.isActiveAndEnabled)
            {
                return interactionCamera;
            }

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
            {
                return Camera.main;
            }

            Camera bestCamera = null;
            var bestDepth = float.NegativeInfinity;
            var cameras = Camera.allCameras;

            for (var i = 0; i < cameras.Length; i++)
            {
                var currentCamera = cameras[i];
                if (currentCamera == null || !currentCamera.isActiveAndEnabled)
                {
                    continue;
                }

                if (!currentCamera.pixelRect.Contains(pointerPosition))
                {
                    continue;
                }

                if (currentCamera.depth > bestDepth)
                {
                    bestCamera = currentCamera;
                    bestDepth = currentCamera.depth;
                }
            }

            return bestCamera;
        }

        private int GetEffectiveSubdivisions()
        {
            var clampedBaseSubdivisions = Mathf.Clamp(subdivisions, 1, maxSubdivisions);
            if (!scaleTileDensityWithRadius)
            {
                return clampedBaseSubdivisions;
            }

            var scale = radius / densityReferenceRadius;
            var scaleBoost = Mathf.RoundToInt(Mathf.Log(scale, 2f));
            return Mathf.Clamp(clampedBaseSubdivisions + scaleBoost, 1, maxSubdivisions);
        }

        private void EnsureCoreComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshCollider = GetComponent<MeshCollider>();
        }

        private void EnsureHighlightComponents()
        {
            if (_highlightRoot == null)
            {
                var child = transform.Find("TileHighlights");
                if (child == null)
                {
                    var childObject = new GameObject("TileHighlights");
                    child = childObject.transform;
                    child.SetParent(transform, false);
                }

                _highlightRoot = child;
            }

            _highlightRoot.localPosition = Vector3.zero;
            _highlightRoot.localRotation = Quaternion.identity;
            _highlightRoot.localScale = Vector3.one;

            _highlightMeshFilter = _highlightRoot.GetComponent<MeshFilter>();
            if (_highlightMeshFilter == null)
            {
                _highlightMeshFilter = _highlightRoot.gameObject.AddComponent<MeshFilter>();
            }

            _highlightMeshRenderer = _highlightRoot.GetComponent<MeshRenderer>();
            if (_highlightMeshRenderer == null)
            {
                _highlightMeshRenderer = _highlightRoot.gameObject.AddComponent<MeshRenderer>();
            }

            _highlightMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _highlightMeshRenderer.receiveShadows = false;
        }

        private void EnsureWaterContactComponents()
        {
            if (_waterContactRoot == null)
            {
                var child = transform.Find("WaterContactTiles");
                if (child == null)
                {
                    var childObject = new GameObject("WaterContactTiles");
                    child = childObject.transform;
                    child.SetParent(transform, false);
                }

                _waterContactRoot = child;
            }

            _waterContactRoot.localPosition = Vector3.zero;
            _waterContactRoot.localRotation = Quaternion.identity;
            _waterContactRoot.localScale = Vector3.one;

            _waterContactMeshFilter = _waterContactRoot.GetComponent<MeshFilter>();
            if (_waterContactMeshFilter == null)
            {
                _waterContactMeshFilter = _waterContactRoot.gameObject.AddComponent<MeshFilter>();
            }

            _waterContactMeshRenderer = _waterContactRoot.GetComponent<MeshRenderer>();
            if (_waterContactMeshRenderer == null)
            {
                _waterContactMeshRenderer = _waterContactRoot.gameObject.AddComponent<MeshRenderer>();
            }

            _waterContactMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _waterContactMeshRenderer.receiveShadows = false;
        }

        private void EnsureSnowCapComponents()
        {
            if (_snowCapsRoot == null)
            {
                var child = transform.Find("HighlandSnowCaps");
                if (child == null)
                {
                    var childObject = new GameObject("HighlandSnowCaps");
                    child = childObject.transform;
                    child.SetParent(transform, false);
                }

                _snowCapsRoot = child;
            }

            _snowCapsRoot.localPosition = Vector3.zero;
            _snowCapsRoot.localRotation = Quaternion.identity;
            _snowCapsRoot.localScale = Vector3.one;

            _snowCapsMeshFilter = _snowCapsRoot.GetComponent<MeshFilter>();
            if (_snowCapsMeshFilter == null)
            {
                _snowCapsMeshFilter = _snowCapsRoot.gameObject.AddComponent<MeshFilter>();
            }

            _snowCapsMeshRenderer = _snowCapsRoot.GetComponent<MeshRenderer>();
            if (_snowCapsMeshRenderer == null)
            {
                _snowCapsMeshRenderer = _snowCapsRoot.gameObject.AddComponent<MeshRenderer>();
            }

            _snowCapsMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _snowCapsMeshRenderer.receiveShadows = true;
        }

        private void BuildRuntimeMaterials()
        {
            DestroyMaterial(ref _pentagonMaterial);
            DestroyMaterial(ref _hexagonMaterial);
            DestroyMaterial(ref _highTileMaterial);
            DestroyMaterial(ref _clickTintMaterial);
            DestroyMaterial(ref _waterContactMaterial);
            DestroyMaterial(ref _snowCapsMaterial);

            var source = tileMaterial;
            if (source == null)
            {
                var fallbackShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                if (fallbackShader == null)
                {
                    throw new InvalidOperationException("No fallback shader found for planet tile materials.");
                }

                source = new Material(fallbackShader);
            }

            _pentagonMaterial = CreateRuntimeMaterial(source, "Planet_Pentagon", pentagonColor);
            _hexagonMaterial = CreateRuntimeMaterial(source, "Planet_Hexagon", hexagonColor);
            _highTileMaterial = CreateRuntimeMaterial(source, "Planet_HighTile", highTileColor);
            _clickTintMaterial = CreateClickTintMaterial();
            _waterContactMaterial = CreateRuntimeMaterial(source, "Planet_WaterContact", waterContactTileColor);
            _snowCapsMaterial = CreateRuntimeMaterial(source, "Planet_HighlandSnowCaps", highlandSnowCapColor);

            if (tileMaterial == null)
            {
                DestroyMaterial(ref source);
            }

            _meshRenderer.sharedMaterials = new[] { _pentagonMaterial, _hexagonMaterial, _highTileMaterial };
            _highlightMeshRenderer.sharedMaterials = new[] { _clickTintMaterial };
            _waterContactMeshRenderer.sharedMaterials = new[] { _waterContactMaterial };
            _snowCapsMeshRenderer.sharedMaterials = new[] { _snowCapsMaterial };
        }

        private static Material CreateRuntimeMaterial(Material source, string name, Color color)
        {
            var material = new Material(source)
            {
                name = name
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private Material CreateClickTintMaterial()
        {
            var shader = Shader.Find("Custom/PlanetClickTintLit")
                ?? Shader.Find("Custom/PlanetClickTint")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No shader available for click tint material.");
            }

            var material = new Material(shader)
            {
                name = "Planet_ClickTint"
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
            return material;
        }

        private void ClearGeneratedData()
        {
            CleanupLegacyTileChildren();
            HideTileInfo();

            for (var i = 0; i < _tiles.Count; i++)
            {
                _tiles[i].Clicked -= HandleTileClicked;
            }

            _tiles.Clear();
            _triangleToTile = null;
            _generatedSurfaceMaxRadius = 0f;
            _generatedSurfaceMinRadius = 0f;
            _isHighlandTile = null;
            _tileMinSurfaceRadius = null;
            _tileMaxSurfaceRadius = null;
            _waterTouchedTiles = null;
            _tileTintCurrent = null;
            _tileTintTarget = null;
            _highlightTileVertexStarts = null;
            _highlightTileVertexCounts = null;
            _highlightTileVisible = null;
            _highlightVertexColors = null;
            _highlightMeshGeometryBuilt = false;
            _highlightGeometryOffset = float.NaN;
            _highlightVisibleTileCount = 0;
            _clickTintDirty = false;
            _activeClickTintWaves.Clear();
            _changedTintTiles.Clear();
            _activeTintTiles.Clear();
            _isTintTileActive = null;
            _waterInitialized = false;

            if (_meshFilter != null)
            {
                _meshFilter.sharedMesh = null;
            }

            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = null;
            }

            if (_highlightMeshFilter != null)
            {
                _highlightMeshFilter.sharedMesh = null;
            }

            if (_waterContactMeshFilter != null)
            {
                _waterContactMeshFilter.sharedMesh = null;
            }
            if (_snowCapsMeshFilter != null)
            {
                _snowCapsMeshFilter.sharedMesh = null;
            }

            DestroyMesh(ref _renderMesh);
            DestroyMesh(ref _collisionMesh);

            if (_highlightMesh == null)
            {
                _highlightMesh = new Mesh { name = "Planet_HighlightMesh" };
                _highlightMesh.MarkDynamic();
            }
            else
            {
                _highlightMesh.Clear();
                _highlightMesh.MarkDynamic();
            }

            if (_highlightMeshFilter != null)
            {
                _highlightMeshFilter.sharedMesh = _highlightMesh;
            }

            if (_waterContactMesh == null)
            {
                _waterContactMesh = new Mesh { name = "Planet_WaterContactMesh" };
            }
            else
            {
                _waterContactMesh.Clear();
            }

            if (_waterContactMeshFilter != null)
            {
                _waterContactMeshFilter.sharedMesh = _waterContactMesh;
            }

            if (_waterContactMeshRenderer != null)
            {
                _waterContactMeshRenderer.enabled = false;
            }

            if (_snowCapsMesh == null)
            {
                _snowCapsMesh = new Mesh { name = "Planet_HighlandSnowCapsMesh" };
            }
            else
            {
                _snowCapsMesh.Clear();
            }

            if (_snowCapsMeshFilter != null)
            {
                _snowCapsMeshFilter.sharedMesh = _snowCapsMesh;
            }

            if (_snowCapsMeshRenderer != null)
            {
                _snowCapsMeshRenderer.enabled = false;
            }
        }

        private void ReleaseRuntimeResources()
        {
            ClearGeneratedData();
            DestroyMesh(ref _highlightMesh);
            DestroyMesh(ref _waterContactMesh);
            DestroyMesh(ref _snowCapsMesh);
            DestroyMaterial(ref _pentagonMaterial);
            DestroyMaterial(ref _hexagonMaterial);
            DestroyMaterial(ref _highTileMaterial);
            DestroyMaterial(ref _clickTintMaterial);
            DestroyMaterial(ref _waterContactMaterial);
            DestroyMaterial(ref _snowCapsMaterial);
            ReleaseTileInfoInstanceIfOwned();
        }

        private void DestroyMesh(ref Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }

            mesh = null;
        }

        private void DestroyMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }

            material = null;
        }

        private void CleanupLegacyTileChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (_highlightRoot != null && child == _highlightRoot)
                {
                    continue;
                }

                if (!child.name.StartsWith("Tile_", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void InitializeWaterIfNeeded(bool forceReset)
        {
            if (!manageWater)
            {
                return;
            }

            ResolveWaterReferences();
            if (waterSphere == null)
            {
                ClearWaterContactMesh();
                return;
            }

            if (!forceReset && _waterInitialized)
            {
                _currentWaterRadius = Mathf.Min(_currentWaterRadius, GetMaxWaterRadius());
                ApplyWaterRadius();
                return;
            }

            _currentWaterRadius = useReliefBasedInitialWaterRadius
                ? GetReliefBasedInitialWaterRadius()
                : GetMaxWaterRadius() * initialWaterFillNormalized;
            _waterInitialized = true;
            ApplyWaterRadius();
        }

        private void ResolveWaterReferences()
        {
            if (waterSphere == null && !string.IsNullOrWhiteSpace(waterObjectName))
            {
                var found = GameObject.Find(waterObjectName);
                if (found != null)
                {
                    waterSphere = found.transform;
                }
            }

            if (waterSphere == null)
            {
                _waterRenderer = null;
                _waterCollider = null;
                return;
            }

            _waterRenderer ??= waterSphere.GetComponent<MeshRenderer>();
            _waterCollider ??= waterSphere.GetComponent<Collider>();
        }

        private void ApplyWaterRadius()
        {
            if (waterSphere == null)
            {
                ClearWaterContactMesh();
                return;
            }

            waterSphere.position = transform.position;
            waterSphere.rotation = Quaternion.identity;

            var isVisible = _currentWaterRadius > 0.0001f;
            var diameter = Mathf.Max(0f, _currentWaterRadius * 2f);
            waterSphere.localScale = Vector3.one * diameter;

            if (_waterRenderer != null)
            {
                _waterRenderer.enabled = isVisible;
            }

            if (_waterCollider != null)
            {
                _waterCollider.enabled = isVisible;
            }

            UpdateWaterContactMesh();
        }

        private float GetMaxWaterRadius()
        {
            return GetWaterReferenceRadius() * maxWaterRadiusRatio;
        }

        private float GetReliefBasedInitialWaterRadius()
        {
            var basinRadius = Mathf.Max(0f, GetSurfaceMinRadius() - initialWaterBelowReliefOffset);
            return Mathf.Min(basinRadius, GetMaxWaterRadius());
        }

        private float GetSurfaceMinRadius()
        {
            if (_generatedSurfaceMinRadius > 0f)
            {
                return _generatedSurfaceMinRadius;
            }

            var reliefGuess = enableRelief ? reliefHeight : 0f;
            return Mathf.Max(0f, radius + tileElevation - reliefGuess);
        }

        private float GetWaterReferenceRadius()
        {
            if (_generatedSurfaceMaxRadius > 0f)
            {
                return _generatedSurfaceMaxRadius;
            }

            var reliefGuess = enableRelief ? reliefHeight : 0f;
            return radius + tileElevation + reliefGuess;
        }


        private void BindAddWaterButtonIfNeeded()
        {
            if (!manageWater || _isAddWaterButtonBound)
            {
                return;
            }

            if (addWaterButton == null && autoFindAddWaterButton && !string.IsNullOrWhiteSpace(addWaterButtonObjectName))
            {
                var buttonObject = GameObject.Find(addWaterButtonObjectName);
                if (buttonObject != null)
                {
                    addWaterButton = buttonObject.GetComponent<Button>();
                }
            }

            if (addWaterButton == null)
            {
                return;
            }

            addWaterButton.onClick.AddListener(AddWater);
            _boundAddWaterButton = addWaterButton;
            _isAddWaterButtonBound = true;
        }

        private void UnbindAddWaterButton()
        {
            if (!_isAddWaterButtonBound || _boundAddWaterButton == null)
            {
                return;
            }

            _boundAddWaterButton.onClick.RemoveListener(AddWater);
            _boundAddWaterButton = null;
            _isAddWaterButtonBound = false;
        }

        private void BindRegenerateButtonIfNeeded()
        {
            if (_isRegenerateButtonBound)
            {
                return;
            }

            if (regenerateButton == null && autoFindRegenerateButton && !string.IsNullOrWhiteSpace(regenerateButtonObjectName))
            {
                var buttonObject = GameObject.Find(regenerateButtonObjectName);
                if (buttonObject != null)
                {
                    regenerateButton = buttonObject.GetComponent<Button>();
                }
            }

            if (regenerateButton == null)
            {
                return;
            }

            regenerateButton.onClick.AddListener(Generate);
            _boundRegenerateButton = regenerateButton;
            _isRegenerateButtonBound = true;
        }

        private void UnbindRegenerateButton()
        {
            if (!_isRegenerateButtonBound || _boundRegenerateButton == null)
            {
                return;
            }

            _boundRegenerateButton.onClick.RemoveListener(Generate);
            _boundRegenerateButton = null;
            _isRegenerateButtonBound = false;
        }

        private void BindAddClickPowerButtonIfNeeded()
        {
            if (_isAddClickPowerButtonBound)
            {
                return;
            }

            if (addClickPowerButton == null && autoFindAddClickPowerButton && !string.IsNullOrWhiteSpace(addClickPowerButtonObjectName))
            {
                var buttonObject = GameObject.Find(addClickPowerButtonObjectName);
                if (buttonObject != null)
                {
                    addClickPowerButton = buttonObject.GetComponent<Button>();
                }
            }

            if (addClickPowerButton == null)
            {
                return;
            }

            addClickPowerButton.onClick.AddListener(AddClickPower);
            _boundAddClickPowerButton = addClickPowerButton;
            _isAddClickPowerButtonBound = true;
        }

        private void UnbindAddClickPowerButton()
        {
            if (!_isAddClickPowerButtonBound || _boundAddClickPowerButton == null)
            {
                return;
            }

            _boundAddClickPowerButton.onClick.RemoveListener(AddClickPower);
            _boundAddClickPowerButton = null;
            _isAddClickPowerButtonBound = false;
        }

        private void BindAddClickRadiusButtonIfNeeded()
        {
            if (_isAddClickRadiusButtonBound)
            {
                return;
            }

            if (addClickRadiusButton == null && autoFindAddClickRadiusButton && !string.IsNullOrWhiteSpace(addClickRadiusButtonObjectName))
            {
                var buttonObject = GameObject.Find(addClickRadiusButtonObjectName);
                if (buttonObject != null)
                {
                    addClickRadiusButton = buttonObject.GetComponent<Button>();
                }
            }

            if (addClickRadiusButton == null)
            {
                return;
            }

            addClickRadiusButton.onClick.AddListener(AddClickRadius);
            _boundAddClickRadiusButton = addClickRadiusButton;
            _isAddClickRadiusButtonBound = true;
        }

        private void UnbindAddClickRadiusButton()
        {
            if (!_isAddClickRadiusButtonBound || _boundAddClickRadiusButton == null)
            {
                return;
            }

            _boundAddClickRadiusButton.onClick.RemoveListener(AddClickRadius);
            _boundAddClickRadiusButton = null;
            _isAddClickRadiusButtonBound = false;
        }

        private void BindShowInfoButtonIfNeeded()
        {
            if (_isShowInfoButtonBound)
            {
                return;
            }

            if (showInfoButton == null && autoFindShowInfoButton && !string.IsNullOrWhiteSpace(showInfoButtonObjectName))
            {
                var buttonObject = GameObject.Find(showInfoButtonObjectName);
                if (buttonObject != null)
                {
                    showInfoButton = buttonObject.GetComponent<Button>();
                }
            }

            if (showInfoButton == null)
            {
                return;
            }

            showInfoButton.onClick.AddListener(ToggleTileInfoMode);
            _boundShowInfoButton = showInfoButton;
            _isShowInfoButtonBound = true;
        }

        private void UnbindShowInfoButton()
        {
            if (!_isShowInfoButtonBound || _boundShowInfoButton == null)
            {
                return;
            }

            _boundShowInfoButton.onClick.RemoveListener(ToggleTileInfoMode);
            _boundShowInfoButton = null;
            _isShowInfoButtonBound = false;
        }

        private static Vector2 CreateSphericalUv(Vector3 point)
        {
            var p = point.normalized;
            return new Vector2(0.5f + Mathf.Atan2(p.z, p.x) / (2f * Mathf.PI), 0.5f - Mathf.Asin(p.y) / Mathf.PI);
        }

        private Vector3 GetTileSurfacePoint(int tileIndex, Vector3 normalizedDirection, float extraOffset, bool isCenterPoint)
        {
            var mountainOffset = EvaluateHighlandMountainOffset(tileIndex, normalizedDirection, isCenterPoint);
            return GetSurfacePoint(normalizedDirection, extraOffset + mountainOffset);
        }

        private Vector3 GetSurfacePoint(Vector3 normalizedDirection, float extraOffset)
        {
            var reliefOffset = EvaluateRelief(normalizedDirection);
            var surfaceRadius = radius + extraOffset + reliefOffset;
            return normalizedDirection * surfaceRadius;
        }

        private float EvaluateHighlandMountainOffset(int tileIndex, Vector3 normalizedDirection, bool isCenterPoint)
        {
            if (!enableHighlandMountains || highlandMountainHeight <= 0f || _isHighlandTile == null)
            {
                return 0f;
            }

            if (tileIndex < 0 || tileIndex >= _isHighlandTile.Length || !_isHighlandTile[tileIndex])
            {
                return 0f;
            }

            var seedOffset = new Vector3(
                (highlandMountainSeed * 0.00091f) % 97f,
                (highlandMountainSeed * 0.00137f) % 89f,
                (highlandMountainSeed * 0.00179f) % 83f);

            var n = SampleNoise3D(normalizedDirection * highlandMountainNoiseFrequency + seedOffset);
            n = Mathf.Clamp01(n);
            n = Mathf.Pow(n, highlandMountainSharpness);

            var edgeFactor = isCenterPoint ? 1f : Mathf.Clamp01(1f - highlandMountainEdgeFalloff);
            return highlandMountainHeight * edgeFactor * n;
        }

        private float EvaluateRelief(Vector3 normalizedDirection)
        {
            if (!enableRelief || reliefHeight <= 0f)
            {
                return 0f;
            }

            var seedOffset = new Vector3(
                (reliefSeed * 0.00123f) % 97f,
                (reliefSeed * 0.00217f) % 89f,
                (reliefSeed * 0.00311f) % 83f);

            var p = normalizedDirection * reliefFrequency + seedOffset;
            var amplitude = 1f;
            var frequency = 1f;
            var total = 0f;
            var totalAmplitude = 0f;

            for (var octave = 0; octave < reliefOctaves; octave++)
            {
                var n = SampleNoise3D(p * frequency + new Vector3(octave * 11.13f, octave * 7.31f, octave * 5.17f));
                n = n * 2f - 1f;

                if (ridgedRelief)
                {
                    n = 1f - Mathf.Abs(n);
                    n = n * 2f - 1f;
                }

                total += n * amplitude;
                totalAmplitude += amplitude;
                amplitude *= reliefPersistence;
                frequency *= reliefLacunarity;
            }

            if (totalAmplitude <= 0f)
            {
                return 0f;
            }

            var normalizedNoise = total / totalAmplitude;
            return normalizedNoise * reliefHeight;
        }

        private static float SampleNoise3D(Vector3 p)
        {
            var xy = Mathf.PerlinNoise(p.x, p.y);
            var yz = Mathf.PerlinNoise(p.y, p.z);
            var zx = Mathf.PerlinNoise(p.z, p.x);
            var yx = Mathf.PerlinNoise(p.y, p.x);
            var zy = Mathf.PerlinNoise(p.z, p.y);
            var xz = Mathf.PerlinNoise(p.x, p.z);
            return (xy + yz + zx + yx + zy + xz) / 6f;
        }

        private static float DetermineWinding(Vector3 center, Vector3 first, Vector3 second, Vector3 expectedNormal)
        {
            var triangleNormal = Vector3.Cross(first - center, second - center).normalized;
            return Vector3.Dot(triangleNormal, expectedNormal);
        }
    }
}
