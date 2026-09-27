using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MageCast.Gestures;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Turns real strokes from CastLog.csv into acceptance thresholds.
    ///
    /// The inversion here is the point: you do not pick distances, you pick how often each outcome
    /// should happen -- how often a crit, how often nothing at all -- and the thresholds fall out of
    /// what your hand actually did. Picking distances directly means guessing at a number nobody has
    /// any intuition for, which is how the current values came to be, and they were derived from
    /// invented hand-shake rather than a person drawing while being shot at.
    ///
    /// Draw at least ~40 strokes per shape with the practice target set (keys 1-4), ideally a mix of
    /// standing still and running, before trusting the output.
    /// </summary>
    public static class CastLogCalibration
    {
        // The distribution to aim for. These are design choices, not measurements -- change them.
        const float PerfectShare = 0.20f;   // top 20% of your strokes crit
        const float CleanShare = 0.55f;     // next 35% cast normally
        const float WeakShare = 0.80f;      // next 25% cast weakly
        const float MisfireShare = 0.93f;   // next 13% misfire; the last 7% fizzle

        [MenuItem("Tools/Arena/Calibrate From Cast Log")]
        public static void Run()
        {
            Debug.Log(Report());
        }

        public static string Report()
        {
            string path = CastLog.FilePath;
            if (!File.Exists(path))
                return "[Calibrate] No CastLog.csv yet at " + path +
                       "\n  Enter play mode, set a practice target with 1-4, and draw some gestures.";

            string[] lines = File.ReadAllLines(path);
            var all = new List<float>();
            var byIntent = new Dictionary<string, List<float>>();
            var moving = new List<float>();
            var still = new List<float>();
            int rows = 0, unattributed = 0, mismatched = 0;

            for (int i = 1; i < lines.Length; i++)   // skip header
            {
                string[] c = lines[i].Split(',');
                if (c.Length < 10) continue;

                float excess;
                if (!float.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out excess)) continue;
                if (float.IsInfinity(excess) || excess > 10f) continue;   // no template matched at all

                string intended = c[1];
                string recognised = c[2];
                rows++;

                if (intended == "?") { unattributed++; continue; }
                if (recognised != intended && recognised != "-") mismatched++;

                all.Add(excess);
                if (!byIntent.ContainsKey(intended)) byIntent[intended] = new List<float>();
                byIntent[intended].Add(excess);

                if (c[9].Trim() == "1") moving.Add(excess); else still.Add(excess);
            }

            var sb = new StringBuilder("[Calibrate] " + path + "\n");
            sb.AppendFormat("  {0} rows, {1} with a declared target, {2} unattributed (ignored)\n",
                            rows, all.Count, unattributed);

            if (all.Count < 20)
                return sb.Append("  Not enough attributed strokes to calibrate -- draw at least 40 per shape.\n").ToString();

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "  recognised as something other than intended: {0} ({1:F0}%)\n",
                mismatched, 100f * mismatched / Mathf.Max(1, all.Count));

            sb.AppendLine("  median excess by shape:");
            foreach (var kv in byIntent)
            {
                kv.Value.Sort();
                sb.AppendFormat(CultureInfo.InvariantCulture, "    {0,-10} n={1,-4} median {2:F4}  p90 {3:F4}\n",
                                kv.Key, kv.Value.Count, Percentile(kv.Value, 0.5f), Percentile(kv.Value, 0.9f));
            }

            // The number that decides whether the mechanic is fair: drawing while running is the
            // realistic case, and if it costs much more than standing still the tiers are punishing
            // movement rather than sloppiness.
            if (moving.Count > 3 && still.Count > 3)
            {
                moving.Sort(); still.Sort();
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  median while moving {0:F4} (n={1})  vs standing still {2:F4} (n={3})\n",
                    Percentile(moving, 0.5f), moving.Count, Percentile(still, 0.5f), still.Count);
            }

            all.Sort();
            sb.AppendLine("  proposed thresholds for the target distribution "
                          + "(perfect/clean/weak/misfire/fizzle = 20/35/25/13/7%):");
            sb.AppendFormat(CultureInfo.InvariantCulture, "    perfectDistance = {0:F4}f;\n", Percentile(all, PerfectShare));
            sb.AppendFormat(CultureInfo.InvariantCulture, "    cleanDistance   = {0:F4}f;\n", Percentile(all, CleanShare));
            sb.AppendFormat(CultureInfo.InvariantCulture, "    weakDistance    = {0:F4}f;\n", Percentile(all, WeakShare));
            sb.AppendFormat(CultureInfo.InvariantCulture, "    misfireDistance = {0:F4}f;\n", Percentile(all, MisfireShare));
            sb.AppendLine("  Paste into GestureCaster, or type them into the inspector on the Player.");
            return sb.ToString();
        }

        static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0f;
            int i = Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[i];
        }

        [MenuItem("Tools/Arena/Clear Cast Log")]
        public static void Clear()
        {
            if (File.Exists(CastLog.FilePath) &&
                EditorUtility.DisplayDialog("Clear cast log?", CastLog.FilePath, "Delete", "Keep"))
            {
                File.Delete(CastLog.FilePath);
                Debug.Log("[Calibrate] cast log cleared.");
            }
        }
    }
}
