using System.Collections.Generic;
using MageCast.Gestures;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Does the rune vocabulary actually hold together?
    ///
    /// Every symbol added to a gesture set makes the ones already in it harder to tell apart, and
    /// nothing in the recogniser warns about that -- a rune that collides with another simply gets
    /// cast wrong sometimes, which reads as "the game misread me" rather than "these two shapes are
    /// too alike". So it gets measured before it gets played.
    ///
    /// Two things matter per rune. Whether a wobbled stroke of it still comes back as itself, and how
    /// much room there is between it and its nearest rival. The second is the one that predicts
    /// trouble: a rune that wins by a hair today will lose to a tired hand tomorrow.
    /// </summary>
    public static class RuneSetTests
    {
        /// <summary>Hand shake to draw with. 0.10 is the sloppiness the quality thresholds call "weak".</summary>
        const float Wobble = 0.075f;

        /// <summary>
        /// How much daylight there must be between first and second place, in the SAME units the game
        /// grades quality in -- excess, meaning (distance - floor) / spread.
        ///
        /// Getting this scale wrong the first time reported every shape as dangerously close. Raw
        /// distances live around 0.03 while spread is around 0.013, so a raw margin of 0.03 is really
        /// more than two excess units -- and the quality bands only run to 1.80 before a stroke counts
        /// as a misfire. One unit is the bar: closer than that and the rival is inside a quality band.
        /// </summary>
        const float SafeMargin = 1.0f;

        [MenuItem("Tools/Arena/Test Rune Set")]
        public static void Run()
        {
            List<GestureTemplate> all = GestureTemplates.All;
            var report = new System.Text.StringBuilder();
            report.AppendLine("[Runes] every shape drawn with a shaky hand, " + all.Count
                              + " templates in the vocabulary\n");
            report.AppendLine("shape        drawn as     own excess   margin to 2nd place");

            int problems = 0;
            uint seed = 20260827u;

            foreach (GestureTemplate t in all)
            {
                float worstMargin = float.MaxValue;
                float worstExcess = 0f;
                int wrong = 0, tries = 0;
                float spread = t.Spread > 1e-5f ? t.Spread : 1e-5f;

                float[] tilts = { 0f, 10f, -10f, 20f, -20f };
                foreach (float tilt in tilts)
                {
                    for (int rep = 0; rep < 4; rep++)
                    {
                        List<Vector2> stroke = Draw(t.Name, tilt, Wobble, ref seed);

                        // Recognise already reports the runner-up and the gap; no need to re-run it
                        // per template, which is also how the game itself decides.
                        RecognitionResult r = PDollarRecognizer.Recognise(stroke, all, float.MaxValue, 0f);

                        tries++;
                        if (r.Name != t.Name) wrong++;

                        float margin = r.Margin / spread;      // same scale the quality bands use
                        if (margin < worstMargin) worstMargin = margin;
                        if (r.Excess > worstExcess) worstExcess = r.Excess;
                    }
                }

                bool tooClose = worstMargin < SafeMargin;
                if (wrong > 0 || tooClose) problems++;

                report.AppendLine("  " + t.Name.PadRight(11)
                    + (wrong == 0 ? "itself     " : (wrong + "/" + tries + " WRONG").PadRight(11))
                    + worstExcess.ToString("F2").PadLeft(9) + " worst"
                    + worstMargin.ToString("F1").PadLeft(11)
                    + (wrong > 0 ? "   <-- MISREAD"
                                 : tooClose ? "   <-- too close for comfort" : "   ok"));
            }
            report.AppendLine();
            report.AppendLine("  (excess: 0.35 = perfect, 1.15 = weak, 1.80 = misfire. margin is in the same units)");

            report.AppendLine();
            report.AppendLine(problems == 0
                ? "the set holds: every shape came back as itself with room to spare"
                : problems + " shape(s) worth looking at");

            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>
        /// What a hand of a given shakiness actually scores, across the whole vocabulary.
        ///
        /// This is the table the thresholds should be read off, rather than picked. The current ones
        /// were set when the vocabulary was a circle and a line; runes are harder to draw, so the same
        /// numbers grade a steadier hand as a worse cast without anybody having changed them.
        ///
        /// Both gates are reported because they are separate and easy to confuse: DISTANCE decides
        /// whether a stroke casts at all, PRECISION decides how hard it hits.
        /// </summary>
        [MenuItem("Tools/Arena/Test Cast Difficulty")]
        public static void Sweep()
        {
            List<GestureTemplate> all = GestureTemplates.All;
            float[] wobbles = { 0.02f, 0.04f, 0.06f, 0.08f, 0.10f, 0.13f, 0.16f, 0.20f, 0.25f };

            var report = new System.Text.StringBuilder();
            report.AppendLine("[Casting] how a hand of each shakiness scores, median over all shapes\n");
            // Precision is deliberately NOT reported here. This wobble is independent noise on every
            // point, which manufactures far more turning than a real hand -- measured, it graded a near
            // perfect hand as "weak" while real rune strokes in CastLog.csv held steadiness at 0.86-0.95.
            // A column that confident and that wrong is worse than no column.
            report.AppendLine("  hand      excess     casts   misfire   nothing");

            uint seed = 7771u;
            foreach (float wobble in wobbles)
            {
                var excesses = new List<float>();
                var outcomes = new List<CastQuality>();

                foreach (GestureTemplate t in all)
                {
                    float[] tilts = { 0f, 10f, -10f, 20f, -20f };
                    foreach (float tilt in tilts)
                    {
                        List<Vector2> stroke = Draw(t.Name, tilt, wobble, ref seed);
                        RecognitionResult r = PDollarRecognizer.Recognise(stroke, all, float.MaxValue, 0f);
                        excesses.Add(r.Excess);
                        outcomes.Add(GestureCaster.Grade(r));
                    }
                }

                excesses.Sort();
                float excess = excesses[excesses.Count / 2];

                int cast = 0, wild = 0, nothing = 0;
                foreach (CastQuality q in outcomes)
                {
                    if (q == CastQuality.Fizzle) nothing++;
                    else if (q == CastQuality.Misfire) wild++;
                    else cast++;
                }
                float n = Mathf.Max(1, outcomes.Count);

                report.AppendLine("  " + wobble.ToString("F2").PadLeft(5)
                    + excess.ToString("F2").PadLeft(11)
                    + (cast / n).ToString("P0").PadLeft(9)
                    + (wild / n).ToString("P0").PadLeft(10)
                    + (nothing / n).ToString("P0").PadLeft(10));
            }

            report.AppendLine();
            report.AppendLine("  graded by GestureCaster.Grade itself, so this follows the game's real gates.");
            report.AppendLine("  compare against CastLog.csv: a real hand is far steadier than this white-noise");
            report.AppendLine("  wobble, so read the SHAPE of the curve here and the real numbers there.");
            Debug.Log(report.ToString());
        }

        /// <summary>
        /// A synthetic stroke of one shape, tilted and shaken.
        ///
        /// Deliberately not the template's own points fed straight back in -- that would measure zero
        /// and prove nothing. The wobble is what a real hand contributes, and it is the thing that
        /// pushes one shape into another's territory.
        /// </summary>
        static List<Vector2> Draw(string shape, float tiltDegrees, float wobble, ref uint seed)
        {
            List<Vector2> ideal = GestureTemplates.BasePointsFor(shape);
            float rad = tiltDegrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);

            var stroke = new List<Vector2>(ideal.Count);
            foreach (Vector2 point in ideal)
            {
                Vector2 p = point * 200f;
                Vector2 q = new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                q += new Vector2(NextUnit(ref seed), NextUnit(ref seed)) * (wobble * 200f);
                stroke.Add(q + new Vector2(500f, 300f));
            }
            return stroke;
        }

        static float NextUnit(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return ((state >> 9) & 0xFFFF) / 32767.5f - 1f;
        }
    }
}
