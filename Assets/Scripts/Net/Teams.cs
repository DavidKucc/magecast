using MageCast.Combat;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Sides in a team game. A player's side is <see cref="PlayerNet.Team"/>: 0 in a free-for-all (and in
    /// training), 1 or 2 in a team game, set by the server from the lobby when the body is spawned.
    ///
    /// The rule between teammates is one line: a teammate's spell never hurts you or knocks you about.
    /// Their shots fly through you, their fire does not burn you, their shock patch does not take the rune
    /// out of your hand. What is just the ground stays the ground: their ice is slippery for you too, and
    /// their updraft throws you as high as it throws them -- the one thing a teammate's spell does FOR you.
    /// Your own spells still hurt yourself, exactly as before.
    /// </summary>
    public static class Teams
    {
        public const int None = 0;

        /// <summary>Most players one side can have -- each side has three spawn points.</summary>
        public const int PerTeam = 3;

        public static readonly string[] Names = { "", "Blue", "Red" };

        /// <summary>Index 0 is how anybody looks in a free-for-all: everyone else is an enemy, in red.</summary>
        public static readonly Color[] Colours =
        {
            new Color(1f, 0.55f, 0.5f),
            new Color(0.45f, 0.72f, 1f),
            new Color(1f, 0.42f, 0.36f)
        };

        public static Color Colour(int team)
        {
            return Colours[Mathf.Clamp(team, 0, Colours.Length - 1)];
        }

        /// <summary>Two different players on the same side of a team game.</summary>
        public static bool Allies(PlayerNet a, PlayerNet b)
        {
            return a != null && b != null && a != b && a.Team.Value != None && a.Team.Value == b.Team.Value;
        }

        /// <summary>Whether <paramref name="victim"/> is on the side of whoever <paramref name="attacker"/> is.</summary>
        public static bool Spares(ulong attacker, Component victim)
        {
            if (attacker == Health.NoAttacker || victim == null) return false;
            PlayerNet v = victim.GetComponentInParent<PlayerNet>();
            if (v == null || v.Team.Value == None) return false;
            return Allies(PlayerNet.Find(attacker), v);
        }

        /// <summary>Whether <paramref name="victim"/> is on the side of the player whose body <paramref name="caster"/> is.</summary>
        public static bool Spares(Transform caster, Component victim)
        {
            if (caster == null || victim == null) return false;
            return Allies(caster.GetComponentInParent<PlayerNet>(), victim.GetComponentInParent<PlayerNet>());
        }
    }
}
