using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>What became of one of your own shots.</summary>
    public enum ShotOutcome { Player, Dummy, Near, Barrier, Miss }

    /// <summary>
    /// How often your spells actually land, per spell, for this session -- the number that decides
    /// whether aiming is fun or a chore. Measured on the caster's own machine, from their own copy of
    /// every shot: a direct hit on a player or a training dummy, a near miss (it came down within
    /// <see cref="NearRadius"/> of somebody), stopped by a barrier, or a clean miss.
    ///
    /// The target to aim for is roughly 35-45% direct hits with projectiles in a real match. Far under
    /// that and dodging wins every exchange; far over and there is nothing to dodge.
    /// </summary>
    public static class HitStats
    {
        public const float NearRadius = 2f;

        class Row
        {
            public string Spell;
            public readonly int[] Count = new int[5];
            public int Total { get { int t = 0; foreach (int c in Count) t += c; return t; } }
        }

        static readonly List<Row> rows = new List<Row>();

        public static int Total
        {
            get { int t = 0; foreach (Row r in rows) t += r.Total; return t; }
        }

        public static void Record(string spell, ShotOutcome outcome)
        {
            if (string.IsNullOrEmpty(spell)) return;
            Row row = rows.Find(r => r.Spell == spell);
            if (row == null) { row = new Row { Spell = spell }; rows.Add(row); }
            row.Count[(int)outcome]++;
        }

        public static void Reset() { rows.Clear(); }

        /// <summary>One line per spell, plus all of them together. Plain text, so it pastes anywhere.</summary>
        public static string Report()
        {
            var sb = new StringBuilder();
            var all = new Row { Spell = "ALL" };
            foreach (Row r in rows)
            {
                Line(sb, r);
                for (int i = 0; i < 5; i++) all.Count[i] += r.Count[i];
            }
            if (rows.Count > 1) Line(sb, all);
            return sb.Length == 0 ? "no shots yet" : sb.ToString().TrimEnd();
        }

        static void Line(StringBuilder sb, Row r)
        {
            int n = Mathf.Max(1, r.Total);
            int hit = r.Count[(int)ShotOutcome.Player] + r.Count[(int)ShotOutcome.Dummy];
            sb.AppendFormat("{0,-10} {1,3} shots   hit {2,3}%  (players {3}, dummies {4})   near {5,3}%   barrier {6}   missed {7,3}%\n",
                            r.Spell, r.Total, Pct(hit, n), r.Count[(int)ShotOutcome.Player], r.Count[(int)ShotOutcome.Dummy],
                            Pct(r.Count[(int)ShotOutcome.Near], n), r.Count[(int)ShotOutcome.Barrier],
                            Pct(r.Count[(int)ShotOutcome.Miss], n));
        }

        static int Pct(int part, int whole) { return Mathf.RoundToInt(100f * part / whole); }
    }
}
