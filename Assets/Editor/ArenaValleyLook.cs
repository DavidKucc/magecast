using System.Collections.Generic;
using MageCast.World;
using UnityEditor;
using UnityEngine;
using static MageCast.EditorTools.ArenaKit;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The Valley's look: a highland valley on a clear morning, painted like the Sanctum (ArenaKit). The
    /// terrain and the ruins are ArenaValleyBuilder's; this dresses them.
    ///
    ///   keep      a ring of runes on its floor, a crystal turning over it, torches on brackets round its
    ///             outside, banners on its two tallest stones
    ///   ruins     ivy down the towers and the broken walls
    ///   woods     pine forest up the slopes and broad trees lower down -- all of it outside the fence, where
    ///             nobody walks into a trunk that is not there; every tree has a twin turned half round,
    ///             like everything else in this map
    ///   beyond    snow peaks over the valley's own mountains, rocks drifting high up, pollen in the air
    /// </summary>
    public static class ArenaValleyLook
    {
        // the Valley's numbers, as ArenaValleyBuilder builds them
        const float Fence = 52f, KeepRadius = 6f, KeepFloor = 4.8f, WallThick = 0.9f;
        const string Mats = "Assets/Materials/Valley/";

        static Material stone, trim, gold, iron, crystal, rock, vine, leaves, pine, bark, far;
        static readonly Dictionary<string, Material> cloths = new Dictionary<string, Material>();

        public static void Dress(Transform root, Terrain terrain)
        {
            Materials();
            Transform look = Group("Look", root);
            Transform ruins = root.Find("Ruins");
            foreach (Transform r in ruins)
            {
                if (r.name == "Keep") Keep(Group("Keep", look), r);
                else if (r.name == "Tower") Tower(Group("Tower", look), r);
                else if (r.name == "Ruin_Wall") RuinWall(Group("Ruin_Wall", look), r);
            }
            Woods(Group("Woods", look), terrain);
            Beyond(Group("Beyond", look));
            Motes(look, new Vector3(0f, 3.5f, 0f), new Vector3(90f, 6f, 90f), 24f, 240, 0.1f,
                  new Color(1f, 0.95f, 0.7f), new Color(0.95f, 0.98f, 1f), new Color(0.8f, 1f, 0.7f));
            BakeGlows(look);
        }

        // ------------------------------------------------------------------ materials

        static void Materials()
        {
            Texture2D blocks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);
            stone = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Valley_Stone.mat");
            trim = Stone("Valley_Trim", blocks, 2f, new Color(0.76f, 0.74f, 0.68f), new Color(0.70f, 0.71f, 0.66f), new Color(0.72f, 0.74f, 0.62f),
                         new Color(0.94f, 0.92f, 0.84f), 0.07f, 0.15f);
            rock = Stone("Valley_Rock", Crag(), 8f, new Color(0.54f, 0.53f, 0.52f), new Color(0.48f, 0.50f, 0.50f), new Color(0.42f, 0.56f, 0.32f),
                         Color.white, 0f, 0.4f);
            gold = Standard("Valley_Gold", new Color(0.86f, 0.64f, 0.32f), 0.85f, 0.55f, Color.black);
            iron = Standard("Valley_Iron", new Color(0.16f, 0.15f, 0.15f), 0.6f, 0.4f, Color.black);
            // lit enough to glow, not so much that its facets vanish into one flat shape
            crystal = Standard("Valley_Crystal", new Color(0.30f, 0.90f, 0.85f), 0f, 0.9f, new Color(0.10f, 0.55f, 0.50f));
            vine = Foliage("Valley_Ivy", new Color(0.09f, 0.18f, 0.10f), new Color(0.32f, 0.50f, 0.22f), new Color(0.42f, 0.54f, 0.22f));
            leaves = Foliage("Valley_Leaves", new Color(0.07f, 0.16f, 0.09f), new Color(0.36f, 0.52f, 0.22f), new Color(0.50f, 0.58f, 0.24f));
            pine = Foliage("Valley_Pine", new Color(0.04f, 0.11f, 0.09f), new Color(0.18f, 0.34f, 0.22f), new Color(0.22f, 0.38f, 0.24f));
            bark = Foliage("Valley_Bark", new Color(0.18f, 0.14f, 0.11f), new Color(0.40f, 0.32f, 0.24f), new Color(0.30f, 0.30f, 0.24f));
            far = Distant("Valley_Far", new Color(0.26f, 0.32f, 0.38f), new Color(0.40f, 0.44f, 0.52f), new Color(0.66f, 0.76f, 0.88f), 0.45f, 160f, 135f);
            foreach (var e in Elements()) cloths[e.Key] = Standard("Valley_Banner_" + e.Key, e.Value * 0.55f, 0f, 0.15f, e.Value * 0.12f);
        }

        // ------------------------------------------------------------------ the ruins

        /// <summary>The keep: runes on its floor, a crystal over it, torches round its outside, two banners.</summary>
        static void Keep(Transform t, Transform keep)
        {
            t.localPosition = keep.localPosition;
            t.localRotation = keep.localRotation;

            // a double ring on the perch, and the floor it is cut into lights it up when struck
            Transform floor = keep.Find("Keep_Floor");
            float top = KeepFloor - Proud + 0.015f;
            var lines = new List<LineRenderer>
            {
                Circle("Ring", t, t.TransformPoint(new Vector3(0f, top, 0f)), Vector3.up, 3.2f, Arcane, 0.07f),
                Circle("Ring_Inner", t, t.TransformPoint(new Vector3(0f, top, 0f)), Vector3.up, 2.95f, Arcane, 0.035f),
            };
            if (floor != null) Runes(floor.gameObject, Arcane * 0.8f, 0f, lines.ToArray());

            // the crystal turning high over it: seen from anywhere in the valley
            Transform f = Group("Floating_Crystal", t);
            f.localPosition = new Vector3(0f, KeepFloor + 4.6f, 0f);
            GameObject big = Crystal("Crystal", f, crystal, Vector3.zero, new Vector3(1.0f, 1.9f, 1.0f));
            big.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Crystal("Shard", f, crystal, new Vector3(1.1f, -0.5f, 0.2f), new Vector3(0.28f, 0.55f, 0.28f));
            Crystal("Shard", f, crystal, new Vector3(-0.9f, 0.4f, -0.7f), new Vector3(0.24f, 0.48f, 0.24f));
            Crystal("Shard", f, crystal, new Vector3(-0.3f, -0.8f, 1.0f), new Vector3(0.2f, 0.4f, 0.2f));
            Billboard("Glow", f, Vector3.zero, 5.5f, Arcane * 0.45f);
            FloatingBob bob = f.gameObject.AddComponent<FloatingBob>();
            bob.height = 0.35f;
            bob.period = 5.5f;
            bob.spin = 18f;

            // torches on brackets round the outside, on the tall stones, and banners on the two tallest
            int torch = 0;
            string[] banner = { "Fire", "Ice" };
            int b = 0;
            foreach (Transform piece in keep)
            {
                if (piece.name != "Keep_Wall") continue;
                float h = piece.localPosition.y + piece.localScale.y * 0.5f;    // its top, above the ground
                Vector3 dir = piece.localPosition;
                dir.y = 0f;
                dir.Normalize();
                // a frame on the stone's outer face, looking out: -z of the frame into the valley
                Transform face = Group("Face", t);
                face.localPosition = dir * (KeepRadius + WallThick * 0.5f) ;
                face.localRotation = Quaternion.LookRotation(-dir, Vector3.up);
                if (h > 7f && b < banner.Length)
                {
                    Banner(face, new Vector3(0f, h - 5.4f, -0.05f), banner[b], cloths[banner[b]], gold);
                    b++;
                }
                else if (h > 5.5f && torch++ % 2 == 0)
                {
                    Deco("Bracket", face, iron, new Vector3(0f, 2.95f, -0.25f), new Vector3(0.14f, 0.12f, 0.5f));
                    FireBowl(face, new Vector3(0f, 3.02f, -0.5f), 0.55f, iron);
                }
            }
        }

        static void Tower(Transform t, Transform tower)
        {
            t.localPosition = tower.localPosition;
            t.localRotation = tower.localRotation;
            int k = 0;
            foreach (Transform piece in tower)
            {
                if (piece.name != "Tower_Wall") continue;
                float h = piece.localPosition.y + piece.localScale.y * 0.5f;
                if (h < 3.5f || k++ % 3 != 0) continue;
                Vector3 dir = piece.localPosition;
                dir.y = 0f;
                float r = dir.magnitude;
                dir.Normalize();
                ArenaTrees.Ivy(t, k, dir * (r + WallThick * 0.5f + 0.01f) + Vector3.up * (h - 0.02f), dir, 0.8f, vine);
            }
        }

        static void RuinWall(Transform t, Transform wall)
        {
            t.localPosition = wall.localPosition;
            t.localRotation = wall.localRotation;
            // down one face of the tallest piece (the one on the right, a ledge high)
            Transform first = null;
            foreach (Transform piece in wall) { first = piece; break; }
            if (first == null) return;
            Vector3 p = first.localPosition;
            float h = p.y + first.localScale.y * 0.5f;
            ArenaTrees.Ivy(t, 3, new Vector3(p.x, h - 0.02f, p.z + first.localScale.z * 0.5f + 0.01f), Vector3.forward, 0.6f, vine);
        }

        // ------------------------------------------------------------------ the woods

        /// <summary>Pine forest up the slopes outside the fence, broad trees at its foot; each tree with its twin turned half round.</summary>
        static void Woods(Transform t, Terrain terrain)
        {
            if (terrain == null) return;
            var rnd = new System.Random(5);
            TerrainData td = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            int planted = 0;
            var taken = new List<Vector2>();
            for (int i = 0; i < 6000 && planted < 300; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = Fence + 2f + (float)rnd.NextDouble() * 30f;
                float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < Fence + 1.5f) continue;      // the fence is square
                float u = (x - origin.x) / td.size.x, v = (z - origin.z) / td.size.z;
                if (u < 0.02f || u > 0.98f || v < 0.02f || v > 0.98f) continue;
                Vector3 n = td.GetInterpolatedNormal(u, v);
                float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                if (n.y < 0.5f || y > 32f) continue;                                     // a cliff, or over the tree line
                // trees apart, so none grows out of another (two the same, close, put their faces in one place)
                var here = new Vector2(x, z);
                bool crowded = false;
                foreach (Vector2 o in taken)
                    if ((o - here).sqrMagnitude < 3.2f * 3.2f || (o + here).sqrMagnitude < 3.2f * 3.2f) { crowded = true; break; }
                if (crowded) continue;
                taken.Add(here);
                bool broad = y < 6f && rnd.NextDouble() < 0.5;
                int variant = rnd.Next(4);
                float yaw = (float)rnd.NextDouble() * 360f;
                float scale = broad ? 1.3f + (float)rnd.NextDouble() * 0.5f : 1.1f + (float)rnd.NextDouble() * 0.9f;
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector3 at = new Vector3(x * s, terrain.SampleHeight(new Vector3(x * s, 0f, z * s)) + origin.y - 0.3f, z * s);
                    ArenaTrees.Plant(t, broad ? ArenaTrees.Kind.Broad : ArenaTrees.Kind.Pine, variant, at, yaw + (s < 0 ? 180f : 0f), scale,
                                     bark, broad ? leaves : pine);
                    planted++;
                }
            }
        }

        static void Beyond(Transform t)
        {
            // snow peaks over the valley's own mountains, far out in the haze
            Ridge("Ridge", t, 240f, 50f, 100f, 175f, 8, 21, far);
            var rnd = new System.Random(13);
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + (float)rnd.NextDouble() * 20f) * Mathf.Deg2Rad;
                float r = 60f + (float)rnd.NextDouble() * 50f;
                Island(t, new Vector3(Mathf.Cos(a) * r, 34f + (float)rnd.NextDouble() * 20f, Mathf.Sin(a) * r), 4f + (float)rnd.NextDouble() * 6f,
                       rnd, rock, i % 3 == 0 ? crystal : null, Arcane * 0.3f);
            }
        }

        // ------------------------------------------------------------------ atmosphere

        /// <summary>
        /// A clear morning: the sun from the side across the valley -- both teams look along z, so both get the
        /// same light -- a deep blue sky, round white clouds over the peaks.
        /// </summary>
        public static void Atmosphere()
        {
            Light sun = Sun(new Color(1f, 0.94f, 0.84f), 1.2f, 0.7f, new Vector3(38f, 90f, 0f));
            Ambient(new Color(0.56f, 0.64f, 0.76f), new Color(0.56f, 0.58f, 0.52f), new Color(0.30f, 0.32f, 0.26f), 0.35f);
            Sky("Valley_Sky", sun, new SkyColours
            {
                zenith = new Color(0.18f, 0.38f, 0.70f), middle = new Color(0.48f, 0.66f, 0.88f),
                horizon = new Color(0.84f, 0.89f, 0.93f), below = new Color(0.40f, 0.45f, 0.40f),
                sun = new Color(1.0f, 0.95f, 0.85f), cloud = new Color(1.0f, 1.0f, 1.0f),
                sunSize = 0.0016f, stars = 0f, clouds = 0.8f,
                cloudCover = 0.5f, cloudHeight = 0.85f, cloudScale = 0.5f, cloudStretch = 1.6f,
            });
        }

        public static void BakeEnvironment()
        {
            ArenaKit.BakeEnvironment("Assets/Scenes/Arena_Valley_Lighting.lighting");
        }
    }
}
