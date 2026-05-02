using System.Collections.Generic;
using LittlePlanet.HybridTerraform;
using UnityEngine;

namespace LittlePlanet.PlanetSystem
{
    [DisallowMultipleComponent]
    public sealed class ObjectSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Planet planet;
        [SerializeField] private PlanetCameraController planetCameraController;
        [SerializeField] private PlanetFlyTerraformSkill flightSkill;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform spawnedRoot;

        [Header("Spawning")]
        [SerializeField] private List<GreenSpawnObject> greenSpawnObjects = new();
        [SerializeField, Range(0f, 1f)] private float spawnProbability = 0.25f;
        [SerializeField, Min(1)] private int maxSpawnedObjects = 1800;
        [SerializeField, Min(0f)] private float spawnSurfaceOffset = 0.04f;
        [SerializeField] private bool spawnOnEnable = true;

        [Header("Optimization")]
        [SerializeField] private bool showOnlyInFlightMode = true;
        [SerializeField, Min(0f)] private float flightModeMinDistanceFromPlanetCenter = 22f;
        [SerializeField, Min(0f)] private float maxVisibleDistanceToCamera = 36f;
        [SerializeField, Min(16)] private int updateBatchSize = 512;
        [SerializeField, Min(0.02f)] private float updateIntervalSeconds = 0.08f;
        [SerializeField, Min(0.1f)] private float hiddenWaterCheckIntervalSeconds = 0.75f;
        [SerializeField, Range(0f, 1f)] private float growthUpdateThreshold = 0.01f;
        [SerializeField] private bool disableSpawnedColliders = true;
        [SerializeField] private bool enablePooling = true;

        private readonly List<SpawnEntry> _spawnEntries = new();
        private readonly Dictionary<GreenSpawnObject, Stack<GreenSpawnObject>> _poolByPrefab = new();
        private bool _spawned;
        private bool _wereObjectsHidden;
        private int _updateCursor;
        private float _nextUpdateTime;
        private float _nextHiddenWaterCheckTime;

        private sealed class SpawnEntry
        {
            public Tile Tile;
            public GreenSpawnObject Prefab;
            public GreenSpawnObject Instance;
            public Transform Transform;
            public float LastGrowth01;
            public bool IsVisible;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (planet != null)
            {
                planet.PlanetGenerated += HandlePlanetGenerated;
            }

            if (spawnOnEnable && planet != null && planet.Tiles.Count > 0)
            {
                SpawnForPlanet();
            }
        }

        private void OnDisable()
        {
            if (planet != null)
            {
                planet.PlanetGenerated -= HandlePlanetGenerated;
            }
        }

        private void OnValidate()
        {
            spawnProbability = Mathf.Clamp01(spawnProbability);
            maxSpawnedObjects = Mathf.Max(1, maxSpawnedObjects);
            maxVisibleDistanceToCamera = Mathf.Max(0f, maxVisibleDistanceToCamera);
            updateBatchSize = Mathf.Max(16, updateBatchSize);
            updateIntervalSeconds = Mathf.Max(0.02f, updateIntervalSeconds);
            hiddenWaterCheckIntervalSeconds = Mathf.Max(0.1f, hiddenWaterCheckIntervalSeconds);
            growthUpdateThreshold = Mathf.Clamp01(growthUpdateThreshold);
        }

        private void Update()
        {
            if (!_spawned || planet == null)
            {
                return;
            }

            if (Time.time < _nextUpdateTime)
            {
                return;
            }

            _nextUpdateTime = Time.time + updateIntervalSeconds;
            if (controlledCamera == null)
            {
                ResolveReferences();
            }

            UpdateSpawnedObjects();
        }

        [ContextMenu("Respawn Objects")]
        public void SpawnForPlanet()
        {
            ResolveReferences();
            ClearSpawnedEntries();

            if (planet == null || greenSpawnObjects == null || greenSpawnObjects.Count == 0 || spawnProbability <= 0f)
            {
                _spawned = false;
                return;
            }

            EnsureSpawnRoot();
            if (spawnedRoot == null)
            {
                _spawned = false;
                return;
            }

            var tiles = planet.Tiles;
            for (var i = 0; i < tiles.Count; i++)
            {
                if (_spawnEntries.Count >= maxSpawnedObjects)
                {
                    break;
                }

                var tile = tiles[i];
                if (tile == null || planet.IsHighlandTile(tile) || UnityEngine.Random.value > spawnProbability)
                {
                    continue;
                }

                var prefab = PickRandomPrefab();
                if (prefab == null)
                {
                    continue;
                }

                var instance = GetInstance(prefab);
                if (instance == null)
                {
                    continue;
                }

                var instanceTransform = instance.transform;
                PositionInstanceOnTile(instanceTransform, tile);
                instance.SetGrowth01(planet.GetTileTerraforming01(tile));
                if (disableSpawnedColliders)
                {
                    DisableColliders(instanceTransform);
                }

                SetObjectVisible(instance, false);

                _spawnEntries.Add(new SpawnEntry
                {
                    Tile = tile,
                    Prefab = prefab,
                    Instance = instance,
                    Transform = instanceTransform,
                    LastGrowth01 = -1f,
                    IsVisible = false
                });
            }

            _spawned = _spawnEntries.Count > 0;
            _wereObjectsHidden = false;
            _updateCursor = 0;
            _nextUpdateTime = 0f;
        }

