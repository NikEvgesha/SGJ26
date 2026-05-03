using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LittlePlanet.Balance
{
    public sealed class BalanceChartGraphic : RawImage
    {
        [SerializeField] private Color backgroundColor = new(0.055f, 0.075f, 0.095f, 1f);
        [SerializeField] private Color gridColor = new(0.14f, 0.2f, 0.25f, 1f);
        [SerializeField] private Color axisColor = new(0.35f, 0.48f, 0.58f, 1f);
        [SerializeField] private Color costColor = new(0.12f, 0.85f, 1f, 1f);
        [SerializeField] private Color valueColor = new(0.35f, 1f, 0.28f, 1f);
        [SerializeField, Min(1)] private int lineThickness = 3;
        [SerializeField, Min(64)] private int textureWidth = 768;
        [SerializeField, Min(64)] private int textureHeight = 384;
        [SerializeField] private bool logScale;

        private readonly List<float> _costs = new();
        private readonly List<float> _values = new();
        private readonly List<float> _markers01 = new();
        private Texture2D _texture;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureTextureAssigned();
        }

        private void OnValidate()
        {
            EnsureTextureAssigned();
        }

        public void SetData(IReadOnlyList<float> costs, IReadOnlyList<float> values, bool useLogScale)
        {
            SetData(costs, values, null, useLogScale);
        }

        public void SetData(IReadOnlyList<float> costs, IReadOnlyList<float> values, IReadOnlyList<float> markers01, bool useLogScale)
        {
            _costs.Clear();
            _values.Clear();
            _markers01.Clear();

            if (costs != null)
            {
                _costs.AddRange(costs);
            }

            if (values != null)
            {
                _values.AddRange(values);
            }

            if (markers01 != null)
            {
                for (var i = 0; i < markers01.Count; i++)
                {
                    _markers01.Add(Mathf.Clamp01(markers01[i]));
                }
            }

            logScale = useLogScale;
            EnsureTextureAssigned();
        }

        protected override void OnDestroy()
        {
            if (_texture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_texture);
                }
                else
                {
                    DestroyImmediate(_texture);
                }
            }

            base.OnDestroy();
        }

        private void RebuildTexture()
        {
            var width = Mathf.Max(64, textureWidth);
            var height = Mathf.Max(64, textureHeight);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_texture);
                    }
                    else
                    {
                        DestroyImmediate(_texture);
                    }
                }

                _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Balance Chart Texture",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                };
            }

            ClearTexture(width, height);
            DrawGrid(width, height);
            DrawMarkers(width, height);
            DrawSeries(width, height, _costs, costColor);
            DrawSeries(width, height, _values, valueColor);
            _texture.Apply(updateMipmaps: false);
        }

        private void EnsureTextureAssigned()
        {
            RebuildTexture();
            texture = _texture;
            color = Color.white;
            uvRect = new Rect(0f, 0f, 1f, 1f);
            raycastTarget = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        private void ClearTexture(int width, int height)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    _texture.SetPixel(x, y, backgroundColor);
                }
            }
        }

        private void DrawGrid(int width, int height)
        {
            for (var i = 1; i < 5; i++)
            {
                var x = Mathf.RoundToInt(width * (i / 5f));
                DrawPixelLine(new Vector2Int(x, 0), new Vector2Int(x, height - 1), gridColor, 1);
                var y = Mathf.RoundToInt(height * (i / 5f));
                DrawPixelLine(new Vector2Int(0, y), new Vector2Int(width - 1, y), gridColor, 1);
            }

            DrawPixelLine(new Vector2Int(0, 0), new Vector2Int(width - 1, 0), axisColor, 2);
            DrawPixelLine(new Vector2Int(0, 0), new Vector2Int(0, height - 1), axisColor, 2);
        }

        private void DrawMarkers(int width, int height)
        {
            var markerColor = new Color(1f, 0.9f, 0.25f, 0.85f);
            for (var i = 0; i < _markers01.Count; i++)
            {
                var x = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4, width - 5, _markers01[i])), 0, width - 1);
                DrawPixelLine(new Vector2Int(x, 0), new Vector2Int(x, height - 1), markerColor, 1);
            }
        }

        private void DrawSeries(int width, int height, IReadOnlyList<float> values, Color seriesColor)
        {
            if (values == null || values.Count < 2)
            {
                return;
            }

            var max = 0f;
            for (var i = 0; i < values.Count; i++)
            {
                max = Mathf.Max(max, TransformValue(values[i]));
            }

            if (max <= 0.0001f)
            {
                return;
            }

            var previous = GetPoint(width, height, values, 0, max);
            for (var i = 1; i < values.Count; i++)
            {
                var current = GetPoint(width, height, values, i, max);
                DrawPixelLine(previous, current, seriesColor, lineThickness);
                previous = current;
            }
        }

        private Vector2Int GetPoint(int width, int height, IReadOnlyList<float> values, int index, float max)
        {
            var x01 = values.Count <= 1 ? 0f : index / (float)(values.Count - 1);
            var y01 = Mathf.Clamp01(TransformValue(values[index]) / max);
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4, width - 5, x01)), 0, width - 1),
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4, height - 5, y01)), 0, height - 1));
        }

        private float TransformValue(float value)
        {
            return logScale ? Mathf.Log10(Mathf.Max(1f, value)) : Mathf.Max(0f, value);
        }

        private void DrawPixelLine(Vector2Int from, Vector2Int to, Color lineColor, int thickness)
        {
            var x0 = from.x;
            var y0 = from.y;
            var x1 = to.x;
            var y1 = to.y;
            var dx = Mathf.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Mathf.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;

            while (true)
            {
                DrawPixelBlock(x0, y0, lineColor, thickness);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var e2 = 2 * error;
                if (e2 >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawPixelBlock(int centerX, int centerY, Color lineColor, int thickness)
        {
            var radius = Mathf.Max(0, thickness / 2);
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= _texture.height)
                {
                    continue;
                }

                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x >= 0 && x < _texture.width)
                    {
                        _texture.SetPixel(x, y, lineColor);
                    }
                }
            }
        }
    }
}
