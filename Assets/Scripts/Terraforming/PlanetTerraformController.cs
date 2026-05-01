using System;
using System.Collections.Generic;
using UnityEngine;

namespace SGJ26.Terraforming
{
    [RequireComponent(typeof(MeshFilter))]
    public sealed class PlanetTerraformController : MonoBehaviour
    {
        [Serializable]
        private struct ProbeSource
        {
            public Vector3 localPosition;
            public float currentRadius;
            public float maxRadius;
            public float waveSpeed;
            public float strength;
            public float remainingSeconds;
        }

        [Header("References")]
        [SerializeField] private MeshFilter targetMeshFilter;
        [SerializeField] private PlanetSurfaceData surfaceData;
        [SerializeField] private bool applyVertexColorMaterialOnInitialize = true;
        [SerializeField] private Vector3 localCenterOffset;

        [Header("Simulation")]
        [SerializeField, Min(0.02f)] private float simulationStep = 0.12f;
        [SerializeField, Min(0f)] private float passiveGrowthPerSecond;
        [SerializeField, Min(0f)] private float spreadGrowthPerSecond = 0.28f;
        [SerializeField, Range(0f, 1f)] private float minimumNeighborLifeToSpread = 0.18f;
        [SerializeField, Min(0f)] private float defaultProbeRadius = 1.1f;
        [SerializeField, Min(0f)] private float defaultProbeStrength = 0.55f;
        [SerializeField, Min(0f)] private float defaultProbeDuration = 8f;
        [SerializeField, Min(0.01f)] private float defaultProbeMaxRadius = 9f;
        [SerializeField, Min(0.01f)] private float defaultProbeWaveSpeed = 2.4f;

        [Header("Colors")]
        [SerializeField] private Color dryColor = new Color(0.60f, 0.42f, 0.26f, 1f);
        [SerializeField] private Color rockColor = new Color(0.27f, 0.25f, 0.23f, 1f);
        [SerializeField] private Color mossColor = new Color(0.42f, 0.58f, 0.25f, 1f);
        [SerializeField] private Color grassColor = new Color(0.28f, 0.72f, 0.24f, 1f);
        [SerializeField] private Color lushColor = new Color(0.10f, 0.42f, 0.12f, 1f);
        [SerializeField] private Color waterColor = new Color(0.16f, 0.55f, 0.82f, 1f);
        [SerializeField] private Color sandColor = new Color(0.78f, 0.66f, 0.43f, 1f);
        [SerializeField, Range(0f, 1f)] private float waterLevelRadius01 = 0.42f;
        [SerializeField, Range(0f, 0.25f)] private float shoreWidth01 = 0.06f;
        [SerializeField, Range(0f, 1f)] private float waterRevealLife = 0.52f;
        [SerializeField, Range(0f, 0.5f)] private float dryBrightnessVariation = 0.18f;
        [SerializeField, Range(0f, 0.5f)] private float greenBrightnessVariation = 0.22f;
        [SerializeField, Range(0f, 0.5f)] private float greenPatchVariation = 0.28f;
        [SerializeField, Range(0f, 1f)] private float dryRockPatchChance = 0.22f;
        [SerializeField, Min(0.05f)] private float directPaintCellSize = 1f;
        [SerializeField, Range(0f, 1f)] private float mountainStartHeight01 = 0.72f;
        [SerializeField, Range(0f, 1f)] private float snowStartHeight01 = 0.88f;
        [SerializeField] private Color snowColor = new Color(0.92f, 0.94f, 0.90f, 1f);
        [SerializeField, Range(0f, 2f)] private float materialSaturation = 1.25f;
        [SerializeField, Range(0f, 2f)] private float materialContrast = 1.12f;
        [SerializeField, Range(0f, 2f)] private float materialExposure = 1.05f;
        [SerializeField, Range(0f, 2f)] private float materialAmbientStrength = 0.55f;
        [SerializeField, Range(0f, 2f)] private float materialLightStrength = 1.15f;
        [SerializeField, Range(0f, 1f)] private float highlightRemainingAfterProgress = 0.88f;
        [SerializeField, Range(0f, 1f)] private float highlightRemainingBelowLife = 0.35f;
        [SerializeField] private Color remainingHighlightColor = new Color(1f, 0.92f, 0.22f, 1f);
        [SerializeField] private bool previewTerraformCompleteInEditor;
        [SerializeField] private bool logDebug;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Saturation = Shader.PropertyToID("_Saturation");
        private static readonly int Contrast = Shader.PropertyToID("_Contrast");
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int AmbientStrength = Shader.PropertyToID("_AmbientStrength");
        private static readonly int LightStrength = Shader.PropertyToID("_LightStrength");

