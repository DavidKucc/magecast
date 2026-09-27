using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// The shape vocabulary, generated parametrically rather than recorded by hand -- a hand-recorded
    /// template bakes in one person's wobble, and every later mismatch is then impossible to reason
    /// about.
    ///
    /// The four attack spells used to be one line template plus the direction the hand travelled. That
    /// worked, but a straight line costs about a third of a second and has no ceiling: everybody draws
    /// it identically, so the vulnerability window it was supposed to buy came almost free. The runes
    /// cost real time and real accuracy, which is the whole point of the mechanic.
    ///
    /// Each rune is its own template, unlike the lines, because they are told apart by SHAPE. Which
    /// also means direction no longer matters -- $P is direction-blind by construction, and now that
    /// costs nothing instead of forcing a workaround.
    ///
    /// One thing carries over and is worth knowing: $P is blind to stroke direction but sensitive to
    /// rotation, and the recogniser only searches +/-25 degrees of tilt. For runes that is a feature --
    /// a mirrored Kenaz is a different symbol, not the same one drawn backwards -- but it does mean
    /// tolerance can be loosened on shape and never on angle.
    /// </summary>
    public static class GestureTemplates
    {
        public const string Circle = "circle";

        /// <summary>Kept so the older line-based editor tests still compile. Not in the vocabulary.</summary>
        public const string Line = "line";

        // Kenaz (torch) -> fire, Laguz (water) -> ice, Sowulo (sun) -> lightning,
        // Ehwaz (horse, movement) -> the shove. The meanings are the mnemonic: the player learns a
        // small alphabet rather than five arbitrary squiggles.
        public const string Kenaz = "kenaz";
        public const string Laguz = "laguz";
        public const string Sowulo = "sowulo";
        public const string Ehwaz = "ehwaz";

        /// <summary>
        /// The corner points of each rune, in a -1..1 box, as one continuous stroke.
        ///
        /// Single stroke is a hard requirement -- the recogniser sees one path -- so runes whose
        /// traditional form needs a pen lift or a retrace over their own stem (Algiz, Ansuz) are not
        /// here however well their meaning fits.
        /// </summary>
        static Vector2[] CornersFor(string name)
        {
            if (name == Kenaz)                        // <   one sharp corner, the cheapest to draw
                return new[] { new Vector2(0.8f, 1f), new Vector2(-0.8f, 0f), new Vector2(0.8f, -1f) };

            if (name == Laguz)                        // stem with a branch off the top, drawn branch-first
                return new[] { new Vector2(0.7f, 0.35f), new Vector2(-0.2f, 1f), new Vector2(-0.2f, -1f) };

            if (name == Sowulo)                       // lightning zigzag, two corners
                return new[] { new Vector2(0.7f, 1f), new Vector2(-0.45f, 0.3f),
                               new Vector2(0.45f, -0.3f), new Vector2(-0.7f, -1f) };

            if (name == Ehwaz)                        // M: up, down to the middle, up, down. Three corners.
                return new[] { new Vector2(-0.8f, -1f), new Vector2(-0.8f, 1f), new Vector2(0f, -0.1f),
                               new Vector2(0.8f, 1f), new Vector2(0.8f, -1f) };

            return null;
        }

        public static bool IsRune(string name)
        {
            return name == Kenaz || name == Laguz || name == Sowulo || name == Ehwaz;
        }

        static readonly string[] RuneNames = { Kenaz, Laguz, Sowulo, Ehwaz };

        /// <summary>Walks a corner list into evenly spaced points, the way a hand draws it.</summary>
        public static List<Vector2> RunePoints(string name, int count)
        {
            Vector2[] corners = CornersFor(name);
            var pts = new List<Vector2>(count);
            if (corners == null || corners.Length < 2) return pts;

            float total = 0f;
            for (int i = 1; i < corners.Length; i++) total += Vector2.Distance(corners[i - 1], corners[i]);

            for (int i = 0; i < count; i++)
            {
                float want = total * i / (count - 1);
                float walked = 0f;
                for (int s = 1; s < corners.Length; s++)
                {
                    float len = Vector2.Distance(corners[s - 1], corners[s]);
                    if (walked + len >= want || s == corners.Length - 1)
                    {
                        float f = len < 1e-6f ? 0f : Mathf.Clamp01((want - walked) / len);
                        pts.Add(Vector2.Lerp(corners[s - 1], corners[s], f));
                        break;
                    }
                    walked += len;
                }
            }
            return pts;
        }

        /// <summary>
        /// Total turning along a stroke, in degrees, measured off the points rather than typed in.
        ///
        /// StrokeMetrics reads hand tremor as turning ABOVE what the shape itself needs, so this number
        /// has to be right per shape. Computing it means adding a rune cannot silently ship with a
        /// wrong constant next to it.
        /// </summary>
        static float TurningOf(List<Vector2> pts)
        {
            float total = 0f;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i] - pts[i - 1];
                Vector2 b = pts[i + 1] - pts[i];
                if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) continue;
                total += Vector2.Angle(a, b);
            }
            return total;
        }

        static List<GestureTemplate> cached;

        public static List<GestureTemplate> All
        {
            get
            {
                if (cached == null)
                {
                    var list = new List<GestureTemplate>
                    {
                        new GestureTemplate(Circle, PDollarRecognizer.Normalise(CirclePoints(96), PDollarRecognizer.SampleCount, true))
                        {
                            ExpectedTurning = 360f,   // any closed convex loop
                            IsClosed = true,
                            AlignToChord = false,     // a circle is rotation-symmetric anyway
                            NormaliseAspect = true,   // an oval is a circle as far as the player meant
                        },
                    };

                    // Each rune stands on its own shape, so it is matched at the angle it was authored
                    // at -- AlignToChord would make a rune match any rotation and collapse mirrored
                    // pairs into one. The recogniser's own +/-25 degree tilt search is the tolerance.
                    foreach (string rune in RuneNames)
                    {
                        list.Add(new GestureTemplate(rune,
                                     PDollarRecognizer.Normalise(RunePoints(rune, 64)))
                        {
                            ExpectedTurning = TurningOf(RunePoints(rune, 64)),
                            IsClosed = false,
                            AlignToChord = false,
                            NormaliseAspect = false,   // a squashed rune is a badly drawn rune
                        });
                    }

                    // Calibrate each template against a lightly-wobbled stroke of its own shape, so the
                    // measurement includes the resampling error a real stroke also carries. Anything a
                    // player scores above this floor is their own wobble, which is the only part that
                    // should be graded.
                    foreach (GestureTemplate t in list)
                    {
                        t.Floor = MedianDistance(t, CalibrationWobble);

                        // Two numbers, not one. The floor cancels the offset a shape carries even when
                        // drawn perfectly; the spread cancels how fast it accumulates error afterwards.
                        // Only the second one explains why lines were graded as misfires while circles
                        // sailed through -- see GestureTemplate.Spread.
                        t.Spread = Mathf.Max(1e-5f, MedianDistance(t, SpreadWobble) - t.Floor);

                        t.Concentration = StrokeMetrics.SmoothedConcentration(BasePointsFor(t.Name), t.IsClosed);
                    }

                    cached = list;   // assigned last: MeasureFloor must not re-enter this getter
                }
                return cached;
            }
        }

        public static List<Vector2> CirclePoints(int count)
        {
            List<Vector2> pts = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)(count - 1) * Mathf.PI * 2f;
                pts.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)));
            }
            return pts;
        }

        /// <summary>Horizontal, left to right. Orientation is irrelevant -- the template is matched
        /// chord-aligned -- and so is the direction, which is recovered separately.</summary>
        public static List<Vector2> LinePoints(int count)
        {
            List<Vector2> pts = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
                pts.Add(new Vector2(Mathf.Lerp(-1f, 1f, i / (float)(count - 1)), 0f));
            return pts;
        }

        /// <summary>
        /// The distance this shape scores when drawn with a given amount of hand shake.
        ///
        /// Calibrating on a machine-perfect stroke would measure zero for every shape and miss the
        /// differences entirely -- which it did, on the first attempt. Hence the deliberate wobble:
        /// once at a steady-hand level (the floor) and once at a sloppy level (which gives the spread).
        /// </summary>
        static float MedianDistance(GestureTemplate t, float wobble)
        {
            var single = new List<GestureTemplate> { t };
            var samples = new List<float>();
            uint seed = 12345u;
            float[] tilts = { 0f, 8f, -8f, 16f, -16f };

            foreach (float tilt in tilts)
            {
                for (int rep = 0; rep < 4; rep++)
                {
                    List<Vector2> ideal = BasePointsFor(t.Name);
                    float rad = tilt * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);

                    // Rotating the start index only makes sense for a closed loop; on an open stroke it
                    // would cut the line in half and rejoin it backwards, which is not a wobble, it is
                    // a different shape.
                    int start = t.IsClosed ? rep * 7 : 0;

                    var stroke = new List<Vector2>(ideal.Count);
                    for (int i = 0; i < ideal.Count; i++)
                    {
                        Vector2 p = ideal[(i + start) % ideal.Count] * 200f;
                        Vector2 q = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                        q += new Vector2(NextUnit(ref seed), NextUnit(ref seed)) * (wobble * 200f);
                        stroke.Add(q + new Vector2(500f, 300f));
                    }
                    samples.Add(PDollarRecognizer.Recognise(stroke, single, float.MaxValue, 0f).Distance);
                }
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        /// <summary>How steady a hand counts as "perfect". Everything above this is the player's own error.</summary>
        const float CalibrationWobble = 0.02f;

        /// <summary>
        /// The reference sloppiness that defines one unit of excess. A stroke this shaky scores about
        /// 1.0 whichever shape it was, which is what makes the quality thresholds shape-independent.
        /// </summary>
        const float SpreadWobble = 0.10f;

        public static List<Vector2> BasePointsFor(string name)
        {
            if (name == Circle) return CirclePoints(61);
            if (IsRune(name)) return RunePoints(name, 49);
            return LinePoints(37);
        }

        /// <summary>
        /// Own generator on purpose: Random.InitState would reset Unity's global sequence during static
        /// init and quietly change gameplay randomness -- misfire spread, for one -- somewhere else.
        /// </summary>
        static float NextUnit(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return ((state >> 9) & 0xFFFF) / 32767.5f - 1f;
        }
    }
}
