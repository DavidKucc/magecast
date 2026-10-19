namespace MageCast.Gestures
{
    /// <summary>Which global cooldown a spell shares: attacks, defences, and the special ones (the portal, to come).</summary>
    public enum SpellRole { Offensive, Defensive, Special }

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

        // Every element's own cooldown: a spell of the same element cannot be made again until this long
        // after the last one was sent. A rune of it finished sooner makes nothing (NOT READY) and the
        // drawing was wasted -- watch the bar. Within QueueWindow of the end it does make its spell.
        // The other elements are free. (2 s until 13 Oct, raised with the global cooldown.)
        public const float ElementCooldown = 2.5f;

        // The global cooldown, as in WoW, one per kind of spell: after sending an attack, no other
        // attack can be SENT for this long, whatever its element. Drawing is free meanwhile -- the next
        // rune is drawn during the pause and sent as it ends. With five elements on separate cooldowns
        // there was always something ready, and six players found the best play was to fire whatever
        // came up. A barrier is on its own clock: defence never waits for the rhythm of attack.
        // (1.3 s until 13 Oct; 1.5 is WoW's.)
        public const float GlobalCooldown = 1.5f;

        /// <summary>
        /// A send clicked this close to the end of a cooldown goes off by itself when it ends (WoW's spell
        /// queue window), so the rhythm does not punish a click a moment early, or a ping.
        /// </summary>
        public const float QueueWindow = 0.4f;

        /// <summary>
        /// Interrupted mid-rune (air, a shock patch): the element that could already be read from what was
        /// drawn is locked for this long -- WoW's school lockout. Read too early to tell, nothing is locked.
        /// </summary>
        public const float SchoolLock = 3.5f;

        /// <summary>The role of a spell, by its id (GestureCaster.BarrierId...). Special: the portal, one day.</summary>
        public static SpellRole RoleOf(byte spellId)
        {
            return spellId == GestureCaster.BarrierId ? SpellRole.Defensive : SpellRole.Offensive;
        }

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
        // Shoved into a wall: air does no damage itself, but the wall does. Only an air shove counts,
        // only while it is still carrying you this fast, and the stagger is a short stop on the spot.
        public const float SlamDamage = 8f;
        public const float SlamStagger = 0.4f;
        public const float SlamSpeed = 3f;            // m/s of the shove still left: a clean shove slams up to ~2.3 m from a wall
        public const float SlamWindow = 1.5f;         // s after the shove that a slam still counts
        // Your own updraft throws you this fast: 13.3 m/s against the motor's 22 m/s^2 is 4 m up --
        // a tower roof or a bridge. Everyone else's throws you the usual 2 m.
        public const float UpdraftSelfLaunch = 13.3f;
        public const float BombReachBeyondPatch = 1.5f;
        public const float BombLift = 6.5f;              // m/s up; with the motor's gravity, ~0.6 s in the air
        /// <summary>From the air landing in the patch to the bang: the time to get out of reach.</summary>
        public const float BombDelay = 0.4f;

        // ice wall: ice aimed at the floor this close in front of you (from your feet) rises as a wall of
        // ice instead of flying -- cover that one hit shatters, gone in a few seconds anyway. See IceWall.
        public const float IceWallReach = 4.5f;
        /// <summary>Never closer than this, or it would come up through its caster.</summary>
        public const float IceWallMinDistance = 1.6f;
        /// <summary>How far below your feet the floor may be -- off a wall top, onto the ground under it.</summary>
        public const float IceWallDrop = 8f;
        public const float IceWallSeconds = 6f;
        public const float IceWallThickness = 0.7f;
        /// <summary>By tier (index 1-3): a tier I wall covers you crouched, a III a little more than standing.</summary>
        public static readonly float[] IceWallWidth = { 0f, 2.0f, 2.8f, 3.4f };
        public static readonly float[] IceWallHeight = { 0f, 1.7f, 2.3f, 2.6f };

        // fire line: fire aimed at the floor this close in front of you (the ice wall's reach) lays a line
        // of fire across the way instead of flying. It stops nothing; walking through it sets you burning
        // like a tier II hit, again every FireLineReburn seconds you stay. See FireLine.
        public const float FireLineSeconds = 4f;
        public const float FireLineWidth = 0.8f;
        public const float FireLineFlameHeight = 1.8f;
        public const float FireLineReburn = 1f;
        /// <summary>By tier (index 1-3).</summary>
        public static readonly float[] FireLineLength = { 0f, 3f, 4.5f, 6f };

        // water: ice thrown through a fire line comes out as water -- the ice's hit, no slow, and whoever it
        // hits is wet for WetSeconds. Lightning into somebody wet strikes this many times as hard (and dries
        // them); fire on somebody wet only steams. See Wet.
        public const float WetSeconds = 4f;
        public const float WetShockMultiplier = 2f;
        /// <summary>Wet feet on an ice patch -- walked onto, or laid under them -- freeze to it for this long.</summary>
        public const float WetFreezeSeconds = 1.5f;

        // barrier -- since 14 Oct the strong, lasting cover (the ice wall is the quick, fragile one):
        // 50 % more durability and 8 s instead of 6, the dome 6 s instead of 4.5
        public const float WallSeconds = 8f;
        public const float WallWidthII = 1.35f;
        public const float WallHeightII = 1.15f;
        public const float WallDurabilityI = 0.8f;
        public const float WallDurabilityII = 1.4f;
        public const float DomeRadius = 2.1f;
        public const float DomeSeconds = 6f;
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
