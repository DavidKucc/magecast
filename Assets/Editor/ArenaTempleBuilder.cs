using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The second map, "Temple": a ruined mage temple on three levels. Built entirely from this file --
    /// change a number, run Tools > Arena > Build Temple Map, then Tools > Arena > Audit Temple.
    ///
    /// Scale contract, the same as the blockout (1 unit = 1 m):
    ///   player 2 | eye 1.6 | jump 1.2 | climb (mantle) 2.4 | storey 4
    /// and the heights in this map are only ever one of those, so the player learns them once:
    ///   1.2 a jump, 1.6 chest cover you can climb, 2.4 a ledge you climb, 4 a floor you take stairs to.
    ///
    /// Layout, one quarter stamped four times by rotation (fair for two teams on opposite sides):
    ///   centre    the altar, 2.4 m, an obelisk on top; stairs wind up its four sides
    ///   corners   a two-storey tower each: a room with two doors, a ramp inside, a roof with battlements
    ///   bridges   from every tower roof down to an altar corner -- high routes with nothing to hide behind,
    ///             and nothing to stop an air spell throwing you off
    ///   lanes     each side lane is half sunken pit (1.2 m deep: spells fly over you, patches pool in it)
    ///             and half ruin (a 2.4 m wall to climb, a fallen column, chest cover)
    /// </summary>
    public static class ArenaTempleBuilder
    {
        public const string ScenePath = "Assets/Scenes/Arena_Temple.unity";
        const string TemplateScene = "Assets/Scenes/Arena_Blockout.unity";
        const string RootName = "Arena";

        // --- shell ---
        const float Half = 22f;              // 44 x 44
        const float WallHeight = 7f;
        const float WallThick = 1f;

        // --- the heights this map is made of ---
        const float Ledge = 2.4f;            // climb onto it
        const float ChestHigh = 1.6f;
        const float Storey = 4f;
        const float PitDepth = 1.2f;         // jump out of it
        const float StepRise = 0.3f;         // under the controller's 0.4 step offset
        const float StepRun = 0.5f;

        // --- the altar ---
        const float AltarHalf = 4f;

        // --- one quarter, in its own coordinates (x and z both positive) ---
        static readonly Rect Pit = Rect.MinMaxRect(3f, 13.5f, 9f, 19.5f);
        static readonly Rect Tower = Rect.MinMaxRect(11.5f, 11.5f, 18.5f, 18.5f);
        const float TowerWall = 0.6f;
        const float DoorWidth = 2.5f, DoorHeight = 3f;

        // --- audit ---
        const float MaxCoverDist = 8f;
        const float MantleReach = 2.65f;     // PlayerMotor: highest ledge a jump-and-pull gets you onto
        const float JumpReach = 1.3f;

        static Material floorMat, pitMat, wallMat, stoneMat, darkMat, bridgeMat, coverMat, glowMat, fireMat;

        [MenuItem("Tools/Arena/Build Temple Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Temple] stop play mode first"); return; }
            if (EditorSceneManager.GetActiveScene().isDirty && EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogWarning("[Temple] the open scene has unsaved changes -- save or discard them first");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                // The arena scene already has everything a match needs besides the map: the camera rig,
                // the match director, the HUD. A copy keeps it all in step.
                if (!AssetDatabase.CopyAsset(TemplateScene, ScenePath)) { Debug.LogError("[Temple] could not copy the arena scene"); return; }
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            GameObject oldSun = GameObject.Find("Sun");
            if (oldSun != null) Object.DestroyImmediate(oldSun);

            Materials();
            GameObject root = new GameObject(RootName);
            BuildMap(root.transform);
            Atmosphere();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EnsureInBuild();
            Debug.Log("[Temple] built: " + (root.GetComponentsInChildren<Transform>().Length - 1) + " objects\n" + Audit());
        }

        static void EnsureInBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == ScenePath) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ the map

        static void BuildMap(Transform root)
        {
            Transform shell = Group("Shell", root);
            Transform centre = Group("Centre", root);
            Transform spawns = Group("Spawns", root);
            Transform targets = Group("Targets", root);

            // outer walls
            float wo = Half + WallThick * 0.5f, wl = Half * 2f + WallThick * 2f;
            Box("Wall_N", shell, wallMat, new Vector3(0f, WallHeight * 0.5f, wo), new Vector3(wl, WallHeight, WallThick));
            Box("Wall_S", shell, wallMat, new Vector3(0f, WallHeight * 0.5f, -wo), new Vector3(wl, WallHeight, WallThick));
            Box("Wall_E", shell, wallMat, new Vector3(wo, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, wl));
            Box("Wall_W", shell, wallMat, new Vector3(-wo, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, wl));

            // The altar: the one place you see the whole map from, and the one place the whole map sees.
            Box("Altar", centre, stoneMat, new Vector3(0f, Ledge * 0.5f, 0f), new Vector3(AltarHalf * 2f, Ledge, AltarHalf * 2f));
            // the obelisk kills every line across the altar top -- standing up there is strong, not a turret
            Box("Obelisk", centre, darkMat, new Vector3(0f, Ledge + 3f, 0f), new Vector3(1.4f, 6f, 1.4f));
            foreach (float h in new[] { 1.2f, 2.8f, 4.4f })
                Deco("Obelisk_Band", centre, glowMat, new Vector3(0f, Ledge + h, 0f), new Vector3(1.5f, 0.12f, 1.5f));
            GameObject crystal = Deco("Obelisk_Crystal", centre, glowMat, new Vector3(0f, Ledge + 7.2f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
            crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);

            for (int q = 0; q < 4; q++)
            {
                Transform quarter = Group("Quarter_" + q, root);
                quarter.localRotation = Quaternion.Euler(0f, 90f * q, 0f);
                BuildQuarter(quarter);
            }

            // Spawns. Team B on +z, team A the same points turned 180 degrees -- the map is four-fold
            // symmetric, so that is exactly fair. Index 1 (the middle of the side lane) is the 1v1 pair.
            // Each tower spawn stands in the ground-floor room and looks out through its west door; the
            // middle one looks at the altar, which hides it from the other side's middle spawn.
            Vector3 inTower = new Vector3(15.5f, 0f, 13.8f);
            Vector3[] b = {
                Quaternion.Euler(0f, 0f, 0f) * inTower,
                new Vector3(0f, 0f, 18f),
                Quaternion.Euler(0f, 270f, 0f) * inTower };
            float[] bYaw = { 270f, 180f, 270f + 270f };
            for (int i = 0; i < 3; i++)
            {
                Marker("SpawnB_" + i, spawns, b[i], bYaw[i]);
                Marker("SpawnA_" + i, spawns, new Vector3(-b[i].x, 0f, -b[i].z), bYaw[i] + 180f);
            }

            Dummy("Dummy_Near", targets, new Vector3(-1.5f, 1f, -11f));
            Dummy("Dummy_Mid", targets, new Vector3(10f, 1f, -2f));
            Dummy("Dummy_Far", targets, new Vector3(-12f, 1f, 2f));
        }

        /// <summary>One quarter, x and z positive. Rotated copies make the rest.</summary>
        static void BuildQuarter(Transform t)
        {
            // --- floor, with the pit cut out of it. Thick, so the cut has walls.
            const float floorThick = 1.5f;
            FloorPiece(t, floorMat, Rect.MinMaxRect(0f, 0f, Half, Pit.yMin), floorThick);
            FloorPiece(t, floorMat, Rect.MinMaxRect(0f, Pit.yMax, Half, Half), floorThick);
            FloorPiece(t, floorMat, Rect.MinMaxRect(0f, Pit.yMin, Pit.xMin, Pit.yMax), floorThick);
            FloorPiece(t, floorMat, Rect.MinMaxRect(Pit.xMax, Pit.yMin, Half, Pit.yMax), floorThick);
            Box("Pit_Floor", t, pitMat, new Vector3(Pit.center.x, -PitDepth - 0.25f, Pit.center.y),
                new Vector3(Pit.width, 0.5f, Pit.height));
            // a sarcophagus in the pit: cover down there, and a step on the way out
            Box("Pit_Tomb", t, darkMat, new Vector3(Pit.center.x, -PitDepth + 0.4f, Pit.center.y),
                new Vector3(1.2f, 0.8f, 2.6f));

            // --- the altar stair, along its north face, climbing towards the corner the bridge lands on
            int steps = Mathf.RoundToInt(Ledge / StepRise);
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * StepRise;
                Box("Stair_" + i, t, stoneMat,
                    new Vector3(-0.5f + (i + 0.5f) * StepRun, h * 0.5f, AltarHalf + 1f),
                    new Vector3(StepRun, h, 2f));
            }
            Deco("Altar_Edge", t, glowMat, new Vector3(0f, Ledge - 0.18f, AltarHalf + 0.02f), new Vector3(AltarHalf * 2f, 0.08f, 0.06f));
            // a stump on the altar top: something to duck behind up there besides the obelisk
            Box("Altar_Stump", t, stoneMat, new Vector3(2.6f, Ledge + ChestHigh * 0.5f, 2.6f), new Vector3(0.9f, ChestHigh, 0.9f));
            // the ley line from the altar to the wall, and the sigil it runs into
            Deco("Ley_Line", t, glowMat, new Vector3(0f, 0.015f, (AltarHalf + 2.2f + Half) * 0.5f),
                 new Vector3(0.18f, 0.03f, Half - AltarHalf - 2.2f));
            Deco("Wall_Sigil", t, glowMat, new Vector3(0f, 3.6f, Half - 0.04f), new Vector3(0.25f, 3.4f, 0.08f));
            Deco("Wall_Sigil_Bar", t, glowMat, new Vector3(0f, 4.4f, Half - 0.04f), new Vector3(1.6f, 0.2f, 0.08f));
            Deco("Wall_Sigil_Arm", t, glowMat, new Vector3(0f, 2.8f, Half - 0.04f), new Vector3(1.0f, 0.2f, 0.08f));

            BuildTower(t);

            // --- the bridge: tower roof corner down to the altar corner, and the pillar holding it up
            Ramp("Bridge", t, bridgeMat, new Vector3(3.4f, Ledge, 3.4f),
                 new Vector3(Tower.xMin + 0.9f, Storey, Tower.yMin + 0.9f), 2.4f, 0.45f);
            Box("Bridge_Pillar", t, stoneMat, new Vector3(8f, 1.45f, 8f), new Vector3(1.1f, 2.9f, 1.1f), 45f);

            // --- the inner ring, between the altar and the towers
            Box("Arch_Pillar", t, stoneMat, new Vector3(9.5f, 2.25f, 2.2f), new Vector3(1.2f, 4.5f, 1.2f));
            Box("Cover_Inner", t, coverMat, new Vector3(5.5f, ChestHigh * 0.5f, 9.8f), new Vector3(3f, ChestHigh, 0.9f));

            // --- the ruin, the other half of the side lane: a wall to climb, a block to climb it by,
            //     a fallen column, chest cover
            Box("Ruin_Wall", t, wallMat, new Vector3(18.5f, Ledge * 0.5f, 6f), new Vector3(5f, Ledge, 1f));
            Box("Ruin_Step", t, wallMat, new Vector3(15.3f, 0.6f, 6.3f), new Vector3(1.4f, 1.2f, 1.6f));
            Box("Ruin_Cover", t, coverMat, new Vector3(13f, ChestHigh * 0.5f, 2.5f), new Vector3(1f, ChestHigh, 4f));
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Ruin_Column";
            column.transform.SetParent(t, false);
            column.transform.localPosition = new Vector3(19f, 0.6f, 3.5f);
            column.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            column.transform.localScale = new Vector3(1.2f, 2.5f, 1.2f);     // a cylinder is 2 high: 5 m long
            column.GetComponent<Renderer>().sharedMaterial = stoneMat;
            column.isStatic = true;

            // braziers by the tower doors: light to steer by
            Brazier(t, new Vector3(Tower.xMin - 0.8f, 0f, Tower.yMin + 0.6f));
            Brazier(t, new Vector3(Tower.xMax - 0.9f, 0f, Tower.yMin - 0.8f));
        }

        static void BuildTower(Transform t)
        {
            float x0 = Tower.xMin, x1 = Tower.xMax, z0 = Tower.yMin, z1 = Tower.yMax, w = TowerWall;

            // west wall, door towards the side lane
            float dz0 = z0 + 1.1f, dz1 = dz0 + DoorWidth;
            WallX("Tower_W", t, x0, x0 + w, z0, dz0, 0f, Storey);
            WallX("Tower_W", t, x0, x0 + w, dz1, z1, 0f, Storey);
            WallX("Tower_W_Lintel", t, x0, x0 + w, dz0, dz1, DoorHeight, Storey);
            // south wall, door towards the ruin
            float dx0 = x0 + 3f, dx1 = dx0 + DoorWidth;
            WallX("Tower_S", t, x0, dx0, z0, z0 + w, 0f, Storey);
            WallX("Tower_S", t, dx1, x1, z0, z0 + w, 0f, Storey);
            WallX("Tower_S_Lintel", t, dx0, dx1, z0, z0 + w, DoorHeight, Storey);
            // north and east: solid, they face the outer wall
            WallX("Tower_N", t, x0, x1, z1 - w, z1, 0f, Storey);
            WallX("Tower_E", t, x1 - w, x1, z0, z1, 0f, Storey);

            // The ramp up runs inside along the north wall, west to east, and comes out through a hole
            // in the roof. 34 degrees, under the controller's 50.
            float rz0 = z1 - w - 2.45f, rz1 = z1 - w;
            Ramp("Tower_Ramp", t, stoneMat, new Vector3(x0 + w, 0f, (rz0 + rz1) * 0.5f),
                 new Vector3(x1 - w, Storey, (rz0 + rz1) * 0.5f), rz1 - rz0, 0.4f);
            // roof: everything but the hole over the upper part of the ramp
            float holeFrom = x0 + w + 1.7f;     // ramp is 1.2 m up here: a 2 m player still clears the slab
            // the hole is 0.4 m wider than the ramp on the open side, so a head never meets the slab edge
            WallX("Tower_Roof", t, x0 + w, x1 - w, z0 + w, rz0 - 0.4f, Storey - 0.4f, Storey);
            WallX("Tower_Roof", t, x0 + w, holeFrom, rz0 - 0.4f, rz1, Storey - 0.4f, Storey);

            // battlements on the two edges that look into the arena; the corner stays open for the bridge
            for (int i = 0; i < 3; i++)
            {
                float along = z0 + 2.6f + i * 2f;
                Box("Merlon_W", t, wallMat, new Vector3(x0 + w * 0.5f, Storey + 0.6f, along + 0.5f), new Vector3(w, 1.2f, 1f));
                float across = x0 + 2.6f + i * 2f;
                Box("Merlon_S", t, wallMat, new Vector3(across + 0.5f, Storey + 0.6f, z0 + w * 0.5f), new Vector3(1f, 1.2f, w));
            }
            Box("Parapet_N", t, wallMat, new Vector3((x0 + x1) * 0.5f, Storey + 0.3f, z1 - w * 0.5f), new Vector3(x1 - x0, 0.6f, w));
            Box("Parapet_E", t, wallMat, new Vector3(x1 - w * 0.5f, Storey + 0.3f, (z0 + z1) * 0.5f), new Vector3(w, 0.6f, z1 - z0));
        }

        static void Brazier(Transform t, Vector3 at)
        {
            Box("Brazier", t, darkMat, at + new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 1.1f, 0.45f));
            Deco("Brazier_Fire", t, fireMat, at + new Vector3(0f, 1.25f, 0f), new Vector3(0.35f, 0.3f, 0.35f));
        }

        // ------------------------------------------------------------------ look

        static void Atmosphere()
        {
            // a low warm sun: long shadows read the levels apart
            GameObject sun = new GameObject("Sun");
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(1f, 0.86f, 0.70f);
            l.intensity = 1.15f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
            RenderSettings.sun = l;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.54f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.43f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.23f, 0.22f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.56f, 0.60f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 140f;

            string skyPath = "Assets/Materials/Temple/Temple_Sky.mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_AtmosphereThickness", 1.25f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.52f, 0.68f));
            sky.SetColor("_GroundColor", new Color(0.32f, 0.28f, 0.30f));
            sky.SetFloat("_Exposure", 1.1f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        static void Materials()
        {
            EnsureFolder("Assets/Materials/Temple");
            EnsureFolder("Assets/Textures");
            EnsureFolder("Assets/Textures/Temple");
            Texture2D tiles = PatternTexture("Assets/Textures/Temple/Temple_Tiles.png", 4, 4, false, 11);
            Texture2D bricks = PatternTexture("Assets/Textures/Temple/Temple_Bricks.png", 4, 8, true, 23);
            Texture2D blocks = PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);

            floorMat  = Stone("Temple_Floor",  tiles,  4f, new Color(0.60f, 0.54f, 0.46f), new Color(0.66f, 0.60f, 0.50f), 0.35f);
            pitMat    = Stone("Temple_Pit",    tiles,  4f, new Color(0.36f, 0.34f, 0.33f), new Color(0.38f, 0.36f, 0.34f), 0.2f);
            wallMat   = Stone("Temple_Wall",   bricks, 4f, new Color(0.46f, 0.43f, 0.44f), new Color(0.44f, 0.50f, 0.36f), 0.3f);
            stoneMat  = Stone("Temple_Stone",  blocks, 3f, new Color(0.66f, 0.61f, 0.53f), new Color(0.60f, 0.63f, 0.48f), 0.3f);
            darkMat   = Stone("Temple_Dark",   blocks, 2f, new Color(0.20f, 0.21f, 0.27f), new Color(0.24f, 0.26f, 0.30f), 0.1f);
            bridgeMat = Stone("Temple_Bridge", bricks, 2f, new Color(0.55f, 0.46f, 0.38f), new Color(0.60f, 0.52f, 0.42f), 0.2f);
            coverMat  = Stone("Temple_Cover",  blocks, 2f, new Color(0.64f, 0.52f, 0.40f), new Color(0.60f, 0.58f, 0.44f), 0.3f);
            glowMat = Glow("Temple_Glow", new Color(0.25f, 1.6f, 1.45f));
            fireMat = Glow("Temple_Fire", new Color(2.4f, 1.1f, 0.35f));
        }

        static Material Stone(string name, Texture2D pattern, float scale, Color side, Color top, float grime)
        {
            string path = "Assets/Materials/Temple/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("MageCast/WorldStone"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("MageCast/WorldStone");
            m.SetTexture("_MainTex", pattern);
            m.SetColor("_Color", side);
            m.SetColor("_TopColor", top);
            m.SetFloat("_Scale", scale);
            m.SetFloat("_Grime", grime);
            m.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Glow(string name, Color emission)
        {
            string path = "Assets/Materials/Temple/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = new Color(emission.r, emission.g, emission.b) * 0.3f;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// A grey stone pattern, written once as a PNG: <paramref name="cols"/> x <paramref name="rows"/>
        /// slabs, each a little lighter or darker than its neighbours, mortar between them and grain on
        /// top. Running bond (every other row shifted half a slab) for bricks.
        /// </summary>
        static Texture2D PatternTexture(string path, int cols, int rows, bool runningBond, int seed)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 512;
            var rnd = new System.Random(seed);
            float[,] shade = new float[cols + 1, rows];
            for (int c = 0; c <= cols; c++)
                for (int r = 0; r < rows; r++) shade[c, r] = 0.78f + (float)rnd.NextDouble() * 0.22f;

            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var px = new Color[size * size];
            float cw = size / (float)cols, rh = size / (float)rows;
            float mortar = 5f;
            for (int y = 0; y < size; y++)
            {
                int r = Mathf.Min(rows - 1, (int)(y / rh));
                float shift = runningBond && (r % 2 == 1) ? cw * 0.5f : 0f;
                for (int x = 0; x < size; x++)
                {
                    float sx = (x + shift) % size;
                    int c = (int)(sx / cw);
                    float inX = sx - c * cw, inY = y - r * rh;
                    float edge = Mathf.Min(Mathf.Min(inX, cw - inX), Mathf.Min(inY, rh - inY));
                    float grain = Mathf.PerlinNoise(x * 0.045f + seed, y * 0.045f) * 0.55f
                                + Mathf.PerlinNoise(x * 0.19f, y * 0.19f + seed) * 0.45f;
                    float v = shade[c, r] * (0.84f + grain * 0.22f);
                    if (edge < mortar) v *= Mathf.Lerp(0.52f, 1f, edge / mortar);
                    px[y * size + x] = new Color(v, v, v);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ audit

        struct Spot { public Vector3 p; public int cell; }

        /// <summary>
        /// Measures the map the way a player meets it, on every level, not just the floor:
        ///   cover     the nearest thing that rises at least a metre above where you stand
        ///   sightline the longest clear line at eye height from anywhere you can stand
        ///   reach     whether everywhere you can stand can be got to from a spawn -- walking, jumping,
        ///             climbing a ledge -- and how much of it needs the climb
        /// Treat a cover FAIL or anything unreachable as a broken map.
        /// </summary>
        [MenuItem("Tools/Arena/Audit Temple")]
        public static void AuditMenu() { Debug.Log(Audit()); }

        public static string Audit()
        {
            Physics.SyncTransforms();
            GameObject root = GameObject.Find(RootName);
            if (root == null) return "[Temple] no '" + RootName + "' in the open scene";

            const float step = 0.5f;
            int n = Mathf.RoundToInt(Half * 2f / step);
            var spots = new List<Spot>();
            var byCell = new List<int>[n * n];
            int mask = ~0;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float x = -Half + (i + 0.5f) * step, z = -Half + (j + 0.5f) * step;
                    RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, 15f, z), Vector3.down, 30f, mask, QueryTriggerInteraction.Ignore);
                    foreach (RaycastHit h in hits)
                    {
                        if (h.normal.y < 0.6f) continue;
                        if (h.collider.GetComponentInParent<Combat.TrainingDummy>() != null) continue;
                        Vector3 p = h.point;
                        if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.6f, 0.35f, mask, QueryTriggerInteraction.Ignore))
                            continue;
                        int cell = i * n + j;
                        if (byCell[cell] == null) byCell[cell] = new List<int>();
                        byCell[cell].Add(spots.Count);
                        spots.Add(new Spot { p = p, cell = cell });
                    }
                }
            }

            // reachability: breadth-first from the spawns, once without climbing and once with it
            var starts = new List<int>();
            foreach (Transform s in root.transform.Find("Spawns"))
            {
                int best = -1; float bd = 999f;
                for (int k = 0; k < spots.Count; k++)
                {
                    float d = Vector3.Distance(spots[k].p, s.position);
                    if (d < bd) { bd = d; best = k; }
                }
                if (best >= 0) starts.Add(best);
            }
            bool[] withClimb = Flood(spots, byCell, n, starts, MantleReach);
            bool[] withoutClimb = Flood(spots, byCell, n, starts, JumpReach);
            int reached = 0, needsClimb = 0;
            var stranded = new List<Vector3>();
            for (int k = 0; k < spots.Count; k++)
            {
                if (withClimb[k]) reached++; else stranded.Add(spots[k].p);
                if (withClimb[k] && !withoutClimb[k]) needsClimb++;
            }

            // cover and sightlines on every other sample (1 m)
            var covers = new List<Collider>();
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                string name = c.gameObject.name;
                if (name.StartsWith("Wall_") || name.StartsWith("Dummy")) continue;
                covers.Add(c);
            }
            float worstCover = 0f, longest = 0f;
            Vector3 worstAt = Vector3.zero, longFrom = Vector3.zero, longTo = Vector3.zero;
            int over = 0, measured = 0;
            var exposed = new List<Vector3>();
            var highExposed = new List<Vector3>();
            for (int k = 0; k < spots.Count; k++)
            {
                int i = spots[k].cell / n, j = spots[k].cell % n;
                if (i % 2 != 0 || j % 2 != 0 || !withClimb[k]) continue;     // only where somebody can stand
                measured++;
                Vector3 p = spots[k].p;
                Vector3 chest = p + Vector3.up * 1.2f;
                float best = 999f;
                foreach (Collider c in covers)
                {
                    if (c.bounds.max.y < p.y + 1f) continue;
                    Vector3 cp = c.ClosestPoint(chest);
                    float d = Vector2.Distance(new Vector2(cp.x, cp.z), new Vector2(p.x, p.z));
                    if (d < best) best = d;
                }
                // Up high, cover is not the rule: a roof or a bridge is exposed on purpose, and the way to
                // safety from it is down. Gated on the ground and in the pit, reported for the rest.
                if (p.y > 1.3f) { if (best > MaxCoverDist) highExposed.Add(p); }
                else
                {
                    if (best > MaxCoverDist) { over++; exposed.Add(p); }
                    if (best > worstCover) { worstCover = best; worstAt = p; }
                }

                Vector3 eye = p + Vector3.up * 1.6f;
                for (int a = 0; a < 64; a++)
                {
                    float ang = a * (360f / 64f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    RaycastHit hit;
                    float dist = Physics.Raycast(eye, dir, out hit, 100f, mask, QueryTriggerInteraction.Ignore) ? hit.distance : 100f;
                    if (dist > longest) { longest = dist; longFrom = eye; longTo = eye + dir * dist; }
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("[Temple] standable spots: {0}, reachable from a spawn: {1} ({2:F1}%), of those only by climbing: {3}\n",
                spots.Count, reached, 100f * reached / Mathf.Max(1, spots.Count), needsClimb);
            if (stranded.Count > 0) sb.Append("[Temple] UNREACHABLE: ").Append(Clusters(stranded)).Append("\n");
            sb.AppendFormat("[Temple] cover: worst {0:F1} m at {1}, {2} of {3} samples over {4} m -> {5}\n",
                worstCover, worstAt.ToString("F1"), over, measured, MaxCoverDist, over == 0 ? "PASS" : "FAIL");
            if (exposed.Count > 0) sb.Append("[Temple] ground without cover: ").Append(Clusters(exposed)).Append("\n");
            if (highExposed.Count > 0) sb.Append("[Temple] high ground without cover (INFO): ").Append(Clusters(highExposed)).Append("\n");
            sb.AppendFormat("[Temple] sightline: longest {0:F1} m {1} -> {2}  INFO",
                longest, longFrom.ToString("F0"), longTo.ToString("F0"));
            return sb.ToString();
        }

        /// <summary>Points grouped into 2 m cells per level: "(x,y,z)xcount" for the biggest groups.</summary>
        static string Clusters(List<Vector3> points)
        {
            var groups = new Dictionary<Vector3Int, int>();
            foreach (Vector3 p in points)
            {
                var key = new Vector3Int(Mathf.FloorToInt(p.x / 2f), Mathf.RoundToInt(p.y * 2f), Mathf.FloorToInt(p.z / 2f));
                groups.TryGetValue(key, out int c);
                groups[key] = c + 1;
            }
            var list = new List<KeyValuePair<Vector3Int, int>>(groups);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            var sb = new System.Text.StringBuilder(points.Count + " spots");
            for (int i = 0; i < list.Count && i < 16; i++)
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "  ({0},{1:F1},{2})x{3}",
                    list[i].Key.x * 2 + 1, list[i].Key.y * 0.5f, list[i].Key.z * 2 + 1, list[i].Value);
            return sb.ToString();
        }

        /// <summary>
        /// Which spots can be reached from the starts, moving one or two cells at a time: down any
        /// height (you can always drop), up a step by walking, up to <paramref name="upReach"/> by a jump
        /// or a climb. Two cells, because a ledge is climbed from the cell in front of it.
        /// </summary>
        static bool[] Flood(List<Spot> spots, List<int>[] byCell, int n, List<int> starts, float upReach)
        {
            bool[] seen = new bool[spots.Count];
            var queue = new Queue<int>();
            foreach (int s in starts) { seen[s] = true; queue.Enqueue(s); }
            while (queue.Count > 0)
            {
                int k = queue.Dequeue();
                Spot a = spots[k];
                int ai = a.cell / n, aj = a.cell % n;
                for (int di = -2; di <= 2; di++)
                {
                    for (int dj = -2; dj <= 2; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        bool near = Mathf.Abs(di) <= 1 && Mathf.Abs(dj) <= 1;
                        int bi = ai + di, bj = aj + dj;
                        if (bi < 0 || bj < 0 || bi >= n || bj >= n) continue;
                        List<int> there = byCell[bi * n + bj];
                        if (there == null) continue;
                        foreach (int m in there)
                        {
                            if (seen[m]) continue;
                            float dy = spots[m].p.y - a.p.y;
                            // walking needs the next cell; a drop or a climb starts a body's width from the edge
                            bool ok = dy < -0.45f || (dy <= 0.45f ? near : dy <= upReach);
                            if (!ok) continue;
                            // nothing solid in between at the height of the higher of the two
                            // at the height of the higher one: you walk off an edge, or climb over it
                            float y = Mathf.Max(a.p.y, spots[m].p.y) + 0.6f;
                            Vector3 from = new Vector3(a.p.x, y, a.p.z);
                            Vector3 to = new Vector3(spots[m].p.x, y, spots[m].p.z);
                            if (Physics.Linecast(from, to, ~0, QueryTriggerInteraction.Ignore)) continue;
                            seen[m] = true;
                            queue.Enqueue(m);
                        }
                    }
                }
            }
            return seen;
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(string name, Transform parent)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static GameObject Box(string name, Transform parent, Material m, Vector3 pos, Vector3 size, float yaw = 0f)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            g.isStatic = true;
            return g;
        }

        /// <summary>A box by its corners: x0..x1, z0..z1, from height y0 up to y1.</summary>
        static GameObject WallX(string name, Transform parent, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            return Box(name, parent, name.Contains("Roof") ? stoneMat : wallMat,
                       new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f),
                       new Vector3(x1 - x0, y1 - y0, z1 - z0));
        }

        static void FloorPiece(Transform parent, Material m, Rect r, float thick)
        {
            Box("Floor", parent, m, new Vector3(r.center.x, -thick * 0.5f, r.center.y), new Vector3(r.width, thick, r.height));
        }

        /// <summary>Looks only: no collider, so spells and players go through it.</summary>
        static GameObject Deco(string name, Transform parent, Material m, Vector3 pos, Vector3 size)
        {
            GameObject g = Box(name, parent, m, pos, size);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        /// <summary>A sloped slab whose walking surface runs from <paramref name="low"/> to <paramref name="high"/>.</summary>
        static void Ramp(string name, Transform parent, Material m, Vector3 low, Vector3 high, float width, float thick)
        {
            Vector3 along = high - low;
            Quaternion rot = Quaternion.LookRotation(along.normalized, Vector3.up);
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localRotation = rot;
            g.transform.localPosition = (low + high) * 0.5f - rot * Vector3.up * (thick * 0.5f);
            g.transform.localScale = new Vector3(width, thick, along.magnitude + 0.3f);
            g.GetComponent<Renderer>().sharedMaterial = m;
            g.isStatic = true;
        }

        static void Marker(string name, Transform parent, Vector3 pos, float yaw)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static void Dummy(string name, Transform parent, Vector3 pos)
        {
            Physics.SyncTransforms();
            if (Physics.CheckCapsule(pos + Vector3.up * -0.4f, pos + Vector3.up * 0.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                Debug.LogWarning("[Temple] " + name + " overlaps the map at " + pos);

            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            Material red = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BO_Target.mat");
            if (red != null) g.GetComponent<Renderer>().sharedMaterial = red;

            Combat.Health hp = g.AddComponent<Combat.Health>();
            SerializedObject so = new SerializedObject(hp);
            so.FindProperty("maxHealth").floatValue = 150f;
            so.ApplyModifiedProperties();
            g.AddComponent<Combat.TrainingDummy>();
            g.AddComponent<Combat.HealthBar>();
            g.AddComponent<Unity.Netcode.NetworkObject>();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
