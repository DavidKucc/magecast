using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Runes read as what they are: a few straight lines meeting at corners.
    ///
    /// The stroke is boiled down to its corners, and the lines between them are compared, by angle,
    /// with each rune's own lines. The hand does not have to reproduce a shape point by point -- it has
    /// to go the right ways, in the right order. A wobble along a line costs a little precision, never
    /// the spell; turning the wrong way does.
    ///
    ///  - Where you start does not matter: every rune is tried both ways round (the lines in reverse
    ///    order, each turned 180 degrees). A mirrored rune is still a different rune.
    ///  - A corner is a real change of direction, not a sudden one. The stroke is simplified
    ///    (Ramer-Douglas-Peucker, tolerance relative to the stroke's own size, so it means the same on
    ///    any screen), and what is left is tidied: bends too gentle to be a corner are straightened
    ///    out, a rounded corner that came out as two close corners becomes one, and a flick at either
    ///    end is dropped.
    ///  - How well it was drawn comes from how close each line's angle was to the rune's, how straight
    ///    the lines were and how their lengths compare -- measured, so it can be shown and logged.
    ///
    /// The lines found so far are also what the trail and the glyph over your head draw: straight, the
    /// way the rune is meant to look, whatever the hand did on the way.
    /// </summary>
    public static class RuneSegments
    {
        /// <summary>This recogniser (true) or the older $P point cloud (false). Set by GestureCaster.</summary>
        public static bool Enabled = true;

        /// <summary>A line this far off the rune's angle is a different line. Also the whole tolerance.</summary>
        public const float MaxSegmentError = 35f;

        /// <summary>A bend gentler than this is part of one line, not a corner.</summary>
        const float MinTurn = 36f;

        /// <summary>
        /// A middle line shorter than this share, bending the same way at both ends, is a corner drawn
        /// round -- a shallow corner taken at speed comes out as two gentle bends with a short piece
        /// between. It becomes one corner where the lines on either side meet.
        /// </summary>
        const float MaxChamferShare = 0.16f;

        /// <summary>How far the simplified path may stray from the stroke, as a fraction of its size.</summary>
        const float Tolerance = 0.075f;

        /// <summary>A last or first line shorter than this share of the stroke is a flick, not part of the rune.</summary>
        const float MinEndShare = 0.09f;

        /// <summary>A middle line shorter than this share is a rounded corner seen as two.</summary>
        const float MinMiddleShare = 0.07f;

        const int Resolution = 96;

        public struct Match
        {
            public string Name;          // null: no rune fits
            public int Lines;            // how many lines were found
            public float MeanError;      // degrees, average over the lines
            public float WorstError;     // degrees, the worst line
            public float LengthError;    // 0 = proportions exactly the rune's, 1 = nothing alike
            public float Straightness;   // 0-1, how straight the lines themselves were
            public float Precision;      // 0-1, all of the above in one number
            public string RunnerUp;      // the next rune that would also have fitted, or null
            public float RunnerUpError;
        }

        class Rune
        {
            public string Name;
            public float[] Angles;
            public float[] Shares;
        }

        static List<Rune> runes;

        static List<Rune> Runes
        {
            get
            {
                if (runes != null) return runes;
                runes = new List<Rune>();
                foreach (string name in GestureTemplates.Runes)
                {
                    Vector2[] c = GestureTemplates.Corners(name);
                    if (c == null || c.Length < 2) continue;
                    var r = new Rune { Name = name, Angles = new float[c.Length - 1], Shares = new float[c.Length - 1] };
                    float total = 0f;
                    for (int i = 0; i < c.Length - 1; i++) total += Vector2.Distance(c[i], c[i + 1]);
                    for (int i = 0; i < c.Length - 1; i++)
                    {
                        Vector2 d = c[i + 1] - c[i];
                        r.Angles[i] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                        r.Shares[i] = d.magnitude / total;
                    }
                    runes.Add(r);
                }
                return runes;
            }
        }

        // ---------------------------------------------------------------- corners

        /// <summary>The stroke's corners, first point to last. Screen space, y up.</summary>
        public static List<Vector2> Corners(IList<Vector2> raw)
        {
            List<Vector2> pts = Resample(raw, Resolution);
            if (pts.Count < 2) return pts;

            float size = Size(pts);
            float length = PathLength(pts);
            if (size < 1e-3f || length < 1e-3f) return new List<Vector2> { pts[0], pts[pts.Count - 1] };

            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            Simplify(pts, 0, pts.Count - 1, Tolerance * size, keep);
            var v = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) v.Add(pts[i]);

            Tidy(v, length);
            return v;
        }

        static void Tidy(List<Vector2> v, float length)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                bool changed = false;

                // a corner drawn round: one corner, where the lines either side of it meet
                for (int i = 1; i < v.Count - 2 && !changed; i++)
                {
                    Vector2 before = v[i] - v[i - 1], piece = v[i + 1] - v[i], after = v[i + 2] - v[i + 1];
                    if (piece.magnitude > MaxChamferShare * length) continue;
                    float turnIn = Cross(before, piece), turnOut = Cross(piece, after);
                    if (turnIn * turnOut <= 0f) continue;                      // an S, not one corner
                    Vector2 meet;
                    if (!Intersect(v[i - 1], v[i], v[i + 1], v[i + 2], out meet)
                        || (meet - (v[i] + v[i + 1]) * 0.5f).magnitude > MaxChamferShare * length)
                        meet = (v[i] + v[i + 1]) * 0.5f;
                    v[i] = meet;
                    v.RemoveAt(i + 1);
                    changed = true;
                }

                // a bend too gentle to be a corner: one line
                for (int i = 1; i < v.Count - 1 && !changed; i++)
                    if (Vector2.Angle(v[i] - v[i - 1], v[i + 1] - v[i]) < MinTurn) { v.RemoveAt(i); changed = true; }

                // a rounded corner that came out as two: one corner, between them
                for (int i = 1; i < v.Count - 2 && !changed; i++)
                    if (Vector2.Distance(v[i], v[i + 1]) < MinMiddleShare * length)
                    {
                        v[i] = (v[i] + v[i + 1]) * 0.5f;
                        v.RemoveAt(i + 1);
                        changed = true;
                    }

                // a flick as the pen went down or came up
                if (!changed && v.Count > 2 && Vector2.Distance(v[0], v[1]) < MinEndShare * length) { v.RemoveAt(0); changed = true; }
                if (!changed && v.Count > 2 && Vector2.Distance(v[v.Count - 2], v[v.Count - 1]) < MinEndShare * length)
                {
                    v.RemoveAt(v.Count - 1);
                    changed = true;
                }

                if (!changed) return;
            }
        }

        static void Simplify(List<Vector2> p, int a, int b, float tolerance, bool[] keep)
        {
            if (b <= a + 1) return;
            float worst = -1f;
            int at = -1;
            for (int i = a + 1; i < b; i++)
            {
                float d = DistanceToSegment(p[i], p[a], p[b]);
                if (d > worst) { worst = d; at = i; }
            }
            if (worst <= tolerance) return;
            keep[at] = true;
            Simplify(p, a, at, tolerance, keep);
            Simplify(p, at, b, tolerance, keep);
        }

        // ---------------------------------------------------------------- matching

        public static Match Recognise(IList<Vector2> raw)
        {
            var m = new Match { MeanError = 999f, WorstError = 999f, RunnerUpError = 999f };
            List<Vector2> c = Corners(raw);
            int n = c.Count - 1;
            m.Lines = Mathf.Max(0, n);
            if (n < 1) return m;

            var angles = new float[n];
            var lengths = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = c[i + 1] - c[i];
                angles[i] = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                lengths[i] = d.magnitude;
                total += lengths[i];
            }

            string best = null, second = null;
            float bestMean = 999f, secondMean = 999f, bestWorst = 0f, bestLength = 0f;

            foreach (Rune r in Runes)
            {
                if (r.Angles.Length != n) continue;

                // as drawn, and drawn from the other end
                for (int way = 0; way < 2; way++)
                {
                    float sum = 0f, worst = 0f, lengthError = 0f;
                    for (int j = 0; j < n; j++)
                    {
                        int k = way == 0 ? j : n - 1 - j;
                        float a = way == 0 ? angles[k] : angles[k] + 180f;
                        float e = Mathf.Abs(Mathf.DeltaAngle(a, r.Angles[j]));
                        sum += e;
                        worst = Mathf.Max(worst, e);
                        lengthError += Mathf.Abs(lengths[k] / total - r.Shares[j]);
                    }
                    if (worst > MaxSegmentError) continue;

                    float mean = sum / n;
                    if (mean < bestMean)
                    {
                        if (best != r.Name) { second = best; secondMean = bestMean; }
                        best = r.Name;
                        bestMean = mean;
                        bestWorst = worst;
                        bestLength = lengthError * 0.5f;
                    }
                    else if (r.Name != best && mean < secondMean)
                    {
                        second = r.Name;
                        secondMean = mean;
                    }
                }
            }

            if (best == null) return m;

            m.Name = best;
            m.MeanError = bestMean;
            m.WorstError = bestWorst;
            m.LengthError = bestLength;
            m.RunnerUp = second;
            m.RunnerUpError = secondMean;
            m.Straightness = Straightness(raw, c);

            // Most of it is the angles -- that is the rune. Straight lines and fair proportions are the
            // craft on top.
            float angleScore = Mathf.Clamp01(1f - bestMean / 50f);
            m.Precision = Mathf.Clamp01(angleScore * 0.65f + m.Straightness * 0.35f - m.LengthError * 0.5f);
            return m;
        }

        /// <summary>How close the stroke kept to its own lines: 1 = ruler-straight.</summary>
        static float Straightness(IList<Vector2> raw, List<Vector2> corners)
        {
            List<Vector2> pts = Resample(raw, Resolution);
            float size = Size(pts);
            if (size < 1e-3f || corners.Count < 2) return 0f;
            float sum = 0f;
            foreach (Vector2 p in pts)
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < corners.Count - 1; i++)
                    nearest = Mathf.Min(nearest, DistanceToSegment(p, corners[i], corners[i + 1]));
                sum += nearest;
            }
            float wobble = sum / pts.Count / size;
            return Mathf.Clamp01(1f - wobble / 0.04f);
        }

        // ---------------------------------------------------------------- showing it

        /// <summary>
        /// The stroke as it should be seen: every line that is finished drawn straight, from corner to
        /// corner, and the one still being drawn following the hand. <paramref name="finished"/> draws
        /// the last line straight as well.
        /// </summary>
        public static void Straighten(IList<Vector2> raw, List<Vector2> into, bool finished)
        {
            into.Clear();
            if (raw == null || raw.Count < 3)
            {
                if (raw != null) into.AddRange(raw);
                return;
            }

            List<Vector2> c = Corners(raw);
            if (finished || c.Count < 3)
            {
                if (finished) { into.AddRange(c); return; }
                into.AddRange(raw);              // one line so far: nothing is finished yet
                return;
            }

            // up to the last corner, straight; from there, the hand
            for (int i = 0; i < c.Count - 1; i++) into.Add(c[i]);
            Vector2 last = c[c.Count - 2];
            int from = 0;
            float nearest = float.MaxValue;
            for (int i = raw.Count - 1; i >= 0; i--)
            {
                float d = (raw[i] - last).sqrMagnitude;
                if (d < nearest) { nearest = d; from = i; }
            }
            for (int i = from + 1; i < raw.Count; i++) into.Add(raw[i]);
        }

        // ---------------------------------------------------------------- geometry

        static List<Vector2> Resample(IList<Vector2> raw, int count)
        {
            var result = new List<Vector2>(count);
            if (raw == null || raw.Count == 0) return result;
            float total = 0f;
            for (int i = 1; i < raw.Count; i++) total += Vector2.Distance(raw[i - 1], raw[i]);
            if (total < 1e-4f) { result.Add(raw[0]); return result; }

            float step = total / (count - 1);
            result.Add(raw[0]);
            float carried = 0f;
            Vector2 prev = raw[0];
            for (int i = 1; i < raw.Count && result.Count < count; i++)
            {
                Vector2 cur = raw[i];
                float d = Vector2.Distance(prev, cur);
                while (carried + d >= step && result.Count < count)
                {
                    float t = (step - carried) / d;
                    Vector2 q = Vector2.Lerp(prev, cur, t);
                    result.Add(q);
                    d -= step - carried;
                    prev = q;
                    carried = 0f;
                }
                carried += d;
                prev = cur;
            }
            if (result.Count < count) result.Add(raw[raw.Count - 1]);
            return result;
        }

        static float PathLength(List<Vector2> p)
        {
            float t = 0f;
            for (int i = 1; i < p.Count; i++) t += Vector2.Distance(p[i - 1], p[i]);
            return t;
        }

        static float Size(List<Vector2> p)
        {
            Vector2 min = p[0], max = p[0];
            foreach (Vector2 q in p) { min = Vector2.Min(min, q); max = Vector2.Max(max, q); }
            return (max - min).magnitude;
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>Where the line through a1-a2 meets the line through b1-b2.</summary>
        static bool Intersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2, out Vector2 at)
        {
            Vector2 r = a2 - a1, s = b2 - b1;
            float d = Cross(r, s);
            at = Vector2.zero;
            if (Mathf.Abs(d) < 1e-6f) return false;
            float t = Cross(b1 - a1, s) / d;
            at = a1 + r * t;
            return true;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l = ab.sqrMagnitude;
            if (l < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
