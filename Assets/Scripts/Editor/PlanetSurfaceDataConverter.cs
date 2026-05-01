using System.Collections.Generic;
using SGJ26.Terraforming;
using UnityEditor;
using UnityEngine;
using VoxelImporter;

namespace SGJ26.EditorTools
{
    public static class PlanetSurfaceDataConverter
    {
        private const string MenuPath = "Tools/SGJ26/Terraforming/Create Surface Data From Selected Voxel Planet";

        [MenuItem(MenuPath, true)]
        private static bool ValidateCreateFromSelection()
        {
            return TryResolveSelection(out _, out _);
        }

        [MenuItem(MenuPath)]
        private static void CreateFromSelection()
        {
            if (!TryResolveSelection(out Mesh mesh, out VoxelStructure structure))
            {
                EditorUtility.DisplayDialog(
                    "Planet Surface Data",
                    "Select an imported .vox prefab, a scene instance of it, or one of its generated sub-assets. The .vox importer must have Output > Voxel Structure enabled, then reimported.",
                    "OK");
                return;
            }

            string baseName = Selection.activeObject != null ? Selection.activeObject.name : "Planet";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Planet Surface Data",
                baseName + "_SurfaceData",
                "asset",
                "Choose where to save generated planet surface data.",
                "Assets");

            if (string.IsNullOrEmpty(path))
                return;

            PlanetSurfaceData data = BuildSurfaceData(mesh, structure);
            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(data);
        }

        private static bool TryResolveSelection(out Mesh mesh, out VoxelStructure structure)
        {
            mesh = null;
            structure = null;

            Object selected = Selection.activeObject;
            GameObject selectedGameObject = Selection.activeGameObject;

            if (selectedGameObject != null)
            {
                MeshFilter meshFilter = selectedGameObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                    mesh = meshFilter.sharedMesh;

                VoxelBase voxelBase = selectedGameObject.GetComponent<VoxelBase>();
                if (voxelBase != null)
                    structure = voxelBase.voxelStructure;

                if (structure == null)
                {
                    GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(selectedGameObject);
                    structure = FindStructureInAsset(prefabSource);
                }
            }

            if (mesh == null)
                mesh = selected as Mesh;

            if (structure == null)
                structure = selected as VoxelStructure;

            if (structure == null)
                structure = FindStructureInAsset(selected);

            if (structure == null && mesh != null)
                structure = FindStructureInAsset(mesh);

            if (mesh == null && selected != null)
                mesh = FindMeshInAsset(selected);

            return mesh != null && structure != null && structure.voxels != null && structure.voxels.Length > 0;
        }

