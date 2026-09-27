using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Generates the gesture-arena blockout. Everything is driven by the constants below --
    /// change a number, hit Tools > Arena > Rebuild Blockout, re-run the audit.
    ///
    /// Scale contract (1 unit = 1 m), do not drift from this:
    ///   player 2 | eye 1.6 | jump 1.2 | doorway 2.5-3 | storey 4
    ///
    /// Layout rules this geometry has to satisfy, because the caster is blind for 1-2 s:
    ///   1. cover within ~7-8 m of anywhere  -> you can reach safety in ~1.5 s
    ///   2. no long straight sightlines      -> a locked direction must never be a free execution
    /// Tools > Arena > Audit Layout measures both. Treat a failing audit as a broken map.
    /// </summary>
    public static class ArenaBlockoutBuilder
    {
        // --- arena shell ---
        const float Half = 20f;            // arena is 40 x 40
        const float WallHeight = 6f;
        const float WallThick = 1f;

        // --- heights ---
        const float ChestHigh = 1.6f;      // shoot over it, hide your body behind it
        const float EyeBlock = 2.8f;       // breaks line of sight outright
        const float JumpUp = 1.2f;         // exactly one jump
        const float CoreHigh = 5f;

        // --- audit thresholds ---
        const float MaxCoverDist = 8f;
        // Raised from 22. At 22 the arena needed so much geometry that only 41% of standable ground
        // had a 10 m lob available from it, and the third-person camera sat under 1.5 m for a third
        // of the match. 26 m is still under two thirds of the arena's width.
        // Relaxed twice now, and deliberately. The rule came from "the caster is blind AND committed
        // to a direction"; the second half stopped being true when aiming moved to after the draw, so
        // a long lane is now a shot you can dodge. Audit it, but do not let it dictate the layout.
        const float MaxSightline = 34f;

                // Team A spawns; team B is these rotated 180 degrees, which a four-fold symmetric layout
        // makes exactly fair. Measured, not eyeballed -- once the sightline solver had packed the
        // map, three of the six original edge spawns were facing a wall about a metre away.
        //
        // Moved again after the arena was thinned, because nobody re-checked them: four of the six
        // stood inside a Slab_A, including the 1v1 pair -- found on the first online test, when the
        // host spawned half inside a wall and every shot at them hit the slab. Each one now has a
        // metre of clear floor around it (and so does its mirror), and faces the most open line
        // within 45 degrees of the centre.
        static readonly Vector3[] SpawnA = {
            new Vector3(-2f, 0f, -14f), new Vector3(-13f, 0f, -9f), new Vector3(9f, 0f, -13f) };
        static readonly float[] SpawnYaw = { 323f, 15f, 350f };

const string RootName = "Arena";

        [MenuItem("Tools/Arena/Rebuild Blockout")]
        public static void Rebuild()
        {
            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            GameObject root = new GameObject(RootName);
            Transform walls = Group("Walls", root.transform);
            Transform pillars = Group("Pillars", root.transform);
            Transform cover = Group("Cover", root.transform);
            Transform spawns = Group("Spawns", root.transform);
            Transform targets = Group("Targets", root.transform);

            Material matFloor = Mat("BO_Floor", 0.42f, 0.44f, 0.46f);
            Material matWall = Mat("BO_Wall", 0.28f, 0.30f, 0.33f);
            Material matPillar = Mat("BO_Pillar", 0.56f, 0.53f, 0.48f);
            Material matChest = Mat("BO_Cover", 0.64f, 0.44f, 0.28f);
            Material matBlock = Mat("BO_Blocker", 0.46f, 0.36f, 0.42f);
            Material matPlat = Mat("BO_Platform", 0.34f, 0.50f, 0.42f);
            Material matTarget = Mat("BO_Target", 0.78f, 0.18f, 0.20f);

            Box("Floor", root.transform, matFloor, new Vector3(0f, -0.25f, 0f), new Vector3(Half * 2f, 0.5f, Half * 2f));

            float wo = Half + WallThick * 0.5f;
            float wl = Half * 2f + WallThick * 2f;
            Box("Wall_N", walls, matWall, new Vector3(0f, WallHeight * 0.5f, wo), new Vector3(wl, WallHeight, WallThick));
            Box("Wall_S", walls, matWall, new Vector3(0f, WallHeight * 0.5f, -wo), new Vector3(wl, WallHeight, WallThick));
            Box("Wall_E", walls, matWall, new Vector3(wo, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, wl));
            Box("Wall_W", walls, matWall, new Vector3(-wo, WallHeight * 0.5f, 0f), new Vector3(WallThick, WallHeight, wl));

            // central massif: kills both diagonals and the centre cross in one object
            // Bigger rather than more. With the arena cut to a third of its old piece count the long
            // diagonals reopened, and the cheap fix is to widen the one landmark that is already there
            // instead of scattering fresh geometry back in -- a single large massif reads as a place,
            // a dozen small blockers read as clutter.
            Box("Core", root.transform, matPillar, new Vector3(0f, CoreHigh * 0.5f, 0f), new Vector3(13f, CoreHigh, 13f));

            // four, not eight -- see BuildQuadrant for why the arena no longer needs the density
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Box("Pillar_" + i, pillars, matPillar,
                    new Vector3(Mathf.Cos(a) * 12f, WallHeight * 0.5f, Mathf.Sin(a) * 12f),
                    new Vector3(1.5f, WallHeight, 1.5f));
            }

            // One quadrant, stamped four times by rotation -> symmetric, so 1v1 is fair by construction.
            for (int q = 0; q < 4; q++)
            {
                GameObject quad = new GameObject("Quadrant_" + q);
                quad.transform.SetParent(cover, false);
                quad.transform.localRotation = Quaternion.Euler(0f, 90f * q, 0f);
                BuildQuadrant(quad.transform, matChest, matBlock, matWall, matPlat);
            }

            // 3v3 on opposite edges; the middle pair doubles as the 1v1 spawns
            // 3v3 on opposite ends; index 1 doubles as the 1v1 pair
            for (int i = 0; i < 3; i++)
            {
                Marker("SpawnA_" + i, spawns, SpawnA[i], SpawnYaw[i]);
                Marker("SpawnB_" + i, spawns, new Vector3(-SpawnA[i].x, 0f, -SpawnA[i].z), SpawnYaw[i] + 180f);
            }

            Dummy("Dummy_Near", targets, matTarget, new Vector3(6f, 1f, -9f));
            Dummy("Dummy_Mid", targets, matTarget, new Vector3(14f, 1f, 2f));
            Dummy("Dummy_Far", targets, matTarget, new Vector3(-2f, 1f, 16f));

            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0f, 40f, -36f);
                cam.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
                cam.farClipPlane = 300f;
            }
            foreach (Light l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) l.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

                        // Flat bright ambient. Not a look, a working condition: a blockout you cannot read from
            // an overhead screenshot is a blockout you cannot judge.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.62f);

AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Arena] Blockout rebuilt: " + (root.GetComponentsInChildren<Transform>().Length - 1) + " objects.");
        }

        /// <summary>
        /// One 90-degree wedge, in local coords. Mixed heights on purpose:
        /// chest-high stops projectiles but you still read the enemy over it,
        /// eye-blockers break the read entirely. That mix is what makes lines "bent".
        /// </summary>
static void BuildQuadrant(Transform t, Material chest, Material blocker, Material wall, Material plat)
        {
            // Cut from twelve pieces to three. The arena was dense because of a rule that no longer
            // applies the same way: every sightline had to be broken because the caster committed to a
            // direction BEFORE drawing and could not react. Aiming now happens after the draw, so a
            // long lane is a shot you can still dodge rather than an execution, and the geometry that
            // was there to prevent it can go.
            //
            // What is left is one of each thing a fight actually needs: something to crouch behind,
            // something to break line of sight, and something to stand on.
            Box("Cover_Chest", t, chest, new Vector3(7f, ChestHigh * 0.5f, 3f), new Vector3(4f, ChestHigh, 1f));
            Box("Slab_A", t, blocker, new Vector3(15f, 1.75f, 11f), new Vector3(6f, 3.5f, 1.5f));
            Box("Platform", t, plat, new Vector3(8.5f, JumpUp * 0.5f, 13f), new Vector3(5f, JumpUp, 5f));
        }

        [MenuItem("Tools/Arena/Audit Layout")]
        public static void AuditMenu()
        {
            Debug.Log(Audit());
        }

