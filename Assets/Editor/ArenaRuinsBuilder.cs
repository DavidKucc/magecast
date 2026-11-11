using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Kit = MageCast.EditorTools.ArenaRuinsKit;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The fifth map, "Ruins": the square of a ruined town, built out of David's own models (ArenaRuinsKit)
    /// instead of boxes. Tools > Arena > Build Ruins Map, then Audit Ruins.
    ///
    /// Three levels, on the same height set as the other maps:
    ///   -1.2  a sunken lane down the middle, from one side's half to the other's, lined with brick
    ///    0    the square: columns along the lane, low walls, a house in each back corner
    ///    2.4  the bridge over the lane (its deck 2.2, its end sills 2.4) with a stair at each end, and two
    ///         terraces a side -- one with a stair, one you climb or reach over a brick dais
    ///
    /// Why the lane is dug: the bridge model is open underneath, but at the size where its deck is a ledge
    /// you can climb to, the timber arches under it leave 1.5 m of headroom. Dug 1.2 m down, the way under
    /// has 2.2 m at the lane's edge and 2.7 m in the middle.
    ///
    /// Layout, one half (team B's, z positive) turned 180 degrees for the other. The lane and the bridge
    /// stand on the line between them and are built once.
    /// </summary>
    public static class ArenaRuinsBuilder
    {
        public const string ScenePath = "Assets/Scenes/Arena_Ruins.unity";
        const string TemplateScene = "Assets/Scenes/Arena_Blockout.unity";

        const float HalfX = 25f, HalfZ = 23f;          // 50 x 46
        const float WallHeight = 5f, WallThick = 1f;
        const float Ledge = 2.4f, Hop = 1.2f, Bed = 1.2f;
        const float LaneHalf = 2.5f, LaneEnd = 11f;    // the lane is x -2.5..2.5, z -11..11
        const float StepRise = 0.3f, StepRun = 0.5f;
        const int LaneSteps = 3;                       // three steps and the ground make the 1.2 m
        const float StairLength = 4.4f, StairWidth = 1.8f;      // 35 degrees over the steps' noses

        static Material groundMat, bedMat, wallMat, trimMat;

        [MenuItem("Tools/Arena/Build Ruins Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Ruins] stop play mode first"); return; }
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                Debug.LogWarning("[Ruins] the open scene has unsaved changes -- save or discard them first");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                && !AssetDatabase.CopyAsset(TemplateScene, ScenePath))
            {
                Debug.LogError("[Ruins] could not copy the arena scene");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string old in new[] { "Arena", "Sun" })
            {
                GameObject g = GameObject.Find(old);
                if (g != null) Object.DestroyImmediate(g);
            }

            Kit.Import();
            Materials();
            GameObject root = new GameObject("Arena");
            BuildMap(root.transform);
            Dress(root.transform);
            Atmosphere();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            // the light is worked out from the saved scene, then saved with it
            ArenaKit.BakeEnvironment("Assets/Scenes/Arena_Ruins_Lighting.lighting");
            EditorSceneManager.SaveScene(scene);
            ArenaTempleBuilder.EnsureInBuild(ScenePath);
            Debug.Log("[Ruins] built: " + (root.GetComponentsInChildren<Transform>().Length - 1) + " objects\n" + Audit());
        }

        [MenuItem("Tools/Arena/Audit Ruins")]
        public static void AuditMenu() { Debug.Log(Audit()); }

        public static string Audit() { return ArenaTempleBuilder.AuditArea("[Ruins]", HalfX, HalfZ); }

        // ------------------------------------------------------------------ the map

        static void BuildMap(Transform root)
        {
            Transform shell = ArenaTempleBuilder.Group("Shell", root);
            Transform ground = ArenaTempleBuilder.Group("Ground", root);
            Transform centre = ArenaTempleBuilder.Group("Centre", root);
            Transform spawns = ArenaTempleBuilder.Group("Spawns", root);
            Transform targets = ArenaTempleBuilder.Group("Targets", root);

            float wx = HalfX + WallThick * 0.5f, wz = HalfZ + WallThick * 0.5f;
            Box("Wall_N", shell, wallMat, new Vector3(0f, WallHeight * 0.5f, wz), new Vector3(HalfX * 2f + 2f, WallHeight, WallThick));
            Box("Wall_S", shell, wallMat, new Vector3(0f, WallHeight * 0.5f, -wz), new Vector3(HalfX * 2f + 2f, WallHeight, WallThick));
            // east and west fit between north and south: overlapping at the corners, their faces would flicker
            Box("Wall_E", shell, wallMat, new Vector3(wx, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, HalfZ * 2f));
            Box("Wall_W", shell, wallMat, new Vector3(-wx, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, HalfZ * 2f));

            // --- the ground: the square either side of the lane, the lane 1.2 m down, steps out of its ends
            const float thick = 1.5f;
            float stepsEnd = LaneEnd + LaneSteps * StepRun;
            Floor(ground, groundMat, -HalfX, -LaneHalf, -HalfZ, HalfZ, 0f, thick);
            Floor(ground, groundMat, LaneHalf, HalfX, -HalfZ, HalfZ, 0f, thick);
            Floor(ground, groundMat, -LaneHalf, LaneHalf, stepsEnd, HalfZ, 0f, thick);
            Floor(ground, groundMat, -LaneHalf, LaneHalf, -HalfZ, -stepsEnd, 0f, thick);
            Floor(ground, bedMat, -LaneHalf, LaneHalf, -stepsEnd, stepsEnd, -Bed, 0.5f);
            foreach (float s in new[] { 1f, -1f })
                for (int i = 0; i < LaneSteps; i++)
                {
                    float z0 = LaneEnd + i * StepRun, z1 = z0 + StepRun;
                    Block("Lane_Step", ground, groundMat, -LaneHalf, LaneHalf, s > 0f ? z0 : -z1, s > 0f ? z1 : -z0, -Bed, -Bed + (i + 1) * StepRise);
                }

            // the lane's lining: brick, a hand higher than the square so its cap shows as a kerb
            const int lining = 7;
            float piece = LaneEnd * 2f / lining;
            foreach (float side in new[] { 1f, -1f })
                for (int i = 0; i < lining; i++)
                    Kit.WallBrick(centre, new Vector3(side * (LaneHalf + 0.12f), -Bed, -LaneEnd + piece * (i + 0.5f)), 90f, Bed + 0.05f, piece);

            // --- the bridge across the lane, a stair up to each end
            Kit.Bridge(centre, Vector3.zero, 0f);
            float stairAt = Kit.BridgeSillEnd + StairLength * 0.5f;
            Kit.StairsStone(centre, new Vector3(stairAt, 0f, 0f), 90f, StairWidth, StairLength, Ledge);
            Kit.StairsStone(centre, new Vector3(-stairAt, 0f, 0f), -90f, StairWidth, StairLength, Ledge);

            for (int h = 0; h < 2; h++)
            {
                Transform half = ArenaTempleBuilder.Group("Half_" + h, root);
                half.localRotation = Quaternion.Euler(0f, 180f * h, 0f);
                BuildHalf(half);
            }

            // Spawns: team B on +z, team A the same points turned 180 degrees. Index 1 is the 1v1 pair,
            // behind the gate ruin at the head of the lane.
            Vector3[] b = { new Vector3(9f, 0f, 19.5f), new Vector3(0f, 0f, 20f), new Vector3(-12f, 0f, 19.5f) };
            for (int i = 0; i < 3; i++)
            {
                ArenaTempleBuilder.Marker("SpawnB_" + i, spawns, b[i], 180f);
                ArenaTempleBuilder.Marker("SpawnA_" + i, spawns, new Vector3(-b[i].x, 0f, -b[i].z), 0f);
            }

            ArenaTempleBuilder.Dummy("Dummy_Near", targets, new Vector3(4.5f, 1f, -14f));
            ArenaTempleBuilder.Dummy("Dummy_Mid", targets, new Vector3(9.5f, 1f, -5.5f));
            ArenaTempleBuilder.Dummy("Dummy_Far", targets, new Vector3(-9f, 1f, 6.5f));
        }

        /// <summary>Team B's half, z positive. Turned 180 degrees it is team A's.</summary>
        static void BuildHalf(Transform t)
        {
            // --- west: the mossy terrace, its stair coming up from the base side
            Kit.PlatformMoss(t, new Vector3(-15f, 0f, 7f), 0f);
            Kit.StairsMoss(t, new Vector3(-15f, 0f, 10f + 2.5f), 90f, 2.4f, 5f, Ledge);

            // --- east: the sandstone terrace, a brick dais beside it as the step up
            Kit.PlatformSand(t, new Vector3(16f, 0f, 4.5f), 0f);
            Kit.PlatformBrick(t, new Vector3(16f, 0f, 9f), 0f, 3f, Hop);

            // --- the house in the back corner, its door to the square; an alley behind it and one beside it
            Kit.House(t, new Vector3(18.2f, 0f, 16.2f), 180f, true);

            // --- columns along the lane
            Kit.Column(t, new Vector3(5.6f, 0f, 5.2f), 15f, true);
            Kit.Column(t, new Vector3(5.6f, 0f, 9.6f), 40f, false);
            Kit.Column(t, new Vector3(-5.6f, 0f, 5.2f), 0f, false);
            Kit.Column(t, new Vector3(-5.6f, 0f, 9.6f), 25f, true);

            // --- the gate ruin at the head of the lane: the middle spawn stands behind it
            for (int i = -1; i <= 1; i++)
                Kit.WallTall(t, new Vector3(i * 1.47f, 0f, 15.6f), 90f);

            // --- low walls: chest-high cover where the square is open
            Kit.WallLow(t, new Vector3(-22f, 0f, 3f), 90f);
            Kit.WallLow(t, new Vector3(9.8f, 0f, 11f), 30f);
            Kit.WallLow(t, new Vector3(-8.5f, 0f, 16.5f), -20f);
            Kit.WallLow(t, new Vector3(22.4f, 0f, 6.5f), 90f);
            Kit.WallLow(t, new Vector3(-10.4f, 0f, 2.8f), 0f);

            // --- brick walls to hop over
            Kit.WallBrick(t, new Vector3(6.2f, 0f, 17.2f), 90f);
            Kit.WallBrick(t, new Vector3(-21.6f, 0f, 14.6f), 0f);
            Kit.WallBrick(t, new Vector3(10.8f, 0f, 2.6f), 0f);
            // the corner behind the mossy stair was 8.0 m from anything (audit, first build)
            Kit.WallBrick(t, new Vector3(-17.4f, 0f, 20.3f), 15f);

            // --- two tall pieces by the west wall
            Kit.WallTall(t, new Vector3(-21.8f, 0f, 9.2f), 0f);
            Kit.WallTall(t, new Vector3(-21.8f, 0f, 10.7f), 0f);
        }

        // ------------------------------------------------------------------ look

        static Material leaves, pine, bark, grass, far;

        static void Materials()
        {
            ArenaKit.Begin(Kit.MatFolder, "Ruins");
            ArenaTempleBuilder.EnsureFolder("Assets/Textures");
            ArenaTempleBuilder.EnsureFolder("Assets/Textures/Temple");
            Texture2D blocks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);
            Texture2D paving = Kit.Texture("Ruins_Paving.png", false, 1024, true);

            groundMat = Paving("Ruins_Ground", paving, Color.white, new Color(0.74f, 0.72f, 0.70f));
            bedMat = Paving("Ruins_Bed", paving, new Color(0.80f, 0.78f, 0.74f), new Color(0.66f, 0.64f, 0.62f));
            // the town wall: warm grey, between the models' sandstone and their slate
            Color pale = new Color(0.86f, 0.83f, 0.76f);
            wallMat = ArenaKit.Stone("Ruins_Wall", blocks, 4f, new Color(0.60f, 0.56f, 0.50f), new Color(0.54f, 0.54f, 0.51f), new Color(0.58f, 0.58f, 0.50f),
                                     pale, 0.12f, 0.35f);
            trimMat = ArenaKit.Stone("Ruins_Trim", blocks, 2f, new Color(0.70f, 0.67f, 0.62f), new Color(0.66f, 0.66f, 0.62f), new Color(0.68f, 0.68f, 0.60f),
                                     new Color(0.92f, 0.89f, 0.82f), 0.08f, 0.15f);
            leaves = ArenaKit.Foliage("Ruins_Leaves", new Color(0.10f, 0.17f, 0.10f), new Color(0.40f, 0.50f, 0.24f), new Color(0.54f, 0.52f, 0.26f));
            pine = ArenaKit.Foliage("Ruins_Pine", new Color(0.06f, 0.13f, 0.11f), new Color(0.22f, 0.36f, 0.25f), new Color(0.26f, 0.38f, 0.27f));
            bark = ArenaKit.Foliage("Ruins_Bark", new Color(0.20f, 0.16f, 0.13f), new Color(0.42f, 0.35f, 0.28f), new Color(0.33f, 0.31f, 0.26f));
            grass = ArenaKit.Standard("Ruins_Outskirts", new Color(0.33f, 0.40f, 0.24f), 0f, 0.04f, Color.black);
            far = ArenaKit.Distant("Ruins_Far", new Color(0.24f, 0.34f, 0.24f), new Color(0.34f, 0.42f, 0.40f), new Color(0.62f, 0.70f, 0.80f), 0.45f, 50f);
        }

        static Material Paving(string name, Texture2D tex, Color top, Color side)
        {
            string path = Kit.MatFolder + "/" + name + ".mat";
            Shader s = Shader.Find("MageCast/WorldPaving");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", top);
            m.SetColor("_SideColor", side);
            m.SetFloat("_Scale", 3f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// What is not the game: the town wall's cornice and battlements, the town behind it (the same house,
        /// lighter, turned and sized a little differently each time), trees between the roofs, hills.
        /// Nothing here has a collider.
        /// </summary>
        static void Dress(Transform root)
        {
            Transform look = ArenaKit.Group("Look", root);

            Transform walls = ArenaKit.Group("Walls", look);
            for (int side = 0; side < 4; side++)
            {
                bool odd = side % 2 == 1;
                float along = odd ? HalfZ : HalfX, off = odd ? HalfX : HalfZ;
                // each wall in its own frame: x along it, z out of the arena
                Transform w = ArenaKit.Group("Wall_" + side, walls);
                w.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                float wo = off + WallThick * 0.5f;
                // every other side's cornice a touch deeper, so where two cross at a corner their tops are not one plane
                ArenaKit.Deco("Cornice", w, trimMat, new Vector3(0f, WallHeight + 0.2f, wo),
                              new Vector3((along + WallThick) * 2f + 0.6f, 0.4f + (odd ? 2f * ArenaKit.Proud : 0f), WallThick + 0.7f));
                int n = Mathf.RoundToInt((along * 2f - 2.6f) / 2.6f);
                for (int k = 0; k <= n; k++)
                {
                    float x = -along + 1.3f + k * (along * 2f - 2.6f) / n;
                    ArenaKit.Deco("Merlon", w, wallMat, new Vector3(x, WallHeight + 0.85f, wo + 0.1f), new Vector3(1.2f, 0.9f, WallThick + 0.1f));
                }
            }

            // the town: a row of houses behind each long wall, two behind each short one, the far side the
            // near side turned round
            Transform town = ArenaKit.Group("Town", look);
            var rnd = new System.Random(19);
            var roofs = new List<Vector2>();
            float rowZ = HalfZ + WallThick + 6.6f, rowX = HalfX + WallThick + 6.6f;
            var places = new List<Vector3>();      // x, z, yaw
            foreach (float x in new[] { -20.5f, -7f, 6.5f, 20f }) places.Add(new Vector3(x, rowZ, 180f));
            foreach (float z in new[] { -10.5f, 4.5f }) places.Add(new Vector3(rowX, z, 270f));
            for (int turn = 0; turn < 2; turn++)
                foreach (Vector3 p in places)
                {
                    float sgn = turn == 0 ? 1f : -1f;
                    float jx = ((float)rnd.NextDouble() - 0.5f) * 1.6f, jz = (float)rnd.NextDouble() * 2.4f;
                    bool row = Mathf.Approximately(p.z, 180f);
                    Vector3 at = new Vector3(sgn * (p.x + (row ? jx : jz)), 0f, sgn * (p.y + (row ? jz : jx)));
                    float yaw = p.z + 180f * turn + ((float)rnd.NextDouble() - 0.5f) * 24f;
                    Kit.House(town, at, yaw, false, Kit.HouseScale * (0.92f + (float)rnd.NextDouble() * 0.2f));
                    roofs.Add(new Vector2(at.x, at.z));
                }

            Transform trees = ArenaKit.Group("Trees", look);
            for (int i = 0; i < 90; i++)
            {
                // a band 4 to 22 m outside the wall, all the way round
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, d = 4f + (float)rnd.NextDouble() * 18f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float scaleOut = Mathf.Min((HalfX + WallThick + d) / Mathf.Abs(dir.x + 1e-4f), (HalfZ + WallThick + d) / Mathf.Abs(dir.y + 1e-4f));
                Vector2 p = dir * scaleOut;
                bool clear = true;
                foreach (Vector2 r in roofs) if ((r - p).sqrMagnitude < 6.8f * 6.8f) { clear = false; break; }
                if (!clear) continue;
                bool isPine = rnd.NextDouble() < 0.35;
                float size = (isPine ? 1.2f : 1.3f) + (float)rnd.NextDouble() * 0.6f;
                ArenaTrees.Plant(trees, isPine ? ArenaTrees.Kind.Pine : ArenaTrees.Kind.Broad, rnd.Next(4), new Vector3(p.x, -0.3f, p.y),
                                 (float)rnd.NextDouble() * 360f, size, bark, isPine ? pine : leaves);
            }

            // grass outside the wall, in four pieces round the arena: one sheet under everything would lie over the lane
            Transform beyond = ArenaKit.Group("Beyond", look);
            float o = 110f, ex = HalfX + WallThick, ez = HalfZ + WallThick;
            ArenaKit.Deco("Outskirts", beyond, grass, new Vector3(0f, -0.3f, (ez + o) * 0.5f), new Vector3(o * 2f, 0.5f, o - ez));
            ArenaKit.Deco("Outskirts", beyond, grass, new Vector3(0f, -0.3f, -(ez + o) * 0.5f), new Vector3(o * 2f, 0.5f, o - ez));
            ArenaKit.Deco("Outskirts", beyond, grass, new Vector3((ex + o) * 0.5f, -0.3f, 0f), new Vector3(o - ex, 0.5f, ez * 2f));
            ArenaKit.Deco("Outskirts", beyond, grass, new Vector3(-(ex + o) * 0.5f, -0.3f, 0f), new Vector3(o - ex, 0.5f, ez * 2f));
            ArenaKit.Ridge("Ridge", beyond, 170f, 36f, 24f, 46f, 6, 13, far, false);

            ArenaKit.Motes(look, new Vector3(0f, 3f, 0f), new Vector3(48f, 5f, 44f), 8f, 80, 0.1f, new Color(1f, 0.90f, 0.70f), new Color(0.95f, 0.93f, 0.86f));
        }

        /// <summary>
        /// An early afternoon: the sun from the side, across the lane -- both teams look along z, so both get
        /// the same light -- under a blue sky with fair-weather cloud. The models carry their own colour, so
        /// the light is kept near white.
        /// </summary>
        static void Atmosphere()
        {
            Light sun = ArenaKit.Sun(new Color(1f, 0.95f, 0.84f), 1.15f, 0.72f, new Vector3(46f, 90f, 0f));
            ArenaKit.Ambient(new Color(0.58f, 0.64f, 0.74f), new Color(0.62f, 0.60f, 0.56f), new Color(0.34f, 0.33f, 0.30f), 0.35f);
            ArenaKit.Sky("Ruins_Sky", sun, new ArenaKit.SkyColours
            {
                zenith = new Color(0.20f, 0.40f, 0.72f), middle = new Color(0.50f, 0.68f, 0.88f),
                horizon = new Color(0.84f, 0.88f, 0.90f), below = new Color(0.42f, 0.42f, 0.44f),
                sun = new Color(1.0f, 0.94f, 0.80f), cloud = new Color(1.0f, 0.98f, 0.95f),
                sunSize = 0.0016f, stars = 0f, clouds = 0.8f,
                cloudCover = 0.42f, cloudHeight = 0.7f, cloudScale = 0.6f, cloudStretch = 2.2f,
            });
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Box(string name, Transform parent, Material m, Vector3 pos, Vector3 size, float yaw = 0f)
        {
            return ArenaTempleBuilder.Box(name, parent, m, pos, size, yaw);
        }

        /// <summary>A box by its extents: x0..x1, z0..z1, from height y0 up to y1.</summary>
        static GameObject Block(string name, Transform parent, Material m, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            return Box(name, parent, m, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                       new Vector3(x1 - x0, y1 - y0, z1 - z0));
        }

        /// <summary>Ground with its top at <paramref name="top"/>, <paramref name="thick"/> deep.</summary>
        static void Floor(Transform parent, Material m, float x0, float x1, float z0, float z1, float top, float thick)
        {
            Block("Floor", parent, m, x0, x1, z0, z1, top - thick, top);
        }
    }
}