        private static VoxelStructure FindStructureInAsset(Object assetObject)
        {
            if (assetObject == null)
                return null;

            string path = AssetDatabase.GetAssetPath(assetObject);
            if (string.IsNullOrEmpty(path))
                return null;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is VoxelStructure structure)
                    return structure;
            }

            return null;
        }

        private static Mesh FindMeshInAsset(Object assetObject)
        {
            if (assetObject == null)
                return null;

            string path = AssetDatabase.GetAssetPath(assetObject);
            if (string.IsNullOrEmpty(path))
                return null;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Mesh mesh)
                    return mesh;
            }

            return null;
        }

        private static PlanetSurfaceData BuildSurfaceData(Mesh mesh, VoxelStructure structure)
        {
            Bounds bounds = mesh.bounds;
            Vector3Int voxelSize = new Vector3Int(
                Mathf.Max(1, structure.voxelSize.x),
                Mathf.Max(1, structure.voxelSize.y),
                Mathf.Max(1, structure.voxelSize.z));

            var surfaceVoxels = new List<VoxelStructure.Voxel>();
            var cellByVoxel = new Dictionary<long, int>();

            for (int i = 0; i < structure.voxels.Length; i++)
            {
                VoxelStructure.Voxel voxel = structure.voxels[i];
                if ((int)voxel.visible == 0)
                    continue;

                int cellIndex = surfaceVoxels.Count;
                surfaceVoxels.Add(voxel);
                cellByVoxel[Pack(voxel.x, voxel.y, voxel.z)] = cellIndex;
            }

            var cells = new PlanetSurfaceData.SurfaceCell[surfaceVoxels.Count];
            var neighborIndices = new List<int>(surfaceVoxels.Count * 8);
            var verticesByCell = new List<int>[surfaceVoxels.Count];
            Vector3 center = bounds.center;

            float minRadius = float.PositiveInfinity;
            float maxRadius = 0f;
            var localPositions = new Vector3[surfaceVoxels.Count];

            for (int i = 0; i < surfaceVoxels.Count; i++)
            {
                VoxelStructure.Voxel voxel = surfaceVoxels[i];
                Vector3 normalized = new Vector3(
                    (voxel.x + 0.5f) / voxelSize.x,
                    (voxel.y + 0.5f) / voxelSize.y,
                    (voxel.z + 0.5f) / voxelSize.z);

                Vector3 localPosition = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, normalized.x),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, normalized.y),
                    Mathf.Lerp(bounds.min.z, bounds.max.z, normalized.z));

                localPositions[i] = localPosition;
                float radius = (localPosition - center).magnitude;
                minRadius = Mathf.Min(minRadius, radius);
                maxRadius = Mathf.Max(maxRadius, radius);
                verticesByCell[i] = new List<int>(8);
            }

            AssignMeshVertices(mesh.vertices, bounds, voxelSize, cellByVoxel, verticesByCell);

            for (int i = 0; i < surfaceVoxels.Count; i++)
            {
                VoxelStructure.Voxel voxel = surfaceVoxels[i];
                int neighborStart = neighborIndices.Count;

                for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0)
                        continue;

                    if (cellByVoxel.TryGetValue(Pack(voxel.x + dx, voxel.y + dy, voxel.z + dz), out int neighborIndex))
                        neighborIndices.Add(neighborIndex);
                }

                Vector3 normal = (localPositions[i] - center).normalized;
                float height01 = Mathf.InverseLerp(minRadius, maxRadius, (localPositions[i] - center).magnitude);

                cells[i] = new PlanetSurfaceData.SurfaceCell
                {
                    localPosition = localPositions[i],
                    normal = normal.sqrMagnitude > 0f ? normal : Vector3.up,
                    height01 = height01,
                    palette = voxel.palette,
                    waterCandidate = height01 < 0.33f,
                    vertexStart = 0,
                    vertexCount = 0,
                    neighborStart = neighborStart,
                    neighborCount = neighborIndices.Count - neighborStart
                };
            }

            var vertexIndices = new List<int>(mesh.vertexCount);
            for (int i = 0; i < cells.Length; i++)
            {
                PlanetSurfaceData.SurfaceCell cell = cells[i];
                cell.vertexStart = vertexIndices.Count;
                vertexIndices.AddRange(verticesByCell[i]);
                cell.vertexCount = verticesByCell[i].Count;
                cells[i] = cell;
            }

            var data = ScriptableObject.CreateInstance<PlanetSurfaceData>();
            data.SetData(cells, vertexIndices.ToArray(), neighborIndices.ToArray(), mesh.vertexCount, bounds);
            return data;
        }

        private static void AssignMeshVertices(
            Vector3[] vertices,
            Bounds bounds,
            Vector3Int voxelSize,
            IReadOnlyDictionary<long, int> cellByVoxel,
            IReadOnlyList<List<int>> verticesByCell)
        {
            Vector3 size = bounds.size;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                int x = ToVoxelCoord(vertex.x, bounds.min.x, size.x, voxelSize.x);
                int y = ToVoxelCoord(vertex.y, bounds.min.y, size.y, voxelSize.y);
                int z = ToVoxelCoord(vertex.z, bounds.min.z, size.z, voxelSize.z);

                if (!TryFindNearbyCell(x, y, z, cellByVoxel, out int cellIndex))
                    continue;

                verticesByCell[cellIndex].Add(i);
            }
        }

        private static bool TryFindNearbyCell(int x, int y, int z, IReadOnlyDictionary<long, int> cellByVoxel, out int cellIndex)
        {
            if (cellByVoxel.TryGetValue(Pack(x, y, z), out cellIndex))
                return true;

            for (int radius = 1; radius <= 2; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy), Mathf.Abs(dz)) != radius)
                        continue;

                    if (cellByVoxel.TryGetValue(Pack(x + dx, y + dy, z + dz), out cellIndex))
                        return true;
                }
            }

            cellIndex = -1;
            return false;
        }

        private static int ToVoxelCoord(float value, float min, float size, int voxelCount)
        {
            if (size <= 0.0001f)
                return 0;

            float normalized = Mathf.InverseLerp(min, min + size, value);
            return Mathf.Clamp(Mathf.FloorToInt(normalized * voxelCount), 0, voxelCount - 1);
        }

        private static long Pack(int x, int y, int z)
        {
            const long mask = 0x1FFFFF;
            return ((long)x & mask) | (((long)y & mask) << 21) | (((long)z & mask) << 42);
        }
    }
}