/// <summary>
        /// Greedy sightline solver. Repeatedly finds the longest remaining line of sight and drops a
        /// fin across the middle of it, in all four quadrants at once so the map stays symmetric.
        /// Stops when the longest line is under MaxSightline or when there is nowhere legal to build.
        ///
        /// The fins it creates are throwaway -- it prints the equivalent Box() calls to the console,
        /// and those are what you paste into BuildQuadrant so the layout stays reproducible from
        /// source. Run it after moving anything; hand-placed geometry always reopens a lane somewhere.
        /// </summary>
        [MenuItem("Tools/Arena/Solve Sightlines")]
        public static void SolveSightlines()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null) { Debug.LogWarning("[Arena] Rebuild the blockout first."); return; }

            Transform cover = root.transform.Find("Cover");
            Material blocker = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BO_Blocker.mat");
            Transform[] quads = new Transform[4];
            for (int q = 0; q < 4; q++) quads[q] = cover.Find("Quadrant_" + q);

            var spawns = new System.Collections.Generic.List<Vector3>();
            foreach (Vector3 s in SpawnA) { spawns.Add(s); spawns.Add(new Vector3(-s.x, 0f, -s.z)); }

            Vector3 finSize = new Vector3(1f, EyeBlock, 6f);
            var log = new System.Text.StringBuilder("[Arena] sightline solver:\n");
            float longest = 0f;

            for (int iter = 0; iter < 8; iter++)
            {
                Physics.SyncTransforms();
                float best = 0f;
                Vector3 from = Vector3.zero, dir = Vector3.forward;
                FindLongestLine(out best, out from, out dir);
                longest = best;
                if (best <= MaxSightline) break;

                // long axis of the fin perpendicular to the offending line
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                float[] offsets = { 0f, 3f, -3f, 6f, -6f, 9f, -9f, 12f, -12f };
                bool placed = false;
                Vector3 mid = Vector3.zero;

                for (int o = 0; o < offsets.Length && !placed; o++)
                {
                    Vector3 c = from + dir * (best * 0.5f + offsets[o]);
                    c.y = EyeBlock * 0.5f;
                    if (Mathf.Abs(c.x) > Half - 2f || Mathf.Abs(c.z) > Half - 2f) continue;
                    if (Physics.CheckSphere(c, 0.7f)) continue;              // don't bury it in a wall
                    bool nearSpawn = false;
                    foreach (Vector3 s in spawns)
                        if (Vector3.Distance(new Vector3(c.x, 0f, c.z), s) < 6f) nearSpawn = true;
                    if (nearSpawn) continue;                                 // never wall a team into its spawn
                    mid = c; placed = true;
                }

                if (!placed)
                {
                    log.AppendLine("  stuck: nowhere legal to cut the " + best.ToString("F1") + " m line");
                    break;
                }

                for (int q = 0; q < 4; q++)
                    Box("Fin_Solved_" + iter, quads[q], blocker, mid, finSize, yaw);

                log.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "  Box(\"Fin_Cut_{0}\", t, blocker, new Vector3({1:F1}f, EyeBlock * 0.5f, {2:F1}f), new Vector3(1f, EyeBlock, 6f), {3:F1}f);   // cut a {4:F1} m line\n",
                    iter, mid.x, mid.z, yaw, best);
            }

            log.AppendLine("  longest now: " + longest.ToString("F1") + " m (limit " + MaxSightline + " m)");
            Debug.Log(log.ToString());
        }

        static void FindLongestLine(out float best, out Vector3 from, out Vector3 dir)
        {
            best = 0f; from = Vector3.zero; dir = Vector3.forward;
            for (float x = -Half + 1f; x <= Half - 1f; x += 1f)
            {
                for (float z = -Half + 1f; z <= Half - 1f; z += 1f)
                {
                    if (Physics.CheckSphere(new Vector3(x, 1f, z), 0.45f)) continue;
                    Vector3 eye = new Vector3(x, 1.6f, z);
                    for (int a = 0; a < 64; a++)
                    {
                        float ang = a * (360f / 64f) * Mathf.Deg2Rad;
                        Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        RaycastHit hit;
                        float len = Physics.Raycast(eye, d, out hit, 100f) ? hit.distance : 100f;
                        if (len > best) { best = len; from = eye; dir = d; }
                    }
                }
            }
        }


        /// <summary>
        /// Walks a 1 m grid and measures the two rules. Cover distance ignores floor, outer
        /// walls and dummies -- none of those is something you can take cover behind.
        /// </summary>
