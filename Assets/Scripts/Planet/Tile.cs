using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittlePlanet.PlanetSystem
{
    public sealed class Tile
    {
        private readonly List<Tile> _neighbors = new();

        public Tile(int index, PlanetTileShape shape, Vector3 center, IReadOnlyList<Vector3> corners)
        {
            Index = index;
            Shape = shape;
            Center = center;
            Corners = corners;
        }

        public int Index { get; }
        public PlanetTileShape Shape { get; }
        public Vector3 Center { get; }
        public IReadOnlyList<Vector3> Corners { get; }
        public IReadOnlyList<Tile> Neighbors => _neighbors;

        public event Action<Tile> Clicked;

        public void SetNeighbors(IEnumerable<Tile> neighbors)
        {
            _neighbors.Clear();
            _neighbors.AddRange(neighbors);
        }

        public void ApplyToNeighbors(Action<Tile> action)
        {
            if (action == null)
            {
                return;
            }

            for (var i = 0; i < _neighbors.Count; i++)
            {
                action(_neighbors[i]);
            }
        }

        public void TriggerClick()
        {
            Clicked?.Invoke(this);
        }
    }
}
