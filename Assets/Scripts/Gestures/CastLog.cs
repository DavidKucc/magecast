using System.Globalization;
using System.IO;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Appends one row per cast to CastLog.csv in the project root.
    ///
    /// This exists because every acceptance threshold in the game is currently derived from synthetic
    /// hand-shake that was invented, not measured. The mapping from "wobble 0.09" to an actual person
    /// drawing under pressure is a guess, and a guess is a bad thing to hang the core mechanic on --
    /// nobody draws a clean circle while being shot at. Play, draw, then run
    /// Tools > Arena > Calibrate From Cast Log to turn the rows into thresholds.
    ///
    /// Records intent as well as outcome: without knowing what the player was TRYING to draw, a
    /// fizzle is unattributable and the sample is worthless.
    /// </summary>
    public static class CastLog
    {
        public static string FilePath
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../CastLog.csv")); }
        }

        const string Header = "time,intended,recognised,excess,margin,quality,seconds,pixels,points,moving," +
                              "steadiness,definition,closure,sizepx";

public static void Record(string intended, string recognised, float excess, float margin,
                                  CastQuality quality, float seconds, float pixels, int points, bool moving,
                                  StrokeMetrics metrics)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // A browser has nowhere to put a file next to the game. The web build is for rules and
            // mechanics; calibration data comes from the exe.
            return;
#else
            try
            {
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, Header + "\n");

                string row = string.Format(CultureInfo.InvariantCulture,
                    "{0:F1},{1},{2},{3:F5},{4:F5},{5},{6:F2},{7:F0},{8},{9},{10:F3},{11:F3},{12:F3},{13:F0}\n",
                    Time.time, intended ?? "?", recognised ?? "-", excess, margin, quality,
                    seconds, pixels, points, moving ? 1 : 0,
                    metrics.Steadiness, metrics.Definition, metrics.Closure, metrics.SizePixels);

                File.AppendAllText(FilePath, row);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CastLog] could not write: " + e.Message);
            }
#endif
        }
    }
}