public static string Audit()
        {
            Physics.SyncTransforms();
            GameObject root = GameObject.Find(RootName);
            if (root == null) return "[Arena] No '" + RootName + "' in the scene -- rebuild first.";

            var covers = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                string n = c.gameObject.name;
                if (n == "Floor" || n.StartsWith("Wall_") || n.StartsWith("Dummy")) continue;
                covers.Add(c);
            }

            float worstCover = 0f, sumCover = 0f, longest = 0f;
            int samples = 0, overLimit = 0;
            Vector3 worstAt = Vector3.zero, longFrom = Vector3.zero, longTo = Vector3.zero;

            // 64 directions, not 32: at 32 the sweep steps straight over the narrowest lanes and
            // under-reports by several metres, which turns a FAIL into a false PASS.
            const int Dirs = 64;

            for (float x = -Half + 1f; x <= Half - 1f; x += 1f)
            {
                for (float z = -Half + 1f; z <= Half - 1f; z += 1f)
                {
                    Vector3 p = new Vector3(x, 1f, z);
                    if (Physics.CheckSphere(p, 0.45f)) continue;   // inside geometry, nobody stands here
                    samples++;

                    float best = 999f;
                    for (int i = 0; i < covers.Count; i++)
                    {
                        float d = Vector3.Distance(p, covers[i].ClosestPoint(p));
                        if (d < best) best = d;
                    }
                    sumCover += best;
                    if (best > MaxCoverDist) overLimit++;
                    if (best > worstCover) { worstCover = best; worstAt = p; }

                    Vector3 eye = new Vector3(x, 1.6f, z);
                    for (int a = 0; a < Dirs; a++)
                    {
                        float ang = a * (360f / Dirs) * Mathf.Deg2Rad;
                        Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        RaycastHit hit;
                        float dist = Physics.Raycast(eye, dir, out hit, 100f) ? hit.distance : 100f;
                        if (dist > longest) { longest = dist; longFrom = eye; longTo = eye + dir * dist; }
                    }
                }
            }

            bool coverOk = overLimit == 0;

            // Sightline is reported, not gated, and that is a deliberate demotion. The rule existed
            // because the caster locked a direction BEFORE drawing, which made a long lane an execution
            // with no counterplay. Aiming now happens after the draw, so what a long lane costs you is
            // flight time -- and flight time is something the target can spend running. The audit
            // prints how far the fastest spell travels down the longest lane and how far a walking
            // target moves meanwhile; if the second number stops being comfortably large, bring the
            // gate back.
            const float FastestSpellSpeed = 26f;    // fire, the quickest thing with real damage
            const float WalkSpeed = 6f;
            float flight = longest / FastestSpellSpeed;
            float dodge = flight * WalkSpeed;
            return string.Format(
                "[Arena] cover: worst {0:F1} m at {1}, avg {2:F1} m, {3} samples over {4} m -> {5}\n" +
                "[Arena] sightline: longest {6:F1} m {7} -> {8}   ({9:F2} s flight, target walks {10:F1} m) INFO\n" +
                "[Arena] standable samples: {11}",
                worstCover, worstAt.ToString("F0"), sumCover / Mathf.Max(1, samples), overLimit, MaxCoverDist,
                coverOk ? "PASS" : "FAIL",
                longest, longFrom.ToString("F0"), longTo.ToString("F0"), flight, dodge,
                samples);
        }

        // ---------- helpers ----------

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

        static void Marker(string name, Transform parent, Vector3 pos, float yaw)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

static void Dummy(string name, Transform parent, Material m, Vector3 pos)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Capsule);   // default capsule is 2 m = player height
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.GetComponent<Renderer>().sharedMaterial = m;

            // A target you can actually practise on: it takes damage, shows what is left, flashes when
            // hit so a graze is distinguishable from a miss, and puts itself back together a few
            // seconds after it drops. Without the reset, testing a spell thirty times in a row means
            // thirty trips to the hierarchy.
            MageCast.Combat.Health hp = g.AddComponent<MageCast.Combat.Health>();
            SerializedObject so = new SerializedObject(hp);
            so.FindProperty("maxHealth").floatValue = 150f;
            so.ApplyModifiedProperties();

            g.AddComponent<MageCast.Combat.TrainingDummy>();
            g.AddComponent<MageCast.Combat.HealthBar>();

            // networked, so everyone sees the same dummy take the same hit; the server owns its health
            g.AddComponent<Unity.Netcode.NetworkObject>();
        }

        static Material Mat(string name, float r, float g, float b)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string path = "Assets/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = new Color(r, g, b);
            m.SetFloat("_Glossiness", 0.05f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
