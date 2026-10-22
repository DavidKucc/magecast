using System.Collections.Generic;
using MageCast.Gestures;
using MageCast.World;
using UnityEditor;
using UnityEngine;
using static MageCast.EditorTools.ArenaKit;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The first map's look: the Sanctum. The blockout's boxes (ArenaBlockoutBuilder) are the game -- this
    /// only dresses them, and adds what is on them, in the painted style of the spell effects: a stone
    /// courtyard floating in a violet dusk, runes of the six elements glowing in the floor, a crystal
    /// turning over the core.
    ///
    ///   stone     SanctumStone: triplanar slabs and bricks with worn, lit edges and big colour patches
    ///   walls     a cornice and battlements against the sky, pilasters, banners in the element colours
    ///             with their runes in gold, crystal lanterns between them
    ///   core      a plinth, buttresses, a band of runes that lights up in the colour of what hits it;
    ///             on top the healing spring under a floating crystal; a launch pad at the foot of each side
    ///   pillars   rune pillars, each in one element's colour, with fire in a bowl on top
    ///   floor     a ring of rune channels round the core with the six element runes on it
    ///   beyond    floating rocks and a painted sky; motes of light drifting over the arena
    ///
    /// Nothing here changes how the map plays except the actives (launch pads, spring): every addition
    /// with a collider sits within a few tenths of a metre of the box it dresses, and nothing changes
    /// a height the game relies on (chest cover stays 1.6 m, platforms 1.2 m).
    /// </summary>
    public static class ArenaSanctum
    {
        const string MatFolder = "Assets/Materials/Sanctum";

        const float Half = 20f, WallHeight = 6f, WallThick = 1f, CoreHalf = 6.5f, CoreHigh = 5f;

        static Material floor, wall, core, pillar, cover, slab, plat, trim, rock, disc, metal, crystal, pool;
        static readonly Dictionary<string, Material> cloths = new Dictionary<string, Material>();

        public static void Dress(Transform root)
        {
            Materials();
            ApplyToLayout(root);

            Transform look = Group("Look", root);
            Walls(Group("Walls", look));
            Core(Group("Core", look), root.Find("Core"));
            Pillars(Group("Pillars", look), root.Find("Pillars"));
            Quarters(Group("Quarters", look), root.Find("Cover"));
            FloorRunes(Group("FloorRunes", look));
            Beyond(Group("Beyond", look));
            Motes(look, new Vector3(0f, 3.5f, 0f), new Vector3(38f, 6f, 38f), 14f, 140, 0.13f, Arcane, Gold, new Color(1f, 0.6f, 0.8f));

            BakeGlows(look);
        }

        // ------------------------------------------------------------------ materials

        static void Materials()
        {
            Begin(MatFolder, "Sanctum");
            Texture2D flags = ArenaTempleBuilder.PatternTexture(TexFolder + "/Sanctum_Flagstones.png", 3, 3, true, 51);
            Texture2D bricks = ArenaTempleBuilder.PatternTexture(TexFolder + "/Sanctum_Bricks.png", 4, 8, true, 53);
            Texture2D blocks = ArenaTempleBuilder.PatternTexture(TexFolder + "/Sanctum_Blocks.png", 2, 3, true, 57);

            floor  = Stone("Sanctum_Floor",  flags,  5f, new Color(0.63f, 0.57f, 0.52f), new Color(0.58f, 0.55f, 0.58f), new Color(0.68f, 0.62f, 0.54f), new Color(0.78f, 0.72f, 0.62f), 0.05f, 0.3f);
            wall   = Stone("Sanctum_Wall",   bricks, 4f, new Color(0.46f, 0.43f, 0.54f), new Color(0.40f, 0.44f, 0.50f), new Color(0.42f, 0.50f, 0.40f), new Color(0.74f, 0.70f, 0.82f), 0.14f, 0.35f);
            core   = Stone("Sanctum_Core",   blocks, 3f, new Color(0.82f, 0.78f, 0.73f), new Color(0.76f, 0.74f, 0.80f), new Color(0.80f, 0.78f, 0.70f), new Color(1.00f, 0.88f, 0.62f), 0.16f, 0.3f);
            pillar = Stone("Sanctum_Pillar", blocks, 2f, new Color(0.80f, 0.76f, 0.70f), new Color(0.74f, 0.72f, 0.78f), new Color(0.80f, 0.78f, 0.70f), new Color(1.00f, 0.90f, 0.70f), 0.12f, 0.3f);
            cover  = Stone("Sanctum_Cover",  blocks, 2f, new Color(0.72f, 0.58f, 0.44f), new Color(0.66f, 0.56f, 0.50f), new Color(0.66f, 0.62f, 0.48f), new Color(0.94f, 0.82f, 0.64f), 0.12f, 0.3f);
            slab   = Stone("Sanctum_Slab",   bricks, 3f, new Color(0.44f, 0.41f, 0.52f), new Color(0.38f, 0.42f, 0.50f), new Color(0.44f, 0.50f, 0.42f), new Color(0.72f, 0.68f, 0.82f), 0.14f, 0.3f);
            plat   = Stone("Sanctum_Dais",   flags,  3f, new Color(0.44f, 0.55f, 0.56f), new Color(0.48f, 0.52f, 0.60f), new Color(0.58f, 0.66f, 0.62f), new Color(0.78f, 0.92f, 0.86f), 0.14f, 0.25f);
            trim   = Stone("Sanctum_Trim",   blocks, 2f, new Color(0.86f, 0.76f, 0.56f), new Color(0.82f, 0.74f, 0.60f), new Color(0.90f, 0.82f, 0.62f), new Color(1.00f, 0.92f, 0.70f), 0.08f, 0.15f);
            rock   = Stone("Sanctum_Rock",   blocks, 4f, new Color(0.50f, 0.44f, 0.48f), new Color(0.42f, 0.40f, 0.50f), new Color(0.46f, 0.58f, 0.40f), Color.white, 0f, 0.4f);
            // round things (cylinders) have no box edges to find
            disc   = Stone("Sanctum_Disc",   flags,  2f, new Color(0.40f, 0.48f, 0.52f), new Color(0.42f, 0.46f, 0.54f), new Color(0.46f, 0.56f, 0.58f), Color.white, 0f, 0f);

            metal = Standard("Sanctum_Metal", new Color(0.18f, 0.15f, 0.14f), 0.6f, 0.45f, Color.black);
            // lit enough to glow, not so much that its facets vanish into one flat shape
            crystal = Standard("Sanctum_Crystal", new Color(0.30f, 0.90f, 0.90f), 0f, 0.9f, new Color(0.10f, 0.55f, 0.50f));
            pool = Standard("Sanctum_Spring", new Color(0.25f, 0.65f, 0.55f), 0f, 0.95f, new Color(0.25f, 1.1f, 0.75f));

            foreach (var e in Elements()) cloths[e.Key] = Standard("Sanctum_Banner_" + e.Key, e.Value * 0.55f, 0f, 0.15f, e.Value * 0.12f);
        }

        /// <summary>The blockout's own boxes in the Sanctum's stone.</summary>
        static void ApplyToLayout(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                Material m = n == "Floor" ? floor
                           : n.StartsWith("Wall_") ? wall
                           : n == "Core" ? core
                           : n.StartsWith("Pillar_") ? pillar
                           : n.StartsWith("Cover_") ? cover
                           : n.StartsWith("Slab_") || n.StartsWith("Fin_") ? slab
                           : n.StartsWith("Platform") ? plat
                           : null;
                if (m == null) continue;
                r.sharedMaterial = m;
                Prepare(r.gameObject);
            }
        }

        // ------------------------------------------------------------------ walls

        static void Walls(Transform t)
        {
            float wo = Half + WallThick * 0.5f, wl = Half * 2f + WallThick * 2f;
            string[] order = { "Fire", "Ice", "Lightning", "Air", "Earth", "Barrier", "Fire", "Ice" };
            int banner = 0;
            for (int side = 0; side < 4; side++)
            {
                // each wall in its own frame: x along it, z out of the arena
                Transform w = Group("Wall_" + side, t);
                w.localRotation = Quaternion.Euler(0f, 90f * side, 0f);

                // every other side's cornice a touch deeper, so where two cross at a corner their tops do not share a plane
                Box("Cornice", w, trim, new Vector3(0f, WallHeight + 0.2f, wo), new Vector3(wl + 0.6f, 0.4f + (side % 2) * 2f * Proud, WallThick + 0.7f));
                for (float x = -Half + 1.3f; x <= Half - 1.2f; x += 2.6f)
                    Box("Merlon", w, wall, new Vector3(x, WallHeight + 0.85f, wo + 0.1f), new Vector3(1.2f, 0.9f, WallThick + 0.1f));

                foreach (float x in new[] { -14f, -7f, 0f, 7f, 14f })
                {
                    // stops inside its head, so the two tops are not one plane
                    Box("Pilaster", w, wall, new Vector3(x, (WallHeight - 0.1f) * 0.5f, Half - 0.22f), new Vector3(1.1f, WallHeight - 0.1f, 0.45f));
                    Box("Pilaster_Base", w, trim, new Vector3(x, 0.35f, Half - 0.35f), new Vector3(1.45f, 0.7f, 0.7f));
                    Box("Pilaster_Head", w, trim, new Vector3(x, WallHeight - 0.25f, Half - 0.32f), new Vector3(1.35f, 0.5f, 0.64f));
                    if (Mathf.Abs(x) > 6f && Mathf.Abs(x) < 8f)
                        Banner(w, new Vector3(x, 0f, Half - 0.47f), order[banner++ % order.Length]);
                }

                foreach (float x in new[] { -10.5f, -3.5f, 3.5f, 10.5f })
                    Lantern(w, new Vector3(x, 3.6f, Half));
            }
        }

        /// <summary>A banner in an element's colour, hung on a pilaster's face (at z, facing -z).</summary>
        static void Banner(Transform w, Vector3 at, string element)
        {
            ArenaKit.Banner(w, at, element, cloths[element], metal);
        }

        /// <summary>A crystal lantern on a bracket, its glow on the wall (wall face at z, facing -z).</summary>
        static void Lantern(Transform w, Vector3 at)
        {
            Transform l = Group("Lantern", w);
            l.localPosition = at;
            Deco("Bracket", l, metal, new Vector3(0f, -0.15f, -0.25f), new Vector3(0.18f, 0.12f, 0.5f));
            Deco("Cup", l, metal, new Vector3(0f, -0.05f, -0.45f), new Vector3(0.3f, 0.12f, 0.3f));
            GameObject c = Crystal("Crystal", l, crystal, new Vector3(0f, 0.22f, -0.45f), new Vector3(0.22f, 0.42f, 0.22f));
            c.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            HaloQuad("Glow", l, new Vector3(0f, 0.15f, -0.02f), Quaternion.identity, 1.7f, Halo("Arcane", Arcane * 0.55f));
        }

        // ------------------------------------------------------------------ the core

        static void Core(Transform t, Transform coreBox)
        {
            Box("Plinth", t, trim, new Vector3(0f, 0.22f, 0f), new Vector3(CoreHalf * 2f + 0.8f, 0.45f, CoreHalf * 2f + 0.8f));
            foreach (Vector2 c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                Box("Buttress", t, core, new Vector3(c.x * CoreHalf, (CoreHigh + 0.3f) * 0.5f, c.y * CoreHalf), new Vector3(1.2f, CoreHigh + 0.3f, 1.2f));

            var lines = new List<LineRenderer>();
            for (int side = 0; side < 4; side++)
            {
                Transform f = Group("Face_" + side, t);
                f.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                Box("Cornice", f, trim, new Vector3(0f, CoreHigh - 0.35f, -CoreHalf - 0.12f), new Vector3(CoreHalf * 2f + 0.2f, 0.4f + (side % 2) * 2f * Proud, 0.35f));
                Vector3 normal = -f.forward;
                Vector3 onFace = f.TransformPoint(new Vector3(0f, 0f, -CoreHalf - 0.025f));
                lines.Add(Line("Band_Low", f, new[] { onFace + f.right * -5.6f + Vector3.up * 1.1f, onFace + f.right * 5.6f + Vector3.up * 1.1f }, Arcane, 0.07f, normal));
                lines.Add(Line("Band_High", f, new[] { onFace + f.right * -5.6f + Vector3.up * 3.95f, onFace + f.right * 5.6f + Vector3.up * 3.95f }, Arcane, 0.07f, normal));
                // a ring on each face (no rune symbol in it: not wanted in the maps, 22 Oct)
                lines.Add(Circle("Ring", f, onFace + Vector3.up * 2.5f, normal, 1.3f, Arcane, 0.05f));
            }
            RuneGlow glow = coreBox.gameObject.AddComponent<RuneGlow>();
            glow.rest = Arcane * 0.85f;
            glow.lines = lines.ToArray();

            // on top: the healing spring, under a floating crystal
            Transform top = Group("Top", t);
            top.localPosition = new Vector3(0f, CoreHigh, 0f);
            GameObject water = Cylinder("Spring", top, pool, new Vector3(0f, 0.02f, 0f), 4.4f, 0.04f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            Box("Spring_Rim", top, trim, new Vector3(0f, 0.06f, 2.35f), new Vector3(4.9f, 0.12f, 0.25f), 0f, false);
            Box("Spring_Rim", top, trim, new Vector3(0f, 0.06f, -2.35f), new Vector3(4.9f, 0.12f, 0.25f), 0f, false);
            // these two fit between the other two rather than crossing them at the corners
            Box("Spring_Rim", top, trim, new Vector3(2.35f, 0.06f, 0f), new Vector3(0.25f, 0.12f, 4.45f), 0f, false);
            Box("Spring_Rim", top, trim, new Vector3(-2.35f, 0.06f, 0f), new Vector3(0.25f, 0.12f, 4.45f), 0f, false);
            Vector3 c0 = top.position + Vector3.up * 0.06f;
            RuneGlow springGlow = top.gameObject.AddComponent<RuneGlow>();
            springGlow.rest = new Color(0.4f, 1f, 0.6f);
            springGlow.lines = new[] { Circle("Ring", top, c0, Vector3.up, 2.05f, springGlow.rest, 0.08f),
                                       Circle("Ring_Inner", top, c0, Vector3.up, 1.35f, springGlow.rest, 0.05f) };
            HealingSpring spring = top.gameObject.AddComponent<HealingSpring>();
            spring.radius = 2.2f;
            Effect("Spring_Fx", top, new Vector3(0f, 0.05f, 0f), AoE + "Heal/Particles/VFX_Heal_Area_01.prefab", 0.75f, 0f,
                   "Wave LOOP", "Light Rays", "Embers LOOP", "Main Area Indicator LOOP", "Sparkle Star", "Pillar", "Embers Pillar");

            Transform float_ = Group("Floating_Crystal", top);
            float_.localPosition = new Vector3(0f, 3.6f, 0f);
            GameObject big = Crystal("Crystal", float_, crystal, Vector3.zero, new Vector3(1.3f, 2.3f, 1.3f));
            big.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Crystal("Shard", float_, crystal, new Vector3(1.4f, -0.6f, 0.3f), new Vector3(0.35f, 0.7f, 0.35f));
            Crystal("Shard", float_, crystal, new Vector3(-1.1f, 0.5f, -0.9f), new Vector3(0.3f, 0.6f, 0.3f));
            Crystal("Shard", float_, crystal, new Vector3(-0.4f, -0.9f, 1.3f), new Vector3(0.25f, 0.5f, 0.25f));
            Billboard("Glow", float_, Vector3.zero, 6f, Arcane * 0.45f);
            FloatingBob bob = float_.gameObject.AddComponent<FloatingBob>();
            bob.height = 0.35f;
            bob.period = 5f;
            bob.spin = 22f;

            // a launch pad at the foot of each side, throwing you up onto the top near that edge
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, 90f * side, 0f);
                // Far enough out that a throw from its near edge still clears the cornice: at 1.9 m from the
                // core a player stepping on there came down on the cornice's lip instead of the top (21 Oct).
                Vector3 at = q * new Vector3(0f, 0.02f, -(CoreHalf + 3.1f));
                Vector3 land = q * new Vector3(0f, CoreHigh + 0.05f, -(CoreHalf - 3.2f));
                Pad(t, at, land);
            }
        }

        static void Pad(Transform t, Vector3 at, Vector3 landing)
        {
            Transform p = Group("LaunchPad", t);
            p.localPosition = at;
            GameObject padDisc = Cylinder("Disc", p, disc, new Vector3(0f, 0.015f, 0f), 2.4f, 0.03f);
            Object.DestroyImmediate(padDisc.GetComponent<Collider>());
            Vector3 c = p.position + Vector3.up * 0.05f;
            var lines = new List<LineRenderer>
            {
                Circle("Ring", p, c, Vector3.up, 1.1f, Air, 0.08f),
                Circle("Ring_Inner", p, c, Vector3.up, 0.8f, Air, 0.04f),
            };
            HaloQuad("Glow", p, new Vector3(0f, 0.055f, 0f), Quaternion.Euler(90f, 0f, 0f), 3.2f, Halo("Air", Air * 0.35f));
            Effect("Air_Fx", p, Vector3.zero, AoE + "Air/Particles/VFX_Air_Area_01.prefab", 0.35f, 0f,
                   "Light Rays", "Embers LOOP", "Thin Smoke LOOP", "Zone Indicator LOOP");

            RuneGlow g = p.gameObject.AddComponent<RuneGlow>();
            g.rest = Air * 0.9f;
            g.breath = 0.4f;
            g.breathPeriod = 1.8f;
            g.lines = lines.ToArray();
            LaunchPad pad = p.gameObject.AddComponent<LaunchPad>();
            pad.landing = landing;
            // a high arc: well over the cornice before it is reached, even in a browser at a few frames a second
            pad.apex = 3f;
            pad.radius = 1.1f;
            pad.rune = g;
            pad.sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Anime + "Sounds/WAV/SFX_Dash.wav");
        }

        // ------------------------------------------------------------------ pillars

        static void Pillars(Transform t, Transform boxes)
        {
            string[] elements = { "Fire", "Ice", "Lightning", "Air" };
            for (int i = 0; i < 4; i++)
            {
                Transform box = boxes.Find("Pillar_" + i);
                if (box == null) continue;
                Transform p = Group("Pillar_" + i, t);
                p.position = new Vector3(box.position.x, 0f, box.position.z);
                Color colour = ColourOf(elements[i]);

                Box("Base", p, trim, new Vector3(0f, 0.3f, 0f), new Vector3(2.1f, 0.6f, 2.1f));
                Box("Collar", p, trim, new Vector3(0f, 0.75f, 0f), new Vector3(1.75f, 0.3f, 1.75f));
                Box("Capital", p, trim, new Vector3(0f, WallHeight - 0.22f + Proud, 0f), new Vector3(2.0f, 0.45f, 2.0f));
                GameObject bowl = Cylinder("Bowl", p, metal, new Vector3(0f, WallHeight + 0.18f, 0f), 1.45f, 0.18f);
                Object.DestroyImmediate(bowl.GetComponent<Collider>());
                Flame(p, new Vector3(0f, WallHeight + 0.24f, 0f), 1.15f);
                Billboard("Glow", p, new Vector3(0f, WallHeight + 0.9f, 0f), 4.5f, new Color(1f, 0.55f, 0.2f) * 0.4f);

                var lines = new List<LineRenderer>();
                for (int side = 0; side < 4; side++)
                {
                    Quaternion q = Quaternion.Euler(0f, 90f * side, 0f);
                    Vector3 normal = q * Vector3.back;
                    Vector3 right = q * Vector3.right;
                    Vector3 face = p.position + normal * 0.775f;
                    lines.Add(Line("Edge_L", p, new[] { face - right * 0.5f + Vector3.up * 1.05f, face - right * 0.5f + Vector3.up * 5.3f }, colour, 0.05f, normal));
                    lines.Add(Line("Edge_R", p, new[] { face + right * 0.5f + Vector3.up * 1.05f, face + right * 0.5f + Vector3.up * 5.3f }, colour, 0.05f, normal));
                }
                RuneGlow g = box.gameObject.AddComponent<RuneGlow>();
                g.rest = colour * 0.85f;
                g.phase = i * 0.25f;
                g.lines = lines.ToArray();
            }
        }

        // ------------------------------------------------------------------ the quarters' cover

        static void Quarters(Transform t, Transform cover)
        {
            string[] slabRunes = { "Earth", "Barrier", "Earth", "Barrier" };
            for (int q = 0; q < 4; q++)
            {
                Transform quarter = cover.Find("Quadrant_" + q);
                if (quarter == null) continue;
                Transform look = Group("Quarter_" + q, t);
                look.localRotation = quarter.localRotation;

                Transform chest = quarter.Find("Cover_Chest");
                if (chest != null)
                    Box("Cap", look, trim, chest.localPosition + new Vector3(0f, 0.72f + Proud, 0f), new Vector3(chest.localScale.x + 0.2f, 0.16f, chest.localScale.z + 0.2f), 0f, false);

                Transform s = quarter.Find("Slab_A");
                if (s != null)
                {
                    Box("Cap", look, trim, s.localPosition + new Vector3(0f, s.localScale.y * 0.5f - 0.15f + Proud, 0f), new Vector3(s.localScale.x + 0.3f, 0.3f, s.localScale.z + 0.3f), 0f, false);
                    var lines = new List<LineRenderer>();
                    Color colour = ColourOf(slabRunes[q]);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        Vector3 normal = s.forward * sign;
                        Vector3 face = s.position + normal * (s.localScale.z * 0.5f + 0.025f);
                        Vector3 at = new Vector3(face.x, 0f, face.z);
                        lines.Add(Line("Band", look, new[] { at - s.right * 2.6f + Vector3.up * 2.75f, at + s.right * 2.6f + Vector3.up * 2.75f }, colour, 0.05f, normal));
                        lines.Add(Circle("Ring", look, at + Vector3.up * 1.6f, normal, 0.78f, colour, 0.04f));
                    }
                    RuneGlow g = s.gameObject.AddComponent<RuneGlow>();
                    g.rest = colour * 0.8f;
                    g.phase = q * 0.3f + 0.1f;
                    g.lines = lines.ToArray();
                }

                Transform p = quarter.Find("Platform");
                if (p != null)
                {
                    Vector3 top = p.position + Vector3.up * (p.localScale.y * 0.5f + 0.02f);
                    var lines = new List<LineRenderer>
                    {
                        Circle("Ring", look, top, Vector3.up, 1.9f, Arcane, 0.06f),
                        Circle("Ring_Inner", look, top, Vector3.up, 1.55f, Arcane, 0.03f)
                    };
                    RuneGlow g = p.gameObject.AddComponent<RuneGlow>();
                    g.rest = Arcane * 0.7f;
                    g.phase = q * 0.3f + 0.5f;
                    g.lines = lines.ToArray();
                    HaloQuad("Glow", look, look.InverseTransformPoint(top + Vector3.up * 0.01f), Quaternion.Euler(90f, 0f, 0f), 4.4f, Halo("Arcane_Floor", Arcane * 0.18f));
                }
            }
        }

        // ------------------------------------------------------------------ the floor

        /// <summary>A double ring on the floor round the core (once with the six element runes on it).</summary>
        static void FloorRunes(Transform t)
        {
            Vector3 c = new Vector3(0f, 0.012f, 0f);
            Transform ring = Group("Ring", t);
            RuneGlow rg = ring.gameObject.AddComponent<RuneGlow>();
            rg.rest = Arcane * 0.7f;
            rg.breath = 0.3f;
            rg.breathPeriod = 5f;
            rg.lines = new[] { Circle("Outer", ring, c, Vector3.up, 10.95f, Arcane, 0.07f),
                               Circle("Inner", ring, c, Vector3.up, 10.25f, Arcane, 0.04f) };
        }

        // ------------------------------------------------------------------ beyond the walls

        static void Beyond(Transform t)
        {
            var rnd = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                float a = (i * 20f + (float)rnd.NextDouble() * 12f) * Mathf.Deg2Rad;
                float r = 40f + (float)rnd.NextDouble() * 34f;
                float y = 2f + (float)rnd.NextDouble() * 20f;
                float s = 2.5f + (float)rnd.NextDouble() * 6f;
                Island(t, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), s, rnd, rock, i % 3 == 0 ? crystal : null, Arcane * 0.3f);
            }
            // a few big ones far out, to give the horizon a shape
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 30f) * Mathf.Deg2Rad;
                Island(t, new Vector3(Mathf.Cos(a) * 130f, -6f + i * 3f, Mathf.Sin(a) * 130f), 22f + i * 3f, rnd, rock, crystal, Arcane * 0.3f);
            }
        }

        // ------------------------------------------------------------------ atmosphere

        public static void Atmosphere()
        {
            Light sun = Sun(new Color(1f, 0.80f, 0.64f), 1.1f, 0.85f, new Vector3(30f, 145f, 0f));
            Ambient(new Color(0.48f, 0.43f, 0.70f), new Color(0.56f, 0.44f, 0.50f), new Color(0.24f, 0.20f, 0.27f), 0.45f);
            Sky("Sanctum_Sky", sun, new SkyColours
            {
                zenith = new Color(0.10f, 0.08f, 0.27f), middle = new Color(0.38f, 0.24f, 0.52f),
                horizon = new Color(1.0f, 0.60f, 0.40f), below = new Color(0.16f, 0.12f, 0.24f),
                sun = new Color(1.0f, 0.78f, 0.55f), cloud = new Color(1.0f, 0.70f, 0.62f),
                sunSize = 0.0012f, stars = 0.8f, clouds = 0.55f,
                cloudCover = 0.55f, cloudHeight = 0.55f, cloudScale = 1f, cloudStretch = 4f,
            });
        }

        public static void BakeEnvironment()
        {
            ArenaKit.BakeEnvironment("Assets/Scenes/Arena_Blockout_Lighting.lighting");
        }
    }
}
