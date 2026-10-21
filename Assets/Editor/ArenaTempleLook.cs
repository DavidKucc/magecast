using System.Collections.Generic;
using MageCast.World;
using UnityEditor;
using UnityEngine;
using static MageCast.EditorTools.ArenaKit;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The Temple's look: a sandstone temple in a jungle at golden hour, in the painted style of the spell
    /// effects (the same pieces as the Sanctum, ArenaKit). ArenaTempleBuilder makes the map; this only dresses
    /// it, and nothing here has a collider where a player could walk into it, except the wall pilasters,
    /// which are as solid as the wall they stand against.
    ///
    ///   altar     a carved band under its top, runes of the elements on its faces and up the obelisk, a gold
    ///             tip, and a crystal turning over it all
    ///   ley lines from the foot of the altar stairs to a great rune on each outer wall, all one glow: strike
    ///             the wall and the line lights up back to the altar
    ///   walls     cornice and battlements against the sky, pilasters with banners, ivy hanging from the lip
    ///   towers    a cornice at the roof line, carved door frames, ivy; fire burning in the braziers
    ///   beyond    jungle crowding the walls, forested hills and a sister temple in the haze, floating rocks
    /// </summary>
    public static class ArenaTempleLook
    {
        // the Temple's numbers, as ArenaTempleBuilder builds them
        const float Half = 22f, WallHeight = 7f, WallThick = 1f, Ledge = 2.4f, Storey = 4f, AltarHalf = 4f;
        const float TowerMin = 11.5f, TowerMax = 18.5f, TowerWall = 0.6f, DoorWidth = 2.5f, DoorHeight = 3f;
        const string Mats = "Assets/Materials/Temple/";

        static Material wall, stone, dark, trim, gold, crystal, rock, vine, leaves, bark, hills, far;
        static readonly Dictionary<string, Material> cloths = new Dictionary<string, Material>();

        // the rune on each side's wall: north, east, south, west
        static readonly string[] SideRune = { "Fire", "Ice", "Lightning", "Air" };
        static readonly string[] Shell = { "Wall_N", "Wall_E", "Wall_S", "Wall_W" };

        public static void Dress(Transform root)
        {
            Materials();
            Transform look = Group("Look", root);
            Walls(Group("Walls", look), root.Find("Shell"));
            Altar(Group("Altar", look), root.Find("Centre"));
            for (int q = 0; q < 4; q++)
            {
                Transform t = Group("Quarter_" + q, look);
                t.localRotation = Quaternion.Euler(0f, 90f * q, 0f);
                Quarter(t, root.Find("Quarter_" + q), root.Find("Shell/" + Shell[q]), q);
            }
            Jungle(Group("Jungle", look));
            Beyond(Group("Beyond", look));
            Motes(look, new Vector3(0f, 3.5f, 0f), new Vector3(42f, 6f, 42f), 14f, 140, 0.12f, Gold, new Color(1f, 0.93f, 0.72f), Arcane);
            BakeGlows(look);
        }

        // ------------------------------------------------------------------ materials

        static void Materials()
        {
            Texture2D blocks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);
            wall = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Temple_Wall.mat");
            stone = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Temple_Stone.mat");
            dark = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Temple_Dark.mat");
            trim = Stone("Temple_Trim", blocks, 2f, new Color(0.92f, 0.80f, 0.60f), new Color(0.85f, 0.77f, 0.66f), new Color(0.92f, 0.84f, 0.64f),
                         new Color(1.00f, 0.93f, 0.74f), 0.08f, 0.15f);
            rock = Stone("Temple_Rock", Crag(), 8f, new Color(0.64f, 0.52f, 0.42f), new Color(0.54f, 0.50f, 0.48f), new Color(0.46f, 0.58f, 0.32f),
                         Color.white, 0f, 0.4f);
            gold = Standard("Temple_Gold", new Color(0.86f, 0.63f, 0.30f), 0.85f, 0.55f, Color.black);
            // lit enough to glow, not so much that its facets vanish into one flat shape
            crystal = Standard("Temple_Crystal", new Color(0.30f, 0.90f, 0.82f), 0f, 0.9f, new Color(0.10f, 0.55f, 0.46f));
            vine = Foliage("Temple_Ivy", new Color(0.09f, 0.19f, 0.10f), new Color(0.34f, 0.52f, 0.20f), new Color(0.46f, 0.56f, 0.20f));
            leaves = Foliage("Temple_Leaves", new Color(0.06f, 0.15f, 0.09f), new Color(0.30f, 0.48f, 0.18f), new Color(0.42f, 0.53f, 0.19f));
            bark = Foliage("Temple_Bark", new Color(0.20f, 0.14f, 0.10f), new Color(0.44f, 0.33f, 0.23f), new Color(0.32f, 0.32f, 0.22f));
            // the jungle further out takes a little of the sky's blue
            hills = Foliage("Temple_Hills", new Color(0.07f, 0.15f, 0.13f), new Color(0.24f, 0.36f, 0.25f), new Color(0.30f, 0.40f, 0.24f));
            hills.SetFloat("_PatchScale", 14f);      // a hill is thirty metres across: patches the size of groves, not leaves
            EditorUtility.SetDirty(hills);
            // the mountains in the haze: blue-green, darker than the sky behind them
            far = Distant("Temple_Far", new Color(0.18f, 0.28f, 0.28f), new Color(0.28f, 0.36f, 0.42f), new Color(0.48f, 0.56f, 0.66f), 0.5f, 80f);
            foreach (var e in Elements()) cloths[e.Key] = Standard("Temple_Banner_" + e.Key, e.Value * 0.55f, 0f, 0.15f, e.Value * 0.12f);
        }

        // ------------------------------------------------------------------ outer walls

        static void Walls(Transform t, Transform shell)
        {
            float wo = Half + WallThick * 0.5f, wl = Half * 2f + WallThick * 2f;
            string[] banners = { "Fire", "Earth", "Ice", "Air", "Lightning", "Barrier", "Air", "Ice" };
            for (int side = 0; side < 4; side++)
            {
                // each wall in its own frame: x along it, z out of the arena
                Transform w = Group("Wall_" + side, t);
                w.localRotation = Quaternion.Euler(0f, 90f * side, 0f);

                // every other side's cornice a touch deeper, so where two cross at a corner their tops are not one plane
                Deco("Cornice", w, trim, new Vector3(0f, WallHeight + 0.2f, wo), new Vector3(wl + 0.6f, 0.4f + (side % 2) * 2f * Proud, WallThick + 0.7f));
                for (int k = 0; k <= 16; k++)
                {
                    float x = -Half + 1.3f + k * (Half * 2f - 2.6f) / 16f;
                    Deco("Merlon", w, wall, new Vector3(x, WallHeight + 0.85f, wo + 0.1f), new Vector3(1.2f, 0.9f, WallThick + 0.1f));
                }

                int b = 0;
                foreach (float x in new[] { -9.5f, -5f, 5f, 9.5f })
                {
                    // stops inside its head, so the two tops are not one plane
                    Box("Pilaster", w, wall, new Vector3(x, (WallHeight - 0.1f) * 0.5f, Half - 0.22f), new Vector3(1.1f, WallHeight - 0.1f, 0.45f));
                    Box("Pilaster_Base", w, trim, new Vector3(x, 0.35f, Half - 0.35f), new Vector3(1.45f, 0.7f, 0.7f));
                    Deco("Pilaster_Head", w, trim, new Vector3(x, WallHeight - 0.25f, Half - 0.32f), new Vector3(1.35f, 0.5f, 0.64f));
                    if (Mathf.Abs(x) > 6f) continue;
                    string e = banners[(side * 2 + b++) % banners.Length];
                    Banner(w, new Vector3(x, 0f, Half - 0.47f), e, cloths[e], gold);
                }

                // ivy: off the cornice's lip in the open, down the wall itself behind the towers
                foreach (float x in new[] { -7.25f, 7.25f })
                    ArenaTrees.Ivy(w, side + (x > 0 ? 1 : 0), new Vector3(x, WallHeight + 0.02f, wo - 0.86f), Vector3.back, 1.2f, vine);
                foreach (float x in new[] { -16.5f, -13f, 13.5f, 17f })
                    ArenaTrees.Ivy(w, side + 2 + (x > 0 ? 1 : 0), new Vector3(x, WallHeight - 0.02f, Half), Vector3.back, 1.1f, vine);
            }
        }

        // ------------------------------------------------------------------ the altar

        static void Altar(Transform t, Transform centre)
        {
            Transform altar = centre.Find("Altar"), obelisk = centre.Find("Obelisk");

            // a carved band under the top edge; no collider, so the top stays what it was
            Band("Band", t, trim, Ledge - 0.24f, AltarHalf, AltarHalf, 0.26f, 0.12f);
            Band("Plinth", t, trim, 0.12f, AltarHalf, AltarHalf, 0.24f, 0.1f);

            // a rune on each face, left of the stair that climbs it (the stairs run from x -0.5 to 3.5 on each face)
            var lines = new List<LineRenderer>();
            string[] faces = { "Barrier", "Earth", "Barrier", "Earth" };
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, 90f * side, 0f);
                Vector3 normal = q * Vector3.forward, along = q * Vector3.right;
                Vector3 face = normal * (AltarHalf + 0.025f);
                Vector3 at = face + along * -2.25f + Vector3.up * 1.15f;
                // read from in front of the face, whose right is -along
                lines.Add(Glyph("Rune", t, RuneOf(faces[side]), at, -along, Vector3.up, 0.55f, Arcane, 0.08f, normal));
                lines.Add(Circle("Ring", t, at, normal, 0.78f, Arcane, 0.04f));
                lines.Add(Line("Band", t, new[] { face + along * -3.85f + Vector3.up * 0.4f, face + along * -0.65f + Vector3.up * 0.4f }, Arcane, 0.05f, normal));
            }
            Runes(altar.gameObject, Arcane * 0.85f, 0f, lines.ToArray());

            // the obelisk: gold bands, three runes up each face between them, a gold tip
            foreach (float h in new[] { 1.2f, 2.8f, 4.4f })
                Band("Obelisk_Band", t, gold, Ledge + h, 0.7f, 0.7f, 0.14f, 0.06f);
            var ob = new List<LineRenderer>();
            string[] order = { "Fire", "Ice", "Lightning", "Air", "Earth", "Barrier" };
            for (int side = 0; side < 4; side++)
            {
                Quaternion q = Quaternion.Euler(0f, 90f * side, 0f);
                Vector3 normal = q * Vector3.forward, along = q * Vector3.right;
                for (int k = 0; k < 3; k++)
                {
                    string e = order[(side * 3 + k) % order.Length];
                    Vector3 at = normal * 0.725f + Vector3.up * (Ledge + 2.0f + k * 1.6f);
                    ob.Add(Glyph("Rune", t, RuneOf(e), at, -along, Vector3.up, 0.32f, ColourOf(e), 0.055f, normal));
                }
            }
            Runes(obelisk.gameObject, Arcane * 0.8f, 0.3f, ob.ToArray());
            GameObject tip = Crystal("Tip", t, gold, new Vector3(0f, Ledge + 6f - 0.08f, 0f), new Vector3(1.98f, 1.6f, 1.98f));
            tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            tip.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // the crystal turning over it all
            Transform f = Group("Floating_Crystal", t);
            f.localPosition = new Vector3(0f, Ledge + 6f + 2.6f, 0f);
            GameObject big = Crystal("Crystal", f, crystal, Vector3.zero, new Vector3(0.9f, 1.7f, 0.9f));
            big.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Crystal("Shard", f, crystal, new Vector3(1.0f, -0.5f, 0.2f), new Vector3(0.26f, 0.5f, 0.26f));
            Crystal("Shard", f, crystal, new Vector3(-0.8f, 0.4f, -0.6f), new Vector3(0.22f, 0.44f, 0.22f));
            Crystal("Shard", f, crystal, new Vector3(-0.3f, -0.7f, 0.9f), new Vector3(0.18f, 0.36f, 0.18f));
            Billboard("Glow", f, Vector3.zero, 4.5f, Arcane * 0.45f);
            FloatingBob bob = f.gameObject.AddComponent<FloatingBob>();
            bob.height = 0.3f;
            bob.period = 5f;
            bob.spin = 20f;

            // a ring of runes on the floor round the altar, where the ley lines leave it
            Transform ring = Group("Ring", t);
            var rl = new List<LineRenderer>
            {
                Circle("Outer", ring, new Vector3(0f, 0.015f, 0f), Vector3.up, 7.6f, Arcane, 0.07f),
                Circle("Inner", ring, new Vector3(0f, 0.015f, 0f), Vector3.up, 7.25f, Arcane, 0.035f),
            };
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + 90f * i) * Mathf.Deg2Rad;
                Vector3 at = new Vector3(Mathf.Cos(a) * 7.42f, 0.016f, Mathf.Sin(a) * 7.42f);
                Vector3 outward = new Vector3(at.x, 0f, at.z).normalized;
                Color c = ColourOf(order[i]);
                rl.Add(Glyph("Rune", ring, RuneOf(order[i]), at, -Vector3.Cross(Vector3.up, outward), outward, 0.5f, c, 0.08f, Vector3.up));
                HaloQuad("Glow", ring, at + Vector3.up * -0.004f, Quaternion.Euler(90f, 0f, 0f), 2.6f, Halo(order[i] + "_Floor", c * 0.28f));
            }
            RuneGlow rg = Runes(ring.gameObject, Arcane * 0.7f, 0.5f, rl.ToArray());
            rg.breath = 0.3f;
            rg.breathPeriod = 5f;
        }

        // ------------------------------------------------------------------ a quarter

        static void Quarter(Transform t, Transform quarter, Transform shellWall, int q)
        {
            // the ley line, from the foot of the altar stair to the wall, and the great rune it runs into
            var ley = new List<LineRenderer>();
            float from = AltarHalf + 2.3f, to = Half - 0.75f;
            foreach (float x in new[] { -0.14f, 0.14f })
                ley.Add(Line("Ley", t, new[] { W(t, x, 0.015f, from), W(t, x, 0.015f, to) }, Arcane, 0.045f, Vector3.up));
            for (float z = from + 2.5f; z < to - 1f; z += 3f)
                ley.Add(Line("Ley_Mark", t, new[] { W(t, 0f, 0.016f, z - 0.32f), W(t, 0.2f, 0.016f, z), W(t, 0f, 0.016f, z + 0.32f),
                                                     W(t, -0.2f, 0.016f, z), W(t, 0f, 0.016f, z - 0.32f) }, Arcane, 0.04f, Vector3.up));

            string e = SideRune[q];
            Color c = ColourOf(e);
            // seen from inside, looking at the wall along +z, its right is +x
            Vector3 normal = t.TransformDirection(Vector3.back), right = t.TransformDirection(Vector3.right);
            Vector3 centre = W(t, 0f, 3.9f, Half - 0.03f);
            ley.Add(Glyph("Wall_Rune", t, RuneOf(e), centre, right, Vector3.up, 1.25f, c, 0.12f, normal));
            ley.Add(Circle("Wall_Ring", t, centre, normal, 1.75f, c, 0.06f));
            ley.Add(Circle("Wall_Ring_Inner", t, centre, normal, 1.58f, c, 0.03f));
            HaloQuad("Wall_Glow", t, new Vector3(0f, 3.9f, Half - 0.025f), Quaternion.identity, 5.5f, Halo(e + "_Wall", c * 0.22f));
            // the wall it is cut into lights it when struck, and the line with it, all the way back to the altar
            Runes(shellWall.gameObject, c * 0.85f, q * 0.25f, ley.ToArray());

            // fire in the braziers
            foreach (Transform child in quarter)
                if (child.name == "Brazier")
                {
                    Vector3 top = child.position + Vector3.up * (child.lossyScale.y * 0.5f);
                    FireBowl(t, t.InverseTransformPoint(top), 0.85f, gold);
                }

            Tower(Group("Tower", t));

            // the arch pillar and the pillar under the bridge: a base and a capital each
            Transform arch = quarter.Find("Arch_Pillar");
            if (arch != null)
            {
                Vector3 p = arch.localPosition;
                Deco("Arch_Base", t, trim, new Vector3(p.x, 0.2f, p.z), new Vector3(1.5f, 0.4f, 1.5f));
                Deco("Arch_Capital", t, trim, new Vector3(p.x, 4.5f - 0.15f + Proud, p.z), new Vector3(1.5f, 0.3f, 1.5f));
            }
            Transform pier = quarter.Find("Bridge_Pillar");
            if (pier != null)
                Deco("Pier_Base", t, trim, new Vector3(pier.localPosition.x, 0.2f, pier.localPosition.z), new Vector3(1.45f, 0.4f, 1.45f), 45f);

            // the sarcophagus in the pit: a gold-edged lid
            Transform tomb = quarter.Find("Pit_Tomb");
            if (tomb != null)
            {
                Vector3 p = tomb.localPosition, s = tomb.localScale;
                Deco("Tomb_Lid", t, gold, new Vector3(p.x, p.y + s.y * 0.5f + 0.05f, p.z), new Vector3(s.x + 0.16f, 0.1f, s.z + 0.16f));
            }

            // ivy down both faces of the broken wall in the lane
            Transform ruin = quarter.Find("Ruin_Wall");
            if (ruin != null)
            {
                Vector3 p = ruin.localPosition, s = ruin.localScale;
                ArenaTrees.Ivy(t, q, new Vector3(p.x - 0.8f, Ledge - 0.02f, p.z - s.z * 0.5f), Vector3.back, 0.55f, vine);
                ArenaTrees.Ivy(t, q + 1, new Vector3(p.x + 1.1f, Ledge - 0.02f, p.z + s.z * 0.5f), Vector3.forward, 0.5f, vine);
            }
        }

        /// <summary>A point in the quarter's frame, in the world.</summary>
        static Vector3 W(Transform t, float x, float y, float z) { return t.TransformPoint(new Vector3(x, y, z)); }

        /// <summary>The corner tower: a cornice at the roof line, carved frames round its two doors, ivy, a banner.</summary>
        static void Tower(Transform t)
        {
            float x0 = TowerMin, x1 = TowerMax, z0 = TowerMin, z1 = TowerMax, w = TowerWall;
            t.localPosition = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            float half = (x1 - x0) * 0.5f;
            Band("Cornice", t, trim, Storey - 0.2f, half, half, 0.3f, 0.15f);

            // the west door (on x0, z from z0+1.1) and the south door (on z0, x from x0+3), in the tower's own frame
            float dz0 = z0 + 1.1f - t.localPosition.z, dx0 = x0 + 3f - t.localPosition.x;
            DoorFrame(t, new Vector3(-half + w * 0.5f, 0f, dz0 + DoorWidth * 0.5f), 90f, w);
            DoorFrame(t, new Vector3(dx0 + DoorWidth * 0.5f, 0f, -half + w * 0.5f), 0f, w);

            // ivy on the two faces towards the outer wall, a banner beside the west door
            ArenaTrees.Ivy(t, 5, new Vector3(1.5f, Storey - 0.05f, half + 0.17f), Vector3.forward, 0.9f, vine);
            ArenaTrees.Ivy(t, 6, new Vector3(half + 0.17f, Storey - 0.05f, -1.2f), Vector3.right, 0.9f, vine);
            // a frame whose -z looks out of the west face (along the tower's -x); its x runs along the tower's -z
            Transform side = Group("West", t);
            side.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Banner(side, new Vector3(-1.6f, -1.6f, -half - 0.05f), "Barrier", cloths["Barrier"], gold);
        }

        /// <summary>Carved jambs and a lintel round a doorway through a wall <paramref name="w"/> thick, centred at <paramref name="at"/>.</summary>
        static void DoorFrame(Transform t, Vector3 at, float yaw, float w)
        {
            Transform d = Group("Door", t);
            d.localPosition = at;
            d.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // in the door's frame: the opening along x, the wall's thickness along z
            foreach (float s in new[] { -1f, 1f })
                Deco("Jamb", d, trim, new Vector3(s * DoorWidth * 0.5f, DoorHeight * 0.5f, 0f), new Vector3(0.16f, DoorHeight, w + 0.16f));
            Deco("Lintel", d, trim, new Vector3(0f, DoorHeight + 0.12f - Proud, 0f), new Vector3(DoorWidth + 0.4f, 0.24f, w + 0.2f));
        }

        // ------------------------------------------------------------------ beyond the walls

        /// <summary>Jungle crowding the outside of the walls, its crowns over the battlements.</summary>
        static void Jungle(Transform t)
        {
            // one loose row, with gaps: a wall of crowns all round shut the sky out
            var rnd = new System.Random(31);
            for (int side = 0; side < 4; side++)
            {
                Transform s = Group("Side_" + side, t);
                s.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                for (float x = -Half - 3f; x <= Half + 3f; x += 5.6f)
                {
                    if (rnd.NextDouble() < 0.3) continue;
                    float px = x + ((float)rnd.NextDouble() - 0.5f) * 2.6f;
                    float pz = Half + 4.5f + (float)rnd.NextDouble() * 4.5f;
                    float scale = 1.3f + (float)rnd.NextDouble() * 0.6f;
                    ArenaTrees.Plant(s, ArenaTrees.Kind.Broad, rnd.Next(4), new Vector3(px, -0.5f, pz), (float)rnd.NextDouble() * 360f, scale, bark, leaves);
                }
            }
        }

        static void Beyond(Transform t)
        {
            var rnd = new System.Random(17);
            // forested hills all round, big enough to rise over the walls from anywhere inside
            for (int i = 0; i < 22; i++)
            {
                float a = (i * 360f / 22f + (float)rnd.NextDouble() * 10f) * Mathf.Deg2Rad;
                float r = 70f + (float)rnd.NextDouble() * 26f;
                float s = 11f + (float)rnd.NextDouble() * 7f;
                ArenaTrees.Plant(t, ArenaTrees.Kind.Bush, rnd.Next(4), new Vector3(Mathf.Cos(a) * r, -6f, Mathf.Sin(a) * r),
                                 (float)rnd.NextDouble() * 360f, s, bark, hills);
            }
            // Seen from the floor, the battlements hide everything under a line rising 0.3 m a metre: things out
            // here are as tall as they are far, over that.
            Ridge("Ridge", t, 150f, 36f, 42f, 80f, 7, 11, far);

            // a sister temple out in the jungle, a crystal burning on its top
            Transform p = Group("Pyramid", t);
            p.localPosition = new Vector3(-48f, 0f, 72f);
            p.localRotation = Quaternion.Euler(0f, 18f, 0f);
            const float tier = 4.8f;
            for (int k = 0; k < 6; k++)
            {
                float size = 34f - k * 5.4f;
                Deco("Tier", p, k % 2 == 0 ? stone : wall, new Vector3(0f, k * tier + tier * 0.5f, 0f), new Vector3(size, tier, size));
            }
            Deco("Shrine", p, dark, new Vector3(0f, 6 * tier + 1.6f, 0f), new Vector3(4f, 3.2f, 4f));
            Crystal("Beacon", p, crystal, new Vector3(0f, 6 * tier + 5.4f, 0f), new Vector3(1.6f, 3f, 1.6f));
            Billboard("Beacon_Glow", p, new Vector3(0f, 6 * tier + 5.4f, 0f), 10f, Arcane * 0.35f);

            // floating rocks over the jungle, the higher ones with crystals
            for (int i = 0; i < 10; i++)
            {
                float a = (i * 36f + (float)rnd.NextDouble() * 20f) * Mathf.Deg2Rad;
                float r = 48f + (float)rnd.NextDouble() * 40f;
                float y = 16f + (float)rnd.NextDouble() * 22f;
                Island(t, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), 3f + (float)rnd.NextDouble() * 5f, rnd, rock,
                       i % 3 == 0 ? crystal : null, Arcane * 0.3f);
            }
        }

        // ------------------------------------------------------------------ atmosphere

        /// <summary>Golden hour: a low warm sun, long shadows to read the levels apart, a deep blue sky going gold at the rim.</summary>
        public static void Atmosphere()
        {
            Light sun = Sun(new Color(1f, 0.84f, 0.64f), 1.2f, 0.8f, new Vector3(34f, -40f, 0f));
            Ambient(new Color(0.50f, 0.58f, 0.74f), new Color(0.64f, 0.54f, 0.44f), new Color(0.30f, 0.26f, 0.20f), 0.4f);
            Sky("Temple_Sky", sun, new SkyColours
            {
                zenith = new Color(0.14f, 0.28f, 0.56f), middle = new Color(0.44f, 0.60f, 0.78f),
                horizon = new Color(1.0f, 0.76f, 0.50f), below = new Color(0.36f, 0.30f, 0.26f),
                sun = new Color(1.0f, 0.82f, 0.55f), cloud = new Color(1.0f, 0.88f, 0.74f),
                sunSize = 0.0016f, stars = 0f, clouds = 0.7f,
                cloudCover = 0.5f, cloudHeight = 0.75f, cloudScale = 0.7f, cloudStretch = 2.5f,
            });
        }

        public static void BakeEnvironment()
        {
            ArenaKit.BakeEnvironment("Assets/Scenes/Arena_Temple_Lighting.lighting");
        }
    }
}
