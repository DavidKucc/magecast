using MageCast.Gestures;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Fills Resources/SpellFx.asset with the effect prefabs each spell uses. Rerunnable: it only sets
    /// the spells it knows about and leaves anything else in the asset alone.
    ///
    /// The effects come from the Vefects packs in Assets/Vefects, which are bought assets and kept out of
    /// the public repository. Where a pack is missing the entry is simply left empty and the spell falls
    /// back to the placeholder sphere.
    /// </summary>
    public static class SpellFxBuilder
    {
        const string AssetPath = "Assets/Resources/SpellFx.asset";
        const string Magic = "Assets/Vefects/Stylized VFX/Stylized VFX Shuriken/Skills/Magic Attacks/";

        [MenuItem("Tools/Arena/Assign Spell Effects")]
        public static void Assign()
        {
            SpellFx fx = AssetDatabase.LoadAssetAtPath<SpellFx>(AssetPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<SpellFx>();
                AssetDatabase.CreateAsset(fx, AssetPath);
            }

            // Fire: the Vefects fire magic set -- a pentacle flare in the hands, a trailing fireball and a
            // burst, each carrying its own sound (cast, a flight loop, the hit).
            Set(fx, "FIRE", Magic + "Fire/VFX_Fire_Magic_Cast.prefab", Magic + "Fire/VFX_Fire_Magic_Projectile.prefab",
                Magic + "Fire/VFX_Fire_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.5f,
                // the pentacle is a plane facing its local X; turned so it faces along the shot, toward the camera
                castTurn: new Vector3(0f, -90f, 0f));

            // Ice: the matching ice set, same pieces, same orientation.
            Set(fx, "ICE", Magic + "Ice/VFX_Ice_Magic_Cast.prefab", Magic + "Ice/VFX_Ice_Magic_Projectile.prefab",
                Magic + "Ice/VFX_Ice_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.5f,
                castTurn: new Vector3(0f, -90f, 0f));

            // Lightning: the electric set is pink-violet; lightning here is yellow, so it is used through
            // recoloured copies (see Recolour).
            // Made fresh each time, so a change of hue is picked up.
            AssetDatabase.DeleteAsset(Recoloured);
            string[] bolt = new string[3];
            string[] parts = { "Cast", "Projectile", "Hit" };
            for (int i = 0; i < 3; i++)
                bolt[i] = Recolour(Magic + "Electric/VFX_Electric_Magic_" + parts[i] + ".prefab", LightningHue, LightningSaturation);
            // the bolt is thin (radius 0.18) and fast; at 1:1 its effect is a speck, so it is drawn twice as big
            Set(fx, "LIGHTNING", bolt[0], bolt[1], bolt[2], projectileScale: 2f, castScale: 0.22f, impactScale: 0.5f,
                castTurn: new Vector3(0f, -90f, 0f));

            // Air: the pack has no wind attack, but its "sound" set -- rings and pressure waves -- reads as a
            // gust once it is the colour of air, and the AoE pack's air sounds replace its own.
            //
            // Sounds picked by their shape, not their name. The AoE pack's air burst swells for 0.6 s before
            // it peaks -- it is timed for a telegraphed area -- so it is used from just before its peak,
            // and the gust leaving the hands is a short whoosh with its attack at the very start.
            string[] gust = new string[3];
            AudioClip[] gustSounds = {
                null,                                                        // the circle in the hands is silent
                AreaSound("Air", "Area_Loop_01"),                            // wind while it flies
                Trim(AreaSound("Air", "Burst_01"), 0.42f, "Air_Burst_Impact") };   // the burst where it lands
            for (int i = 0; i < 3; i++)
                gust[i] = Recolour(Magic + "Sound/VFX_Sound_Magic_" + parts[i] + ".prefab", AirHue, AirSaturation, gustSounds[i],
                                   // what makes it music rather than wind: the notes, the staff lines, the emblem
                                   "PS_VFX_Notes", "PS_VFX_Rings", "PS_VFX_Flare_Black",
                                   // and the solid flash sphere, which up close is just a blob
                                   "PS_VFX_Flare_Sphere");
            Set(fx, "AIR", gust[0], gust[1], gust[2], projectileScale: 0.8f, castScale: 0.22f, impactScale: 0.6f,
                castTurn: new Vector3(0f, -90f, 0f));
            fx.entries.Find(x => x.spell == "AIR").launch =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Vefects/Anime Stylized VFX/Sounds/WAV/SFX_Dash.wav");

            // Barrier: the energy wall is a shader of our own (EnergyWall); from the packs it takes its
            // sounds and, for breaking, the ice hit's shards turned the barrier's gold.
            SpellFx.Entry wall = fx.entries.Find(x => x.spell == "BARRIER");
            if (wall == null) { wall = new SpellFx.Entry { spell = "BARRIER" }; fx.entries.Add(wall); }
            // breaking: the AoE pack's crystal burst, turned gold -- shards thrown up from where it stood
            string shards = Recolour(Area + "VFX/Crystal/Particles/VFX_Crystal_Burst_01.prefab", BarrierHue, BarrierSaturation, null);
            wall.impact = AssetDatabase.LoadAssetAtPath<GameObject>(shards ?? "");
            wall.impactScale = 0.3f;    // times the wall's width
            // going up: the golden light burst, a dome of light with rays
            wall.special = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/Light/Particles/VFX_Light_Burst_01.prefab");
            wall.specialScale = 0.75f;
            wall.specialLeadIn = 0.5f;
            const string Anime = "Assets/Vefects/Anime Stylized VFX/Sounds/WAV/";
            wall.raise = AssetDatabase.LoadAssetAtPath<AudioClip>(Anime + "SFX_Heal_Cast.wav");        // a shimmer with its attack up front
            wall.hum = AreaSound("Light", "Area_Loop_01");                                            // low, steady
            wall.struck = AssetDatabase.LoadAssetAtPath<AudioClip>(Anime + "SFX_Arrow_Shot_Hit.wav");  // one short crack
            wall.shatter = AssetDatabase.LoadAssetAtPath<AudioClip>(Anime + "SFX_Explosion_Ice.wav");  // glassy, all at once
            EnsureWallMaterial();

            // Patches: the AoE pack's area effects, with its area sounds (a start, a loop, an end).
            SetZone(fx, "FIRE", "Fire");
            SetZone(fx, "ICE", "Ice");
            SetZone(fx, "AIR", "Air");
            SetZone(fx, "BARRIER", "Light");     // the glowing ring on the floor under the tier III dome
            SetZone(fx, "LIGHTNING", "Electric");
            SpellFx.Entry lightningEntry = fx.entries.Find(x => x.spell == "LIGHTNING");
            string yellowArea = Recolour(Area + "VFX/Electric/Particles/VFX_Electric_Area_01.prefab", LightningHue, LightningSaturation, null);
            if (lightningEntry != null && yellowArea != null) lightningEntry.zone = AssetDatabase.LoadAssetAtPath<GameObject>(yellowArea);

            // Tier effects (see SpellTiers). The AoE bursts wind up for half a second before their bang;
            // PlayBurst drops that, and their sounds are trimmed to start at the bang the same way.
            const string AnimeFx = "Assets/Vefects/Anime Stylized VFX/Shared/Particles/";
            const string AnimeSfx = "Assets/Vefects/Anime Stylized VFX/Sounds/WAV/";
            SpellFx.Entry fire = fx.entries.Find(x => x.spell == "FIRE");
            if (fire != null)
            {
                fire.status = AssetDatabase.LoadAssetAtPath<GameObject>(AnimeFx + "VFX_Fire.prefab");      // flames on the burning
                fire.statusScale = 0.45f;
                fire.statusSound = AreaSound("Fire", "Area_Loop_01");
                fire.special = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/Fire/Particles/VFX_Fire_Burst_01.prefab");
                fire.specialScale = 0.85f;                                                                  // ~2.5 m blast
                fire.specialLeadIn = 0.5f;
                fire.specialSound = Trim(AreaSound("Fire", "Burst_01"), 0.42f, "Fire_Burst_Impact");
            }
            SpellFx.Entry ice = fx.entries.Find(x => x.spell == "ICE");
            if (ice != null)
            {
                ice.status = AssetDatabase.LoadAssetAtPath<GameObject>(AnimeFx + "VFX_Explosion_Omni_Ice.prefab");   // frozen
                ice.statusScale = 0.45f;
                ice.statusSound = AssetDatabase.LoadAssetAtPath<AudioClip>(AnimeSfx + "SFX_Explosion_Ice.wav");
            }
            SpellFx.Entry air = fx.entries.Find(x => x.spell == "AIR");
            if (air != null)
            {
                air.special = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/Air/Particles/VFX_Air_Burst_01.prefab");   // the air bomb
                air.specialScale = 1f;
                air.specialLeadIn = 0.5f;
                air.specialSound = Trim(AreaSound("Air", "Burst_01"), 0.42f, "Air_Bomb_Impact");
            }

            AssignIceWall(fx);
            AssignWater(fx);
            AssignFireLine(fx);
            AssignEarth(fx);

            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpellFx] assigned: " + fx.entries.Count + " spell(s) with effects");
        }

        /// <summary>Only water's pieces -- without making the recoloured effects again.</summary>
        [MenuItem("Tools/Arena/Assign Water Effects")]
        public static void AssignWaterOnly()
        {
            SpellFx fx = AssetDatabase.LoadAssetAtPath<SpellFx>(AssetPath);
            if (fx == null) { Debug.LogWarning("[SpellFx] no " + AssetPath + " - run Assign Spell Effects first"); return; }
            AssignWater(fx);
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Only the fire line's piece -- without making the recoloured effects again.</summary>
        [MenuItem("Tools/Arena/Assign Fire Line Effects")]
        public static void AssignFireLineOnly()
        {
            SpellFx fx = AssetDatabase.LoadAssetAtPath<SpellFx>(AssetPath);
            if (fx == null) { Debug.LogWarning("[SpellFx] no " + AssetPath + " - run Assign Spell Effects first"); return; }
            AssignFireLine(fx);
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Only earth's pieces (and magma's and mud's) -- without making the other recoloured effects again.</summary>
        [MenuItem("Tools/Arena/Assign Earth Effects")]
        public static void AssignEarthOnly()
        {
            SpellFx fx = AssetDatabase.LoadAssetAtPath<SpellFx>(AssetPath);
            if (fx == null) { Debug.LogWarning("[SpellFx] no " + AssetPath + " - run Assign Spell Effects first"); return; }
            AssignEarth(fx);
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Earth: the magic set's own earth pieces (a ring of rock in the hands, a boulder trailing dust, a
        /// burst of stones), and for the quake where it lands the AoE pack's earth burst -- which swells for
        /// half a second before its bang, so it is played from the bang and its sound trimmed to start
        /// there (measured: the peak is at 0.50 s). Its patch is the pack's earth area; magma its magma
        /// area; mud the water's area turned the brown of mud.
        /// </summary>
        static void AssignEarth(SpellFx fx)
        {
            Set(fx, "EARTH", Magic + "Earth/VFX_Earth_Magic_Cast.prefab", Magic + "Earth/VFX_Earth_Magic_Projectile.prefab",
                Magic + "Earth/VFX_Earth_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.6f,
                castTurn: new Vector3(0f, -90f, 0f));
            SpellFx.Entry earth = fx.entries.Find(x => x.spell == "EARTH");
            earth.special = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/Earth/Particles/VFX_Earth_Burst_01.prefab");
            earth.specialScale = 0.6f;                                                                  // ~1.8 m quake
            earth.specialLeadIn = 0.5f;
            earth.specialSound = Trim(AreaSound("Earth", "Burst_01"), 0.47f, "Earth_Quake_Impact");
            SetZone(fx, "EARTH", "Earth");
            SetZone(fx, "MAGMA", "Magma");

            SetZone(fx, "MUD", "Water");
            SpellFx.Entry mud = fx.entries.Find(x => x.spell == "MUD");
            darken = MudDarken;
            string brown;
            try { brown = Recolour(Area + "VFX/Water/Particles/VFX_Water_Area_01.prefab", MudHue, MudSaturation, null); }
            finally { darken = 1f; }
            if (mud != null && brown != null) mud.zone = AssetDatabase.LoadAssetAtPath<GameObject>(brown);
            Debug.Log("[SpellFx] earth: " + (earth.projectile != null) + ", quake " + (earth.special != null) + ", magma zone "
                      + (fx.entries.Find(x => x.spell == "MAGMA").zone != null) + ", mud zone " + (mud != null && mud.zone != null));
        }

        /// <summary>Mud: water turned a dark, muddy brown.</summary>
        const float MudHue = 0.075f;
        const float MudSaturation = 0.75f;
        const float MudDarken = 0.42f;

        static void AssignFireLine(SpellFx fx)
        {
            SpellFx.Entry fire = fx.entries.Find(x => x.spell == "FIRE");
            if (fire == null) return;
            fire.linePiece = BuildFireLinePiece();
            if (fire.linePiece == null) Debug.LogWarning("[SpellFx] fire line: pack pieces missing - the plain look will be used");
            else Debug.Log("[SpellFx] fire line: " + AssetDatabase.GetAssetPath(fire.linePiece));
        }

        const string FireMagic = Magic + "Fire/";

        /// <summary>
        /// One metre of a wall of flame, put together from the packs' own fire rather than drawn anew:
        ///   flames      the fireball's flames (Stylized VFX), rising from a 1 m strip instead of trailing a ball
        ///   hot smoke   the fire hit's glowing smoke, slower and above them -- what makes it read tall
        ///   embers      the AoE fire burst's sparks, drifting up
        ///   floor glow  the AoE fire burst's soft glow on the floor -- the line on the ground, without a hard edge
        /// Every system emits along a box 1 m long (x) by the line's width (z), upwards; FireLine stretches
        /// the box and the rate to however far apart it stands its pieces.
        /// </summary>
        static GameObject BuildFireLinePiece()
        {
            const string AreaBurst = Area + "VFX/Fire/Particles/VFX_Fire_Burst_01.prefab";
            ParticleSystem flamesSrc = PackSystem(FireMagic + "VFX_Fire_Magic_Projectile.prefab", "PS_VFX_Flames");
            ParticleSystem smokeSrc = PackSystem(FireMagic + "VFX_Fire_Magic_Hit.prefab", "PS_VFX_Fire_Smokes");
            ParticleSystem embersSrc = PackSystem(AreaBurst, "Embers");
            ParticleSystem glowSrc = PackSystem(AreaBurst, "Glow Floor");
            if (flamesSrc == null) return null;

            var root = new GameObject("VFX_FireLine_Piece");
            try
            {
                // tall tongues: big at the foot, narrowing as they rise, pushed up harder the higher they get
                ParticleSystem flames = Piece(flamesSrc, root, "Flames", 0f);
                Rising(flames, life: new Vector2(0.65f, 1.0f), speed: new Vector2(1.8f, 2.8f), lift: 2.5f,
                       size: new Vector2(1.2f, 1.7f), perMetre: 38f, depth: 0.45f);
                var shrink = flames.sizeOverLifetime;
                shrink.enabled = true;
                shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.75f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.3f)));
                var wobble = flames.noise;
                wobble.strength = 0.5f;

                if (smokeSrc != null)
                {
                    ParticleSystem smoke = Piece(smokeSrc, root, "Hot Smoke", 0.4f);
                    Rising(smoke, life: new Vector2(0.9f, 1.25f), speed: new Vector2(1.8f, 2.6f), lift: 1.2f,
                           size: new Vector2(1.3f, 1.9f), perMetre: 4f, depth: 0.3f);
                }
                if (embersSrc != null)
                {
                    ParticleSystem embers = Piece(embersSrc, root, "Embers", 0.1f);
                    Rising(embers, life: new Vector2(0.6f, 1.3f), speed: new Vector2(2f, 4f), lift: 0.5f,
                           size: new Vector2(0.03f, 0.07f), perMetre: 9f, depth: 0.5f);
                    var drift = embers.noise;
                    drift.enabled = true;
                    drift.strength = 0.8f;
                }
                if (glowSrc != null)
                {
                    ParticleSystem glow = Piece(glowSrc, root, "Floor Glow", 0.06f);
                    Rising(glow, life: new Vector2(0.45f, 0.7f), speed: Vector2.zero, lift: 0f,
                           size: new Vector2(2.0f, 2.6f), perMetre: 6f, depth: 0.2f);
                }

                string folder = Recoloured + "/FireLine";
                EnsureFolder(folder);
                string path = folder + "/VFX_FireLine_Piece.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static ParticleSystem PackSystem(string prefabPath, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.name == name) return ps;
            return null;
        }

        /// <summary>A copy of one of a pack effect's particle systems, on its own, under the piece.</summary>
        static ParticleSystem Piece(ParticleSystem source, GameObject root, string name, float height)
        {
            var copy = (GameObject)Object.Instantiate(source.gameObject);
            copy.name = name;
            foreach (Transform child in copy.transform) Object.DestroyImmediate(child.gameObject);
            copy.transform.SetParent(root.transform, false);
            copy.transform.localPosition = Vector3.up * height;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            return copy.GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// Made to burn on and rise: looping, emitted steadily along a 1 m box lying on the floor, going up
        /// at <paramref name="speed"/> and pushed up by <paramref name="lift"/> m/s^2, not slowed.
        /// </summary>
        static void Rising(ParticleSystem ps, Vector2 life, Vector2 speed, float lift, Vector2 size, float perMetre, float depth)
        {
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startDelay = 0f;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            // not along the shape: a flat box throws its particles out sideways, so the rise is a velocity of its own
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            emission.rateOverTime = perMetre;
            emission.rateOverDistance = 0f;

            // a box 1 m along the line by its depth, lying flat
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, depth, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.position = Vector3.zero;
            shape.randomDirectionAmount = 0f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = false;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = speed.y > 0f;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.speedModifier = 1f;          // the fireball's flames had it at half
            var force = ps.forceOverLifetime;
            force.enabled = lift > 0f;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(0f);
            force.y = new ParticleSystem.MinMaxCurve(lift);
            force.z = new ParticleSystem.MinMaxCurve(0f);
        }

        /// <summary>
        /// Water is not a spell anybody draws: it is ice that went through a line of fire (Projectile), and
        /// the patch fire and ice leave together. It looks like the pack's own water set -- the same
        /// pieces as fire and ice -- and its patch is the AoE pack's water area.
        /// </summary>
        static void AssignWater(SpellFx fx)
        {
            Set(fx, "WATER", Magic + "Water/VFX_Water_Magic_Cast.prefab", Magic + "Water/VFX_Water_Magic_Projectile.prefab",
                Magic + "Water/VFX_Water_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.5f,
                castTurn: new Vector3(0f, -90f, 0f));
            SetZone(fx, "WATER", "Water");
        }

        /// <summary>Only the ice wall's pieces -- without making the recoloured effects again.</summary>
        [MenuItem("Tools/Arena/Assign Ice Wall Effects")]
        public static void AssignIceWallOnly()
        {
            SpellFx fx = AssetDatabase.LoadAssetAtPath<SpellFx>(AssetPath);
            if (fx == null) { Debug.LogWarning("[SpellFx] no " + AssetPath + " - run Assign Spell Effects first"); return; }
            AssignIceWall(fx);
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The ice wall (see IceWall): the AoE pack's pointy rock in its ice material for the spikes, the
        /// pack's ice burst at its foot as it comes up, the anime pack's ice explosion when it breaks.
        /// </summary>
        static void AssignIceWall(SpellFx fx)
        {
            SpellFx.Entry ice = fx.entries.Find(x => x.spell == "ICE");
            if (ice == null) return;
            ice.wallPiece = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(Area + "VFX/Earth/Meshes/SM_VFX_Pointy_Rock_01.FBX"))
                if (o is Mesh) { ice.wallPiece = (Mesh)o; break; }
            ice.wallPieceMaterial = AssetDatabase.LoadAssetAtPath<Material>(Area + "VFX/Ice/Materials/M_VFX_Pointy_Rock_01_Ice.mat");
            ice.wallRaise = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/Ice/Particles/VFX_Ice_Burst_01.prefab");
            ice.wallRaiseScale = 0.8f;
            ice.wallRaiseLeadIn = 0.5f;
            ice.wallBreak = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Anime Stylized VFX/Shared/Particles/VFX_Explosion_Omni_Ice.prefab");
            ice.wallBreakScale = 0.5f;
            ice.wallRaiseSound = Trim(AreaSound("Ice", "Burst_01"), 0.42f, "Ice_Burst_Impact");
            ice.wallBreakSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Vefects/Anime Stylized VFX/Sounds/WAV/SFX_Explosion_Ice.wav");
            if (ice.wallPiece == null || ice.wallPieceMaterial == null)
                Debug.LogWarning("[SpellFx] ice wall: the pointy rock or its ice material is missing - plain crystals will be used");
            else Debug.Log("[SpellFx] ice wall: " + ice.wallPiece.name + " in " + ice.wallPieceMaterial.name);
        }

        static void Set(SpellFx fx, string spell, string cast, string projectile, string impact,
                        float projectileScale, float castScale, float impactScale, Vector3 castTurn = default(Vector3))
        {
            SpellFx.Entry e = fx.entries.Find(x => x.spell == spell);
            if (e == null) { e = new SpellFx.Entry { spell = spell }; fx.entries.Add(e); }

            e.cast = AssetDatabase.LoadAssetAtPath<GameObject>(cast ?? "");
            e.projectile = AssetDatabase.LoadAssetAtPath<GameObject>(projectile ?? "");
            e.impact = AssetDatabase.LoadAssetAtPath<GameObject>(impact ?? "");
            e.projectileScale = projectileScale;
            e.castScale = castScale;
            e.impactScale = impactScale;
            e.castTurn = castTurn;

            if (e.cast == null || e.projectile == null || e.impact == null)
                Debug.LogWarning("[SpellFx] " + spell + ": some effects are missing - is the Vefects pack in Assets/Vefects?");
        }

        const string Area = "Assets/Vefects/Stylized AoE VFX/";

        /// <summary>
        /// A spell's patch: VFX/{element}/Particles/VFX_{element}_Area_01 and its three sounds. The sounds
        /// sit in one of two folders, by who made them, so both are tried.
        /// </summary>
        static void SetZone(SpellFx fx, string spell, string element)
        {
            SpellFx.Entry e = fx.entries.Find(x => x.spell == spell);
            if (e == null) { e = new SpellFx.Entry { spell = spell }; fx.entries.Add(e); }

            e.zone = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/" + element + "/Particles/VFX_" + element + "_Area_01.prefab");
            e.zoneAuthoredRadius = 3f;
            e.zoneLeadIn = 2f;
            // No start sound: the pack's swell for 1.5 s up to an impact that, here, has already happened
            // -- the landing's own impact sound is the start.
            e.zoneStart = null;
            e.zoneLoop = AreaSound(element, "Area_Loop_01");
            e.zoneEnd = AreaSound(element, "Area_End_01");

            if (e.zone == null) Debug.LogWarning("[SpellFx] " + spell + ": no area effect - is the Stylized AoE pack in Assets/Vefects?");
            else MakeMeshesReadable(e.zone);
        }

        /// <summary>
        /// ZoneFx reshapes the area effects' rings and discs to follow a patch cut short by an edge, which
        /// needs their vertices at runtime. The pack imports its meshes unreadable; this turns that on
        /// for the ones an area effect uses.
        /// </summary>
        static void MakeMeshesReadable(GameObject prefab)
        {
            foreach (ParticleSystemRenderer r in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r.renderMode != ParticleSystemRenderMode.Mesh || r.mesh == null || r.mesh.isReadable) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.mesh)) as ModelImporter;
                if (importer == null || importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        static AudioClip AreaSound(string element, string part)
        {
            foreach (string who in new[] { "Chinchi", "Sergi" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    Area + "Audio/WAV/" + who + "/SFX_Vefects_Stylized_AoE_" + element + "_" + part + ".wav");
                if (clip != null) return clip;
            }
            return null;
        }

        // ---------------------------------------------------------------- recolouring

        /// <summary>Electric yellow: a warm yellow, a little paler than pure, so the cores still read white-hot.</summary>
        const float LightningHue = 0.14f;
        const float LightningSaturation = 0.85f;

        /// <summary>The barrier's gold (its spell colour, 1 / 0.85 / 0.4).</summary>
        const float BarrierHue = 0.12f;
        const float BarrierSaturation = 0.9f;

        /// <summary>Air: the spell's own pale mint, kept pale -- wind is barely coloured.</summary>
        const float AirHue = 0.43f;
        const float AirSaturation = 0.35f;

        /// <summary>
        /// Recoloured copies live inside the pack's folder: they are the pack's art, so they stay out of the
        /// public repository with it.
        /// </summary>
        const string Recoloured = "Assets/Vefects/_MageCast";

        /// <summary>
        /// Copies an effect prefab with every colour in it moved to one hue: particle colours and
        /// gradients, lights, and the textures that carry colour of their own (the electric flares and
        /// bolts are painted pink, so tinting the particles alone would leave them pink). Brightness,
        /// transparency and anything already grey or white are kept, so it still glows the same way.
        /// Rerunnable: the copies are simply made again. Returns the copy's path, or null.
        /// </summary>
        static string Recolour(string sourcePath, float hue, float saturation, AudioClip sound = null, params string[] drop)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null) return null;

            string folder = Recoloured + "/" + System.IO.Path.GetFileNameWithoutExtension(sourcePath).Replace("VFX_", "");
            EnsureFolder(folder);
            string target = folder + "/" + System.IO.Path.GetFileName(sourcePath);
            AssetDatabase.DeleteAsset(target);
            AssetDatabase.CopyAsset(sourcePath, target);

            var materials = new System.Collections.Generic.Dictionary<Material, Material>();
            GameObject root = PrefabUtility.LoadPrefabContents(target);
            try
            {
                // parts that do not belong to the spell this is being made into
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t != root.transform && System.Array.IndexOf(drop, t.name) >= 0)
                        Object.DestroyImmediate(t.gameObject);

                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startColor = Shift(main.startColor, hue, saturation);
                    var life = ps.colorOverLifetime;
                    life.color = Shift(life.color, hue, saturation);
                    var speed = ps.colorBySpeed;
                    speed.color = Shift(speed.color, hue, saturation);
                    var trails = ps.trails;
                    trails.colorOverLifetime = Shift(trails.colorOverLifetime, hue, saturation);
                    trails.colorOverTrail = Shift(trails.colorOverTrail, hue, saturation);

                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    if (r == null) continue;
                    Material[] shared = r.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++) shared[i] = CopyMaterial(shared[i], folder, hue, saturation, materials);
                    r.sharedMaterials = shared;
                    if (r.trailMaterial != null) r.trailMaterial = CopyMaterial(r.trailMaterial, folder, hue, saturation, materials);
                }
                foreach (Light l in root.GetComponentsInChildren<Light>(true)) l.color = Shift(l.color, hue, saturation);
                if (sound != null)
                    foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true)) a.clip = sound;
                foreach (TrailRenderer t in root.GetComponentsInChildren<TrailRenderer>(true)) t.colorGradient = Shift(t.colorGradient, hue, saturation);
                foreach (LineRenderer l in root.GetComponentsInChildren<LineRenderer>(true)) l.colorGradient = Shift(l.colorGradient, hue, saturation);

                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return target;
        }

        static Material CopyMaterial(Material m, string folder, float hue, float saturation,
                                     System.Collections.Generic.Dictionary<Material, Material> done)
        {
            if (m == null) return null;
            Material copy;
            if (done.TryGetValue(m, out copy)) return copy;

            copy = new Material(m);
            var shader = m.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
                    copy.SetColor(name, Shift(m.GetColor(name), hue, saturation));
                // Only the pictures that carry colour: the main one, and the colour ramp (LUT) the AoE pack
                // colours its effects through. The other slots (distortion, cutout, noise) pack data into
                // their channels, and "recolouring" those would scramble what they mean.
                else if (type == UnityEngine.Rendering.ShaderPropertyType.Texture
                         && (name == "_Texture" || name == "_MainTex" || name == "_MainTexture" || name == "_LUT"))
                {
                    Texture2D tex = m.GetTexture(name) as Texture2D;
                    Texture2D shifted = RecolourTexture(tex, hue, saturation);
                    if (shifted != null) copy.SetTexture(name, shifted);
                }
            }
            string path = folder + "/" + m.name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            done[m] = copy;
            return copy;
        }

        /// <summary>
        /// A recoloured copy of a texture, if it carries colour at all -- a grey mask is tinted by the
        /// particle colour and needs no copy. Read back through the GPU because the pack's textures are
        /// compressed and not readable. Shared between prefabs: made once per texture.
        /// </summary>
        static Texture2D RecolourTexture(Texture2D tex, float hue, float saturation)
        {
            if (tex == null) return null;
            string path = Recoloured + "/Textures/" + tex.name + "_Hue" + Mathf.RoundToInt(hue * 100f) + "_Sat" + Mathf.RoundToInt(saturation * 100f)
                          + (darken < 1f ? "_Dim" + Mathf.RoundToInt(darken * 100f) : "") + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            int w = tex.width, h = tex.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = readable.GetPixels();
            bool coloured = false;
            for (int i = 0; i < pixels.Length && !coloured; i++)
            {
                float hh, s, v;
                Color.RGBToHSV(pixels[i], out hh, out s, out v);
                coloured = v > 0.2f && s > 0.15f;
            }
            if (!coloured) { Object.DestroyImmediate(readable); return null; }

            for (int i = 0; i < pixels.Length; i++) pixels[i] = Shift(pixels[i], hue, saturation);
            readable.SetPixels(pixels);
            readable.Apply();

            EnsureFolder(Recoloured + "/Textures");
            System.IO.File.WriteAllBytes(path, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path);

            // imported the way the original was, so it blends and wraps the same
            var source = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            var copy = AssetImporter.GetAtPath(path) as TextureImporter;
            if (source != null && copy != null)
            {
                copy.textureType = source.textureType;
                copy.sRGBTexture = source.sRGBTexture;
                copy.alphaSource = source.alphaSource;
                copy.alphaIsTransparency = source.alphaIsTransparency;
                copy.wrapMode = source.wrapMode;
                copy.mipmapEnabled = source.mipmapEnabled;
                copy.maxTextureSize = source.maxTextureSize;
                copy.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Below 1 while recolouring something that must also come out darker -- mud from water: every
        /// colour's brightness times this, white and grey included (they become the hue, dimmed).
        /// </summary>
        static float darken = 1f;

        static Color Shift(Color c, float hue, float saturation)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (darken < 1f)
            {
                Color dim = Color.HSVToRGB(hue, Mathf.Max(Mathf.Clamp01(s * saturation), 0.55f), v * darken, true);
                dim.a = c.a;
                return dim;
            }
            if (s < 0.05f) return c;                    // white and grey stay as they are
            Color shifted = Color.HSVToRGB(hue, Mathf.Clamp01(s * saturation), v, true);
            shifted.a = c.a;
            return shifted;
        }

        static Gradient Shift(Gradient g, float hue, float saturation)
        {
            if (g == null) return null;
            GradientColorKey[] keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Shift(keys[i].color, hue, saturation);
            var copy = new Gradient { mode = g.mode };
            copy.SetKeys(keys, g.alphaKeys);
            return copy;
        }

        static ParticleSystem.MinMaxGradient Shift(ParticleSystem.MinMaxGradient m, float hue, float saturation)
        {
            switch (m.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Shift(m.color, hue, saturation));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Shift(m.colorMin, hue, saturation), Shift(m.colorMax, hue, saturation));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Shift(m.gradient, hue, saturation));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Shift(m.gradientMin, hue, saturation), Shift(m.gradientMax, hue, saturation));
                case ParticleSystemGradientMode.RandomColor:
                    var r = new ParticleSystem.MinMaxGradient(Shift(m.gradient, hue, saturation));
                    r.mode = ParticleSystemGradientMode.RandomColor;
                    return r;
            }
            return m;
        }

        const string WallMaterialPath = "Assets/Resources/RuntimeMaterials/EnergyWall.mat";

        /// <summary>The barrier's material, so the shader is in every build (Shader.Find alone is not).</summary>
        static void EnsureWallMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath) != null) return;
            Shader shader = Shader.Find("MageCast/EnergyWall");
            if (shader == null) { Debug.LogWarning("[SpellFx] MageCast/EnergyWall shader not found"); return; }
            AssetDatabase.CreateAsset(new Material(shader) { name = "EnergyWall" }, WallMaterialPath);
        }

        /// <summary>
        /// A copy of a clip that starts <paramref name="from"/> seconds in, with a few milliseconds' fade so
        /// it does not click -- for a sound whose peak is where it should start. Saved as a WAV next to the
        /// recoloured effects (it is the pack's sound, so it stays out of the repository with it).
        /// </summary>
        static AudioClip Trim(AudioClip source, float from, string name)
        {
            if (source == null) return null;
            int channels = source.channels, rate = source.frequency;
            var all = new float[source.samples * channels];
            if (!source.GetData(all, 0)) return source;

            int skip = Mathf.Clamp(Mathf.RoundToInt(from * rate), 0, source.samples - 1) * channels;
            int count = all.Length - skip;
            int fade = Mathf.RoundToInt(0.008f * rate) * channels;

            var bytes = new byte[44 + count * 2];
            using (var w = new System.IO.BinaryWriter(new System.IO.MemoryStream(bytes)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + count * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
                w.Write((short)1); w.Write((short)channels); w.Write(rate); w.Write(rate * channels * 2);
                w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float v = all[skip + i] * (i < fade ? i / (float)fade : 1f);
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
                }
            }

            EnsureFolder(Recoloured + "/Audio");
            string path = Recoloured + "/Audio/" + name + ".wav";
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