        private readonly List<ProbeSource> probes = new();
        private Mesh runtimeMesh;
        private Vector3[] runtimeVertices = Array.Empty<Vector3>();
        private Color32[] vertexColors = Array.Empty<Color32>();
        private float[] vertexLife = Array.Empty<float>();
        private Vector3Int[] runtimeVertexCells = Array.Empty<Vector3Int>();
        private readonly Dictionary<Vector3Int, List<int>> directCellVertices = new();
        private readonly Dictionary<Vector3Int, float> directCellLife = new();
        private float[] life = Array.Empty<float>();
        private float[] nextLife = Array.Empty<float>();
        private bool[] dirtyCells = Array.Empty<bool>();
        private bool initialized;
        private bool editorPreviewApplied;
        private Mesh editorPreviewMesh;
        private Mesh editorOriginalMesh;
        private Material[] editorOriginalMaterials;
        private float stepTimer;
        private float highlightRefreshTimer;
        private Vector3 directCenter;
        private float directMinRadius;
        private float directMaxRadius = 1f;

        public PlanetSurfaceData SurfaceData
        {
            get => surfaceData;
            set
            {
                surfaceData = value;
                Initialize();
            }
        }

        public float TerraformProgress
        {
            get
            {
                if (life == null || life.Length == 0)
                    return 0f;

                float sum = 0f;
                for (int i = 0; i < life.Length; i++)
                    sum += life[i];

                return sum / life.Length;
            }
        }

        public float DirectTerraformProgress
        {
            get
            {
                if (vertexLife == null || vertexLife.Length == 0)
                    return 0f;

                float sum = 0f;
                for (int i = 0; i < vertexLife.Length; i++)
                    sum += vertexLife[i];

                return sum / vertexLife.Length;
            }
        }

        public float VisibleTerraformProgress => Mathf.Max(TerraformProgress, DirectTerraformProgress);
        public Vector3 LocalCenter => directCenter + localCenterOffset;
        public Vector3 WorldCenter => transform.TransformPoint(LocalCenter);
        public float WorldSurfaceRadius => directMaxRadius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        private void Reset()
        {
            targetMeshFilter = GetComponent<MeshFilter>();
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null)
                Destroy(runtimeMesh);

            ClearEditorPreview();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (previewTerraformCompleteInEditor)
                ApplyEditorPreview();
            else
                ClearEditorPreview();
        }

        private void Update()
        {
            if (!initialized)
                return;

            stepTimer += Time.deltaTime;
            if (stepTimer < simulationStep)
                return;

            float dt = stepTimer;
            stepTimer = 0f;
            Simulate(dt);
            RefreshRemainingHighlight(dt);
        }

        public void DropProbe(Vector3 worldPosition)
        {
            DropProbe(worldPosition, defaultProbeRadius, defaultProbeStrength, defaultProbeDuration);
        }

        public void DropProbe(Vector3 worldPosition, float radius, float strength, float duration)
        {
            if (!initialized)
                Initialize();

            if (!initialized)
            {
                if (logDebug)
                    Debug.LogWarning("[PlanetTerraformController] DropProbe ignored: controller is not initialized.", this);

                return;
            }

            probes.Add(new ProbeSource
            {
                localPosition = transform.InverseTransformPoint(worldPosition),
                currentRadius = Mathf.Max(0.01f, radius),
                maxRadius = Mathf.Max(radius, defaultProbeMaxRadius),
                waveSpeed = Mathf.Max(0.01f, defaultProbeWaveSpeed),
                strength = Mathf.Max(0f, strength),
                remainingSeconds = Mathf.Max(0.01f, duration)
            });

            ApplyLocalBrush(transform.InverseTransformPoint(worldPosition), Mathf.Max(0.01f, radius), strength);
            ApplyDirtyVertexColors(false);

        }

