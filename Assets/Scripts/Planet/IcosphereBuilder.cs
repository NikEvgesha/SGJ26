using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePlanet.PlanetSystem
{
    public static class IcosphereBuilder
    {
        public readonly struct IcosphereMeshData
        {
            public IcosphereMeshData(List<Vector3> vertices, List<int> triangles)
            {
                Vertices = vertices;
                Triangles = triangles;
            }

            public List<Vector3> Vertices { get; }
            public List<int> Triangles { get; }
        }

        public static IcosphereMeshData Build(int subdivisions, float radius)
        {
            subdivisions = Mathf.Max(0, subdivisions);
            radius = Mathf.Max(0.01f, radius);

            var vertices = CreateIcosahedronVertices();
            var triangles = CreateIcosahedronTriangles();

            for (var i = 0; i < subdivisions; i++)
            {
                Subdivide(vertices, triangles);
            }

            for (var i = 0; i < vertices.Count; i++)
            {
                vertices[i] = vertices[i].normalized * radius;
            }

            return new IcosphereMeshData(vertices, triangles);
        }

        private static List<Vector3> CreateIcosahedronVertices()
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var verts = new List<Vector3>
            {
                new(-1f, t, 0f),
                new(1f, t, 0f),
                new(-1f, -t, 0f),
                new(1f, -t, 0f),
                new(0f, -1f, t),
                new(0f, 1f, t),
                new(0f, -1f, -t),
                new(0f, 1f, -t),
                new(t, 0f, -1f),
                new(t, 0f, 1f),
                new(-t, 0f, -1f),
                new(-t, 0f, 1f)
            };

            for (var i = 0; i < verts.Count; i++)
            {
                verts[i] = verts[i].normalized;
            }

            return verts;
        }

        private static List<int> CreateIcosahedronTriangles()
        {
            return new List<int>
            {
                0, 11, 5,
                0, 5, 1,
                0, 1, 7,
                0, 7, 10,
                0, 10, 11,
                1, 5, 9,
                5, 11, 4,
                11, 10, 2,
                10, 7, 6,
                7, 1, 8,
                3, 9, 4,
                3, 4, 2,
                3, 2, 6,
                3, 6, 8,
                3, 8, 9,
                4, 9, 5,
                2, 4, 11,
                6, 2, 10,
                8, 6, 7,
                9, 8, 1
            };
        }

        private static void Subdivide(List<Vector3> vertices, List<int> triangles)
        {
            var midpointCache = new Dictionary<long, int>();
            var nextTriangles = new List<int>(triangles.Count * 4);

            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];

                var ab = GetMidpointIndex(midpointCache, vertices, a, b);
                var bc = GetMidpointIndex(midpointCache, vertices, b, c);
                var ca = GetMidpointIndex(midpointCache, vertices, c, a);

                nextTriangles.AddRange(new[]
                {
                    a, ab, ca,
                    b, bc, ab,
                    c, ca, bc,
                    ab, bc, ca
                });
            }

            triangles.Clear();
            triangles.AddRange(nextTriangles);
        }

        private static int GetMidpointIndex(Dictionary<long, int> cache, List<Vector3> vertices, int indexA, int indexB)
        {
            var min = Math.Min(indexA, indexB);
            var max = Math.Max(indexA, indexB);
            var key = ((long)min << 32) | (uint)max;

            if (cache.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var midpoint = (vertices[indexA] + vertices[indexB]) * 0.5f;
            var newIndex = vertices.Count;
            vertices.Add(midpoint.normalized);
            cache[key] = newIndex;
            return newIndex;
        }
    }
}
