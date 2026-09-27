using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    public enum StrokeDirection { None, Right, Up, Left, Down }

    /// <summary>
    /// Which way the hand travelled, recovered from the stroke's start-to-end chord.
    ///
    /// This exists because $P cannot answer it. The recognizer matches point CLOUDS, so it is blind to
    /// stroke direction by construction -- a line drawn left-to-right and the same line drawn
    /// right-to-left are identical to it, and no amount of extra templates fixes that. Since the
    /// gesture set spends four of its five entries on directional lines, the direction has to be
    /// measured separately, and the chord is both the simplest and the most robust way to do it.
    ///
    /// Diagonals are deliberately rejected. A stroke that is 45 degrees off is not "probably up" -- it
    /// is a stroke whose author did not commit, and guessing at it would occasionally cast fire when
    /// they wanted air. Same principle as the dead zone that kept a squarish rectangle from silently
    /// picking a side.
    /// </summary>
    public static class StrokeDirections
    {
        /// <summary>
        /// Half-width of each accepted cone. At 35 the four cones cover 280 of 360 degrees and leave
        /// four 20-degree dead zones on the diagonals -- forgiving to draw, but you must still commit.
        /// </summary>
        public const float AcceptHalfAngle = 35f;

        public static StrokeDirection Classify(IList<Vector2> raw)
        {
            float ignored;
            return Classify(raw, out ignored);
        }

        /// <summary>Screen space, y up: Up is +y, Right is +x.</summary>
        public static StrokeDirection Classify(IList<Vector2> raw, out float angleDegrees)
        {
            angleDegrees = 0f;
            if (raw == null || raw.Count < 2) return StrokeDirection.None;

            Vector2 chord = raw[raw.Count - 1] - raw[0];
            if (chord.sqrMagnitude < 1e-6f) return StrokeDirection.None;

            angleDegrees = Mathf.Atan2(chord.y, chord.x) * Mathf.Rad2Deg;

            if (Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 0f)) <= AcceptHalfAngle) return StrokeDirection.Right;
            if (Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 90f)) <= AcceptHalfAngle) return StrokeDirection.Up;
            if (Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 180f)) <= AcceptHalfAngle) return StrokeDirection.Left;
            if (Mathf.Abs(Mathf.DeltaAngle(angleDegrees, 270f)) <= AcceptHalfAngle) return StrokeDirection.Down;

            return StrokeDirection.None;   // diagonal: ambiguous on purpose
        }

        /// <summary>The gesture id a directional line resolves to, e.g. "line_up".</summary>
        public static string GestureId(StrokeDirection d)
        {
            switch (d)
            {
                case StrokeDirection.Right: return "line_right";
                case StrokeDirection.Up: return "line_up";
                case StrokeDirection.Left: return "line_left";
                case StrokeDirection.Down: return "line_down";
                default: return null;
            }
        }
    }
}
