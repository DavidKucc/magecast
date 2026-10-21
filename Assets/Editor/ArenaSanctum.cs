using System.Collections.Generic;
using MageCast.Gestures;
using MageCast.World;
using UnityEditor;
using UnityEngine;

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
        const string TexFolder = "Assets/Textures/Sanctum";
        const string MeshFolder = "Assets/Meshes";
        const string AoE = "Assets/Vefects/Stylized AoE VFX/VFX/";
        const string Anime = "Assets/Vefects/Anime Stylized VFX/";

        const float Half = 20f, WallHeight = 6f, WallThick = 1f, CoreHalf = 6.5f, CoreHigh = 5f;

        // the elements, as the game colours them (MenuArt.ElementColours)
        static readonly Color Fire = new Color(1f, 0.45f, 0.15f), Ice = new Color(0.55f, 0.85f, 1f),
                              Bolt = new Color(1f, 0.9f, 0.35f), Air = new Color(0.75f, 1f, 0.9f),
                              Earth = new Color(0.85f, 0.62f, 0.38f), Ward = new Color(1f, 0.85f, 0.4f),
                              Arcane = new Color(0.3f, 1f, 0.92f), Gold = new Color(1f, 0.8f, 0.42f);

        static Material floor, wall, core, pillar, cover, slab, plat, trim, rock, disc, metal, crystal, pool, glowLine, mote;
        static readonly Dictionary<string, Material> halos = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> cloths = new Dictionary<string, Material>();
        static Mesh crystalMesh;

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
            Motes(look);
        }

        // ------------------------------------------------------------------ materials

        static void Materials()
        {
            Folder(MatFolder);
            Folder(TexFolder);
            Folder(MeshFolder);
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

            glowLine = Glow("Sanctum_GlowLine", Color.white * 1.15f, false, 1.6f, 0.9f);
            mote = Glow("Sanctum_Mote", Color.white * 1.2f, true, 2.6f, 1.2f);
            foreach (var e in Elements()) cloths[e.Key] = Standard("Sanctum_Banner_" + e.Key, e.Value * 0.55f, 0f, 0.15f, e.Value * 0.12f);

            crystalMesh = CrystalMesh();
        }

        static Material Halo(string name, Color colour)
        {
            Material m;
            if (halos.TryGetValue(name, out m)) return m;
            m = Glow("Sanctum_Halo_" + name, colour, true, 2.2f, 0.25f);
            halos[name] = m;
            return m;
        }

        static IEnumerable<KeyValuePair<string, Color>> Elements()
        {
            yield return new KeyValuePair<string, Color>("Fire", Fire);
            yield return new KeyValuePair<string, Color>("Ice", Ice);
            yield return new KeyValuePair<string, Color>("Lightning", Bolt);
            yield return new KeyValuePair<string, Color>("Air", Air);
            yield return new KeyValuePair<string, Color>("Earth", Earth);
            yield return new KeyValuePair<string, Color>("Barrier", Ward);
        }

        static string RuneOf(string element)
        {
            switch (element)
            {
                case "Fire": return GestureTemplates.Kenaz;
                case "Ice": return GestureTemplates.Laguz;
                case "Lightning": return GestureTemplates.Sowulo;
                case "Air": return GestureTemplates.Ehwaz;
                case "Earth": return GestureTemplates.Thurisaz;
                default: return GestureTemplates.Uruz;
            }
        }

        static Color ColourOf(string element)
        {
            foreach (var e in Elements()) if (e.Key == element) return e.Value;
            return Arcane;
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
                Unbatched(r.gameObject);
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

                Box("Cornice", w, trim, new Vector3(0f, WallHeight + 0.2f, wo), new Vector3(wl + 0.6f, 0.4f, WallThick + 0.7f));
                for (float x = -Half + 1.3f; x <= Half - 1.2f; x += 2.6f)
                    Box("Merlon", w, wall, new Vector3(x, WallHeight + 0.85f, wo + 0.1f), new Vector3(1.2f, 0.9f, WallThick + 0.1f));

                foreach (float x in new[] { -14f, -7f, 0f, 7f, 14f })
                {
                    Box("Pilaster", w, wall, new Vector3(x, WallHeight * 0.5f, Half - 0.22f), new Vector3(1.1f, WallHeight, 0.45f));
                    Box("Pilaster_Base", w, trim, new Vector3(x, 0.35f, Half - 0.35f), new Vector3(1.45f, 0.7f, 0.7f));
                    Box("Pilaster_Head", w, trim, new Vector3(x, WallHeight - 0.25f, Half - 0.32f), new Vector3(1.35f, 0.5f, 0.64f));
                    if (Mathf.Abs(x) > 6f && Mathf.Abs(x) < 8f)
                        Banner(w, new Vector3(x, 0f, Half - 0.47f), order[banner++ % order.Length]);
                }

                foreach (float x in new[] { -17.5f, -10.5f, -3.5f, 3.5f, 10.5f, 17.5f })
                    Lantern(w, new Vector3(x, 3.6f, Half));
            }
        }

        /// <summary>A banner in an element's colour with its rune in gold, hung on a pilaster's face (at z, facing -z).</summary>
        static void Banner(Transform w, Vector3 at, string element)
        {
            Transform b = Group("Banner_" + element, w);
            b.localPosition = at;
            Deco("Rod", b, metal, new Vector3(0f, 5.05f, -0.02f), new Vector3(1.3f, 0.08f, 0.08f));
            Deco("Cloth", b, cloths[element], new Vector3(0f, 3.75f, 0f), new Vector3(1.05f, 2.55f, 0.04f));
            // the swallowtail: two small pieces at the bottom
            Deco("Tail_L", b, cloths[element], new Vector3(-0.3f, 2.35f, 0f), new Vector3(0.42f, 0.32f, 0.04f));
            Deco("Tail_R", b, cloths[element], new Vector3(0.3f, 2.35f, 0f), new Vector3(0.42f, 0.32f, 0.04f));
            Vector3 face = b.TransformPoint(new Vector3(0f, 3.85f, -0.035f));
            LineRenderer rune = Glyph("Rune", b, RuneOf(element), face, b.right, b.up, 0.62f, Gold, 0.07f, -b.forward);
            RuneGlow g = b.gameObject.AddComponent<RuneGlow>();
            g.rest = Gold * 0.9f;
            g.breath = 0.15f;
            g.lines = new[] { rune };
        }

        /// <summary>A crystal lantern on a bracket, its glow on the wall (wall face at z, facing -z).</summary>
        static void Lantern(Transform w, Vector3 at)
        {
            Transform l = Group("Lantern", w);
            l.localPosition = at;
            Deco("Bracket", l, metal, new Vector3(0f, -0.15f, -0.25f), new Vector3(0.18f, 0.12f, 0.5f));
            Deco("Cup", l, metal, new Vector3(0f, -0.05f, -0.45f), new Vector3(0.3f, 0.12f, 0.3f));
            GameObject c = Crystal("Crystal", l, new Vector3(0f, 0.22f, -0.45f), new Vector3(0.22f, 0.42f, 0.22f));
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
            string[] faces = { "Barrier", "Earth", "Barrier", "Earth" };
            for (int side = 0; side < 4; side++)
            {
                Transform f = Group("Face_" + side, t);
                f.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                Box("Cornice", f, trim, new Vector3(0f, CoreHigh - 0.35f, -CoreHalf - 0.12f), new Vector3(CoreHalf * 2f + 0.2f, 0.4f, 0.35f));
                Vector3 normal = -f.forward;
                Vector3 onFace = f.TransformPoint(new Vector3(0f, 0f, -CoreHalf - 0.025f));
                lines.Add(Line("Band_Low", f, new[] { onFace + f.right * -5.6f + Vector3.up * 1.1f, onFace + f.right * 5.6f + Vector3.up * 1.1f }, Arcane, 0.07f, normal));
                lines.Add(Line("Band_High", f, new[] { onFace + f.right * -5.6f + Vector3.up * 3.95f, onFace + f.right * 5.6f + Vector3.up * 3.95f }, Arcane, 0.07f, normal));
                lines.Add(Glyph("Rune", f, RuneOf(faces[side]), onFace + Vector3.up * 2.5f, f.right, Vector3.up, 1.1f, Arcane, 0.11f, normal));
                // a ring round the rune
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
            Box("Spring_Rim", top, trim, new Vector3(2.35f, 0.06f, 0f), new Vector3(0.25f, 0.12f, 4.9f), 0f, false);
            Box("Spring_Rim", top, trim, new Vector3(-2.35f, 0.06f, 0f), new Vector3(0.25f, 0.12f, 4.9f), 0f, false);
            Vector3 c0 = top.position + Vector3.up * 0.06f;
            RuneGlow springGlow = top.gameObject.AddComponent<RuneGlow>();
            springGlow.rest = new Color(0.4f, 1f, 0.6f);
            springGlow.lines = new[] { Circle("Ring", top, c0, Vector3.up, 2.05f, springGlow.rest, 0.08f),
                                       Circle("Ring_Inner", top, c0, Vector3.up, 1.35f, springGlow.rest, 0.05f) };
            HealingSpring spring = top.gameObject.AddComponent<HealingSpring>();
            spring.radius = 2.2f;
            Effect("Spring_Fx", top, new Vector3(0f, 0.05f, 0f), AoE + "Heal/Particles/VFX_Heal_Area_01.prefab", 0.75f);

            Transform float_ = Group("Floating_Crystal", top);
            float_.localPosition = new Vector3(0f, 3.6f, 0f);
            GameObject big = Crystal("Crystal", float_, Vector3.zero, new Vector3(1.3f, 2.3f, 1.3f));
            big.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Crystal("Shard", float_, new Vector3(1.4f, -0.6f, 0.3f), new Vector3(0.35f, 0.7f, 0.35f));
            Crystal("Shard", float_, new Vector3(-1.1f, 0.5f, -0.9f), new Vector3(0.3f, 0.6f, 0.3f));
            Crystal("Shard", float_, new Vector3(-0.4f, -0.9f, 1.3f), new Vector3(0.25f, 0.5f, 0.25f));
            Billboard("Glow", float_, Vector3.zero, 6f, Arcane * 0.45f);
            PointLight("Light", float_, Vector3.zero, Arcane, 2.2f, 18f);
            FloatingBob bob = float_.gameObject.AddComponent<FloatingBob>();
            bob.height = 0.35f;
            bob.period = 5f;
            bob.spin = 22f;

            // a launch pad at the foot of each side, throwing you up onto the top near that edge
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, 90f * side, 0f);
                Vector3 at = q * new Vector3(0f, 0.02f, -(CoreHalf + 1.9f));
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
            Vector3 c = p.position + Vector3.up * 0.04f;
            Vector3 toward = new Vector3(-at.x, 0f, -at.z).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            var lines = new List<LineRenderer>
            {
                Circle("Ring", p, c, Vector3.up, 1.1f, Air, 0.08f),
                Circle("Ring_Inner", p, c, Vector3.up, 0.8f, Air, 0.04f),
                Glyph("Rune", p, GestureTemplates.Ehwaz, c, side, toward, 0.5f, Air, 0.07f, Vector3.up)
            };
            HaloQuad("Glow", p, new Vector3(0f, 0.035f, 0f), Quaternion.Euler(90f, 0f, 0f), 3.2f, Halo("Air", Air * 0.35f));
            Effect("Air_Fx", p, Vector3.zero, AoE + "Air/Particles/VFX_Air_Area_01.prefab", 0.35f);

            RuneGlow g = p.gameObject.AddComponent<RuneGlow>();
            g.rest = Air * 0.9f;
            g.breath = 0.4f;
            g.breathPeriod = 1.8f;
            g.lines = lines.ToArray();
            LaunchPad pad = p.gameObject.AddComponent<LaunchPad>();
            pad.landing = landing;
            pad.apex = 2f;
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
                Box("Capital", p, trim, new Vector3(0f, WallHeight - 0.22f, 0f), new Vector3(2.0f, 0.45f, 2.0f));
                GameObject bowl = Cylinder("Bowl", p, metal, new Vector3(0f, WallHeight + 0.18f, 0f), 1.45f, 0.18f);
                Object.DestroyImmediate(bowl.GetComponent<Collider>());
                Effect("Flame", p, new Vector3(0f, WallHeight + 0.3f, 0f), Anime + "Shared/Particles/VFX_Fire.prefab", 2.6f, 18f);
                Billboard("Glow", p, new Vector3(0f, WallHeight + 0.9f, 0f), 4.5f, new Color(1f, 0.55f, 0.2f) * 0.4f);
                PointLight("Light", p, new Vector3(0f, WallHeight + 1.2f, 0f), new Color(1f, 0.62f, 0.32f), 1.5f, 12f);

                var lines = new List<LineRenderer>();
                for (int side = 0; side < 4; side++)
                {
                    Quaternion q = Quaternion.Euler(0f, 90f * side, 0f);
                    Vector3 normal = q * Vector3.back;
                    Vector3 right = q * Vector3.right;
                    Vector3 face = p.position + normal * 0.775f;
                    lines.Add(Line("Edge_L", p, new[] { face - right * 0.5f + Vector3.up * 1.05f, face - right * 0.5f + Vector3.up * 5.3f }, colour, 0.05f, normal));
                    lines.Add(Line("Edge_R", p, new[] { face + right * 0.5f + Vector3.up * 1.05f, face + right * 0.5f + Vector3.up * 5.3f }, colour, 0.05f, normal));
                    lines.Add(Glyph("Rune", p, RuneOf(elements[i]), face + Vector3.up * 3.2f, right, Vector3.up, 0.42f, colour, 0.07f, normal));
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
                    Box("Cap", look, trim, chest.localPosition + new Vector3(0f, 0.72f, 0f), new Vector3(chest.localScale.x + 0.2f, 0.16f, chest.localScale.z + 0.2f), 0f, false);

                Transform s = quarter.Find("Slab_A");
                if (s != null)
                {
                    Box("Cap", look, trim, s.localPosition + new Vector3(0f, s.localScale.y * 0.5f - 0.15f, 0f), new Vector3(s.localScale.x + 0.3f, 0.3f, s.localScale.z + 0.3f), 0f, false);
                    var lines = new List<LineRenderer>();
                    Color colour = ColourOf(slabRunes[q]);
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        Vector3 normal = s.forward * sign;
                        Vector3 face = s.position + normal * (s.localScale.z * 0.5f + 0.025f);
                        Vector3 at = new Vector3(face.x, 0f, face.z);
                        lines.Add(Line("Band", look, new[] { at - s.right * 2.6f + Vector3.up * 2.75f, at + s.right * 2.6f + Vector3.up * 2.75f }, colour, 0.05f, normal));
                        lines.Add(Glyph("Rune", look, RuneOf(slabRunes[q]), at + Vector3.up * 1.6f, s.right, Vector3.up, 0.6f, colour, 0.08f, normal));
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

            string[] order = { "Fire", "Air", "Ice", "Earth", "Lightning", "Barrier" };
            for (int i = 0; i < 6; i++)
            {
                float a = (15f + 60f * i) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(a) * 10.6f, 0.015f, Mathf.Sin(a) * 10.6f);
                Vector3 outward = new Vector3(at.x, 0f, at.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, outward);
                Color colour = ColourOf(order[i]);
                Transform g = Group("Rune_" + order[i], t);
                g.position = at;
                LineRenderer rune = Glyph("Rune", g, RuneOf(order[i]), at, -side, outward, 0.62f, colour, 0.1f, Vector3.up);
                HaloQuad("Glow", g, new Vector3(0f, -0.004f, 0f), Quaternion.Euler(90f, 0f, 0f), 3f, Halo(order[i] + "_Floor", colour * 0.3f));
                RuneGlow glow = g.gameObject.AddComponent<RuneGlow>();
                glow.rest = colour * 0.9f;
                glow.phase = i / 6f;
                glow.lines = new[] { rune };
            }
        }

        // ------------------------------------------------------------------ beyond the walls

        static void Beyond(Transform t)
        {
            Mesh spike = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AoE + "Earth/Meshes/SM_VFX_Pointy_Rock_01.FBX"))
                if (o is Mesh) spike = (Mesh)o;

            var rnd = new System.Random(7);
            for (int i = 0; i < 18; i++)
            {
                float a = (i * 20f + (float)rnd.NextDouble() * 12f) * Mathf.Deg2Rad;
                float r = 40f + (float)rnd.NextDouble() * 34f;
                float y = 2f + (float)rnd.NextDouble() * 20f;
                float s = 2.5f + (float)rnd.NextDouble() * 6f;
                Island(t, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), s, spike, rnd, i % 3 == 0);
            }
            // a few big ones far out, to give the horizon a shape
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 30f) * Mathf.Deg2Rad;
                Island(t, new Vector3(Mathf.Cos(a) * 130f, -6f + i * 3f, Mathf.Sin(a) * 130f), 22f + i * 3f, spike, rnd, true);
            }
        }

        /// <summary>A floating rock: the pack's spike turned point-down under a mossy top.</summary>
        static void Island(Transform t, Vector3 at, float size, Mesh spike, System.Random rnd, bool crystalOnTop)
        {
            Transform isl = Group("Island", t);
            isl.position = at;
            isl.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            if (spike != null)
            {
                var go = new GameObject("Rock");
                go.transform.SetParent(isl, false);
                Bounds b = spike.bounds;
                float k = size / Mathf.Max(b.size.x, b.size.y);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(k, k, k * 0.9f);
                // its broad end (min z) up against the top, its point hanging down
                go.transform.localPosition = Vector3.up * (b.min.z * k * 0.9f);
                go.AddComponent<MeshFilter>().sharedMesh = spike;
                go.AddComponent<MeshRenderer>().sharedMaterial = rock;
            }
            GameObject top = Cylinder("Top", isl, rock, new Vector3(0f, 0f, 0f), size * 1.05f, size * 0.12f);
            Object.DestroyImmediate(top.GetComponent<Collider>());
            if (crystalOnTop)
            {
                Crystal("Crystal", isl, new Vector3(size * 0.15f, size * 0.22f, 0f), new Vector3(size * 0.12f, size * 0.3f, size * 0.12f));
                Billboard("Glow", isl, new Vector3(size * 0.15f, size * 0.25f, 0f), size * 0.6f, Arcane * 0.3f);
            }
            FloatingBob bob = isl.gameObject.AddComponent<FloatingBob>();
            bob.height = 0.4f + (float)rnd.NextDouble() * 0.8f;
            bob.period = 7f + (float)rnd.NextDouble() * 6f;
            bob.spin = (float)rnd.NextDouble() * 2f - 1f;
            bob.phase = (float)rnd.NextDouble();
        }

        // ------------------------------------------------------------------ motes

        static void Motes(Transform t)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0f, 3.5f, 0f);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Arcane, 0f), new GradientColorKey(Gold, 0.5f), new GradientColorKey(new Color(1f, 0.6f, 0.8f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(38f, 6f, 38f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = alpha;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mote;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ atmosphere

        public static void Atmosphere()
        {
            GameObject oldSun = GameObject.Find("Sun");
            if (oldSun != null) Object.DestroyImmediate(oldSun);
            foreach (Light l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);

            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.80f, 0.64f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(30f, 145f, 0f);
            RenderSettings.sun = light;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.27f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.29f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.14f);
            RenderSettings.fog = false;      // see ArenaTempleBuilder: the packs' particles fog as squares
            // The pale dusk sky mirrored in everything at a glancing angle washed the whole picture out --
            // the far floor went cream and the characters lost their colour. A third of it is a sheen.
            RenderSettings.reflectionIntensity = 0.3f;

            string skyPath = MatFolder + "/Sanctum_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("MageCast/SanctumSky"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.shader = Shader.Find("MageCast/SanctumSky");
            Vector3 toSun = -sun.transform.forward;
            sky.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
            sky.SetFloat("_SunSize", 0.0012f);
            sky.SetColor("_Zenith", new Color(0.10f, 0.08f, 0.27f));
            sky.SetColor("_Middle", new Color(0.38f, 0.24f, 0.52f));
            sky.SetColor("_Horizon", new Color(1.0f, 0.60f, 0.40f));
            sky.SetColor("_Below", new Color(0.16f, 0.12f, 0.24f));
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        }

        /// <summary>
        /// The scene's ambient light and sky reflection, worked out and stored with it. The editor works
        /// them out by itself as you look; a build has only what was stored, and without this the web
        /// build had neither -- every face out of the sun went near black (21 Oct). Global illumination is
        /// off, so this is the sky alone and takes a second; no lightmaps.
        /// </summary>
        public static void BakeEnvironment()
        {
            const string path = "Assets/Scenes/Arena_Blockout_Lighting.lighting";
            LightingSettings s = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (s == null)
            {
                s = new LightingSettings { name = "Arena_Blockout_Lighting" };
                AssetDatabase.CreateAsset(s, path);
            }
            s.bakedGI = false;
            s.realtimeGI = false;
            s.autoGenerate = false;
            Lightmapping.lightingSettings = s;
            if (!Lightmapping.Bake()) Debug.LogWarning("[Sanctum] could not bake the environment lighting");
        }

        // ------------------------------------------------------------------ pieces

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        /// <summary>A block, solid unless <paramref name="solid"/> is false; local to <paramref name="parent"/>.</summary>
        static GameObject Box(string name, Transform parent, Material m, Vector3 pos, Vector3 size, float yaw = 0f, bool solid = true)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            Unbatched(g);
            return g;
        }

        static GameObject Deco(string name, Transform parent, Material m, Vector3 pos, Vector3 size)
        {
            return Box(name, parent, m, pos, size, 0f, false);
        }

        static GameObject Cylinder(string name, Transform parent, Material m, Vector3 pos, float diameter, float height)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            g.GetComponent<Renderer>().sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return g;
        }

        static GameObject Crystal(string name, Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = crystal;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A flat soft glow lying on a surface (a quad, local to its parent).</summary>
        static GameObject HaloQuad(string name, Transform parent, Vector3 pos, Quaternion rot, float size, Material m)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = rot;
            q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q;
        }

        /// <summary>A soft glow that always faces the camera: one particle that never dies.</summary>
        static void Billboard(string name, Transform parent, Vector3 pos, float size, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 1000000f;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = colour;
            main.maxParticles = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mote;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 5f;
        }

        static void PointLight(string name, Transform parent, Vector3 pos, Color colour, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = colour;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
        }

        /// <summary>One of the packs' effects, kept going in the map (AmbientFx).</summary>
        static void Effect(string name, Transform parent, Vector3 pos, string prefabPath, float scale, float flames = 0f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("[Sanctum] missing effect " + prefabPath); return; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            AmbientFx fx = go.AddComponent<AmbientFx>();
            fx.prefab = prefab;
            fx.scale = scale;
            fx.flames = flames;
        }

        // --- glowing lines (LineRenderers in world space, flat on a surface facing <normal>)

        static LineRenderer NewLine(string name, Transform parent, Color colour, float width, Vector3 normal, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.01f ? normal : Vector3.up);
            var l = go.AddComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.loop = loop;
            l.alignment = LineAlignment.TransformZ;
            l.widthMultiplier = width;
            l.numCornerVertices = 3;
            l.numCapVertices = 3;
            l.sharedMaterial = glowLine;
            l.startColor = l.endColor = colour;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.textureMode = LineTextureMode.Stretch;
            return l;
        }

        static LineRenderer Line(string name, Transform parent, Vector3[] points, Color colour, float width, Vector3 normal)
        {
            LineRenderer l = NewLine(name, parent, colour, width, normal, false);
            l.positionCount = points.Length;
            l.SetPositions(points);
            return l;
        }

        static LineRenderer Circle(string name, Transform parent, Vector3 centre, Vector3 normal, float radius, Color colour, float width)
        {
            LineRenderer l = NewLine(name, parent, colour, width, normal, true);
            Vector3 n = normal.normalized;
            Vector3 a = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 b = Vector3.Cross(n, a);
            int segments = Mathf.Clamp(Mathf.RoundToInt(radius * 18f), 24, 160);
            l.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                l.SetPosition(i, centre + (a * Mathf.Cos(t) + b * Mathf.Sin(t)) * radius);
            }
            return l;
        }

        /// <summary>A rune drawn as the game draws it (its template's corners), <paramref name="half"/> metres from middle to edge.</summary>
        static LineRenderer Glyph(string name, Transform parent, string rune, Vector3 centre, Vector3 right, Vector3 up, float half,
                                  Color colour, float width, Vector3 normal)
        {
            Vector2[] corners = GestureTemplates.Corners(rune);
            LineRenderer l = NewLine(name, parent, colour, width, normal, false);
            l.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                l.SetPosition(i, centre + (right * corners[i].x + up * corners[i].y) * half);
            return l;
        }

        // --- materials and meshes

        /// <summary>
        /// Colours as written above, deepened: a little more saturated and a good deal darker. Picked by
        /// eye in the editor's game view (21 Oct) -- as written they read pastel and washed out under the
        /// dusk sun, and the runes did not stand out from the stone.
        /// </summary>
        static Color Tone(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            return Color.HSVToRGB(h, Mathf.Min(1f, s * 1.25f), v * 0.72f);
        }

        static Material Stone(string name, Texture2D pattern, float scale, Color side, Color tint2, Color top, Color edge, float edgeWidth, float grime)
        {
            side = Tone(side);
            tint2 = Tone(tint2);
            top = Tone(top);
            edge = Tone(edge);
            string path = MatFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("MageCast/SanctumStone");
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            m.SetTexture("_MainTex", pattern);
            m.SetColor("_Color", side);
            m.SetColor("_Tint2", tint2);
            m.SetColor("_TopColor", top);
            m.SetColor("_EdgeColor", edge);
            m.SetFloat("_EdgeWidth", edgeWidth);
            m.SetFloat("_Scale", scale);
            m.SetFloat("_PatchScale", 7f);
            m.SetFloat("_Grime", grime);
            m.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Standard(string name, Color albedo, float metallic, float smoothness, Color emission)
        {
            string path = MatFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
            m.color = albedo;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Glow(string name, Color colour, bool radial, float softness, float core)
        {
            string path = MatFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("MageCast/SanctumGlow");
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            m.SetColor("_Color", colour);
            m.SetFloat("_Radial", radial ? 1f : 0f);
            m.SetFloat("_Softness", softness);
            m.SetFloat("_Core", core);
            m.SetFloat("_Boost", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>An eight-sided crystal, 1 m tall and 1 m across, flat-shaded, kept as an asset.</summary>
        static Mesh CrystalMesh()
        {
            string path = MeshFolder + "/Sanctum_Crystal.asset";
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            Vector3 top = new Vector3(0f, 0.5f, 0f), bottom = new Vector3(0f, -0.5f, 0f);
            Vector3[] ring = { new Vector3(0.5f, 0.05f, 0f), new Vector3(0f, 0.05f, 0.5f), new Vector3(-0.5f, 0.05f, 0f), new Vector3(0f, 0.05f, -0.5f) };
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % 4];
                int s = v.Count;
                v.Add(top); v.Add(b); v.Add(a);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                s = v.Count;
                v.Add(bottom); v.Add(a); v.Add(b);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            }
            m = new Mesh { name = "Sanctum_Crystal" };
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// Out of static batching: SanctumStone finds a block's edges from its own matrix, which batching
        /// would bake away. Everything else static stays.
        /// </summary>
        static void Unbatched(GameObject g)
        {
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
                                                      | StaticEditorFlags.OccludeeStatic
                                                      | StaticEditorFlags.ReflectionProbeStatic);
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1));
        }
    }
}
