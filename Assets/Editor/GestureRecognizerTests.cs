using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MageCast.Gestures;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Throws synthetic strokes at the recognizer so thresholds come from measurement instead of taste.
    ///
    /// The gesture set is a circle plus four directional lines, and those two halves fail in completely
    /// different ways, so they are tested differently. A circle can be misread as a line; a line can
    /// only be misread as the wrong DIRECTION, which $P knows nothing about -- it is decided by the
    /// stroke's chord. The angle sweep is therefore the important test here, not the shape sweep.
    /// </summary>
    public static class GestureRecognizerTests
    {
        static readonly string[] Ids = { "circle", "line_up", "line_down", "line_left", "line_right" };

        [MenuItem("Tools/Arena/Test Gesture Recognizer")]
        public static void Run()
        {
            Debug.Log(Report());
        }

        public static string Report()
        {
            var sb = new StringBuilder("[Gestures] synthetic stroke test\n");
            var templates = GestureTemplates.All;

            sb.AppendLine("  template calibration:");
            foreach (GestureTemplate t in templates)
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "    {0,-8} floor {1:F4}  concentration {2:F3}  expectedTurning {3:F0}  closed {4}\n",
                    t.Name, t.Floor, t.Concentration, t.ExpectedTurning, t.IsClosed);

            sb.AppendLine("  wobble sweep (correct gesture id, including direction):");
            foreach (float wobble in new[] { 0f, 0.02f, 0.04f, 0.06f, 0.09f, 0.12f, 0.16f, 0.22f, 0.30f })
            {
                int total = 0, correct = 0;
                float worst = 0f;

                foreach (string id in Ids)
                    for (int trial = 0; trial < 10; trial++)
                    {
                        var stroke = MakeStroke(id, Mathf.Lerp(80f, 320f, (trial % 4) / 3f), wobble, trial);
                        var r = PDollarRecognizer.Recognise(stroke, templates, 999f, 0f, 25f);
                        total++;
                        if (ResolveId(r, stroke) == id) { correct++; if (r.Excess > worst) worst = r.Excess; }
                    }

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "    {0:F2}: {1}/{2} correct, worst excess {3:F4}\n", wobble, correct, total, worst);
            }

            return sb.ToString();
        }

        /// <summary>
        /// The one that decides whether the four lines are usable: how far off-axis a line can be drawn
        /// before it stops being read as the direction the player meant, and where the deliberate dead
        /// zones on the diagonals actually sit.
        /// </summary>
        [MenuItem("Tools/Arena/Test Line Angles")]
        public static void RunAngles()
        {
            Debug.Log(AngleReport());
        }

        public static string AngleReport()
        {
            var templates = GestureTemplates.All;
            var sb = new StringBuilder("[Gestures] line angle sweep (wobble 0.06)\n");
            sb.AppendLine("    drawn at   read as        shape excess");

            for (float angle = 0f; angle < 360f; angle += 7.5f)
            {
                var stroke = MakeLineAt(angle, 220f, 0.06f, Mathf.RoundToInt(angle));
                var r = PDollarRecognizer.Recognise(stroke, templates, 999f, 0f, 25f);
                string id = ResolveId(r, stroke) ?? "-- dead zone --";

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "    {0,6:F1}     {1,-14} {2:F4}\n", angle, id, r.Excess);
            }
            return sb.ToString();
        }

        /// <summary>Which gesture gets cast when the player meant a different one.</summary>
        [MenuItem("Tools/Arena/Test Gesture Confusion")]
        public static void RunConfusion()
        {
            Debug.Log(ConfusionReport());
        }

        public static string ConfusionReport()
        {
            var templates = GestureTemplates.All;
            var sb = new StringBuilder("[Gestures] confusion at wobble 0.09 (drawn -> cast)\n");
            sb.AppendLine("    drawn        circle  up      down    left    right   nothing");

            foreach (string drawn in Ids)
            {
                var got = new Dictionary<string, int>();
                foreach (string id in Ids) got[id] = 0;
                int nothing = 0, total = 0;

                for (int trial = 0; trial < 40; trial++)
                {
                    var stroke = MakeStroke(drawn, Mathf.Lerp(80f, 320f, (trial % 4) / 3f), 0.09f, trial);
                    var r = PDollarRecognizer.Recognise(stroke, templates, 0.030f, 0.001f, 25f);
                    var q = MageCast.Gestures.GestureCaster.Grade(r);
                    string id = ResolveId(r, stroke);
                    total++;

                    if (q == CastQuality.Fizzle || q == CastQuality.Misfire || id == null) { nothing++; continue; }
                    got[id]++;
                }

                sb.AppendFormat("    {0,-12}", drawn);
                foreach (string id in Ids)
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0,6:F0}%  ", 100f * got[id] / total);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0,6:F0}%\n", 100f * nothing / total);
            }
            return sb.ToString();
        }

        /// <summary>How the quality tiers land against hand steadiness.</summary>
        [MenuItem("Tools/Arena/Test Cast Quality Tiers")]
        public static void RunTiers()
        {
            Debug.Log(TierReport());
        }

        public static string TierReport()
        {
            var templates = GestureTemplates.All;
            var sb = new StringBuilder("[Gestures] quality tiers vs hand wobble\n");
            sb.AppendLine("    wobble   perfect  clean  weak  misfire  fizzle");

            foreach (float w in new[] { 0f, 0.02f, 0.04f, 0.06f, 0.09f, 0.12f, 0.16f, 0.22f, 0.30f, 0.45f })
            {
                int[] tally = new int[5];
                foreach (string id in Ids)
                    for (int trial = 0; trial < 10; trial++)
                    {
                        var stroke = MakeStroke(id, Mathf.Lerp(80f, 320f, (trial % 4) / 3f), w, trial);
                        var r = PDollarRecognizer.Recognise(stroke, templates, 0.030f, 0.001f, 25f);
                        tally[(int)MageCast.Gestures.GestureCaster.Grade(r)]++;
                    }

                int total = 0;
                for (int i = 0; i < tally.Length; i++) total += tally[i];

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "    {0:F2}    {1,6:F0}% {2,6:F0}% {3,5:F0}% {4,7:F0}% {5,6:F0}%\n", w,
                    100f * tally[(int)CastQuality.Perfect] / total,
                    100f * tally[(int)CastQuality.Clean] / total,
                    100f * tally[(int)CastQuality.Weak] / total,
                    100f * tally[(int)CastQuality.Misfire] / total,
                    100f * tally[(int)CastQuality.Fizzle] / total);
            }
            return sb.ToString();
        }

        /// <summary>How the caster resolves a match into a gesture id, duplicated here on purpose --
        /// the caster's own copy is private and this one has to stay in step with it.</summary>
        static string ResolveId(RecognitionResult r, IList<Vector2> stroke)
        {
            if (r.Name == GestureTemplates.Circle) return GestureTemplates.Circle;
            if (r.Name == GestureTemplates.Line) return StrokeDirections.GestureId(StrokeDirections.Classify(stroke));
            return null;
        }

        static List<Vector2> MakeStroke(string id, float scale, float wobble, int seed)
        {
            if (id == GestureTemplates.Circle)
            {
                Random.InitState(seed * 977 + 11);
                var pts = new List<Vector2>();
                foreach (Vector2 v in GestureTemplates.CirclePoints(60))
                    pts.Add(v * scale + Jitter(wobble * scale) + new Vector2(640f, 360f));
                return pts;
            }

            float angle = 0f;
            if (id == "line_up") angle = 90f;
            else if (id == "line_left") angle = 180f;
            else if (id == "line_down") angle = 270f;

            return MakeLineAt(angle, scale, wobble, seed);
        }

        static List<Vector2> MakeLineAt(float angleDegrees, float scale, float wobble, int seed)
        {
            Random.InitState(seed * 977 + 29);
            float rad = angleDegrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);

            var pts = new List<Vector2>();
            foreach (Vector2 v in GestureTemplates.LinePoints(40))
            {
                Vector2 p = v * scale * 0.5f;
                Vector2 rotated = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);

                // Jitter is scaled by HALF the extent, matching the circle above and the calibration
                // in GestureTemplates. Using the full extent here made test lines twice as shaky as
                // the strokes the spread was calibrated on, and the mismatch looked exactly like a
                // recognizer that hated lines.
                pts.Add(rotated + Jitter(wobble * scale * 0.5f) + new Vector2(640f, 360f));
            }
            return pts;
        }

        static Vector2 Jitter(float amount)
        {
            return new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * amount;
        }
    }
}
