using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The fourth map, "Valley": a grassy, uneven valley floor ringed by mountains, boulders, and ruined
    /// round towers that can still be climbed. Four times the floor of the Temple (88 x 88 m against
    /// 44 x 44). Built entirely from this file -- Tools > Arena > Build Valley Map, then Audit Valley.
    ///
    /// The ground is a Unity Terrain generated here: gentle hills (a couple of metres) on the valley
    /// floor, steep mountains past its edge, an invisible wall where the slope gets too steep to fight
    /// on. Grass where it is flat, rock where it is steep, bare earth round the ruins and the spawns.
    ///
    /// Same scale contract and height set as the other maps: 1.2 a jump, 1.6 chest cover, 2.4 a ledge
    /// you climb. The towers are climbed the way a ruin is: each broken piece of wall a jump or a
    /// pull-up higher than the one before, round the ring, up to a floor near the top.
    ///
    /// Fair for two teams: the terrain and everything on it are the same turned 180 degrees.
    /// Team B on +z, team A on -z.
    /// </summary>
    public static class ArenaValleyBuilder
    {
        public const string ScenePath = "Assets/Scenes/Arena_Valley.unity";
        const string TemplateScene = "Assets/Scenes/Arena_Blockout.unity";
        const string MatFolder = "Assets/Materials/Valley";
        const string TexFolder = "Assets/Textures/Valley";
        const string TerrainPath = "Assets/Materials/Valley/Valley_Terrain.asset";

        // --- size ---
        const float Half = 44f;               // the fighting floor: 88 x 88
        const float TerrainHalf = 95f;        // the terrain runs on into the mountains
        const float Fence = 52f;              // invisible wall, on the lower slopes
        const float TerrainBase = -4f;        // the terrain's lowest possible point
        const float TerrainHeight = 52f;
        const int Res = 257;

        // --- ground shape ---
        const float HillHeight = 2.4f;        // valley floor: up and down by about this much
        const float MountainFrom = 49f, MountainTo = 78f, MountainHeight = 36f;

        // --- heights this map is made of ---
        const float Jump = 1.2f, Chest = 1.6f, Ledge = 2.4f;

        static Material stoneMat, rockMat, woodMat;
        /// <summary>Everything placed so far that a player can hide behind: centre x, z and rough radius.</summary>
        static readonly List<Vector3> cover = new List<Vector3>();
        static Terrain terrain;
        static readonly List<Vector4> flats = new List<Vector4>();   // x, z, radius, height
        static System.Random rnd;

        [MenuItem("Tools/Arena/Build Valley Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Valley] stop play mode first"); return; }
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                Debug.LogWarning("[Valley] the open scene has unsaved changes -- save or discard them first");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                && !AssetDatabase.CopyAsset(TemplateScene, ScenePath))
            {
                Debug.LogError("[Valley] could not copy the arena scene");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string old in new[] { "Arena", "Sun", "Valley_Terrain" })
            {
                GameObject g = GameObject.Find(old);
                if (g != null) Object.DestroyImmediate(g);
            }

            ArenaTempleBuilder.EnsureFolder(MatFolder);
            ArenaTempleBuilder.EnsureFolder(TexFolder);
            Materials();
            rnd = new System.Random(4207);

            GameObject root = new GameObject("Arena");
            PlanFlatSpots();
            BuildTerrain(root.transform);
            BuildProps(root.transform);
            ArenaValleyLook.Dress(root.transform, terrain);
            ArenaValleyLook.Atmosphere();

            Camera cam = Camera.main;
            if (cam != null) cam.farClipPlane = 400f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // the light is worked out from the saved scene, then saved with it
            ArenaValleyLook.BakeEnvironment();
            EditorSceneManager.SaveScene(scene);
            ArenaTempleBuilder.EnsureInBuild(ScenePath);
            Debug.Log("[Valley] built: " + (root.GetComponentsInChildren<Transform>().Length - 1) + " objects\n" + Audit());
        }

        [MenuItem("Tools/Arena/Audit Valley")]
        public static void AuditMenu() { Debug.Log(Audit()); }

        public static string Audit() { return ArenaTempleBuilder.AuditArea("[Valley]", Half, Half); }

        // ------------------------------------------------------------------ layout

        // Team B's half (z positive); the other half is the same turned 180 degrees.
        static readonly Vector3[] SpawnsB = { new Vector3(-20f, 0f, 36f), new Vector3(0f, 0f, 38f), new Vector3(20f, 0f, 36f) };
        static readonly Vector2[] TowersB = { new Vector2(-22f, 17f), new Vector2(27f, 9f) };
        // ruined wall runs: centre x, centre z, yaw
        static readonly Vector3[] WallsB = { new Vector3(8f, 23f, 15f), new Vector3(-9f, 29f, -20f), new Vector3(34f, 25f, 60f),
                                             new Vector3(-34f, 6f, 80f) };
        const float KeepRadius = 6f;

        static void PlanFlatSpots()
        {
            flats.Clear();
            Flat(0f, 0f, KeepRadius + 4f);
            foreach (Vector3 s in SpawnsB) { Flat(s.x, s.z, 4f); Flat(-s.x, -s.z, 4f); }
            foreach (Vector2 t in TowersB) { Flat(t.x, t.y, 6.5f); Flat(-t.x, -t.y, 6.5f); }
        }

        static void Flat(float x, float z, float radius)
        {
            flats.Add(new Vector4(x, z, radius, RawGround(x, z)));
        }

        // ------------------------------------------------------------------ terrain

        /// <summary>Hills, the same turned 180 degrees: the average of a noise and its rotation.</summary>
        static float Hills(float x, float z)
        {
            return 0.5f * (HillNoise(x, z) + HillNoise(-x, -z));
        }

        static float HillNoise(float x, float z)
        {
            float broad = Mathf.PerlinNoise(x * 0.032f + 13.1f, z * 0.032f + 7.7f) - 0.5f;
            float fine = Mathf.PerlinNoise(x * 0.085f + 51.3f, z * 0.085f + 21.9f) - 0.5f;
            return broad * 2f * HillHeight + fine * 0.7f;
        }

        /// <summary>The ground before any flattening: valley hills, rising into mountains past the edge.</summary>
        static float RawGround(float x, float z)
        {
            // a nearly square ring: flat right into the corners of the fighting floor, mountains past it
            float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 8f) + Mathf.Pow(Mathf.Abs(z), 8f), 0.125f);
            float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MountainFrom, MountainTo, d));
            float ridge = 0.5f * (Mathf.PerlinNoise(x * 0.018f + 3f, z * 0.018f + 9f)
                                + Mathf.PerlinNoise(-x * 0.018f + 3f, -z * 0.018f + 9f));
            // ridges and gullies on top of the broad shape, so the mountains are rock, not dunes
            float rugged = 0.5f * (Ridged(x * 0.055f, z * 0.055f) + Ridged(-x * 0.055f, -z * 0.055f));
            return Hills(x, z) * (1f - m) + MountainHeight * m * (0.45f + 0.7f * ridge + 0.45f * rugged);
        }

        static float Ridged(float x, float z)
        {
            return 1f - Mathf.Abs(Mathf.PerlinNoise(x + 41f, z + 17f) * 2f - 1f);
        }

        /// <summary>The ground as built: flattened under the ruins and the spawns, so nothing floats.</summary>
        static float Ground(float x, float z)
        {
            float h = RawGround(x, z);
            foreach (Vector4 f in flats)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(f.x, f.y));
                float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(f.z * 0.7f, f.z * 1.4f, d));
                h = Mathf.Lerp(h, f.w, w);
            }
            return h;
        }

        static void BuildTerrain(Transform root)
        {
            TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, TerrainPath);
            }
            td.heightmapResolution = Res;
            td.size = new Vector3(TerrainHalf * 2f, TerrainHeight, TerrainHalf * 2f);

            float[,] heights = new float[Res, Res];
            for (int iz = 0; iz < Res; iz++)
                for (int ix = 0; ix < Res; ix++)
                {
                    float x = -TerrainHalf + ix / (Res - 1f) * TerrainHalf * 2f;
                    float z = -TerrainHalf + iz / (Res - 1f) * TerrainHalf * 2f;
                    heights[iz, ix] = Mathf.Clamp01((Ground(x, z) - TerrainBase) / TerrainHeight);
                }
            td.SetHeights(0, 0, heights);

            // A meadow with a scatter of small flowers, a deeper meadow, crag, a trodden path: painted, not
            // photographed -- soft patches of a warmer and a cooler green, the flowers a dot of colour each.
            td.terrainLayers = new[]
            {
                Layer("Valley_Grass", Noise("Valley_Meadow", new Color(0.43f, 0.51f, 0.28f), new Color(0.29f, 0.39f, 0.21f), 31, false,
                                            new Color(0.53f, 0.54f, 0.30f), 0.35f, new Color(0.95f, 0.92f, 0.70f), 0.004f), 5f),
                Layer("Valley_Grass_Dark", Noise("Valley_Meadow_Deep", new Color(0.34f, 0.44f, 0.24f), new Color(0.21f, 0.31f, 0.18f), 47, false,
                                                 new Color(0.30f, 0.44f, 0.30f), 0.3f, new Color(0.80f, 0.70f, 0.95f), 0.003f), 7f),
                Layer("Valley_Rock", Noise("Valley_Crag", new Color(0.60f, 0.57f, 0.53f), new Color(0.36f, 0.35f, 0.37f), 59, true,
                                           new Color(0.50f, 0.52f, 0.44f), 0.25f, Color.black, 0f), 8f),
                Layer("Valley_Earth", Noise("Valley_Path", new Color(0.58f, 0.48f, 0.35f), new Color(0.42f, 0.35f, 0.26f), 71, false,
                                            new Color(0.52f, 0.50f, 0.38f), 0.3f, new Color(0.70f, 0.66f, 0.58f), 0.01f), 4f),
            };

            const int aRes = 256;
            td.alphamapResolution = aRes;
            float[,,] splat = new float[aRes, aRes, 4];
            for (int iz = 0; iz < aRes; iz++)
                for (int ix = 0; ix < aRes; ix++)
                {
                    float u = ix / (aRes - 1f), v = iz / (aRes - 1f);
                    float x = -TerrainHalf + u * TerrainHalf * 2f, z = -TerrainHalf + v * TerrainHalf * 2f;
                    float steep = td.GetSteepness(u, v);
                    float y = Ground(x, z);
                    float rock = Mathf.Max(Mathf.InverseLerp(30f, 40f, steep), Mathf.InverseLerp(16f, 24f, y));
                    float earth = 0f;
                    foreach (Vector4 f in flats)
                    {
                        float d = Vector2.Distance(new Vector2(x, z), new Vector2(f.x, f.y));
                        earth = Mathf.Max(earth, 1f - Mathf.InverseLerp(f.z * 0.6f, f.z * 1.1f, d));
                    }
                    earth *= 1f - rock;
                    float grass = Mathf.Max(0f, 1f - rock - earth);
                    float mix = 0.5f * (Mathf.PerlinNoise(x * 0.06f + 5f, z * 0.06f + 5f) + Mathf.PerlinNoise(-x * 0.06f + 5f, -z * 0.06f + 5f));
                    mix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, mix));
                    splat[iz, ix, 0] = grass * (1f - mix);
                    splat[iz, ix, 1] = grass * mix;
                    splat[iz, ix, 2] = rock;
                    splat[iz, ix, 3] = earth;
                }
            td.SetAlphamaps(0, 0, splat);
            EditorUtility.SetDirty(td);

            GameObject go = Terrain.CreateTerrainGameObject(td);
            go.name = "Valley_Terrain";
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(-TerrainHalf, TerrainBase, -TerrainHalf);
            go.isStatic = true;
            terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 120f;
            terrain.drawInstanced = false;
            Physics.SyncTransforms();
        }

        static float GroundAt(float x, float z)
        {
            return terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : Ground(x, z);
        }

        // ------------------------------------------------------------------ props

        static void BuildProps(Transform root)
        {
            Transform shell = ArenaTempleBuilder.Group("Shell", root);
            Transform spawns = ArenaTempleBuilder.Group("Spawns", root);
            Transform targets = ArenaTempleBuilder.Group("Targets", root);
            Transform ruins = ArenaTempleBuilder.Group("Ruins", root);
            Transform rocks = ArenaTempleBuilder.Group("Rocks", root);

            // the edge of the fight: invisible, on the lower slopes
            foreach (var w in new[] { ("Wall_N", new Vector3(0f, 0f, Fence), new Vector3(Fence * 2f, 1f, 1f)),
                                      ("Wall_S", new Vector3(0f, 0f, -Fence), new Vector3(Fence * 2f, 1f, 1f)),
                                      ("Wall_E", new Vector3(Fence, 0f, 0f), new Vector3(1f, 1f, Fence * 2f)),
                                      ("Wall_W", new Vector3(-Fence, 0f, 0f), new Vector3(1f, 1f, Fence * 2f)) })
            {
                GameObject g = new GameObject(w.Item1);
                g.transform.SetParent(shell, false);
                g.transform.localPosition = w.Item2 + Vector3.up * 15f;
                BoxCollider bc = g.AddComponent<BoxCollider>();
                bc.size = new Vector3(w.Item3.x, 40f, w.Item3.z);
                g.isStatic = true;
            }

            // the keep in the middle: one piece, its heights repeat every half turn
            float[] keep = { 0f, Jump, Ledge, 3.6f, 4.8f, 6f, 7.2f, 6f, 0f, Jump, Ledge, 3.6f, 4.8f, 6f, 7.2f, 6f };
            Tower("Keep", ruins, new Vector2(0f, 0f), KeepRadius, keep, 0f, 4.8f, true, 900);

            for (int h = 0; h < 2; h++)
            {
                float sign = h == 0 ? 1f : -1f;      // the second half is the first turned 180 degrees
                for (int k = 0; k < TowersB.Length; k++)
                {
                    Vector2 t = TowersB[k];
                    float[] ring = { 0f, Jump, Ledge, 3.6f, 4.8f, 6f, 6f, 3.6f, Ledge, 6f, 4.8f, Chest };
                    Tower("Tower", ruins, t * sign, 3.6f, ring, h * 180f + t.x * 3f, 4.8f, false, 100 + k);
                }
                foreach (Vector3 w in WallsB)
                    RuinWall(ruins, new Vector2(w.x, w.y) * sign, w.z + h * 180f);
                // a broken wall beside every spawn: somewhere to step behind the moment you are back
                foreach (Vector3 sp in SpawnsB)
                    RuinWall(ruins, new Vector2(sp.x - 3.5f, sp.z - 4f) * sign, 20f + h * 180f);
            }

            cover.Clear();
            cover.Add(new Vector3(0f, 0f, KeepRadius));
            foreach (Vector2 t in TowersB) { cover.Add(new Vector3(t.x, t.y, 3.6f)); cover.Add(new Vector3(-t.x, -t.y, 3.6f)); }
            foreach (Vector3 w in WallsB) { cover.Add(new Vector3(w.x, w.y, 3.5f)); cover.Add(new Vector3(-w.x, -w.y, 3.5f)); }
            ScatterRocks(rocks);
            FillGaps(rocks);

            for (int i = 0; i < SpawnsB.Length; i++)
            {
                Vector3 b = SpawnsB[i];
                ArenaTempleBuilder.Marker("SpawnB_" + i, spawns, new Vector3(b.x, GroundAt(b.x, b.z), b.z), 180f);
                ArenaTempleBuilder.Marker("SpawnA_" + i, spawns, new Vector3(-b.x, GroundAt(-b.x, -b.z), -b.z), 0f);
            }

            foreach (var d in new[] { ("Dummy_Near", new Vector2(3f, -30f)), ("Dummy_Mid", new Vector2(-9f, -22f)), ("Dummy_Far", new Vector2(13f, -15f)) })
                ArenaTempleBuilder.Dummy(d.Item1, targets, new Vector3(d.Item2.x, GroundAt(d.Item2.x, d.Item2.y) + 1f, d.Item2.y));
        }

        /// <summary>
        /// A ruined round tower: a ring of wall pieces, each its own height. Read round the ring from the
        /// gap, the heights go up a jump or a pull-up at a time, so the broken wall is the stair. A floor
        /// across part of the inside at <paramref name="floorAt"/> is the perch at the top.
        /// </summary>
        static void Tower(string name, Transform parent, Vector2 centre, float radius, float[] heights, float turn, float floorAt,
                          bool fullFloor, int seed)
        {
            var r = new System.Random(seed);
            Transform t = ArenaTempleBuilder.Group(name, parent);
            float ground = GroundAt(centre.x, centre.y);
            t.localPosition = new Vector3(centre.x, ground, centre.y);
            t.localRotation = Quaternion.Euler(0f, turn, 0f);

            int n = heights.Length;
            float chord = 2f * Mathf.PI * radius / n;
            const float thick = 0.9f, sunk = 1.5f;
            for (int i = 0; i < n; i++)
            {
                if (heights[i] <= 0f) continue;      // the way in
                float a = (i + 0.5f) * 360f / n;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                // every other piece 2 cm taller: neighbours overlap at their ends, and two the same height there
                // put their tops in one plane, flickering
                float h = heights[i] + sunk + (i % 2) * ArenaKit.Proud;
                ArenaTempleBuilder.Box(name + "_Wall", t, stoneMat, dir * radius + Vector3.up * (h * 0.5f - sunk),
                                       new Vector3(chord + 0.15f, h, thick), a);
            }

            // The perch: on a tower, a floor over the half of the inside furthest from the way in; on the
            // keep, which has a way in on both sides, across the whole inside (the same seen from either team).
            // 2 cm under the wall pieces of the same height it runs into, so their tops are not one plane
            float fr = radius - thick * 0.5f;
            if (fullFloor)
                ArenaTempleBuilder.Box(name + "_Floor", t, woodMat, new Vector3(0f, floorAt - 0.15f - ArenaKit.Proud, 0f),
                                       new Vector3(fr * 1.4f, 0.3f, fr * 1.4f));
            else
                ArenaTempleBuilder.Box(name + "_Floor", t, woodMat, new Vector3(0f, floorAt - 0.15f - ArenaKit.Proud, -fr * 0.45f),
                                       new Vector3(fr * 1.7f, 0.3f, fr * 1.0f));

            // fallen stones round the foot
            for (int k = 0; k < 3; k++)
            {
                float a = (float)r.NextDouble() * 360f;
                Vector3 at = Quaternion.Euler(0f, a, 0f) * Vector3.forward * (radius + 1.6f + (float)r.NextDouble());
                Rock(t, new Vector3(centre.x, 0f, centre.y) + Quaternion.Euler(0f, turn, 0f) * at,
                     0.8f + (float)r.NextDouble() * 0.5f, true, r.Next(), 0f);
            }
        }

        /// <summary>A stretch of broken wall: three pieces, chest high to a ledge, the middle one lowest.</summary>
        static void RuinWall(Transform parent, Vector2 centre, float yaw)
        {
            Transform t = ArenaTempleBuilder.Group("Ruin_Wall", parent);
            t.localPosition = new Vector3(centre.x, GroundAt(centre.x, centre.y), centre.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float[] h = { Ledge, Chest, EyeHigh };
            for (int i = 0; i < 3; i++)
            {
                float hh = h[i] + 1f;
                ArenaTempleBuilder.Box("Ruin_Piece", t, stoneMat, new Vector3((i - 1) * 2.4f, hh * 0.5f - 1f, 0f), new Vector3(2.3f, hh, 0.9f));
            }
        }

        const float EyeHigh = 2.8f;

        /// <summary>
        /// Boulders over the whole valley, on a shaken grid, so cover is never far: a third small enough
        /// to jump onto, a third chest high, a third big enough to hide behind standing up.
        /// </summary>
        static void ScatterRocks(Transform parent)
        {
            // Outcrops, not a carpet: a few big groups of boulders far apart, each one a place in its own
            // right (a big stone to hide behind, smaller ones to climb it by), with open grass between.
            // 13 m apart they still leave nowhere more than 8 m from cover.
            const float step = 13f;
            for (float x = -Half + 5f; x <= Half - 3f; x += step)
                for (float z = 4f; z <= Half - 2f; z += step)
                {
                    float px = x + ((float)rnd.NextDouble() - 0.5f) * 4f;
                    float pz = z + ((float)rnd.NextDouble() - 0.5f) * 4f;
                    if (!Free(px, pz)) continue;
                    int count = 3 + rnd.Next(3);
                    float spread = 1.6f + (float)rnd.NextDouble() * 1.4f;
                    for (int i = 0; i < count; i++)
                    {
                        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                        float r = i == 0 ? 0f : spread * (0.6f + (float)rnd.NextDouble() * 0.6f);
                        float size = i == 0 ? 2.6f + (float)rnd.NextDouble() * 0.9f
                                   : i == 1 ? 1.9f + (float)rnd.NextDouble() * 0.4f
                                   : 0.9f + (float)rnd.NextDouble() * 0.5f;
                        float rx = px + Mathf.Cos(a) * r, rz = pz + Mathf.Sin(a) * r;
                        int seed = rnd.Next();
                        // the same stone on the other side, turned half round: fair to both teams
                        Rock(parent, new Vector3(rx, 0f, rz), size, false, seed, 0f);
                        Rock(parent, new Vector3(-rx, 0f, -rz), size, false, seed, 180f);
                    }
                }
        }

        /// <summary>
        /// The shaken grid leaves holes. Walk the half on a 4 m grid and put a chest-high stone wherever
        /// nothing is within 6.5 m, so the audit's "cover within 8 m" holds everywhere a player stands --
        /// including round the spawns, where the grid keeps clear.
        /// </summary>
        static void FillGaps(Transform parent)
        {
            // Measured the way the audit measures, not guessed from centres: anything solid that rises a
            // metre over where you stand counts, so a stone down the slope from you does not.
            Physics.SyncTransforms();
            for (int pass = 0; pass < 2; pass++)
                for (float x = -Half + 0.5f; x <= Half - 0.5f; x += 2f)
                    for (float z = 0.5f; z <= Half - 0.5f; z += 2f)
                    {
                        if (HasCover(x, z) && HasCover(-x, -z)) continue;
                        if (!Clear(x, z, 6f)) continue;   // round a spawn the broken wall beside it is the cover
                        int seed = rnd.Next();
                        Rock(parent, new Vector3(x, 0f, z), 2.0f, false, seed, 0f);
                        Rock(parent, new Vector3(-x, 0f, -z), 2.0f, false, seed, 180f);
                        Physics.SyncTransforms();
                    }
        }

        static bool HasCover(float x, float z)
        {
            float ground = GroundAt(x, z);
            Vector3 chest = new Vector3(x, ground + 1.2f, z);
            foreach (Collider c in Physics.OverlapSphere(chest, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider || c.gameObject.name.StartsWith("Wall_")) continue;
                if (c.bounds.max.y < ground + 1f) continue;
                Vector3 cp = c.ClosestPoint(chest);
                if (Vector2.Distance(new Vector2(cp.x, cp.z), new Vector2(x, z)) <= 6f) return true;
            }
            return false;
        }

        /// <summary>Nothing important within <paramref name="margin"/> of the spot: a spawn, the way into a ruin.</summary>
        static bool Clear(float x, float z, float margin)
        {
            var p = new Vector2(x, z);
            foreach (Vector3 s in SpawnsB)
                if (Vector2.Distance(p, new Vector2(s.x, s.z)) < margin || Vector2.Distance(p, new Vector2(-s.x, -s.z)) < margin) return false;
            if (p.magnitude < KeepRadius + 2.5f) return false;
            foreach (Vector2 t in TowersB)
                if (Vector2.Distance(p, t) < 6f || Vector2.Distance(p, -t) < 6f) return false;
            return true;
        }

        static bool Free(float x, float z)
        {
            var p = new Vector2(x, z);
            if (p.magnitude < KeepRadius + 4f) return false;
            foreach (Vector3 s in SpawnsB)
                if (Vector2.Distance(p, new Vector2(s.x, s.z)) < 5f || Vector2.Distance(p, new Vector2(-s.x, -s.z)) < 5f) return false;
            foreach (Vector2 t in TowersB)
                if (Vector2.Distance(p, t) < 7f || Vector2.Distance(p, -t) < 7f) return false;
            foreach (Vector3 w in WallsB)
                if (Vector2.Distance(p, new Vector2(w.x, w.y)) < 5f || Vector2.Distance(p, new Vector2(-w.x, -w.y)) < 5f) return false;
            return Mathf.Abs(x) < Half - 0.5f && Mathf.Abs(z) < Half - 0.5f;
        }

        /// <summary>
        /// A boulder: two or three blocks turned against each other, sunk a little into the ground, so it
        /// reads as one rough stone rather than a crate. <paramref name="size"/> is about its height.
        /// </summary>
        static void Rock(Transform parent, Vector3 at, float size, bool local, int seed, float extraYaw)
        {
            var rnd = new System.Random(seed);
            if (!local && size >= 1.5f) cover.Add(new Vector3(at.x, at.z, size * 0.6f));   // a stone you can jump onto hides nobody
            Transform t = ArenaTempleBuilder.Group("Rock", parent);
            float ground = GroundAt(at.x, at.z);
            Vector3 world = new Vector3(at.x, ground, at.z);
            if (local) t.position = world; else t.localPosition = world;
            t.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f + extraYaw, 0f);
            // Blocks tipped well over against each other and sunk into the turf: the corners that stick
            // out read as the facets of a rough stone, where level blocks read as crates.
            int pieces = size > 2f ? 4 : size > 1.4f ? 3 : 2;
            for (int i = 0; i < pieces; i++)
            {
                float s = size * (i == 0 ? 0.85f : 0.55f + (float)rnd.NextDouble() * 0.25f);
                Vector3 scale = new Vector3(s * (1.0f + (float)rnd.NextDouble() * 0.5f), s * (0.8f + (float)rnd.NextDouble() * 0.3f),
                                            s * (0.9f + (float)rnd.NextDouble() * 0.4f));
                Vector3 offset = new Vector3(((float)rnd.NextDouble() - 0.5f) * size * 0.6f, s * 0.42f - size * 0.12f,
                                             ((float)rnd.NextDouble() - 0.5f) * size * 0.6f);
                GameObject g = ArenaTempleBuilder.Box("Rock_Piece", t, rockMat, offset, scale);
                g.transform.localRotation = Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 60f,
                                                             (float)rnd.NextDouble() * 360f,
                                                             ((float)rnd.NextDouble() - 0.5f) * 60f);
            }
        }

        // ------------------------------------------------------------------ look

        /// <summary>The ruins in the painted stone of the Sanctum (ArenaKit.Stone): weathered grey, lit worn edges, moss on top.</summary>
        static void Materials()
        {
            ArenaKit.Begin(MatFolder, "Valley");
            ArenaTempleBuilder.EnsureFolder("Assets/Textures");
            ArenaTempleBuilder.EnsureFolder("Assets/Textures/Temple");
            Texture2D blocks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);
            Texture2D planks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Bricks.png", 4, 8, true, 23);
            Texture2D rock = Noise("Valley_Crag", new Color(0.60f, 0.57f, 0.53f), new Color(0.36f, 0.35f, 0.37f), 59, true,
                                   new Color(0.50f, 0.52f, 0.44f), 0.25f, Color.black, 0f);
            Color moss = new Color(0.44f, 0.54f, 0.30f);
            stoneMat = ArenaKit.Stone("Valley_Stone", blocks, 3f, new Color(0.64f, 0.62f, 0.58f), new Color(0.56f, 0.58f, 0.54f), moss,
                                      new Color(0.90f, 0.88f, 0.80f), 0.13f, 0.3f);
            // boulders: soft lit edges read their facets as a rough stone's
            rockMat = ArenaKit.Stone("Valley_Boulder", rock, 3f, new Color(0.98f, 0.96f, 0.92f), new Color(0.86f, 0.90f, 0.84f), new Color(0.82f, 0.94f, 0.66f),
                                     new Color(1.15f, 1.12f, 1.05f), 0.08f, 0.2f);
            woodMat = ArenaKit.Stone("Valley_Wood", planks, 2f, new Color(0.50f, 0.38f, 0.27f), new Color(0.46f, 0.37f, 0.29f), new Color(0.56f, 0.44f, 0.32f),
                                     new Color(0.72f, 0.60f, 0.46f), 0.06f, 0.2f);
        }

        static TerrainLayer Layer(string name, Texture2D tex, float tile)
        {
            string path = MatFolder + "/" + name + ".terrainlayer";
            TerrainLayer l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null)
            {
                l = new TerrainLayer();
                AssetDatabase.CreateAsset(l, path);
            }
            l.diffuseTexture = tex;
            l.tileSize = new Vector2(tile, tile);
            // Grass a touch less green than its texture: in full sun it read as lime, louder than the
            // ice and air spells that have to stand out against it.
            l.diffuseRemapMax = name.StartsWith("Valley_Grass") ? new Vector4(0.96f, 0.86f, 0.92f, 1f) : Vector4.one;
            l.smoothness = 0f;
            l.metallic = 0f;
            EditorUtility.SetDirty(l);
            return l;
        }

        /// <summary>
        /// A seamless noise texture between two colours, written once as a PNG. Grass gets fine speckle
        /// for blades, rock gets dark creases. Seamless by blending the noise with itself shifted a whole
        /// tile, so the terrain shows no grid. Over it, soft patches of an accent colour and a scatter of
        /// dots (flowers in a meadow, pebbles on a path), as a painter would dab them.
        /// </summary>
        static Texture2D Noise(string name, Color light, Color dark, int seed, bool creases,
                               Color accent, float accentAmount, Color dot, float dots)
        {
            string path = TexFolder + "/" + name + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 512;
            var r = new System.Random(seed);
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float broad = Tileable(u, v, 4f, seed);
                    float fine = Tileable(u, v, 22f, seed + 11);
                    float t = broad * 0.6f + fine * 0.4f;
                    if (creases)
                    {
                        float crease = Mathf.Abs(Tileable(u, v, 7f, seed + 23) - 0.5f) * 2f;
                        t *= Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, crease * 4f));
                    }
                    else
                    {
                        t += ((float)r.NextDouble() - 0.5f) * 0.18f;      // blades and pebbles
                    }
                    Color c = Color.Lerp(dark, light, Mathf.Clamp01(t));
                    float patch = Tileable(u, v, 3f, seed + 37);
                    c = Color.Lerp(c, accent * (0.85f + t * 0.3f), Mathf.SmoothStep(0.45f, 0.75f, patch) * accentAmount);
                    px[y * size + x] = c;
                }
            // the dots: a few pixels across each, so they stay specks at a distance (mipmaps blend them away)
            for (int k = 0; k < dots * size * size / 9f; k++)
            {
                int cx = r.Next(size), cy = r.Next(size);
                Color d = dot * (0.85f + (float)r.NextDouble() * 0.3f);
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (Mathf.Abs(ox) + Mathf.Abs(oy) < 2)
                            px[((cy + oy + size) % size) * size + (cx + ox + size) % size] = d;
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Perlin noise that repeats every whole tile: four shifted copies blended.</summary>
        static float Tileable(float u, float v, float freq, int seed)
        {
            float ox = seed * 1.37f, oy = seed * 2.11f;
            float a = Mathf.PerlinNoise(u * freq + ox, v * freq + oy);
            float b = Mathf.PerlinNoise((u - 1f) * freq + ox, v * freq + oy);
            float c = Mathf.PerlinNoise(u * freq + ox, (v - 1f) * freq + oy);
            float d = Mathf.PerlinNoise((u - 1f) * freq + ox, (v - 1f) * freq + oy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
