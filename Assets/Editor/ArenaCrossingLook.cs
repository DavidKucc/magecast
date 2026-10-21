using System.Collections.Generic;
using MageCast.World;
using UnityEditor;
using UnityEngine;
using static MageCast.EditorTools.ArenaKit;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The Crossing's look: a ruined courtyard in a forest on a misty morning, painted like the Sanctum
    /// (ArenaKit) but held back -- grey-blue stone with pale worn edges, grey-green moss, dried mud and dead
    /// reeds in the riverbed, banners in deep muted colours, and for light only a few small flames in
    /// lanterns. The spells have to stay the most colourful thing on screen (see ArenaCrossingBuilder).
    ///
    ///   walls     cornice and battlements, pilasters with banners, ivy
    ///   river     pebbles and dry reeds down in the bed, trims under the bridge decks, a pier with a cap
    ///   banks     the colonnade's bases and capitals, a great stone lantern on the ford, lanterns with
    ///             flames, the chapel's altar with candles, caps on the gate ruins and the terrace
    ///   beyond    forest crowding the walls, hills in the haze, a few rocks drifting high up
    ///
    /// Nothing here has a collider except the wall pilasters (as solid as the wall behind them), and nothing
    /// a player could walk into stands higher than a reed.
    /// </summary>
    public static class ArenaCrossingLook
    {
        // the Crossing's numbers, as ArenaCrossingBuilder builds them
        const float HalfX = 24f, HalfZ = 20f, WallHeight = 6f, WallThick = 1f, Ledge = 2.4f, Bed = 1.2f;
        const float ChannelHalf = 3f, FordHalf = 3f, BridgeX = 15f, BridgeHalfWidth = 1.5f;
        const string Mats = "Assets/Materials/Crossing/";

        static Material wall, trim, iron, pebble, reeds, vine, leaves, pine, bark, hills, far, rock, cloth1, cloth2, altarCloth, statue;

        public static void Dress(Transform root)
        {
            Materials();
            Transform look = Group("Look", root);
            Walls(Group("Walls", look));
            for (int h = 0; h < 2; h++)
            {
                Transform t = Group("Half_" + h, look);
                t.localRotation = Quaternion.Euler(0f, 180f * h, 0f);
                Half(t, root.Find("Half_" + h), h);
            }
            Statue(Group("Statue", look));
            Forest(Group("Forest", look));
            Beyond(Group("Beyond", look));
            Motes(look, new Vector3(0f, 3f, 0f), new Vector3(46f, 5f, 38f), 9f, 90, 0.1f, new Color(1f, 0.86f, 0.62f), new Color(0.95f, 0.92f, 0.85f));
            BakeGlows(look);
        }

        // ------------------------------------------------------------------ materials

        static void Materials()
        {
            Texture2D blocks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);
            wall = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Crossing_Wall.mat");
            trim = Stone("Crossing_Trim", blocks, 2f, new Color(0.70f, 0.70f, 0.68f), new Color(0.66f, 0.68f, 0.66f), new Color(0.70f, 0.71f, 0.66f),
                         new Color(0.90f, 0.89f, 0.84f), 0.08f, 0.15f);
            // river stones: dull and rounded -- pale with lit edges they read as ice
            pebble = Stone("Crossing_Pebble", blocks, 1f, new Color(0.44f, 0.43f, 0.41f), new Color(0.40f, 0.41f, 0.39f), new Color(0.50f, 0.49f, 0.46f),
                           Color.white, 0f, 0f);
            rock = Stone("Crossing_Rock", Crag(), 8f, new Color(0.46f, 0.47f, 0.50f), new Color(0.42f, 0.45f, 0.44f), new Color(0.40f, 0.47f, 0.36f),
                         Color.white, 0f, 0.4f);
            iron = Standard("Crossing_Iron", new Color(0.16f, 0.15f, 0.15f), 0.6f, 0.4f, Color.black);
            statue = Stone("Crossing_Statue", blocks, 1.5f, new Color(0.58f, 0.60f, 0.58f), new Color(0.48f, 0.54f, 0.50f), new Color(0.50f, 0.58f, 0.46f),
                           Color.white, 0f, 0.2f, 3f);
            reeds = Foliage("Crossing_Reeds", new Color(0.18f, 0.19f, 0.12f), new Color(0.58f, 0.55f, 0.38f), new Color(0.42f, 0.46f, 0.30f));
            vine = Foliage("Crossing_Ivy", new Color(0.09f, 0.15f, 0.11f), new Color(0.30f, 0.42f, 0.26f), new Color(0.38f, 0.44f, 0.28f));
            leaves = Foliage("Crossing_Leaves", new Color(0.08f, 0.14f, 0.11f), new Color(0.32f, 0.42f, 0.25f), new Color(0.48f, 0.46f, 0.26f));
            pine = Foliage("Crossing_Pine", new Color(0.05f, 0.11f, 0.10f), new Color(0.20f, 0.32f, 0.24f), new Color(0.24f, 0.34f, 0.26f));
            bark = Foliage("Crossing_Bark", new Color(0.18f, 0.15f, 0.13f), new Color(0.38f, 0.33f, 0.28f), new Color(0.30f, 0.30f, 0.26f));
            hills = Foliage("Crossing_Hills", new Color(0.08f, 0.13f, 0.13f), new Color(0.24f, 0.32f, 0.28f), new Color(0.30f, 0.34f, 0.28f));
            hills.SetFloat("_PatchScale", 14f);
            EditorUtility.SetDirty(hills);
            far = Distant("Crossing_Far", new Color(0.18f, 0.27f, 0.25f), new Color(0.25f, 0.33f, 0.35f), new Color(0.55f, 0.62f, 0.70f), 0.45f, 56f);
            // banners in deep, dull colours that no spell shares
            cloth1 = Standard("Crossing_Banner_Wine", new Color(0.30f, 0.08f, 0.10f), 0f, 0.15f, Color.black);
            cloth2 = Standard("Crossing_Banner_Slate", new Color(0.14f, 0.18f, 0.26f), 0f, 0.15f, Color.black);
            altarCloth = Standard("Crossing_Altar_Cloth", new Color(0.32f, 0.10f, 0.11f), 0f, 0.2f, Color.black);
        }

        // ------------------------------------------------------------------ outer walls

        static void Walls(Transform t)
        {
            for (int side = 0; side < 4; side++)
            {
                bool odd = side % 2 == 1;
                float along = odd ? HalfZ : HalfX, off = odd ? HalfX : HalfZ;
                // each wall in its own frame: x along it, z out of the arena
                Transform w = Group("Wall_" + side, t);
                w.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                float wo = off + WallThick * 0.5f;

                // every other side's cornice a touch deeper, so where two cross at a corner their tops are not one plane
                Deco("Cornice", w, trim, new Vector3(0f, WallHeight + 0.2f, wo), new Vector3((along + WallThick) * 2f + 0.6f, 0.4f + (odd ? 2f * Proud : 0f), WallThick + 0.7f));
                int n = Mathf.RoundToInt((along * 2f - 2.6f) / 2.6f);
                for (int k = 0; k <= n; k++)
                {
                    float x = -along + 1.3f + k * (along * 2f - 2.6f) / n;
                    Deco("Merlon", w, wall, new Vector3(x, WallHeight + 0.85f, wo + 0.1f), new Vector3(1.2f, 0.9f, WallThick + 0.1f));
                }

                // pilasters: on the long walls between the spawns, on the short ones clear of the riverbed and the chapel
                float[] at = odd ? new[] { -7.5f, 7.5f } : new[] { -18f, -6f, 6f, 18f };
                foreach (float x in at)
                {
                    // stops inside its head, so the two tops are not one plane
                    Box("Pilaster", w, wall, new Vector3(x, (WallHeight - 0.1f) * 0.5f, off - 0.22f), new Vector3(1.1f, WallHeight - 0.1f, 0.45f));
                    Box("Pilaster_Base", w, trim, new Vector3(x, 0.35f, off - 0.35f), new Vector3(1.45f, 0.7f, 0.7f));
                    Deco("Pilaster_Head", w, trim, new Vector3(x, WallHeight - 0.25f, off - 0.32f), new Vector3(1.35f, 0.5f, 0.64f));
                    if (!odd && Mathf.Abs(x) < 10f) PlainBanner(w, new Vector3(x, -0.6f, off - 0.47f), x < 0 ? cloth1 : cloth2);
                }

                // ivy down the wall, between the pilasters
                float[] ivy = odd ? new[] { -14f, -2.5f, 12f } : new[] { -21.5f, -12f, -1f, 11.5f, 21.5f };
                for (int k = 0; k < ivy.Length; k++)
                    ArenaTrees.Ivy(w, side * 3 + k, new Vector3(ivy[k], WallHeight - 0.02f, off), Vector3.back, 1.1f, vine);
            }
        }

        /// <summary>A plain hanging cloth on a rod, no rune: the courtyard's banners carry no element.</summary>
        static void PlainBanner(Transform w, Vector3 at, Material cloth)
        {
            Transform b = Group("Banner", w);
            b.localPosition = at;
            Deco("Rod", b, iron, new Vector3(0f, 5.05f, -0.02f), new Vector3(1.3f, 0.08f, 0.08f));
            Deco("Cloth", b, cloth, new Vector3(0f, 3.75f, 0f), new Vector3(1.05f, 2.55f, 0.04f));
            Deco("Tail_L", b, cloth, new Vector3(-0.3f, 2.35f, 0f), new Vector3(0.42f, 0.32f, 0.024f));
            Deco("Tail_R", b, cloth, new Vector3(0.3f, 2.35f, 0f), new Vector3(0.42f, 0.32f, 0.024f));
            // a pale stripe down it, in thread, not light
            Deco("Stripe", b, trim, new Vector3(0f, 3.75f, -0.03f), new Vector3(0.12f, 2.3f, 0.02f));
        }

        // ------------------------------------------------------------------ a half

        static void Half(Transform t, Transform half, int h)
        {
            var rnd = new System.Random(71 + h);

            // the riverbed: pebbles and dry reeds along both banks
            for (int i = 0; i < 46; i++)
            {
                float x = FordHalf + 0.6f + (float)rnd.NextDouble() * (HalfX - FordHalf - 1.2f);
                float z = ((float)rnd.NextDouble() * 2f - 1f) * (ChannelHalf - 0.25f);
                if (Mathf.Abs(x - BridgeX) < 0.8f && Mathf.Abs(z) < 0.8f) continue;      // the pier
                float s = 0.14f + (float)rnd.NextDouble() * 0.24f;
                Piece("Pebble", t, Sphere(), new[] { pebble }, new Vector3(x, -Bed + s * 0.12f, z), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f),
                      new Vector3(s * 1.4f, s * 0.55f, s), false);
            }
            for (int i = 0; i < 26; i++)
            {
                float x = FordHalf + 0.8f + (float)rnd.NextDouble() * (HalfX - FordHalf - 1.6f);
                float z = (rnd.NextDouble() < 0.5 ? -1f : 1f) * (ChannelHalf - 0.2f - (float)rnd.NextDouble() * 0.3f);
                ArenaTrees.Plant(t, ArenaTrees.Kind.Reeds, rnd.Next(4), new Vector3(x, -Bed - 0.02f, z), (float)rnd.NextDouble() * 360f,
                                 0.8f + (float)rnd.NextDouble() * 0.4f, bark, reeds);
            }

            // the bridge: trims along the deck's sides, a cap and a foot on the pier
            float bx0 = BridgeX - BridgeHalfWidth, bx1 = BridgeX + BridgeHalfWidth;
            foreach (float x in new[] { bx0, bx1 })
                Deco("Deck_Trim", t, trim, new Vector3(x, Ledge - 0.22f, -0.25f), new Vector3(0.16f, 0.24f, ChannelHalf * 2f + 0.5f));
            Band("Pier_Cap", PlaceAt(t, "Pier", new Vector3(BridgeX, 0f, 0f)), trim, Ledge - 0.55f, 0.5f, 0.5f, 0.2f, 0.08f);
            Band("Pier_Foot", t.Find("Pier"), trim, -Bed + 0.2f, 0.5f, 0.5f, 0.4f, 0.1f);

            // the colonnade: a base and a capital round each column, under the lintel
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = new Vector3(2.2f + i * 2.4f, 0f, 6.2f);
                Cylinder("Column_Base", t, trim, c + new Vector3(0f, 0.15f, 0f), 1.15f, 0.3f, false);
                Cylinder("Column_Capital", t, trim, c + new Vector3(0f, 4f - 0.17f - Proud, 0f), 1.15f, 0.3f, false);
            }

            // lanterns: an iron cage on each post, and a small flame in it
            foreach (Transform child in half)
                if (child.name == "Lantern_Post")
                {
                    Vector3 top = t.InverseTransformPoint(child.position + Vector3.up * (child.lossyScale.y * 0.5f));
                    Lantern(t, top);
                }

            // the chapel: a cornice round its walls' tops, an altar cloth, candles
            Transform chapel = Group("Chapel", t);
            float cx0 = 16f, cx1 = 22f, cz0 = -16f, cz1 = -10.5f, w = 0.7f;
            Deco("Chapel_Cap_S", chapel, trim, new Vector3((cx0 + cx1) * 0.5f, 3.6f + 0.08f, cz0 + w * 0.5f), new Vector3(cx1 - cx0 + 0.16f, 0.16f, w + 0.16f));
            Deco("Chapel_Cap_N", chapel, trim, new Vector3((cx0 + 2.6f + cx1) * 0.5f, 3.6f + 0.08f, cz1 - w * 0.5f), new Vector3(cx1 - cx0 - 2.6f + 0.16f, 0.16f, w + 0.16f));
            Deco("Chapel_Cap_E", chapel, trim, new Vector3(cx1 - w * 0.5f, 3.6f + 0.08f + Proud, (cz0 + cz1) * 0.5f), new Vector3(w + 0.16f + 2f * Proud, 0.16f, cz1 - cz0 + 0.16f + 2f * Proud));
            Transform altar = half.Find("Chapel_Altar");
            if (altar != null)
            {
                Vector3 a = altar.localPosition, s = altar.localScale;
                Deco("Altar_Cloth", chapel, altarCloth, new Vector3(a.x, a.y + s.y * 0.5f + 0.015f, a.z), new Vector3(s.x * 0.7f, 0.03f, s.z + 0.08f));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 c = new Vector3(a.x - 0.55f + k * 0.55f, a.y + s.y * 0.5f + 0.03f, a.z - 0.15f);
                    float tall = 0.16f + (k == 1 ? 0.08f : 0f);
                    Cylinder("Candle", chapel, trim, c + Vector3.up * tall * 0.5f, 0.07f, tall, false);
                    Billboard("Candle_Light", chapel, c + Vector3.up * (tall + 0.06f), 0.45f, new Color(1f, 0.65f, 0.3f) * 0.5f);
                }
            }
            ArenaTrees.Ivy(chapel, 7 + h, new Vector3(cx1 - w - 0.01f, 3.58f, cz0 + 2.8f), Vector3.left, 0.7f, vine);

            // the gate ruins: a cap on each wall stump and on the lintel; caps on the gate posts
            foreach (float sgn in new[] { 1f, -1f })
            {
                float gz = 14.5f * sgn;
                // the wall stump's cap from where the lintel ends; the lintel's to the middle, where the other half's meets it
                Deco("Gate_Cap", t, trim, new Vector3((1.3f + 5.58f) * 0.5f, 2.8f + 0.08f, gz), new Vector3(5.58f - 1.3f, 0.16f, 0.8f + 0.16f));
                Deco("Gate_Lintel_Cap", t, trim, new Vector3(0.66f, 3.4f + 0.08f, gz), new Vector3(1.32f, 0.16f, 0.8f + 0.16f + 2f * Proud));
                Deco("Post_Cap", t, trim, new Vector3(16.5f, 1.6f + 0.06f, 17.5f * sgn), new Vector3(1.06f, 0.12f, 1.06f));
            }

            // the terrace: a carved band under its top on the river side, caps on the merlons
            Deco("Terrace_Band", t, trim, new Vector3(15f, Ledge - 0.24f, 7f - 0.06f), new Vector3(8f + 0.24f, 0.26f, 0.12f));
            foreach (Transform child in half)
                if (child.name == "Merlon" || child.name == "Parapet")
                {
                    Vector3 p = child.localPosition, s = child.localScale;
                    float grow = child.name == "Parapet" ? Proud : 0f;
                    Deco("Cap", t, trim, new Vector3(p.x, p.y + s.y * 0.5f + 0.06f + grow, p.z), new Vector3(s.x + 0.12f + grow * 2f, 0.12f, s.z + 0.12f + grow * 2f));
                }
        }

        /// <summary>An empty frame at <paramref name="at"/> in <paramref name="t"/>, for pieces placed round a point.</summary>
        static Transform PlaceAt(Transform t, string name, Vector3 at)
        {
            Transform g = Group(name, t);
            g.localPosition = at;
            return g;
        }

        /// <summary>An iron cage on a lantern post, a small flame inside: warm, and dim beside any fire spell.</summary>
        static void Lantern(Transform t, Vector3 top)
        {
            Transform l = Group("Lantern", t);
            l.localPosition = top;
            Deco("Base", l, iron, new Vector3(0f, 0.03f, 0f), new Vector3(0.42f, 0.06f, 0.42f));
            foreach (Vector2 c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                Deco("Bar", l, iron, new Vector3(c.x * 0.16f, 0.27f, c.y * 0.16f), new Vector3(0.04f, 0.42f, 0.04f));
            Deco("Roof", l, iron, new Vector3(0f, 0.52f, 0f), new Vector3(0.46f, 0.08f, 0.46f));
            Deco("Finial", l, iron, new Vector3(0f, 0.62f, 0f), new Vector3(0.12f, 0.12f, 0.12f));
            Effect("Flame", l, new Vector3(0f, 0.08f, 0f), Anime + "Shared/Particles/VFX_Fire.prefab", 0.42f, 9f);
            Billboard("Glow", l, new Vector3(0f, 0.28f, 0f), 1.3f, new Color(1f, 0.62f, 0.28f) * 0.32f);
        }

        // ------------------------------------------------------------------ the statue on the ford

        /// <summary>
        /// The pillar on the ford (0.8 m across, from 1.2 to 3.4 m) made a great stone lantern: a fire box on
        /// it with a flame inside, a pointed roof, a finial -- the courtyard's lanterns, grown up.
        /// </summary>
        static void Statue(Transform t)
        {
            Band("Plinth_Cap", t, trim, 1.2f - 0.1f, 0.9f, 0.9f, 0.2f, 0.08f);
            Band("Collar", t, statue, 3.4f - 0.12f, 0.4f, 0.4f, 0.16f, 0.06f);
            Deco("Firebox_Floor", t, statue, new Vector3(0f, 3.46f, 0f), new Vector3(1.1f, 0.12f, 1.1f));
            foreach (Vector2 c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                Deco("Firebox_Post", t, statue, new Vector3(c.x * 0.44f, 3.84f, c.y * 0.44f), new Vector3(0.16f, 0.64f, 0.16f));
            Deco("Firebox_Lid", t, statue, new Vector3(0f, 4.2f, 0f), new Vector3(1.2f, 0.1f, 1.2f));
            Deco("Roof", t, statue, new Vector3(0f, 4.32f, 0f), new Vector3(1.5f, 0.14f, 1.5f));
            Deco("Roof_Top", t, statue, new Vector3(0f, 4.46f, 0f), new Vector3(0.95f, 0.14f, 0.95f));
            Crystal("Finial", t, iron, new Vector3(0f, 4.53f + 0.17f, 0f), new Vector3(0.2f, 0.34f, 0.2f));
            Effect("Flame", t, new Vector3(0f, 3.52f, 0f), Anime + "Shared/Particles/VFX_Fire.prefab", 0.7f, 11f);
            Billboard("Glow", t, new Vector3(0f, 3.85f, 0f), 2.4f, new Color(1f, 0.62f, 0.28f) * 0.35f);
        }

        static Mesh sphere;
        static Mesh Sphere()
        {
            if (sphere == null) sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            return sphere;
        }

        // ------------------------------------------------------------------ beyond the walls

        /// <summary>Forest crowding the courtyard: pines and broad trees in a loose band outside every wall.</summary>
        static void Forest(Transform t)
        {
            var rnd = new System.Random(43);
            for (int side = 0; side < 4; side++)
            {
                bool odd = side % 2 == 1;
                float along = odd ? HalfZ : HalfX, off = odd ? HalfX : HalfZ;
                Transform s = Group("Side_" + side, t);
                s.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                for (int row = 0; row < 2; row++)
                    for (float x = -along - 4f; x <= along + 4f; x += 5.2f + row * 1.6f)
                    {
                        if (rnd.NextDouble() < 0.25) continue;
                        float px = x + ((float)rnd.NextDouble() - 0.5f) * 2.8f;
                        float pz = off + WallThick + 3.5f + row * 5f + (float)rnd.NextDouble() * 3f;
                        bool isPine = rnd.NextDouble() < 0.55;
                        float scale = isPine ? 1.15f + (float)rnd.NextDouble() * 0.6f : 1.25f + (float)rnd.NextDouble() * 0.5f;
                        ArenaTrees.Plant(s, isPine ? ArenaTrees.Kind.Pine : ArenaTrees.Kind.Broad, rnd.Next(4), new Vector3(px, -0.5f, pz),
                                         (float)rnd.NextDouble() * 360f, scale + row * 0.15f, bark, isPine ? pine : leaves);
                    }
            }
        }

        static void Beyond(Transform t)
        {
            var rnd = new System.Random(23);
            for (int i = 0; i < 20; i++)
            {
                float a = (i * 18f + (float)rnd.NextDouble() * 9f) * Mathf.Deg2Rad;
                float r = 72f + (float)rnd.NextDouble() * 24f;
                ArenaTrees.Plant(t, ArenaTrees.Kind.Bush, rnd.Next(4), new Vector3(Mathf.Cos(a) * r, -6f, Mathf.Sin(a) * r),
                                 (float)rnd.NextDouble() * 360f, 11f + (float)rnd.NextDouble() * 7f, bark, hills);
            }
            Ridge("Ridge", t, 170f, 36f, 30f, 56f, 6, 5, far, false);
            // a few rocks drifting high over the forest: the world the arenas share
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + 40f + (float)rnd.NextDouble() * 20f) * Mathf.Deg2Rad;
                float r = 60f + (float)rnd.NextDouble() * 30f;
                Island(t, new Vector3(Mathf.Cos(a) * r, 22f + (float)rnd.NextDouble() * 14f, Mathf.Sin(a) * r), 3f + (float)rnd.NextDouble() * 4f,
                       rnd, rock, null, Color.clear);
            }
        }

        // ------------------------------------------------------------------ atmosphere

        /// <summary>
        /// A soft morning: a low warm sun from the side, across the river -- both teams look along z, so both
        /// get the same light -- under a pale sky with a peach haze at its rim.
        /// </summary>
        public static void Atmosphere()
        {
            Light sun = Sun(new Color(1f, 0.90f, 0.76f), 1.15f, 0.7f, new Vector3(40f, 90f, 0f));
            Ambient(new Color(0.54f, 0.60f, 0.70f), new Color(0.60f, 0.58f, 0.54f), new Color(0.33f, 0.32f, 0.30f), 0.4f);
            Sky("Crossing_Sky", sun, new SkyColours
            {
                zenith = new Color(0.26f, 0.38f, 0.58f), middle = new Color(0.58f, 0.67f, 0.78f),
                horizon = new Color(0.95f, 0.85f, 0.76f), below = new Color(0.40f, 0.40f, 0.42f),
                sun = new Color(1.0f, 0.90f, 0.72f), cloud = new Color(1.0f, 0.95f, 0.90f),
                sunSize = 0.0016f, stars = 0f, clouds = 0.75f,
                cloudCover = 0.45f, cloudHeight = 0.7f, cloudScale = 0.6f, cloudStretch = 2.2f,
            });
        }

        public static void BakeEnvironment()
        {
            ArenaKit.BakeEnvironment("Assets/Scenes/Arena_Crossing_Lighting.lighting");
        }
    }
}