        private void HandlePlanetGenerated()
        {
            SpawnForPlanet();
        }

        private void UpdateSpawnedObjects()
        {
            if (_spawnEntries.Count == 0)
            {
                _spawned = false;
                return;
            }

            var cameraToUse = controlledCamera != null && controlledCamera.isActiveAndEnabled
                ? controlledCamera
                : Camera.main;
            var flightMode = !showOnlyInFlightMode || IsFlightMode(cameraToUse);

            if (!flightMode || cameraToUse == null)
            {
                if (!_wereObjectsHidden)
                {
                    HideAllObjects();
                    _wereObjectsHidden = true;
                }

                if (Time.time < _nextHiddenWaterCheckTime)
                {
                    return;
                }

                _nextHiddenWaterCheckTime = Time.time + hiddenWaterCheckIntervalSeconds;
                ProcessEntryBatch(cameraToUse, evaluateVisibility: false);
                return;
            }

            _wereObjectsHidden = false;
            _nextHiddenWaterCheckTime = Time.time + hiddenWaterCheckIntervalSeconds;
            ProcessEntryBatch(cameraToUse, evaluateVisibility: true);
        }

        private void ProcessEntryBatch(Camera cameraToUse, bool evaluateVisibility)
        {
            if (_spawnEntries.Count == 0)
            {
                _spawned = false;
                return;
            }

            var sqrMaxVisibleDistance = maxVisibleDistanceToCamera * maxVisibleDistanceToCamera;
            var processed = 0;
            var attempts = 0;
            var maxAttempts = Mathf.Max(updateBatchSize, _spawnEntries.Count);

            while (_spawnEntries.Count > 0 && processed < updateBatchSize && attempts < maxAttempts)
            {
                attempts++;

                if (_updateCursor >= _spawnEntries.Count)
                {
                    _updateCursor = 0;
                }

                var entryIndex = _updateCursor;
                if (!ProcessEntry(entryIndex, cameraToUse, sqrMaxVisibleDistance, evaluateVisibility))
                {
                    continue;
                }

                _updateCursor++;
                processed++;
            }

            if (_spawnEntries.Count == 0)
            {
                _spawned = false;
                _updateCursor = 0;
            }
        }

        private bool ProcessEntry(int index, Camera cameraToUse, float sqrMaxVisibleDistance, bool evaluateVisibility)
        {
            if (index < 0 || index >= _spawnEntries.Count)
            {
                return false;
            }

            var entry = _spawnEntries[index];
            if (entry == null || entry.Instance == null || entry.Transform == null || entry.Tile == null)
            {
                RemoveEntryAt(index);
                return false;
            }

            if (planet.IsTileWaterAffected(entry.Tile))
            {
                RemoveEntryAt(index);
                return false;
            }

            if (!evaluateVisibility || cameraToUse == null)
            {
                return true;
            }

            var toCamera = cameraToUse.transform.position - entry.Transform.position;
            var isVisible = toCamera.sqrMagnitude <= sqrMaxVisibleDistance;
            if (entry.IsVisible != isVisible)
            {
                SetObjectVisible(entry.Instance, isVisible);
                entry.IsVisible = isVisible;
            }

            if (!isVisible)
            {
                return true;
            }

            var growth01 = planet.GetTileTerraforming01(entry.Tile);
            if (Mathf.Abs(growth01 - entry.LastGrowth01) < growthUpdateThreshold)
            {
                return true;
            }

            entry.Instance.SetGrowth01(growth01);
            entry.LastGrowth01 = growth01;
            return true;
        }

        private void HideAllObjects()
        {
            for (var i = 0; i < _spawnEntries.Count; i++)
            {
                var entry = _spawnEntries[i];
                if (entry == null || entry.Instance == null || !entry.IsVisible)
                {
                    continue;
                }

                SetObjectVisible(entry.Instance, false);
                entry.IsVisible = false;
            }
        }

