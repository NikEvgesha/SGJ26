using System;
using UnityEngine;

namespace SGJ26.Terraforming
{
    [CreateAssetMenu(menuName = "SGJ26/Terraforming/Planet Surface Data")]
    public sealed class PlanetSurfaceData : ScriptableObject
    {
        [Serializable]
        public struct SurfaceCell
        {
            public Vector3 localPosition;
            public Vector3 normal;
            public float height01;
            public int palette;
            public bool waterCandidate;
            public int vertexStart;
            public int vertexCount;
            public int neighborStart;
            public int neighborCount;
        }

        [SerializeField] private SurfaceCell[] cells = Array.Empty<SurfaceCell>();
        [SerializeField] private int[] vertexIndices = Array.Empty<int>();
        [SerializeField] private int[] neighborIndices = Array.Empty<int>();
        [SerializeField] private int meshVertexCount;
        [SerializeField] private Bounds localBounds;

        public SurfaceCell[] Cells => cells;
        public int[] VertexIndices => vertexIndices;
        public int[] NeighborIndices => neighborIndices;
        public int MeshVertexCount => meshVertexCount;
        public Bounds LocalBounds => localBounds;
        public int CellCount => cells != null ? cells.Length : 0;

        public void SetData(
            SurfaceCell[] newCells,
            int[] newVertexIndices,
            int[] newNeighborIndices,
            int newMeshVertexCount,
            Bounds newLocalBounds)
        {
            cells = newCells ?? Array.Empty<SurfaceCell>();
            vertexIndices = newVertexIndices ?? Array.Empty<int>();
            neighborIndices = newNeighborIndices ?? Array.Empty<int>();
            meshVertexCount = Mathf.Max(0, newMeshVertexCount);
            localBounds = newLocalBounds;
        }

        private void OnValidate()
        {
            cells ??= Array.Empty<SurfaceCell>();
            vertexIndices ??= Array.Empty<int>();
            neighborIndices ??= Array.Empty<int>();
            meshVertexCount = Mathf.Max(0, meshVertexCount);
        }
    }
}
