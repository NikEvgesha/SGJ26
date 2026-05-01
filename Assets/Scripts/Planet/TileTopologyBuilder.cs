using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LittlePlanet.PlanetSystem
{
    public static class TileTopologyBuilder
    {
        public readonly struct TileDescriptor
        {
            public TileDescriptor(int index, PlanetTileShape shape, Vector3 center, Vector3[] corners, int[] neighborIndices)
            {
                Index = index;
                Shape = shape;
                Center = center;
                Corners = corners;
                NeighborIndices = neighborIndices;
            }

            public int Index { get; }
            public PlanetTileShape Shape { get; }
            public Vector3 Center { get; }
            public Vector3[] Corners { get; }
            public int[] NeighborIndices { get; }
        }

        public static List<TileDescriptor> Build(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, float radius)
        {
            var vertexCount = vertices.Count;
            var triangleCount = triangles.Count / 3;

            var incidentTriangles = new List<int>[vertexCount];
            var neighbors = new HashSet<int>[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                incidentTriangles[i] = new List<int>(6);
                neighbors[i] = new HashSet<int>();
            }

            var centroids = new Vector3[triangleCount];

            for (var i = 0; i < triangleCount; i++)
            {
                var a = triangles[i * 3];
                var b = triangles[i * 3 + 1];
                var c = triangles[i * 3 + 2];

                var centroid = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                centroids[i] = centroid.normalized * radius;

                incidentTriangles[a].Add(i);
                incidentTriangles[b].Add(i);
                incidentTriangles[c].Add(i);

                neighbors[a].Add(b);
                neighbors[a].Add(c);
                neighbors[b].Add(a);
                neighbors[b].Add(c);
                neighbors[c].Add(a);
                neighbors[c].Add(b);
            }

            var result = new List<TileDescriptor>(vertexCount);
            for (var i = 0; i < vertexCount; i++)
            {
                var center = vertices[i].normalized * radius;
                var corners = incidentTriangles[i].Select(triangleIndex => centroids[triangleIndex]).ToList();
                SortAroundNormal(corners, center);

                var shape = corners.Count == (int)PlanetTileShape.Pentagon
                    ? PlanetTileShape.Pentagon
                    : PlanetTileShape.Hexagon;

                var orderedNeighbors = neighbors[i].ToList();
                orderedNeighbors.Sort((left, right) =>
                    Vector3.Angle(vertices[i], vertices[left]).CompareTo(Vector3.Angle(vertices[i], vertices[right])));

                result.Add(new TileDescriptor(i, shape, center, corners.ToArray(), orderedNeighbors.ToArray()));
            }

            return result;
        }

        private static void SortAroundNormal(List<Vector3> points, Vector3 center)
        {
            if (points.Count <= 2)
            {
                return;
            }

            var normal = center.normalized;
            var axisX = Vector3.Cross(normal, Vector3.up);
            if (axisX.sqrMagnitude < 0.0001f)
            {
                axisX = Vector3.Cross(normal, Vector3.right);
            }

            axisX.Normalize();
            var axisY = Vector3.Cross(normal, axisX).normalized;

            points.Sort((a, b) =>
            {
                var relativeA = (a - center);
                var relativeB = (b - center);

                var angleA = Mathf.Atan2(Vector3.Dot(relativeA, axisY), Vector3.Dot(relativeA, axisX));
                var angleB = Mathf.Atan2(Vector3.Dot(relativeB, axisY), Vector3.Dot(relativeB, axisX));
                return angleA.CompareTo(angleB);
            });
        }
    }
}
