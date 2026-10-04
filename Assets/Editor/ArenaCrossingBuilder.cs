using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The third map, "Crossing": a dry riverbed across the middle of a ruined courtyard, and three ways
    /// over it. Built entirely from this file -- Tools > Arena > Build Crossing Map, then Audit Crossing.
    ///
    /// The look is held back on purpose. Every spell is a saturated colour -- orange fire, cyan ice,
    /// yellow lightning, pale air, gold barrier -- and they have to be the brightest, most colourful
    /// thing on screen. So the stone is grey-blue and the moss grey-green, nothing glows cold (a cyan
    /// line on the floor reads as ice), and the only lights are a few dim warm lanterns.
    ///
    /// Same scale contract and height set as the Temple:
    ///   1.2 a jump, 1.6 chest cover you can climb, 2.4 a ledge you climb, the riverbed 1.2 down.
    ///
    /// Layout, one half (x positive) turned 180 degrees for the other -- team B on +z, team A on -z:
    ///   riverbed  1.2 m deep along x, rocks to hide behind, steps out
    ///   ford      the middle 6 m is solid ground at floor level, with a statue on it
    ///   bridges   at x = +-15, 2.4 m up, high enough to walk under along the riverbed;
    ///             one end lands on a terrace with battlements, the other comes down a stair
    ///   banks     a colonnade on one side, a roofless chapel on the other, gate ruins at the back
    /// </summary>
    public static class ArenaCrossingBuilder
    {
        public const string ScenePath = "Assets/Scenes/Arena_Crossing.unity";
        const string TemplateScene = "Assets/Scenes/Arena_Blockout.unity";
        const string MatFolder = "Assets/Materials/Crossing";

        const float HalfX = 24f, HalfZ = 20f;         // 48 x 40
        const float WallHeight = 6f, WallThick = 1f;
        const float Ledge = 2.4f, ChestHigh = 1.6f, EyeBlock = 2.8f, Bed = 1.2f;
        const float StepRise = 0.3f, StepRun = 0.5f;
        const float ChannelHalf = 3f;                  // the riverbed is z -3..3
        const float FordHalf = 3f;                     // solid ground over it at x -3..3
        const float BridgeX = 15f, BridgeHalfWidth = 1.5f;

        static Material floorMat, bedMat, wallMat, stoneMat, columnMat, coverMat, darkMat, lampMat;

        [MenuItem("Tools/Arena/Build Crossing Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Crossing] stop play mode first"); return; }
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath)
            {
                Debug.LogWarning("[Crossing] the open scene has unsaved changes -- save or discard them first");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
                && !AssetDatabase.CopyAsset(TemplateScene, ScenePath))
            {
                Debug.LogError("[Crossing] could not copy the arena scene");
                return;
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string old in new[] { "Arena", "Sun" })
            {
                GameObject g = GameObject.Find(old);
                if (g != null) Object.DestroyImmediate(g);
            }

            Materials();
            GameObject root = new GameObject("Arena");
            BuildMap(root.transform);
            Atmosphere();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ArenaTempleBuilder.EnsureInBuild(ScenePath);
            Debug.Log("[Crossing] built: " + (root.GetComponentsInChildren<Transform>().Length - 1) + " objects\n" + Audit());
        }

        [MenuItem("Tools/Arena/Audit Crossing")]
        public static void AuditMenu() { Debug.Log(Audit()); }

        public static string Audit() { return ArenaTempleBuilder.AuditArea("[Crossing]", HalfX, HalfZ); }

        // ------------------------------------------------------------------ the map

        static void BuildMap(Transform root)
        {
            Transform shell = ArenaTempleBuilder.Group("Shell", root);
            Transform spawns = ArenaTempleBuilder.Group("Spawns", root);
            Transform targets = ArenaTempleBuilder.Group("Targets", root);

            float wx = HalfX + WallThick * 0.5f, wz = HalfZ + WallThick * 0.5f;
            Box("Wall_N", shell, wallMat, new Vector3(0f, WallHeight * 0.5f, wz), new Vector3(HalfX * 2f + 2f, WallHeight, WallThick));
            Box("Wall_S", shell, wallMat, new Vector3(0f, WallHeight * 0.5f, -wz), new Vector3(HalfX * 2f + 2f, WallHeight, WallThick));
            Box("Wall_E", shell, wallMat, new Vector3(wx, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, HalfZ * 2f + 2f));
            Box("Wall_W", shell, wallMat, new Vector3(-wx, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, HalfZ * 2f + 2f));

            for (int h = 0; h < 2; h++)
            {
                Transform half = ArenaTempleBuilder.Group("Half_" + h, root);
                half.localRotation = Quaternion.Euler(0f, 180f * h, 0f);
                BuildHalf(half);
            }

            // The statue on the ford: in one piece, not per half -- it stands on the line between them.
            Box("Statue_Base", root, stoneMat, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 1.2f, 1.8f));
            Cylinder("Statue", root, darkMat, new Vector3(0f, 1.2f + 1.1f, 0f), 0.8f, 2.2f);

            // Spawns: team B on +z, team A the same points turned 180 degrees. Index 1 is the 1v1 pair,
            // behind the gate at the back of each side, looking down the ford.
            Vector3[] b = { new Vector3(12f, 0f, 17f), new Vector3(0f, 0f, 17.5f), new Vector3(-12f, 0f, 17f) };
            for (int i = 0; i < 3; i++)
            {
                ArenaTempleBuilder.Marker("SpawnB_" + i, spawns, b[i], 180f);
                ArenaTempleBuilder.Marker("SpawnA_" + i, spawns, new Vector3(-b[i].x, 0f, -b[i].z), 0f);
            }

            ArenaTempleBuilder.Dummy("Dummy_Near", targets, new Vector3(2f, 1f, -9f));
            ArenaTempleBuilder.Dummy("Dummy_Mid", targets, new Vector3(8f, 1f, -5f));
            ArenaTempleBuilder.Dummy("Dummy_Far", targets, new Vector3(-6f, 1f, 7f));
        }

        /// <summary>The half with x positive, both banks. Turned 180 degrees it is the other half.</summary>
        static void BuildHalf(Transform t)
        {
            // --- ground: two banks, the ford, and the riverbed between them
            const float thick = 1.5f;
            Floor(t, floorMat, 0f, HalfX, ChannelHalf, HalfZ, 0f, thick);
            Floor(t, floorMat, 0f, HalfX, -HalfZ, -ChannelHalf, 0f, thick);
            Floor(t, floorMat, 0f, FordHalf, -ChannelHalf, ChannelHalf, 0f, thick);
            Floor(t, bedMat, FordHalf, HalfX, -ChannelHalf, ChannelHalf, -Bed, 0.5f);

            // rocks in the riverbed: somewhere to hide down there
            Box("Bed_Rock", t, coverMat, new Vector3(8f, -Bed + 0.45f, 1.1f), new Vector3(1.6f, 0.9f, 1.4f), 20f);
            Box("Bed_Rock", t, coverMat, new Vector3(20f, -Bed + 0.45f, -1.0f), new Vector3(1.8f, 0.9f, 1.3f), -15f);
            Box("Bed_Rock", t, coverMat, new Vector3(11.5f, -Bed + 0.7f, -1.6f), new Vector3(1.2f, 1.4f, 1.2f), 35f);
            // and steps out of it, one per bank
            Box("Bed_Step", t, stoneMat, new Vector3(6f, -0.3f, ChannelHalf - 0.6f), new Vector3(1.6f, 0.6f, 1.2f));
            Box("Bed_Step", t, stoneMat, new Vector3(18f, -0.3f, -ChannelHalf + 0.6f), new Vector3(1.6f, 0.6f, 1.2f));

            // --- the bridge: deck 2.4 m up over the riverbed, a pier in the middle of it
            float bx0 = BridgeX - BridgeHalfWidth, bx1 = BridgeX + BridgeHalfWidth;
            Block("Bridge_Deck", t, stoneMat, bx0, bx1, -ChannelHalf - 0.5f, ChannelHalf, Ledge - 0.4f, Ledge);
            Block("Bridge_Pier", t, stoneMat, BridgeX - 0.5f, BridgeX + 0.5f, -0.5f, 0.5f, -Bed, Ledge - 0.4f);
            // north end: a causeway onto the terrace
            Block("Causeway", t, stoneMat, bx0, bx1, ChannelHalf, 7f, 0f, Ledge);
            Terrace(t);
            // south end: down a stair along the bridge
            int steps = Mathf.RoundToInt(Ledge / StepRise);
            for (int i = 0; i < steps; i++)
            {
                float h = Ledge - i * StepRise;
                float z1 = -ChannelHalf - 0.5f - i * StepRun;
                Block("Bridge_Stair", t, stoneMat, bx0, bx1, z1 - StepRun, z1, 0f, h);
            }

            // --- north bank (team B's right, team A's left): the colonnade, roofed with a lintel
            for (int i = 0; i < 3; i++)
                Cylinder("Column", t, columnMat, new Vector3(2.2f + i * 2.4f, 2f, 6.2f), 0.9f, 4f);
            Block("Colonnade_Lintel", t, columnMat, 1.5f, 7.5f, 5.8f, 6.6f, 4f, 4.5f);
            Box("Ford_Wall", t, coverMat, new Vector3(4.6f, ChestHigh * 0.5f, 4.2f), new Vector3(3f, ChestHigh, 0.8f));

            // --- south bank: a roofless chapel, open towards the middle, a window in its far wall
            float cx0 = 16f, cx1 = 22f, cz0 = -16f, cz1 = -10.5f, w = 0.7f;
            Block("Chapel_S", t, wallMat, cx0, cx1, cz0, cz0 + w, 0f, 3.6f);
            Block("Chapel_E", t, wallMat, cx1 - w, cx1, cz0, cz1, 0f, 3.6f);
            Block("Chapel_N", t, wallMat, cx0 + 2.6f, cx1, cz1 - w, cz1, 0f, 3.6f);
            Block("Chapel_N_Low", t, wallMat, cx0, cx0 + 1.2f, cz1 - w, cz1, 0f, ChestHigh);
            Block("Chapel_Window_Sill", t, wallMat, cx1 - w, cx1, cz0 + 2f, cz0 + 3.5f, 0f, 1.1f);
            Box("Chapel_Altar", t, darkMat, new Vector3(20f, 0.5f, -14.2f), new Vector3(1.6f, 1f, 0.8f));
            Box("Fallen_Column", t, columnMat, new Vector3(9f, 0.45f, -9f), new Vector3(4.5f, 0.9f, 0.9f), 25f);
            Box("Ford_Wall", t, coverMat, new Vector3(4.6f, ChestHigh * 0.5f, -4.2f), new Vector3(3f, ChestHigh, 0.8f));
            Box("Bank_Block", t, coverMat, new Vector3(9.5f, EyeBlock * 0.5f, -15f), new Vector3(1.2f, EyeBlock, 3.5f), 10f);

            // --- gate ruins at the back of both banks: the spawns hide behind them
            foreach (float s in new[] { 1f, -1f })
            {
                float gz = 14.5f * s;
                Block("Gate_Wall", t, wallMat, 1.3f, 5.5f, gz - 0.4f, gz + 0.4f, 0f, EyeBlock);
                Block("Gate_Lintel", t, wallMat, 0f, 1.3f, gz - 0.4f, gz + 0.4f, EyeBlock, EyeBlock + 0.6f);
                Box("Gate_Post", t, darkMat, new Vector3(16.5f, ChestHigh * 0.5f, 17.5f * s), new Vector3(0.9f, ChestHigh, 0.9f));
            }
            Box("Back_Cover", t, coverMat, new Vector3(20f, ChestHigh * 0.5f, 16f), new Vector3(0.9f, ChestHigh, 3f));

            Lantern(t, new Vector3(FordHalf + 0.4f, 0f, ChannelHalf + 0.5f));
            Lantern(t, new Vector3(6.5f, 0f, 8.7f));
            Lantern(t, new Vector3(cx0 - 0.6f, 0f, cz1 + 0.6f));
        }

        /// <summary>The terrace at the north end of the bridge, with a stair down its inner side.</summary>
        static void Terrace(Transform t)
        {
            float x0 = 11f, x1 = 19f, z0 = 7f, z1 = 13f;
            Block("Terrace", t, stoneMat, x0, x1, z0, z1, 0f, Ledge);
            int steps = Mathf.RoundToInt(Ledge / StepRise);
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * StepRise;
                float xa = x0 - (steps - i) * StepRun;
                Block("Terrace_Stair", t, stoneMat, xa, xa + StepRun, 9f, 11f, 0f, h);
            }
            // battlements towards the river, either side of the bridge, and on the inner corner
            float mh = 1.2f;
            Block("Merlon", t, wallMat, x0, x0 + 1.2f, z0, z0 + 0.6f, Ledge, Ledge + mh);
            Block("Merlon", t, wallMat, x1 - 1.2f, x1, z0, z0 + 0.6f, Ledge, Ledge + mh);
            Block("Merlon", t, wallMat, x0, x0 + 0.6f, 11.6f, z1, Ledge, Ledge + mh);
            Block("Parapet", t, wallMat, x0, x1, z1 - 0.5f, z1, Ledge, Ledge + 0.6f);
        }

        static void Lantern(Transform t, Vector3 at)
        {
            Box("Lantern_Post", t, darkMat, at + new Vector3(0f, 0.7f, 0f), new Vector3(0.3f, 1.4f, 0.3f));
            ArenaTempleBuilder.Deco("Lantern_Light", t, lampMat, at + new Vector3(0f, 1.55f, 0f), new Vector3(0.28f, 0.3f, 0.28f));
        }

        // ------------------------------------------------------------------ look

        static void Materials()
        {
            ArenaTempleBuilder.EnsureFolder(MatFolder);
            ArenaTempleBuilder.EnsureFolder("Assets/Textures");
            ArenaTempleBuilder.EnsureFolder("Assets/Textures/Temple");
            // the Temple's patterns: the same hand, so the maps look like one game
            Texture2D tiles = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Tiles.png", 4, 4, false, 11);
            Texture2D bricks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Bricks.png", 4, 8, true, 23);
            Texture2D blocks = ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);

            // grey-blue stone and grey-green moss: no colour in the scenery that a spell also uses
            floorMat  = Stone("Crossing_Floor",  tiles,  4f, new Color(0.49f, 0.48f, 0.45f), new Color(0.53f, 0.51f, 0.47f), 0.3f);
            bedMat    = Stone("Crossing_Bed",    tiles,  1.5f, new Color(0.40f, 0.38f, 0.34f), new Color(0.43f, 0.40f, 0.35f), 0.35f);
            wallMat   = Stone("Crossing_Wall",   bricks, 4f, new Color(0.40f, 0.41f, 0.44f), new Color(0.40f, 0.45f, 0.38f), 0.3f);
            stoneMat  = Stone("Crossing_Stone",  blocks, 3f, new Color(0.52f, 0.51f, 0.48f), new Color(0.47f, 0.50f, 0.42f), 0.3f);
            columnMat = Stone("Crossing_Column", blocks, 2f, new Color(0.62f, 0.61f, 0.58f), new Color(0.58f, 0.60f, 0.54f), 0.2f);
            coverMat  = Stone("Crossing_Cover",  blocks, 2f, new Color(0.48f, 0.46f, 0.43f), new Color(0.44f, 0.47f, 0.40f), 0.3f);
            darkMat   = Stone("Crossing_Dark",   blocks, 2f, new Color(0.24f, 0.25f, 0.27f), new Color(0.27f, 0.28f, 0.29f), 0.1f);
            // a dim warm lamp, nowhere near as bright as a fire spell
            lampMat = ArenaTempleBuilder.Glow("Crossing_Lamp", new Color(1.1f, 0.8f, 0.48f), MatFolder);
        }

        static Material Stone(string name, Texture2D pattern, float scale, Color side, Color top, float grime)
        {
            return ArenaTempleBuilder.Stone(name, pattern, scale, side, top, grime, MatFolder);
        }

        static void Atmosphere()
        {
            // A high, soft, slightly cool afternoon. No fog: the effect packs' particle shaders turn it
            // into a square around every particle (see the Temple builder).
            GameObject sun = new GameObject("Sun");
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.95f, 0.88f);
            l.intensity = 1.0f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.65f;
            // from the side, across the river: both teams look along z, so both get the same light --
            // from behind one of them, one side would see every wall sunlit and the other every wall in shade
            sun.transform.rotation = Quaternion.Euler(50f, 90f, 0f);
            RenderSettings.sun = l;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.54f, 0.60f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.44f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.26f);
            RenderSettings.fog = false;

            string skyPath = MatFolder + "/Crossing_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_AtmosphereThickness", 1.5f);
            sky.SetColor("_SkyTint", new Color(0.42f, 0.43f, 0.45f));
            sky.SetColor("_GroundColor", new Color(0.33f, 0.33f, 0.34f));
            sky.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
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

        static void Cylinder(string name, Transform parent, Material m, Vector3 centre, float diameter, float height)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = centre;
            g.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);   // a cylinder is 2 high
            g.GetComponent<Renderer>().sharedMaterial = m;
            g.isStatic = true;
        }
    }
}