        private void RemoveEntryAt(int index)
        {
            if (index < 0 || index >= _spawnEntries.Count)
            {
                return;
            }

            var entry = _spawnEntries[index];
            ReleaseEntry(entry);

            var lastIndex = _spawnEntries.Count - 1;
            if (index != lastIndex)
            {
                _spawnEntries[index] = _spawnEntries[lastIndex];
            }

            _spawnEntries.RemoveAt(lastIndex);

            if (_updateCursor > index)
            {
                _updateCursor--;
            }

            if (_updateCursor < 0)
            {
                _updateCursor = 0;
            }
        }

        private void PositionInstanceOnTile(Transform targetTransform, Tile tile)
        {
            if (targetTransform == null || tile == null || planet == null)
            {
                return;
            }

            var worldCenter = planet.GetTileWorldSurfaceCenter(tile, spawnSurfaceOffset);
            targetTransform.position = worldCenter;

            var up = (worldCenter - planet.transform.position).normalized;
            if (up.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            targetTransform.rotation = Quaternion.FromToRotation(Vector3.up, up);
        }

        private bool IsFlightMode(Camera cameraToUse)
        {
            if (flightSkill != null)
            {
                return flightSkill.IsFlightModeActive;
            }

            if (planet == null || cameraToUse == null)
            {
                return false;
            }

            var distanceFromCenter = Vector3.Distance(cameraToUse.transform.position, planet.transform.position);
            return distanceFromCenter >= flightModeMinDistanceFromPlanetCenter;
        }

        private GreenSpawnObject PickRandomPrefab()
        {
            if (greenSpawnObjects == null || greenSpawnObjects.Count == 0)
            {
                return null;
            }

            var attempts = greenSpawnObjects.Count;
            for (var i = 0; i < attempts; i++)
            {
                var index = UnityEngine.Random.Range(0, greenSpawnObjects.Count);
                var candidate = greenSpawnObjects[index];
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private GreenSpawnObject GetInstance(GreenSpawnObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            if (enablePooling && _poolByPrefab.TryGetValue(prefab, out var pool))
            {
                while (pool.Count > 0)
                {
                    var instance = pool.Pop();
                    if (instance != null)
                    {
                        instance.transform.SetParent(spawnedRoot, true);
                        return instance;
                    }
                }
            }

            return Instantiate(prefab, spawnedRoot);
        }

        private void ReleaseEntry(SpawnEntry entry)
        {
            if (entry == null || entry.Instance == null)
            {
                return;
            }

            var instance = entry.Instance;
            SetObjectVisible(instance, false);

            if (enablePooling && entry.Prefab != null)
            {
                instance.transform.SetParent(spawnedRoot, false);
                var pool = GetPool(entry.Prefab);
                pool.Push(instance);
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance.gameObject);
            }
            else
            {
                DestroyImmediate(instance.gameObject);
            }
        }

        private Stack<GreenSpawnObject> GetPool(GreenSpawnObject prefab)
        {
            if (!_poolByPrefab.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<GreenSpawnObject>();
                _poolByPrefab[prefab] = pool;
            }

            return pool;
        }

        private void ResolveReferences()
        {
            if (planet == null)
            {
                planet = GetComponent<Planet>() ?? FindFirstObjectByType<Planet>();
            }

            if (planetCameraController == null)
            {
                planetCameraController = FindFirstObjectByType<PlanetCameraController>();
            }

            if (flightSkill == null)
            {
                flightSkill = FindFirstObjectByType<PlanetFlyTerraformSkill>();
            }

            if (controlledCamera == null)
            {
                controlledCamera = planetCameraController != null
                    ? planetCameraController.GetComponentInChildren<Camera>()
                    : Camera.main;
            }
        }

        private void EnsureSpawnRoot()
        {
            if (planet == null)
            {
                return;
            }

            if (spawnedRoot == null)
            {
                var existing = planet.transform.Find("SpawnedObjects");
                if (existing != null)
                {
                    spawnedRoot = existing;
                }
                else
                {
                    var root = new GameObject("SpawnedObjects");
                    spawnedRoot = root.transform;
                    spawnedRoot.SetParent(planet.transform, false);
                }
            }

            if (spawnedRoot != null)
            {
                spawnedRoot.localPosition = Vector3.zero;
                spawnedRoot.localRotation = Quaternion.identity;
                spawnedRoot.localScale = Vector3.one;
            }
        }

        private void ClearSpawnedEntries()
        {
            for (var i = _spawnEntries.Count - 1; i >= 0; i--)
            {
                ReleaseEntry(_spawnEntries[i]);
            }

            _spawnEntries.Clear();
            _updateCursor = 0;
        }

        private static void SetObjectVisible(GreenSpawnObject instance, bool isVisible)
        {
            if (instance == null)
            {
                return;
            }

            var gameObject = instance.gameObject;
            if (gameObject.activeSelf != isVisible)
            {
                gameObject.SetActive(isVisible);
            }
        }

        private static void DisableColliders(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled)
                {
                    colliders[i].enabled = false;
                }
            }
        }
    }
}
