using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Pictures for the spell bar, made at run time: each spell's icon -- its rune, from the recogniser's
    /// own corners, glowing in the element's colour on a dark tile -- and the cooldown sweep laid over it.
    /// Made once and cached, like RuneIcons.
    /// </summary>
    public static class SpellIcons
    {
        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        /// <summary>A rune's tile: the rune drawn in light, a soft glow round it, the element's colour in the middle.</summary>
        public static Texture2D Icon(string runeId, Color colour, int size = 96)
        {
            string key = runeId + "|" + ColorUtility.ToHtmlStringRGB(colour) + "|" + size;
            Texture2D cached;
            if (icons.TryGetValue(key, out cached) && cached != null) return cached;

            Vector2[] corners = GestureTemplates.Corners(runeId);
            if (corners == null || corners.Length < 2) return null;

            // fitted into the middle of the tile, whatever the rune's own extents
            Vector2 min = corners[0], max = corners[0];
            foreach (Vector2 c in corners) { min = Vector2.Min(min, c); max = Vector2.Max(max, c); }
            Vector2 span = max - min, mid = (min + max) * 0.5f;
            float scale = size * 0.6f / Mathf.Max(0.001f, Mathf.Max(span.x, span.y));
            var pts = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++) pts[i] = new Vector2(size * 0.5f, size * 0.5f) + (corners[i] - mid) * scale;

            float core = size * 0.042f;        // half the width of the rune's line
            float glow = size * 0.12f;
            Color dark = new Color(0.05f, 0.055f, 0.07f);
            Color lit = Color.Lerp(colour, Color.white, 0.45f);

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float u = p.x / size * 2f - 1f, v = p.y / size * 2f - 1f;

                    // dark tile, a breath of the element towards the middle, a little lighter at the top
                    Color c = Color.Lerp(dark + colour * 0.26f, dark, Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) * 0.8f));
                    c += new Color(1f, 1f, 1f) * (0.035f * Mathf.Clamp01(v));

                    float d = float.MaxValue;
                    for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, DistanceToSegment(p, pts[i], pts[i + 1]));
                    float halo = Mathf.Exp(-(d * d) / (glow * glow)) * 0.6f;
                    float line = Mathf.Clamp01((core - d) / 1.3f + 0.5f);
                    c = Color.Lerp(c, colour, halo);
                    c = Color.Lerp(c, lit, line);

                    // a bevel: one pixel lighter along the top and left, darker along the bottom and right
                    if (x == 0 || y == size - 1) c = Color.Lerp(c, Color.white, 0.18f);
                    if (x == size - 1 || y == 0) c = Color.Lerp(c, Color.black, 0.35f);

                    c.a = 1f;
                    pixels[y * size + x] = c;
                }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
            tex.Apply();
            icons[key] = tex;
            return tex;
        }

        const int SweepFrames = 60;
        const int SweepSize = 96;
        static readonly Texture2D[] sweeps = new Texture2D[SweepFrames + 1];

        /// <summary>
        /// The part of a tile still on cooldown, as WoW draws it: everything from the clock hand round to
        /// twelve o'clock, the hand going clockwise from twelve as the time runs out. White, for tinting
        /// with GUI.color. <paramref name="remaining"/> is 1 just after the send, 0 when ready.
        /// </summary>
        public static Texture2D Sweep(float remaining)
        {
            int frame = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(remaining) * SweepFrames), 0, SweepFrames);
            if (sweeps[frame] != null) return sweeps[frame];

            // the angle (clockwise from twelve) up to which the tile has come back
            float hand = (1f - frame / (float)SweepFrames) * 360f;
            var pixels = new Color[SweepSize * SweepSize];
            float centre = SweepSize * 0.5f;
            for (int y = 0; y < SweepSize; y++)
                for (int x = 0; x < SweepSize; x++)
                {
                    float dx = x + 0.5f - centre, dy = y + 0.5f - centre;
                    float angle = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;      // 0 at twelve, 90 at three
                    if (angle < 0f) angle += 360f;
                    float a = frame == SweepFrames ? 1f : Mathf.Clamp01((angle - hand) / 2f + 0.5f);
                    pixels[y * SweepSize + x] = new Color(1f, 1f, 1f, a);
                }

            var tex = new Texture2D(SweepSize, SweepSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
            tex.Apply();
            sweeps[frame] = tex;
            return tex;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l = ab.sqrMagnitude;
            if (l < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