        public void PaintImmediate(Vector3 worldPosition, float radius, float amount)
        {
            if (!initialized)
                Initialize();

            if (!initialized)
            {
                if (logDebug)
                    Debug.LogWarning("[PlanetTerraformController] PaintImmediate ignored: controller is not initialized.", this);

                return;
            }

            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            ApplyLocalBrush(localPosition, Mathf.Max(0.01f, radius), amount);
            ApplyDirtyVertexColors(false);

        }

        public void PaintVerticesImmediate(Vector3 worldPosition, float radius, float amount)
        {
            if (!initialized)
                Initialize();

            if (!initialized)
                return;

            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            float radiusSq = radius * radius;
            bool changed = false;
            HashSet<Vector3Int> touchedCells = new();

            for (int i = 0; i < runtimeVertices.Length; i++)
            {
                float distanceSq = (runtimeVertices[i] - localPosition).sqrMagnitude;
                if (distanceSq > radiusSq)
                    continue;

                touchedCells.Add(runtimeVertexCells[i]);
            }

            foreach (Vector3Int cell in touchedCells)
            {
                Vector3 cellCenter = GetDirectCellCenter(cell);
                float distanceSq = (cellCenter - localPosition).sqrMagnitude;
                if (distanceSq > radiusSq)
                    continue;

                float falloff = 1f - Mathf.Sqrt(distanceSq / radiusSq);
                directCellLife.TryGetValue(cell, out float previousLife);
                float newLife = Mathf.Clamp01(previousLife + amount * Mathf.Max(0.25f, falloff));
                directCellLife[cell] = newLife;

                if (!directCellVertices.TryGetValue(cell, out List<int> indices))
                    continue;

                Color color = ApplyRemainingHighlight(EvaluateDirectVertexColor(cellCenter, newLife), newLife);
                Color32 color32 = color;
                for (int i = 0; i < indices.Count; i++)
                {
                    int vertexIndex = indices[i];
                    vertexLife[vertexIndex] = newLife;
                    vertexColors[vertexIndex] = color32;
                }

                changed = true;
            }

            if (changed)
                runtimeMesh.colors32 = vertexColors;
        }

        [ContextMenu("Fill Terraforming 100%")]
        public void FillTerraformingComplete()
        {
            if (!initialized)
                Initialize();

            if (!initialized)
                return;

            for (int i = 0; i < life.Length; i++)
            {
                life[i] = 1f;
                dirtyCells[i] = true;
            }

            RecalculateDirectRadiusRange();

            for (int i = 0; i < vertexLife.Length; i++)
            {
                vertexLife[i] = 1f;
                vertexColors[i] = EvaluateDirectVertexColor(runtimeVertices[i], 1f);
            }

            directCellLife.Clear();
            foreach (Vector3Int cell in directCellVertices.Keys)
                directCellLife[cell] = 1f;

            runtimeMesh.colors32 = vertexColors;
        }

        private void Initialize()
        {
            targetMeshFilter ??= GetComponent<MeshFilter>();

            if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null || surfaceData == null || surfaceData.CellCount == 0)
            {
                initialized = false;

                if (logDebug)
                {
                    string meshState = targetMeshFilter != null && targetMeshFilter.sharedMesh != null ? targetMeshFilter.sharedMesh.name : "null";
                    string dataState = surfaceData != null ? surfaceData.CellCount.ToString() : "null";
                    Debug.LogWarning($"[PlanetTerraformController] Initialize failed. mesh={meshState}, surfaceCells={dataState}", this);
                }

                return;
            }

            if (runtimeMesh != null)
                Destroy(runtimeMesh);

            runtimeMesh = Instantiate(targetMeshFilter.sharedMesh);
            runtimeMesh.name = targetMeshFilter.sharedMesh.name + " Terraform Runtime";
            runtimeMesh.MarkDynamic();
            targetMeshFilter.sharedMesh = runtimeMesh;

            if (applyVertexColorMaterialOnInitialize)
                ApplyVertexColorMaterial();

