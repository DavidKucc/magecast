namespace MageCast.Gestures
{
    /// <summary>
    /// How well a spell was drawn, as the three tiers it shows: I (Weak), II (Clean), III (Perfect).
    ///
    /// Tiers add what a spell DOES, not just how hard it hits -- the damage itself only moves 0.8-1.2x
    /// with precision. Tier I is the plain spell: its hit and nothing else, no patch, no bounce. Tier II
    /// is the spell as designed. Tier III adds one thing on top that the opponent can see coming, since
    /// the tier is shown over the caster's head.
    ///
    ///            I            II                                   III
    ///   fire     damage       + burning, patch, wall bounce        + explosion around the hit
    ///   ice      damage       + slow, patch                        + frozen in place (can still draw)
    ///   light.   damage       + jumps to the nearest other target, + the patch under whoever it hit
    ///                           into the floor: a shock patch
    ///   air      shove        + updraft patch; blows fire away     + air bomb in a patch: pulls in
    ///                           as a wave, blows people over ice
    ///   barrier  wall         bigger, tougher wall                 dome around you, your spells pass
    ///
    /// Every air hit interrupts drawing, at every tier.
    ///
    /// Tier III does not keep: held for more than TopTierHold seconds it falls to tier II. Otherwise
    /// the best play would be drawing behind cover until a III comes out and walking around with it,
    /// and the risk of the game lives in drawing under pressure.
    /// </summary>
    public static class SpellTiers
    {
        public const float TopTierHold = 4f;

        /// <summary>Precision a tier III is left with when it falls to II -- just under the III line.</summary>
        public const float FallenPrecision = 0.84f;

        /// <summary>Precision a tier II is left with when a hit knocks it to I -- just under the II line.</summary>
        public const float KnockedPrecision = 0.64f;

        // Every element's own button-down: a spell of the same element cannot be SENT again until this
        // long after the last one was. Drawing is free meanwhile -- you may draw it and hold it -- and
        // the other elements are free, so it stops Kenaz-Kenaz-Kenaz without slowing the game down.
        public const float ElementCooldown = 2f;

        // Tier I's splash: a fire or ice that lands on the map (floor, wall, barrier) rather than on a
        // person hurts whoever is near, the caster included. The middle gets this share of the hit,
        // falling to nothing at the edge; a barrier or a wall in between stops it. Damage only -- no
        // burning, no slow, no patch -- and it never knocks the tier of what anybody is holding.
        public const float SplashRadius = 2f;
        public const float SplashShare = 0.3f;

        // fire (5 Oct: 4/s for 3 s down to 3/s for 2 s, with the hit itself down to 16)
        public const float BurnPerSecond = 3f;
        public const float BurnSeconds = 2f;
        public const float ExplosionRadius = 2.5f;
        public const float ExplosionShare = 0.5f;        // of the hit's damage, to everyone else in reach
        public const float ExplosionBarrierWear = 1.5f;  // times the usual

        // ice
        public const float FreezeSeconds = 0.6f;

        // lightning
        public const float ChainRange = 6f;
        public const float ChainShare = 0.5f;
        public const float ShockRadius = 2.2f;
        public const float ShockSeconds = 4f;
        public const float ShockPerSecond = 3f;          // 4 s of it is 12: never more than the bolt itself

        // air
        public const float BombReachBeyondPatch = 1.5f;
        public const float BombLift = 6.5f;              // m/s up; with the motor's gravity, ~0.6 s in the air
        /// <summary>From the air landing in the patch to the bang: the time to get out of reach.</summary>
        public const float BombDelay = 0.4f;

        // barrier
        public const float WallWidthII = 1.35f;
        public const float WallHeightII = 1.15f;
        public const float WallDurabilityI = 0.8f;
        public const float WallDurabilityII = 1.4f;
        public const float DomeRadius = 2.1f;
        public const float DomeSeconds = 4.5f;
        public const float DomeDurability = 1.6f;

        public static int Of(CastQuality q)
        {
            switch (q)
            {
                case CastQuality.Perfect: return 3;
                case CastQuality.Clean: return 2;
                default: return 1;
            }
        }

        public static string Roman(int tier)
        {
            return tier >= 3 ? "III" : tier == 2 ? "II" : "I";
        }
    }
}
