using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    public class GestureTemplate
    {
        public readonly string Name;
        public readonly Vector2[] Points;   // already normalised

        /// <summary>
        /// The distance a machine-perfect stroke of this shape still scores, caused by resampling
        /// rather than by the player. It is NOT the same for every shape, so grading raw distance
        /// would make one shape score worse than another through no fault of the person drawing it.
        /// Quality is graded on the excess over this floor instead.
        /// </summary>
        public float Floor;

        /// <summary>
        /// Share of this shape's turning that falls in its sharpest fifth -- near 1 for a polygon,
        /// near 0.2 for a circle. Only meaningful for closed shapes; StrokeMetrics uses straightness
        /// instead when IsClosed is false.
        /// </summary>
        public float Concentration;

        /// <summary>
        /// How much distance this shape gains for a REFERENCE amount of hand wobble. Excess is divided
        /// by it, so "1.0" means the same amount of sloppiness whichever shape was drawn and the
        /// quality thresholds are dimensionless rather than tuned to whichever shape came first.
        ///
        /// Honest note on why this exists: it was added to explain lines grading as misfires while
        /// circles sailed through, and that turned out to be a bug in the test harness, not in here.
        /// Measured afterwards, circle and line spread almost identically (0.0131 vs 0.0139), so today
        /// this divides by near-enough the same number for both and changes nothing. It is kept because
        /// the floor only cancels the offset a shape carries at zero error, not the rate at which error
        /// accumulates -- and a shape that is genuinely more fragile, a spiral or a zigzag, will differ
        /// here even though these two do not. Do not read the current values as proof it was needed.
        /// </summary>
        public float Spread = 1f;

        /// <summary>
        /// How far this shape turns in total when drawn perfectly: 360 degrees for a closed convex
        /// loop, 0 for a straight line. StrokeMetrics reads tremor as turning ABOVE this, so it has
        /// to be per-shape -- with a fixed 360 a violently shaky line would score as perfectly steady.
        /// </summary>
        public float ExpectedTurning = 360f;

        /// <summary>False for open strokes. Decides whether the closure axis means anything, whether
        /// smoothing may wrap around the ends, and whether turning wraps around the ends.</summary>
        public bool IsClosed = true;

        /// <summary>
        /// Match after rotating the stroke so its own start-to-end chord lies horizontal, instead of
        /// searching a bounded tilt range.
        ///
        /// A line has to match at ANY angle, and the bounded search cannot cover 180 degrees without
        /// either very coarse steps (a line 15 degrees off its nearest step matches badly) or dozens
        /// of extra normalisations per frame during the live preview. Aligning to the chord is exact
        /// and costs one rotation. The direction the stroke actually travelled is recovered separately
        /// by StrokeDirections -- $P itself is direction-blind by construction.
        /// </summary>
        public bool AlignToChord = false;

        /// <summary>
        /// Stretch the stroke's bounding box to square before matching, so an oval counts as a circle.
        ///
        /// Uniform scaling was chosen originally to keep a squashed shape distinguishable from a round
        /// one, which mattered when the set contained both a square and a rectangle. It does not matter
        /// now, and it was actively wrong: measured, an ellipse of only 1.3:1 -- what a hand produces
        /// when it means "circle" -- graded as a misfire, and 1.5:1 vanished entirely. When somebody
        /// draws a circle they mean "a round closed loop", not "a mathematically circular one", so
        /// aspect is not part of the meaning and should not be part of the score.
        ///
        /// The correction is capped (see MaxAspectCorrection) so a nearly-flat stroke is not inflated
        /// into a circle -- without that cap, a line's zero-height bounding box would be stretched by a
        /// vast factor and turn its own sampling noise into a shape.
        /// </summary>
        public bool NormaliseAspect = false;

        public GestureTemplate(string name, Vector2[] normalisedPoints)
        {
            Name = name;
            Points = normalisedPoints;
        }
    }

    public struct RecognitionResult
    {
        public string Name;
        public float Distance;    // lower is better; 0 would be an exact match
        public float Runner;      // distance of the second-best template
        public bool Accepted;

        /// <summary>How much worse than a perfect stroke of this same shape. This is what to grade on.</summary>
        public float Excess;

        public float Margin { get { return Runner - Distance; } }
    }

    /// <summary>
    /// $P point-cloud recognizer. Chosen over $1 deliberately: $1 without its rotation normalisation
    /// is sensitive to where the stroke started -- a circle begun at the top would not match one begun
    /// at the side. $P is invariant to start point and stroke direction but stays sensitive to
    /// rotation, which is the combination we want.
    ///
    /// The direction-blindness is worth stating plainly, because the current gesture set depends on
    /// it: a line drawn left-to-right and one drawn right-to-left are the SAME point cloud here. Which
    /// way the hand travelled cannot come from $P and is recovered by StrokeDirections instead.
    ///
    /// No ML, no dependencies, no training.
    /// </summary>
    public static class PDollarRecognizer
    {
        public const int SampleCount = 32;

        /// <summary>
        /// Resample to a fixed count, scale uniformly, centre on the origin. Scaling is uniform and
        /// not axis-independent on purpose: stretching each axis to fill a unit square would turn a
        /// squashed oval into a perfect circle and lose a difference we care about.
        /// </summary>
        /// <summary>How far the minor axis may be stretched when normalising aspect away.</summary>
        public const float MaxAspectCorrection = 2.5f;

        public static Vector2[] Normalise(IList<Vector2> raw, int n = SampleCount, bool normaliseAspect = false)
        {
            Vector2[] pts = Resample(raw, n);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                if (pts[i].x < minX) minX = pts[i].x;
                if (pts[i].y < minY) minY = pts[i].y;
                if (pts[i].x > maxX) maxX = pts[i].x;
                if (pts[i].y > maxY) maxY = pts[i].y;
            }

            float w = Mathf.Max(1e-5f, maxX - minX);
            float h = Mathf.Max(1e-5f, maxY - minY);
            float size = Mathf.Max(w, h);

            float divX = size, divY = size;
            if (normaliseAspect)
            {
                // Divide each axis by its own extent, so an oval becomes round -- but never stretch the
                // minor axis by more than MaxAspectCorrection, or a near-flat stroke turns its own
                // noise into a shape.
                divX = Mathf.Max(w, size / MaxAspectCorrection);
                divY = Mathf.Max(h, size / MaxAspectCorrection);
            }

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = new Vector2(pts[i].x / divX, pts[i].y / divY);
                centroid += pts[i];
            }
            centroid /= pts.Length;

            for (int i = 0; i < pts.Length; i++) pts[i] -= centroid;
            return pts;
        }

        /// <summary>Angle of the stroke's start-to-end chord, in degrees, screen space (y up).</summary>
        public static float ChordAngle(IList<Vector2> raw)
        {
            if (raw == null || raw.Count < 2) return 0f;
            Vector2 chord = raw[raw.Count - 1] - raw[0];
            if (chord.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Atan2(chord.y, chord.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Best match plus the runner-up. Acceptance needs BOTH an absolute fit and a clear win over
        /// second place. The margin test is what stops a shapeless scribble from confidently casting.
        /// </summary>
        public static RecognitionResult Recognise(IList<Vector2> raw, IList<GestureTemplate> templates,
                                                  float maxDistance, float minMargin,
                                                  float tiltTolerance = 25f, int tiltSteps = 7)
        {
            RecognitionResult r = new RecognitionResult();
            r.Name = null;
            r.Distance = float.MaxValue;
            r.Runner = float.MaxValue;
            r.Accepted = false;

            if (raw == null || raw.Count < 4 || templates == null || templates.Count == 0) return r;

            // Bounded rotation search rather than rotation invariance, for shapes where orientation is
            // part of the meaning. Straight $P is sensitive enough that a few degrees of hand tilt
            // costs as much as a genuinely different shape -- partly because a tilted shape also has a
            // bigger bounding box, so uniform scaling shrinks it on top of the misalignment.
            //
            // The stroke is rotated BEFORE normalising, so the bounding box is re-measured each time;
            // rotating after normalising would leave the scale error in place.
            int steps = Mathf.Max(1, tiltSteps);
            Vector2[][] tilted = new Vector2[steps][];
            for (int s = 0; s < steps; s++)
            {
                float t = steps == 1 ? 0f : Mathf.Lerp(-tiltTolerance, tiltTolerance, s / (float)(steps - 1));
                tilted[s] = Normalise(Rotate(raw, t));
            }

            Vector2[] chordAligned = null;    // built lazily; only line-like templates need it
            Vector2[] aspectFree = null;      // ditto for round ones

            float[] best = new float[templates.Count];
            for (int i = 0; i < best.Length; i++)
            {
                GestureTemplate t = templates[i];
                float d = float.MaxValue;

                if (t.AlignToChord)
                {
                    if (chordAligned == null) chordAligned = Normalise(Rotate(raw, -ChordAngle(raw)));
                    d = GreedyCloudMatch(chordAligned, t.Points);
                }
                else if (t.NormaliseAspect)
                {
                    // No tilt search: a shape that does not care about its aspect ratio is round, and a
                    // round shape does not care about rotation either.
                    if (aspectFree == null) aspectFree = Normalise(CloseLoop(raw), SampleCount, true);
                    d = GreedyCloudMatch(aspectFree, t.Points);
                }
                else
                {
                    for (int s = 0; s < steps; s++)
                        d = Mathf.Min(d, GreedyCloudMatch(tilted[s], t.Points));
                }

                best[i] = d;
            }

            int bestIndex = -1;
            for (int i = 0; i < templates.Count; i++)
            {
                if (best[i] < r.Distance)
                {
                    r.Runner = r.Distance;
                    r.Distance = best[i];
                    r.Name = templates[i].Name;
                    bestIndex = i;
                }
                else if (best[i] < r.Runner)
                {
                    r.Runner = best[i];
                }
            }

            // Normalised: how far past this shape's own floor, measured in units of this shape's own
            // sensitivity. Dimensionless on purpose -- 1.0 is "about as sloppy as the calibration
            // stroke" for every shape alike.
            if (bestIndex >= 0)
            {
                GestureTemplate bt = templates[bestIndex];
                float spread = bt.Spread > 1e-5f ? bt.Spread : 1e-5f;
                r.Excess = Mathf.Max(0f, r.Distance - bt.Floor) / spread;
            }
            else
            {
                r.Excess = float.MaxValue;
            }
            r.Accepted = r.Name != null && r.Distance <= maxDistance && r.Margin >= minMargin;
            return r;
        }

        /// <summary>
        /// How wide a gap, as a fraction of the stroke's size, still counts as a closed loop. Generous
        /// on purpose: with only two shapes in the set there is nothing for a three-quarter loop to be
        /// confused WITH, so the only thing strictness buys is rejected casts the player meant. Tighten
        /// it if an arc or a hook ever becomes its own gesture.
        /// </summary>
        public const float MaxClosableGap = 0.7f;

        /// <summary>
        /// Joins the ends of a nearly-closed stroke before matching.
        ///
        /// A point cloud punishes a gap hard -- the template points along the missing arc have nothing
        /// near them -- and measured, leaving off the last tenth of a circle was enough to turn it into
        /// a misfire. But somebody who draws nine tenths of a loop meant a loop; the gap is sloppiness,
        /// not a different shape. So the loop is closed for RECOGNITION while StrokeMetrics.Closure
        /// still measures the gap, which means the cast lands as the spell you intended and the
        /// sloppiness comes out of its quality instead of cancelling it.
        /// </summary>
        static List<Vector2> CloseLoop(IList<Vector2> raw)
        {
            var outp = new List<Vector2>(raw.Count + 8);
            for (int i = 0; i < raw.Count; i++) outp.Add(raw[i]);
            if (raw.Count < 3) return outp;

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i].x < minX) minX = raw[i].x;
                if (raw[i].y < minY) minY = raw[i].y;
                if (raw[i].x > maxX) maxX = raw[i].x;
                if (raw[i].y > maxY) maxY = raw[i].y;
            }

            float size = Mathf.Max(maxX - minX, maxY - minY);
            if (size < 1e-4f) return outp;

            Vector2 first = raw[0], last = raw[raw.Count - 1];
            float gap = Vector2.Distance(first, last) / size;

            // Too big a gap is not a sloppy loop, it is an arc or a line, and joining it would invent a
            // shape the player never drew.
            if (gap < 1e-3f || gap > MaxClosableGap) return outp;

            const int Steps = 8;
            for (int i = 1; i <= Steps; i++) outp.Add(Vector2.Lerp(last, first, i / (float)Steps));
            return outp;
        }

        static List<Vector2> Rotate(IList<Vector2> pts, float degrees)
        {
            List<Vector2> outp = new List<Vector2>(pts.Count);
            if (Mathf.Abs(degrees) < 1e-4f)
            {
                for (int i = 0; i < pts.Count; i++) outp.Add(pts[i]);
                return outp;
            }

            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            for (int i = 0; i < pts.Count; i++)
                outp.Add(new Vector2(pts[i].x * cos - pts[i].y * sin, pts[i].x * sin + pts[i].y * cos));
            return outp;
        }

        static float GreedyCloudMatch(Vector2[] a, Vector2[] b)
        {
            int n = a.Length;
            float step = Mathf.Floor(Mathf.Pow(n, 0.5f));   // the epsilon = 0.5 of the paper
            if (step < 1f) step = 1f;

            float min = float.MaxValue;
            for (int i = 0; i < n; i += (int)step)
            {
                min = Mathf.Min(min, CloudDistance(a, b, i));
                min = Mathf.Min(min, CloudDistance(b, a, i));
            }
            return min / n;   // per-point, so the threshold does not depend on SampleCount
        }

        static float CloudDistance(Vector2[] a, Vector2[] b, int start)
        {
            int n = a.Length;
            bool[] matched = new bool[n];
            float sum = 0f;
            int i = start;

            do
            {
                int index = -1;
                float min = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (matched[j]) continue;
                    float d = Vector2.Distance(a[i], b[j]);
                    if (d < min) { min = d; index = j; }
                }
                if (index < 0) break;

                matched[index] = true;
                float weight = 1f - ((i - start + n) % n) / (float)n;
                sum += weight * min;
                i = (i + 1) % n;
            }
            while (i != start);

            return sum;
        }

        static Vector2[] Resample(IList<Vector2> pts, int n)
        {
            List<Vector2> src = new List<Vector2>(pts);
            float interval = PathLength(src) / (n - 1);
            if (interval < 1e-6f) interval = 1e-6f;

            List<Vector2> dst = new List<Vector2>(n);
            dst.Add(src[0]);

            float travelled = 0f;
            for (int i = 1; i < src.Count; i++)
            {
                float d = Vector2.Distance(src[i - 1], src[i]);
                if (d < 1e-6f) continue;

                if (travelled + d >= interval)
                {
                    float t = (interval - travelled) / d;
                    Vector2 q = Vector2.Lerp(src[i - 1], src[i], Mathf.Clamp01(t));
                    dst.Add(q);
                    src.Insert(i, q);      // continue measuring from the point we just emitted
                    travelled = 0f;
                }
                else
                {
                    travelled += d;
                }

                if (dst.Count == n) break;
            }

            while (dst.Count < n) dst.Add(src[src.Count - 1]);   // rounding can leave us one short
            return dst.ToArray();
        }

        static float PathLength(IList<Vector2> pts)
        {
            float len = 0f;
            for (int i = 1; i < pts.Count; i++) len += Vector2.Distance(pts[i - 1], pts[i]);
            return len;
        }
    }
}
