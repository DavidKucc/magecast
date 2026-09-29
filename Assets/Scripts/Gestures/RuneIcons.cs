using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A rune drawn into a small texture from the recogniser's own template, so every picture of a rune
    /// in the game -- the sheet in the corner, the help -- shows exactly what is recognised. Cached.
    /// </summary>
    public static class RuneIcons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string id, Color colour, int size)
        {
            string key = id + "|" + ColorUtility.ToHtmlStringRGBA(colour) + "|" + size;
            Texture2D cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            List<Vector2> points = GestureTemplates.BasePointsFor(id);
            if (points == null || points.Count < 2) return null;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0f, 0f, 0f, 0f);
            tex.SetPixels(clear);

            // fitted into the icon rather than assuming a box: the runes do not share extents
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 span = max - min;
            float scale = (size - 9f) / Mathf.Max(0.001f, Mathf.Max(span.x, span.y));
            int thickness = Mathf.Max(2, size / 24);

            for (int i = 1; i < points.Count; i++)
                Segment(tex, ToPixels(points[i - 1], min, span, scale, size), ToPixels(points[i], min, span, scale, size),
                        colour, thickness);

            tex.Apply();
            cache[key] = tex;
            return tex;
        }

        static Vector2 ToPixels(Vector2 p, Vector2 min, Vector2 span, float scale, int size)
        {
            Vector2 centred = p - min - span * 0.5f;
            return new Vector2(size * 0.5f + centred.x * scale, size * 0.5f + centred.y * scale);
        }

        static void Segment(Texture2D tex, Vector2 a, Vector2 b, Color colour, int thickness)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                // at least two pixels wide, or a thin diagonal disappears into the background
                for (int dx = 0; dx < thickness; dx++)
                    for (int dy = 0; dy < thickness; dy++)
                    {
                        int x = Mathf.RoundToInt(p.x) + dx;
                        int y = Mathf.RoundToInt(p.y) + dy;
                        if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) tex.SetPixel(x, y, colour);
                    }
            }
        }
    }
}