            int vertexCount = runtimeMesh.vertexCount;
            runtimeVertices = runtimeMesh.vertices;
            CalculateDirectVertexHeightRange();
            vertexColors = new Color32[vertexCount];
            vertexLife = new float[vertexCount];
            BuildDirectPaintCells();
            life = new float[surfaceData.CellCount];
            nextLife = new float[surfaceData.CellCount];
            dirtyCells = new bool[surfaceData.CellCount];
            initialized = true;

            for (int i = 0; i < dirtyCells.Length; i++)
                dirtyCells[i] = true;

            ApplyDirtyVertexColors(true);

        }

        private void ApplyVertexColorMaterial()
        {
            Renderer targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                if (logDebug)
                    Debug.LogWarning("[PlanetTerraformController] Vertex color material was not applied: no Renderer found.", this);

                return;
            }

            Shader shader = Shader.Find("SGJ26/Terraforming/Vertex Color URP");
            if (shader == null)
            {
                if (logDebug)
                    Debug.LogWarning("[PlanetTerraformController] Vertex color material was not applied: shader not found.", this);

                return;
            }

            Material material = new Material(shader)
            {
                name = "Planet Vertex Color Runtime"
            };
            ConfigureVertexColorMaterial(material);
            targetRenderer.sharedMaterial = material;
        }

        private void Simulate(float dt)
        {
            var cells = surfaceData.Cells;
            var neighbors = surfaceData.NeighborIndices;
            bool changed = false;

            for (int i = probes.Count - 1; i >= 0; i--)
            {
                ProbeSource probe = probes[i];
                probe.remainingSeconds -= dt;
                probe.currentRadius = Mathf.Min(probe.maxRadius, probe.currentRadius + probe.waveSpeed * dt);
                probes[i] = probe;

                if (probe.remainingSeconds <= 0f || probe.currentRadius >= probe.maxRadius)
                    probes.RemoveAt(i);
            }

            for (int i = 0; i < cells.Length; i++)
            {
                float growth = passiveGrowthPerSecond * dt;
                PlanetSurfaceData.SurfaceCell cell = cells[i];

                if (cell.neighborCount > 0)
                {
                    float neighborSum = 0f;
                    int activeNeighbors = 0;
                    int end = cell.neighborStart + cell.neighborCount;

                    for (int n = cell.neighborStart; n < end; n++)
                    {
                        float neighborLife = life[neighbors[n]];
                        if (neighborLife < minimumNeighborLifeToSpread)
                            continue;

                        neighborSum += neighborLife;
                        activeNeighbors++;
                    }

                    if (activeNeighbors > 0)
                    {
                        float averageNeighborLife = neighborSum / activeNeighbors;
                        growth += averageNeighborLife * spreadGrowthPerSecond * dt;
                    }
                }

                for (int p = 0; p < probes.Count; p++)
                {
                    ProbeSource probe = probes[p];
                    float radiusSq = probe.currentRadius * probe.currentRadius;
                    float distanceSq = (cell.localPosition - probe.localPosition).sqrMagnitude;

                    if (distanceSq <= radiusSq)
                    {
                        float falloff = 1f - Mathf.Sqrt(distanceSq / radiusSq);
                        float waveEdge = Mathf.InverseLerp(probe.currentRadius, probe.currentRadius * 0.65f, Mathf.Sqrt(distanceSq));
                        growth += probe.strength * Mathf.Max(falloff * 0.2f, waveEdge) * dt;
                    }
                }

                float newLife = Mathf.Clamp01(life[i] + growth);
                nextLife[i] = newLife;
                if (!Mathf.Approximately(newLife, life[i]))
                {
                    dirtyCells[i] = true;
                    changed = true;
                }
            }

            if (!changed)
                return;

            Array.Copy(nextLife, life, life.Length);
            ApplyDirtyVertexColors(false);
        }

        private void ApplyLocalBrush(Vector3 localPosition, float radius, float amount)
        {
            var cells = surfaceData.Cells;
            float radiusSq = radius * radius;

            for (int i = 0; i < cells.Length; i++)
            {
                float distanceSq = (cells[i].localPosition - localPosition).sqrMagnitude;
                if (distanceSq > radiusSq)
                    continue;

                float falloff = 1f - Mathf.Sqrt(distanceSq / radiusSq);
                life[i] = Mathf.Clamp01(life[i] + amount * falloff);
                dirtyCells[i] = true;
            }
        }

        private void ApplyDirtyVertexColors(bool forceAll)
        {
            if (runtimeMesh == null || surfaceData == null)
                return;

            var cells = surfaceData.Cells;
            var vertices = surfaceData.VertexIndices;

            if (forceAll)
            {
                Color32 dead = dryColor;
                for (int i = 0; i < vertexColors.Length; i++)
                    vertexColors[i] = dead;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                if (!forceAll && !dirtyCells[i])
                    continue;

                Color color = ApplyRemainingHighlight(EvaluateCellColor(cells[i], life[i]), life[i]);
                Color32 color32 = color;
                int end = cells[i].vertexStart + cells[i].vertexCount;

                for (int v = cells[i].vertexStart; v < end; v++)
                {
                    int vertexIndex = vertices[v];
                    if ((uint)vertexIndex < (uint)vertexColors.Length)
                        vertexColors[vertexIndex] = color32;
                }

                dirtyCells[i] = false;
            }

            runtimeMesh.colors32 = vertexColors;
        }

        private Color EvaluateCellColor(PlanetSurfaceData.SurfaceCell cell, float amount)
        {
            float noise = Hash01(cell.localPosition);
            float secondNoise = Hash01(cell.localPosition + new Vector3(13.1f, 7.7f, 31.3f));
            float dryBrightness = Mathf.Lerp(1f - dryBrightnessVariation, 1f + dryBrightnessVariation, noise);
            bool rockPatch = cell.height01 > mountainStartHeight01 || secondNoise < dryRockPatchChance;
            Color dryBase = rockPatch ? Color.Lerp(dryColor, rockColor, Mathf.Lerp(0.25f, 0.75f, secondNoise)) : dryColor;
            Color dead = MultiplyRgb(dryBase, dryBrightness);
            ApplyMountainTint(cell.height01, noise, ref dead);
            bool isBelowWaterRadius = cell.height01 <= waterLevelRadius01;
            bool isShore = !isBelowWaterRadius && cell.height01 <= waterLevelRadius01 + shoreWidth01;

            if (isShore)
            {
                float shoreT = Mathf.InverseLerp(waterLevelRadius01 + shoreWidth01, waterLevelRadius01, cell.height01);
                dead = Color.Lerp(dead, MultiplyRgb(sandColor, dryBrightness), Mathf.Lerp(0.45f, 0.9f, shoreT));
            }

            if (isBelowWaterRadius)
            {
                Color wetSand = MultiplyRgb(sandColor, Mathf.Lerp(0.85f, 1.1f, noise));
                if (amount <= waterRevealLife)
                    return Color.Lerp(dead, wetSand, Mathf.InverseLerp(0f, waterRevealLife, amount));

                float waterT = Mathf.InverseLerp(waterRevealLife, 0.90f, amount);
                return Color.Lerp(wetSand, waterColor, waterT);
            }

            if (cell.height01 >= mountainStartHeight01)
                return dead;

            float greenBrightness = Mathf.Lerp(1f - greenBrightnessVariation, 1f + greenBrightnessVariation, noise);
            float greenPatch = Hash01(cell.localPosition + new Vector3(91.7f, 17.2f, 48.4f));
            Color localMoss = Color.Lerp(mossColor, grassColor, greenPatch * greenPatchVariation);
            Color localGrass = Color.Lerp(grassColor, lushColor, Mathf.Clamp01((greenPatch - 0.35f) * greenPatchVariation * 2.2f));
            Color localLush = Color.Lerp(lushColor, grassColor, Mathf.Clamp01((1f - greenPatch) * greenPatchVariation));
            Color variedMoss = MultiplyRgb(localMoss, greenBrightness);
            Color variedGrass = MultiplyRgb(localGrass, greenBrightness);
            Color variedLush = MultiplyRgb(localLush, greenBrightness);

            if (isShore)
            {
                Color shoreSand = MultiplyRgb(sandColor, Mathf.Lerp(0.9f, 1.15f, noise));
                Color shoreGreen = Color.Lerp(shoreSand, variedGrass, 0.32f);

                if (amount < 0.45f)
                    return Color.Lerp(dead, shoreSand, Mathf.InverseLerp(0f, 0.45f, amount));

                return Color.Lerp(shoreSand, shoreGreen, Mathf.InverseLerp(0.45f, 1f, amount));
            }

            if (amount < 0.25f)
            {
                return Color.Lerp(dead, variedMoss, amount / 0.25f);
            }

            if (amount < 0.65f)
            {
                return Color.Lerp(variedMoss, variedGrass, Mathf.InverseLerp(0.25f, 0.65f, amount));
            }

            return Color.Lerp(variedGrass, variedLush, Mathf.InverseLerp(0.65f, 1f, amount));
        }

        private Color EvaluateDirectVertexColor(Vector3 localPosition, float amount)
        {
            float height01 = Mathf.InverseLerp(directMinRadius, directMaxRadius, (localPosition - LocalCenter).magnitude);

            float noise = Hash01(localPosition);
            float secondNoise = Hash01(localPosition + new Vector3(13.1f, 7.7f, 31.3f));
            float dryBrightness = Mathf.Lerp(1f - dryBrightnessVariation, 1f + dryBrightnessVariation, noise);
            bool rockPatch = height01 > mountainStartHeight01 || secondNoise < dryRockPatchChance;
            Color dryBase = rockPatch ? Color.Lerp(dryColor, rockColor, Mathf.Lerp(0.25f, 0.75f, secondNoise)) : dryColor;
            Color dead = MultiplyRgb(dryBase, dryBrightness);
            ApplyMountainTint(height01, noise, ref dead);
            bool isBelowWaterRadius = height01 <= waterLevelRadius01;
            bool isShore = !isBelowWaterRadius && height01 <= waterLevelRadius01 + shoreWidth01;

            if (isShore)
            {
                float shoreT = Mathf.InverseLerp(waterLevelRadius01 + shoreWidth01, waterLevelRadius01, height01);
                dead = Color.Lerp(dead, MultiplyRgb(sandColor, dryBrightness), Mathf.Lerp(0.45f, 0.9f, shoreT));
            }

            if (isBelowWaterRadius)
            {
                Color wetSand = MultiplyRgb(sandColor, Mathf.Lerp(0.85f, 1.1f, noise));
                if (amount <= waterRevealLife)
                    return Color.Lerp(dead, wetSand, Mathf.InverseLerp(0f, waterRevealLife, amount));

                float waterT = Mathf.InverseLerp(waterRevealLife, 0.90f, amount);
                return Color.Lerp(wetSand, waterColor, waterT);
            }

            if (height01 >= mountainStartHeight01)
                return dead;

            float greenBrightness = Mathf.Lerp(1f - greenBrightnessVariation, 1f + greenBrightnessVariation, noise);
            float greenPatch = Hash01(localPosition + new Vector3(91.7f, 17.2f, 48.4f));
            Color localMoss = Color.Lerp(mossColor, grassColor, greenPatch * greenPatchVariation);
            Color localGrass = Color.Lerp(grassColor, lushColor, Mathf.Clamp01((greenPatch - 0.35f) * greenPatchVariation * 2.2f));
            Color localLush = Color.Lerp(lushColor, grassColor, Mathf.Clamp01((1f - greenPatch) * greenPatchVariation));
            Color variedMoss = MultiplyRgb(localMoss, greenBrightness);
            Color variedGrass = MultiplyRgb(localGrass, greenBrightness);
            Color variedLush = MultiplyRgb(localLush, greenBrightness);

            if (isShore)
            {
                Color shoreSand = MultiplyRgb(sandColor, Mathf.Lerp(0.9f, 1.15f, noise));
                Color shoreGreen = Color.Lerp(shoreSand, variedGrass, 0.32f);

                if (amount < 0.45f)
                    return Color.Lerp(dead, shoreSand, Mathf.InverseLerp(0f, 0.45f, amount));

                return Color.Lerp(shoreSand, shoreGreen, Mathf.InverseLerp(0.45f, 1f, amount));
            }

            if (amount < 0.25f)
            {
                return Color.Lerp(dead, variedMoss, amount / 0.25f);
            }

            if (amount < 0.65f)
            {
                return Color.Lerp(variedMoss, variedGrass, Mathf.InverseLerp(0.25f, 0.65f, amount));
            }

            return Color.Lerp(variedGrass, variedLush, Mathf.InverseLerp(0.65f, 1f, amount));
        }

        private static Color MultiplyRgb(Color color, float multiplier)
        {
            color.r = Mathf.Clamp01(color.r * multiplier);
            color.g = Mathf.Clamp01(color.g * multiplier);
            color.b = Mathf.Clamp01(color.b * multiplier);
            return color;
        }

        private void ApplyMountainTint(float height01, float noise, ref Color color)
        {
            if (height01 < mountainStartHeight01)
                return;

            float mountainT = Mathf.InverseLerp(mountainStartHeight01, 1f, height01);
            Color mountainColor = MultiplyRgb(rockColor, Mathf.Lerp(0.78f, 1.18f, noise));
            color = Color.Lerp(color, mountainColor, Mathf.Clamp01(mountainT * 1.35f));

            if (height01 < snowStartHeight01)
                return;

            float snowT = Mathf.InverseLerp(snowStartHeight01, 1f, height01);
            Color variedSnow = MultiplyRgb(snowColor, Mathf.Lerp(0.88f, 1.08f, noise));
            color = Color.Lerp(color, variedSnow, Mathf.SmoothStep(0f, 1f, snowT));
        }

        private static float Hash01(Vector3 value)
        {
            float dot = Vector3.Dot(value, new Vector3(12.9898f, 78.233f, 37.719f));
            return Mathf.Repeat(Mathf.Sin(dot) * 43758.5453f, 1f);
        }

        private void ApplyEditorPreview()
        {
            targetMeshFilter ??= GetComponent<MeshFilter>();
            Renderer targetRenderer = GetComponent<Renderer>();

            if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null || targetRenderer == null)
                return;

            if (!editorPreviewApplied)
            {
                editorOriginalMesh = targetMeshFilter.sharedMesh;
                editorOriginalMaterials = targetRenderer.sharedMaterials;
            }

            if (editorPreviewMesh != null)
                DestroyImmediate(editorPreviewMesh);

            editorPreviewMesh = Instantiate(editorOriginalMesh);
            editorPreviewMesh.name = editorOriginalMesh.name + " Terraform Preview";
            runtimeMesh = editorPreviewMesh;
            runtimeVertices = editorPreviewMesh.vertices;
            vertexColors = new Color32[editorPreviewMesh.vertexCount];
            vertexLife = new float[editorPreviewMesh.vertexCount];
            CalculateDirectVertexHeightRange();
            RecalculateDirectRadiusRange();

            for (int i = 0; i < runtimeVertices.Length; i++)
            {
                vertexLife[i] = 1f;
                vertexColors[i] = EvaluateDirectVertexColor(runtimeVertices[i], 1f);
            }

            editorPreviewMesh.colors32 = vertexColors;
            targetMeshFilter.sharedMesh = editorPreviewMesh;

            Shader shader = Shader.Find("SGJ26/Terraforming/Vertex Color URP");
            if (shader != null)
            {
                Material material = new(shader)
                {
                    name = "Planet Vertex Color Preview"
                };
                ConfigureVertexColorMaterial(material);
                targetRenderer.sharedMaterial = material;
            }

            editorPreviewApplied = true;
        }

        private void ClearEditorPreview()
        {
            if (Application.isPlaying || !editorPreviewApplied)
                return;

            targetMeshFilter ??= GetComponent<MeshFilter>();
            Renderer targetRenderer = GetComponent<Renderer>();

            if (targetMeshFilter != null && editorOriginalMesh != null)
                targetMeshFilter.sharedMesh = editorOriginalMesh;

            if (targetRenderer != null && editorOriginalMaterials != null)
                targetRenderer.sharedMaterials = editorOriginalMaterials;

            if (editorPreviewMesh != null)
            {
                DestroyImmediate(editorPreviewMesh);
                editorPreviewMesh = null;
            }

            runtimeMesh = null;
            runtimeVertices = Array.Empty<Vector3>();
            vertexColors = Array.Empty<Color32>();
            vertexLife = Array.Empty<float>();
            editorOriginalMesh = null;
            editorOriginalMaterials = null;
            editorPreviewApplied = false;
        }

        private void ConfigureVertexColorMaterial(Material material)
        {
            material.SetColor(BaseColor, Color.white);
            material.SetFloat(Saturation, materialSaturation);
            material.SetFloat(Contrast, materialContrast);
            material.SetFloat(Exposure, materialExposure);
            material.SetFloat(AmbientStrength, materialAmbientStrength);
            material.SetFloat(LightStrength, materialLightStrength);
        }

        private Color ApplyRemainingHighlight(Color color, float amount)
        {
            if (VisibleTerraformProgress < highlightRemainingAfterProgress || amount >= highlightRemainingBelowLife)
                return color;

            float pulse = Application.isPlaying ? Mathf.Lerp(0.35f, 0.75f, Mathf.PingPong(Time.time * 2.5f, 1f)) : 0.55f;
            return Color.Lerp(color, remainingHighlightColor, pulse);
        }

        private void RefreshRemainingHighlight(float dt)
        {
            if (VisibleTerraformProgress < highlightRemainingAfterProgress || runtimeMesh == null || runtimeVertices.Length == 0)
                return;

            highlightRefreshTimer += dt;
            if (highlightRefreshTimer < 0.2f)
                return;

            highlightRefreshTimer = 0f;

            for (int i = 0; i < runtimeVertices.Length; i++)
                vertexColors[i] = ApplyRemainingHighlight(EvaluateDirectVertexColor(runtimeVertices[i], vertexLife[i]), vertexLife[i]);

            runtimeMesh.colors32 = vertexColors;
        }

        private void CalculateDirectVertexHeightRange()
        {
            directCenter = Vector3.zero;

            for (int i = 0; i < runtimeVertices.Length; i++)
                directCenter += runtimeVertices[i];

            if (runtimeVertices.Length > 0)
                directCenter /= runtimeVertices.Length;

            RecalculateDirectRadiusRange();
        }

        private void RecalculateDirectRadiusRange()
        {
            directMinRadius = float.PositiveInfinity;
            directMaxRadius = 0f;
            Vector3 center = LocalCenter;

            for (int i = 0; i < runtimeVertices.Length; i++)
            {
                float radius = (runtimeVertices[i] - center).magnitude;
                directMinRadius = Mathf.Min(directMinRadius, radius);
                directMaxRadius = Mathf.Max(directMaxRadius, radius);
            }

            if (!float.IsFinite(directMinRadius) || directMaxRadius <= directMinRadius)
            {
                Bounds bounds = surfaceData != null ? surfaceData.LocalBounds : runtimeMesh.bounds;
                directCenter = bounds.center;
                directMinRadius = 0f;
                directMaxRadius = Mathf.Max(1f, bounds.extents.magnitude);
            }
        }

        private void BuildDirectPaintCells()
        {
            runtimeVertexCells = new Vector3Int[runtimeVertices.Length];
            directCellVertices.Clear();
            directCellLife.Clear();
            float cellSize = Mathf.Max(0.05f, directPaintCellSize);

            for (int i = 0; i < runtimeVertices.Length; i++)
            {
                Vector3 vertex = runtimeVertices[i];
                Vector3Int cell = new(
                    Mathf.FloorToInt(vertex.x / cellSize),
                    Mathf.FloorToInt(vertex.y / cellSize),
                    Mathf.FloorToInt(vertex.z / cellSize));

                runtimeVertexCells[i] = cell;

                if (!directCellVertices.TryGetValue(cell, out List<int> indices))
                {
                    indices = new List<int>(8);
                    directCellVertices.Add(cell, indices);
                }

                indices.Add(i);
            }
        }

        private Vector3 GetDirectCellCenter(Vector3Int cell)
        {
            float cellSize = Mathf.Max(0.05f, directPaintCellSize);
            return new Vector3(
                (cell.x + 0.5f) * cellSize,
                (cell.y + 0.5f) * cellSize,
                (cell.z + 0.5f) * cellSize);
        }
    }
}
