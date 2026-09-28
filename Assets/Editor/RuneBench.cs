using System.Collections.Generic;
using System.Text;
using MageCast.Gestures;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Hundreds of machine-drawn runes, drawn the way a hand draws them -- shifted corners, a tilt,
    /// corners taken round, a slow wobble, sometimes a flick at the end, half of them from the other
    /// end -- put through RuneSegments at three levels of care. Reports what each was read as and in
    /// which tier, and what shapes that must NOT cast (circles, mirrored runes, random zigzags) did.
    ///
    /// For tuning the recogniser against numbers instead of against one person's wrist. Seeded, so two
    /// runs of the same code give the same result.
    /// </summary>
    public static class RuneBench
    {
        [MenuItem("Tools/Arena/Rune Recognition Bench")]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        struct Care
        {
            public string Name;
            public float Jitter, Round, Wobble;
        }

        static readonly Care[] Levels =
        {
            new Care { Name = "neat", Jitter = 0.08f, Round = 0.15f, Wobble = 0.010f },
            new Care { Name = "average", Jitter = 0.16f, Round = 0.30f, Wobble = 0.022f },
            new Care { Name = "sloppy", Jitter = 0.25f, Round = 0.45f, Wobble = 0.035f },
        };

        static uint seed;

        static float Unit()
        {
            seed = seed * 1664525u + 1013904223u;
            return ((seed >> 8) & 0xFFFFFF) / 16777215f;
        }

        static float Range(float a, float b) { return a + (b - a) * Unit(); }

        public static string Run(int perRune = 300)
        {
            seed = 12345;
            var sb = new StringBuilder();
            IList<string> runes = GestureTemplates.Runes;

            foreach (Care care in Levels)
            {
                sb.Append("== ").Append(care.Name).Append('\n');
                foreach (string name in runes)
                {
                    Vector2[] corners = GestureTemplates.Corners(name);
                    var read = new SortedDictionary<string, int>();
                    int perfect = 0, clean = 0, weak = 0;
                    for (int i = 0; i < perRune; i++)
                    {
                        List<Vector2> stroke = Hand(corners, care, Unit() < 0.5f, Unit() < 0.3f);
                        RuneSegments.Match m = RuneSegments.Recognise(stroke);
                        string got = m.Name ?? "FIZZLE";
                        read[got] = (read.ContainsKey(got) ? read[got] : 0) + 1;
                        if (m.Name == name)
                        {
                            if (m.Precision >= 0.85f) perfect++;
                            else if (m.Precision >= 0.65f) clean++;
                            else weak++;
                        }
                    }
                    int right = read.ContainsKey(name) ? read[name] : 0;
                    sb.Append(name.PadRight(7)).Append(' ').Append((100f * right / perRune).ToString("F0")).Append("%   ");
                    foreach (var kv in read) if (kv.Key != name) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
                    sb.Append("  tiers P/C/W ").Append(perfect).Append('/').Append(clean).Append('/').Append(weak).Append('\n');
                }
            }

            // what must not cast
            var casts = new SortedDictionary<string, int>();
            int bad = 0;
            for (int i = 0; i < 200; i++)
            {
                var circle = new List<Vector2>();
                float r = Range(80f, 160f), start = Range(0f, 6.28f), dir = Unit() < 0.5f ? 1f : -1f;
                for (int k = 0; k <= 90; k++)
                {
                    float a = start + dir * k / 90f * 6.4f;
                    circle.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * (1f + 0.04f * Mathf.Sin(k)));
                }
                if (Casts(circle, "circle", casts)) bad++;
            }
            sb.Append("circles that cast: ").Append(bad).Append("/200\n");

            bad = 0;
            int total = 0;
            foreach (string name in runes)
            {
                Vector2[] c = GestureTemplates.Corners(name);
                var mirrored = new Vector2[c.Length];
                for (int i = 0; i < c.Length; i++) mirrored[i] = new Vector2(-c[i].x, c[i].y);
                for (int i = 0; i < 60; i++, total++)
                    if (Casts(Hand(mirrored, Levels[0], Unit() < 0.5f, false), "mirrored " + name, casts)) bad++;
            }
            sb.Append("mirrored runes that cast: ").Append(bad).Append('/').Append(total).Append("  (a mirrored M is an M)\n");

            bad = 0;
            for (int i = 0; i < 300; i++)
            {
                int n = 2 + (int)(Unit() * 4f);
                var c = new Vector2[n + 1];
                for (int k = 0; k <= n; k++) c[k] = new Vector2(Range(-1f, 1f), Range(-1f, 1f));
                if (Casts(Hand(c, new Care { Round = 0.2f, Wobble = 0.015f }, false, false), "zigzag", casts)) bad++;
            }
            sb.Append("random zigzags that cast: ").Append(bad).Append("/300\n");
            foreach (var kv in casts) sb.Append("   ").Append(kv.Key).Append(": ").Append(kv.Value).Append('\n');
            return sb.ToString();
        }

        static bool Casts(List<Vector2> stroke, string what, SortedDictionary<string, int> casts)
        {
            RuneSegments.Match m = RuneSegments.Recognise(stroke);
            if (m.Name == null) return false;
            string key = what + " -> " + m.Name;
            casts[key] = (casts.ContainsKey(key) ? casts[key] : 0) + 1;
            return true;
        }

        /// <summary>A rune as a hand would draw it, in screen pixels.</summary>
        static List<Vector2> Hand(Vector2[] corners, Care care, bool reverse, bool flick)
        {
            float size = Range(140f, 360f);
            float tilt = Range(-12f, 12f) * Mathf.Deg2Rad;
            var c = new List<Vector2>();
            foreach (Vector2 p in corners)
            {
                Vector2 q = p + new Vector2(Range(-care.Jitter, care.Jitter), Range(-care.Jitter, care.Jitter));
                q = new Vector2(q.x * Mathf.Cos(tilt) - q.y * Mathf.Sin(tilt), q.x * Mathf.Sin(tilt) + q.y * Mathf.Cos(tilt));
                c.Add(q * size * 0.5f);
            }
            if (reverse) c.Reverse();

            // corners taken round: each cut back by a share of its shorter line and bridged by a curve
            var path = new List<Vector2> { c[0] };
            for (int i = 1; i < c.Count - 1; i++)
            {
                Vector2 a = c[i - 1], b = c[i], d = c[i + 1];
                float cut = Mathf.Min((b - a).magnitude, (d - b).magnitude) * care.Round;
                Vector2 p0 = b + (a - b).normalized * cut, p2 = b + (d - b).normalized * cut;
                path.Add(p0);
                for (int k = 1; k <= 6; k++)
                {
                    float t = k / 7f;
                    path.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * b + t * t * p2);
                }
                path.Add(p2);
            }
            path.Add(c[c.Count - 1]);
            if (flick)
            {
                Vector2 end = path[path.Count - 1];
                path.Add(end + new Vector2(Range(-1f, 1f), Range(-1f, 1f)).normalized * size * 0.05f);
            }

            // walked at about 4 px a sample, with a slow wobble and a little noise
            var pts = new List<Vector2>();
            float ph = Range(0f, 6.28f), ph2 = Range(0f, 6.28f), walked = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1], b = path[i];
                float len = (b - a).magnitude;
                int n = Mathf.Max(1, (int)(len / 4f));
                Vector2 normal = len > 0f ? new Vector2(-(b - a).y, (b - a).x) / len : Vector2.zero;
                for (int k = 0; k < n; k++)
                {
                    walked += len / n;
                    float w = (Mathf.Sin(walked * 0.03f + ph) + 0.5f * Mathf.Sin(walked * 0.071f + ph2)) * care.Wobble * size;
                    pts.Add(Vector2.Lerp(a, b, k / (float)n) + normal * w + new Vector2(Range(-1f, 1f), Range(-1f, 1f)) * 1.2f);
                }
            }
            pts.Add(path[path.Count - 1]);
            return pts;
        }
    }
}
