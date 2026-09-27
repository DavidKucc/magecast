using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Measures a stroke along several independent axes instead of collapsing it to one number.
    ///
    /// The $P distance answers "which shape is this", and it is deliberately forgiving about things a
    /// spell might want to care about: it barely notices an unclosed loop, and it cannot tell a shaky
    /// circle from a slightly-oval one. These axes measure those separately.
    ///
    /// Every axis is 0..1, 1 = perfect, and every one is scale and position independent, so drawing
    /// small does not quietly cost you accuracy. Size is reported separately as a raw number, because
    /// it is a choice the player makes rather than a mistake they avoid.
    ///
    /// All three axes are defined for OPEN strokes too, but they mean different things there -- see
    /// each field. Getting that wrong is not cosmetic: with the closed-shape formulas, a violently
    /// shaky line scores as perfectly steady and a perfectly straight one scores zero closure.
    /// </summary>
    public struct StrokeMetrics
    {
        /// <summary>
        /// Freedom from tremor. A closed convex shape turns through 360 degrees and a straight line
        /// through 0; either way, turning ABOVE what the shape wants is the hand going back and forth
        /// without going anywhere.
        /// </summary>
        public float Steadiness;

        /// <summary>
        /// Closed shapes: how well the corners match what the shape wants -- sharp and few for a
        /// polygon, spread evenly for a circle. Open shapes: straightness, the deviation from the line
        /// between the two ends. A bowed line and a rounded square are the same kind of mistake.
        /// </summary>
        public float Definition;

        /// <summary>Closed shapes: whether the ends meet. Open shapes: not applicable, always 1.</summary>
        public float Closure;

        /// <summary>Bounding box of the stroke in pixels. Not a quality -- a deliberate choice.</summary>
        public float SizePixels;

        /// <summary>False for open strokes, where Closure is a constant 1 and must not be scored --
        /// folding it into the payoff anyway would hand every line a free axis the circle has to earn.</summary>
        public bool ClosureApplies;

        public string Weakest;
        public float WeakestValue;

        /// <summary>
        /// Tremor tolerance: turning this much beyond the shape's own expectation scores zero
        /// steadiness. Measured, and far larger than it looks like it should be -- total turning is
        /// violently sensitive to sampling noise. A circle with 2 px of jitter already turns through
        /// 668 degrees and 14 px reaches 2385, so a tolerance in the hundreds saturates instantly.
        /// </summary>
        public const float TurningTolerance = 2200f;

        /// <summary>Corner-profile tolerance, in units of "share of turning in the sharpest fifth".</summary>
        public const float DefinitionTolerance = 0.45f;

        /// <summary>Endpoint gap, as a fraction of the shape's size, that scores zero closure.</summary>
        public const float ClosureTolerance = 0.45f;

        /// <summary>Mean deviation from the chord, as a fraction of chord length, that scores zero.</summary>
        public const float StraightnessTolerance = 0.13f;

        const int Samples = 64;

        public static StrokeMetrics Compute(IList<Vector2> raw, GestureTemplate matched)
        {
            StrokeMetrics m = new StrokeMetrics();
            m.Steadiness = m.Definition = m.Closure = 0f;
            m.Weakest = "-";
            m.WeakestValue = 0f;
            if (raw == null || raw.Count < 6) return m;

            bool closed = matched == null || matched.IsClosed;
            float expectedTurning = matched != null ? matched.ExpectedTurning : 360f;
            m.ClosureApplies = closed;

            // --- size and closure from the raw stroke, before any normalisation ---
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i].x < minX) minX = raw[i].x;
                if (raw[i].y < minY) minY = raw[i].y;
                if (raw[i].x > maxX) maxX = raw[i].x;
                if (raw[i].y > maxY) maxY = raw[i].y;
            }
            float size = Mathf.Max(maxX - minX, maxY - minY);
            m.SizePixels = size;
            if (size < 1e-3f) return m;

            if (closed)
            {
                float gap = Vector2.Distance(raw[0], raw[raw.Count - 1]) / size;
                m.Closure = 1f - Mathf.Clamp01(gap / ClosureTolerance);
            }
            else
            {
                m.Closure = 1f;   // an open stroke is supposed to have its ends apart
            }

            // --- turning profile from a resampled, normalised copy ---
            Vector2[] pts = PDollarRecognizer.Normalise(raw, Samples);
            float total, concentration;
            TurningProfile(pts, closed, out total, out concentration);

            float excessTurning = Mathf.Max(0f, total - expectedTurning);
            m.Steadiness = 1f - Mathf.Clamp01(excessTurning / TurningTolerance);

            // Definition is measured on a SMOOTHED copy. On the raw stroke, tremor distorts the corner
            // profile just as much as genuinely rounded corners do, and the two axes end up measuring
            // the same thing twice. Smoothing strips the high-frequency shake and leaves the shape.
            Vector2[] smooth = Smooth(pts, 5, closed);

            // What "well defined" means depends on whether the shape is supposed to turn at all.
            //
            // A straight line is judged on straightness. EVERY other shape is judged on its corner
            // profile against the template's own -- open or closed alike.
            //
            // This used to split on open vs closed instead, which was harmless while the only open
            // shape was a line. Then the runes arrived as open strokes and inherited "how straight is
            // this?" -- and a rune is deliberately not straight. A perfect Kenaz scores zero on that,
            // because its corner sits far from the line between its ends. Measured on real play, 43
            // rune strokes scored a definition of 0.00 almost without exception, which capped their
            // precision near 0.45 against a 0.65 bar for "clean": no rune could ever be graded better
            // than weak, however well it was drawn.
            bool straightShape = expectedTurning < 10f;

            if (!straightShape)
            {
                float smoothTotal, smoothConcentration;
                TurningProfile(smooth, closed, out smoothTotal, out smoothConcentration);
                float wanted = matched != null ? matched.Concentration : smoothConcentration;
                m.Definition = 1f - Mathf.Clamp01(Mathf.Abs(smoothConcentration - wanted) / DefinitionTolerance);
            }
            else
            {
                m.Definition = 1f - Mathf.Clamp01(ChordDeviation(smooth) / StraightnessTolerance);
            }

            m.Weakest = "steadiness";
            m.WeakestValue = m.Steadiness;
            // named after what was actually measured, or the HUD would tell a rune player to draw straighter
            string definitionName = straightShape ? "straightness" : "definition";
            if (m.Definition < m.WeakestValue) { m.Weakest = definitionName; m.WeakestValue = m.Definition; }
            if (closed && m.Closure < m.WeakestValue) { m.Weakest = "closure"; m.WeakestValue = m.Closure; }

            // naming a weakest axis on a stroke that was fine reads as criticism of a good cast
            if (m.WeakestValue > 0.85f) m.Weakest = "-";
            return m;
        }

        /// <summary>
        /// Mean distance of the points from the straight line joining the two ends, as a fraction of
        /// the distance between those ends. Zero for a perfect line.
        /// </summary>
        static float ChordDeviation(Vector2[] pts)
        {
            Vector2 a = pts[0], b = pts[pts.Length - 1];
            Vector2 chord = b - a;
            float length = chord.magnitude;
            if (length < 1e-5f) return 1f;    // ends in the same place: not a line in any useful sense

            Vector2 dir = chord / length;
            float sum = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 v = pts[i] - a;
                float along = Vector2.Dot(v, dir);
                sum += (v - dir * along).magnitude;
            }
            return (sum / pts.Length) / length;
        }

        /// <summary>
        /// Total absolute turning, and how much of it happens in the sharpest fifth of the stroke.
        ///
        /// The closed flag is load-bearing. Wrapping the neighbour lookup around the ends of an OPEN
        /// stroke invents a fake reversal there: a perfectly straight line would report roughly 360
        /// degrees of turning -- the exact value a perfect circle reports -- and every line would grade
        /// as flawlessly steady no matter how it was drawn.
        /// </summary>
        public static void TurningProfile(Vector2[] pts, bool closed, out float totalDegrees, out float concentration)
        {
            int n = pts.Length;
            float[] turns = new float[n];
            totalDegrees = 0f;

            int first = closed ? 0 : 1;
            int last = closed ? n - 1 : n - 2;

            for (int i = first; i <= last; i++)
            {
                Vector2 a = pts[(i - 1 + n) % n];
                Vector2 b = pts[i];
                Vector2 c = pts[(i + 1) % n];
                Vector2 v1 = b - a, v2 = c - b;
                if (v1.sqrMagnitude < 1e-10f || v2.sqrMagnitude < 1e-10f) continue;

                float t = Vector2.Angle(v1, v2);
                turns[i] = t;
                totalDegrees += t;
            }

            if (totalDegrees < 1e-4f) { concentration = 0f; return; }

            System.Array.Sort(turns);
            int top = Mathf.Max(1, n / 5);
            float sharpest = 0f;
            for (int i = n - top; i < n; i++) sharpest += turns[i];
            concentration = sharpest / totalDegrees;
        }

        /// <summary>
        /// The corner-profile figure, computed one way for everybody. Templates and player strokes MUST
        /// go through this same path: measuring the template one way and the stroke another made a
        /// flawless square score 0.36, because the two numbers were not comparable in the first place.
        /// </summary>
        public static float SmoothedConcentration(IList<Vector2> raw, bool closed)
        {
            Vector2[] pts = PDollarRecognizer.Normalise(raw, Samples);
            float total, concentration;
            TurningProfile(Smooth(pts, 5, closed), closed, out total, out concentration);
            return concentration;
        }

        /// <summary>
        /// Moving average. On a closed shape it wraps so the two ends join; on an open one it clamps,
        /// because wrapping would drag the start of a line towards its end and bend it.
        /// </summary>
        static Vector2[] Smooth(Vector2[] pts, int window, bool closed)
        {
            int n = pts.Length;
            Vector2[] outp = new Vector2[n];
            int half = Mathf.Max(1, window / 2);

            for (int i = 0; i < n; i++)
            {
                Vector2 sum = Vector2.zero;
                for (int k = -half; k <= half; k++)
                {
                    int j = closed ? (i + k + n) % n : Mathf.Clamp(i + k, 0, n - 1);
                    sum += pts[j];
                }
                outp[i] = sum / (half * 2 + 1);
            }
            return outp;
        }
    }
}
