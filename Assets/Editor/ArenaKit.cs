using System.Collections.Generic;
using MageCast.Gestures;
using MageCast.World;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The pieces every map's look is made of, in the painted style of the spell effects -- first made for
    /// the Sanctum (ArenaSanctum), shared so the other maps look like the same game:
    ///
    ///   stone     SanctumStone: triplanar slabs and bricks, worn lit edges, big colour patches, mossy tops
    ///   light     SanctumGlow: rune lines and halos, baked into one mesh per group (BakeGlows)
    ///   sky       SanctumSky: a painted gradient with a soft sun, clouds and (at dusk) stars
    ///   life      the packs' effects kept going (AmbientFx), drifting motes, floating rocks, crystals
    ///   bake      the scene's ambient light and sky reflection, stored with it (BakeEnvironment)
    ///
    /// Call Begin with a map's material folder and name first: its shared materials (the glow, the motes)
    /// are made there under that name.
    ///
    /// Two rules every piece keeps, both measured (Tools > Arena > Audit Overlaps): no surface lies flush
    /// on another facing the same way -- the depth buffer cannot order them and they flicker as the camera
    /// moves (trims stand 2 cm proud, crossing blocks differ in height) -- and stone is static and shares
    /// materials, so the web build draws a map in a few hundred batches.
    /// </summary>
    public static class ArenaKit
    {
        public const string AoE = "Assets/Vefects/Stylized AoE VFX/VFX/";
        public const string Anime = "Assets/Vefects/Anime Stylized VFX/";
        public const string TexFolder = "Assets/Textures/Sanctum";
        const string MeshFolder = "Assets/Meshes";

        /// <summary>How far a trim stands proud of what it dresses, so the two never share a plane.</summary>
        public const float Proud = 0.02f;

        // the elements, as the game colours them (MenuArt.ElementColours)
        public static readonly Color Fire = new Color(1f, 0.45f, 0.15f), Ice = new Color(0.55f, 0.85f, 1f),
                                     Bolt = new Color(1f, 0.9f, 0.35f), Air = new Color(0.75f, 1f, 0.9f),
                                     Earth = new Color(0.85f, 0.62f, 0.38f), Ward = new Color(1f, 0.85f, 0.4f),
                                     Arcane = new Color(0.3f, 1f, 0.92f), Gold = new Color(1f, 0.8f, 0.42f);

        public static string Folder { get; private set; }
        public static string Prefix { get; private set; }
        public static Material GlowLine { get; private set; }
        public static Material Mote { get; private set; }

        static readonly Dictionary<string, Material> halos = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> boxes = new Dictionary<string, Mesh>();
        static Mesh crystalMesh;

        public static void Begin(string folder, string prefix)
        {
            Folder = folder;
            Prefix = prefix;
            MakeFolder(folder);
            MakeFolder(TexFolder);
            MakeFolder(MeshFolder);
            halos.Clear();
            GlowLine = Glow(prefix + "_GlowLine", Color.white * 1.15f, false, 1.6f, 0.9f);
            Mote = Glow(prefix + "_Mote", Color.white * 1.2f, true, 2.6f, 1.2f);
            crystalMesh = CrystalMesh();
        }

        // ------------------------------------------------------------------ elements

        public static IEnumerable<KeyValuePair<string, Color>> Elements()
        {
            yield return new KeyValuePair<string, Color>("Fire", Fire);
            yield return new KeyValuePair<string, Color>("Ice", Ice);
            yield return new KeyValuePair<string, Color>("Lightning", Bolt);
            yield return new KeyValuePair<string, Color>("Air", Air);
            yield return new KeyValuePair<string, Color>("Earth", Earth);
            yield return new KeyValuePair<string, Color>("Barrier", Ward);
        }

        public static string RuneOf(string element)
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

        public static Color ColourOf(string element)
        {
            foreach (var e in Elements()) if (e.Key == element) return e.Value;
            return Arcane;
        }

        // ------------------------------------------------------------------ materials

        /// <summary>
        /// Colours as written, deepened: a little more saturated and somewhat darker, so the runes stand out
        /// from the stone. Picked by eye (21 Oct) in pictures that match the web build -- a camera rendering
        /// into a texture; the editor's own game-view capture shows everything paler than the build does.
        /// </summary>
        public static Color Tone(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            return Color.HSVToRGB(h, Mathf.Min(1f, s * 1.15f), v * 0.85f);
        }

        /// <summary>Painted stone (SanctumStone). An edge width of 0 for anything that is not a box.</summary>
        public static Material Stone(string name, Texture2D pattern, float scale, Color side, Color tint2, Color top, Color edge,
                                     float edgeWidth, float grime, float patchScale = 7f, Color? top2 = null, float topPatch = 0f)
        {
            string path = Folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("MageCast/SanctumStone");
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            m.SetTexture("_MainTex", pattern);
            m.SetColor("_Color", Tone(side));
            m.SetColor("_Tint2", Tone(tint2));
            m.SetColor("_TopColor", Tone(top));
            m.SetColor("_TopColor2", Tone(top2 ?? top));
            m.SetFloat("_TopPatch", topPatch);
            m.SetColor("_EdgeColor", Tone(edge));
            m.SetFloat("_EdgeWidth", edgeWidth);
            m.SetFloat("_Scale", scale);
            m.SetFloat("_PatchScale", patchScale);
            m.SetFloat("_Grime", grime);
            m.SetFloat("_Glossiness", 0.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Standard(string name, Color albedo, float metallic, float smoothness, Color emission)
        {
            string path = Folder + "/" + name + ".mat";
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

        public static Material Glow(string name, Color colour, bool radial, float softness, float core)
        {
            string path = Folder + "/" + name + ".mat";
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

        /// <summary>A soft round halo of one colour, made once per name.</summary>
        public static Material Halo(string name, Color colour)
        {
            Material m;
            if (halos.TryGetValue(name, out m)) return m;
            m = Glow(Prefix + "_Halo_" + name, colour, true, 2.2f, 0.25f);
            halos[name] = m;
            return m;
        }

        /// <summary>The painted foliage (MageCast/Foliage): shade below, light above, soft patches between.</summary>
        public static Material Foliage(string name, Color shade, Color light, Color patch)
        {
            string path = Folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("MageCast/Foliage");
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            m.SetColor("_Shade", shade);
            m.SetColor("_Light", light);
            m.SetColor("_Patch", patch);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ pieces

        public static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        /// <summary>A block, solid unless <paramref name="solid"/> is false; local to <paramref name="parent"/>.</summary>
        public static GameObject Box(string name, Transform parent, Material m, Vector3 pos, Vector3 size, float yaw = 0f, bool solid = true)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            Prepare(g);
            return g;
        }

        public static GameObject Deco(string name, Transform parent, Material m, Vector3 pos, Vector3 size, float yaw = 0f)
        {
            return Box(name, parent, m, pos, size, yaw, false);
        }

        public static GameObject Cylinder(string name, Transform parent, Material m, Vector3 pos, float diameter, float height, bool solid = true)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            Static(g);
            return g;
        }

        public static GameObject Crystal(string name, Transform parent, Material m, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Static(go);
            return go;
        }

        /// <summary>A mesh asset placed as a static, collider-free piece of scenery.</summary>
        public static GameObject Piece(string name, Transform parent, Mesh mesh, Material[] materials, Vector3 pos, Quaternion rot, Vector3 scale,
                                       bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Static(go);
            return go;
        }

        /// <summary>A flat soft glow lying on a surface (a quad, local to its parent).</summary>
        public static GameObject HaloQuad(string name, Transform parent, Vector3 pos, Quaternion rot, float size, Material m)
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
            Static(q);
            return q;
        }

        /// <summary>A soft glow that always faces the camera: one particle that never dies.</summary>
        public static void Billboard(string name, Transform parent, Vector3 pos, float size, Color colour)
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
            r.sharedMaterial = Mote;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 5f;
        }

        /// <summary>One of the packs' effects, kept going in the map (AmbientFx).</summary>
        public static void Effect(string name, Transform parent, Vector3 pos, string prefabPath, float scale, float flames = 0f,
                                  params string[] keep)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogWarning("[" + Prefix + "] missing effect " + prefabPath); return; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            AmbientFx fx = go.AddComponent<AmbientFx>();
            fx.prefab = prefab;
            fx.scale = scale;
            fx.flames = flames;
            fx.keep = keep;
        }

        /// <summary>
        /// A band round the outside of a block <paramref name="halfX"/> by <paramref name="halfZ"/> about the parent's
        /// origin (a cornice, a plinth): four pieces, the two on the x faces a touch bigger every way than the two
        /// on the z faces, so where they cross at the corners none of their faces coincide.
        /// </summary>
        public static void Band(string name, Transform parent, Material m, float y, float halfX, float halfZ, float height, float depth,
                                bool solid = false)
        {
            for (int side = 0; side < 4; side++)
            {
                bool odd = side % 2 == 1;
                float grow = odd ? Proud : 0f;
                float along = odd ? halfZ : halfX, off = odd ? halfX : halfZ;
                Transform f = Group(name + "_" + side, parent);
                f.localRotation = Quaternion.Euler(0f, 90f * side, 0f);
                Box(name, f, m, new Vector3(0f, y, off + (depth + grow) * 0.5f),
                    new Vector3((along + depth) * 2f + 2f * grow, height + 2f * grow, depth + grow), 0f, solid);
            }
        }

        /// <summary>
        /// A banner in an element's colour with its rune in gold, hung on a face at <paramref name="at"/> in the
        /// frame <paramref name="w"/>, the face looking along -z of that frame.
        /// </summary>
        public static void Banner(Transform w, Vector3 at, string element, Material cloth, Material metal)
        {
            Transform b = Group("Banner_" + element, w);
            b.localPosition = at;
            Deco("Rod", b, metal, new Vector3(0f, 5.05f, -0.02f), new Vector3(1.3f, 0.08f, 0.08f));
            Deco("Cloth", b, cloth, new Vector3(0f, 3.75f, 0f), new Vector3(1.05f, 2.55f, 0.04f));
            // the swallowtail: two pieces at the bottom, thinner than the cloth so their faces are not its
            Deco("Tail_L", b, cloth, new Vector3(-0.3f, 2.35f, 0f), new Vector3(0.42f, 0.32f, 0.024f));
            Deco("Tail_R", b, cloth, new Vector3(0.3f, 2.35f, 0f), new Vector3(0.42f, 0.32f, 0.024f));
            Vector3 face = b.TransformPoint(new Vector3(0f, 3.85f, -0.035f));
            LineRenderer rune = Glyph("Rune", b, RuneOf(element), face, b.right, b.up, 0.62f, Gold, 0.07f, -b.forward);
            RuneGlow g = b.gameObject.AddComponent<RuneGlow>();
            g.rest = Gold * 0.9f;
            g.breath = 0.15f;
            g.lines = new[] { rune };
        }

        /// <summary>The far hills (MageCast/Distant): a colour up the slopes, snow above a line, faded into the haze.</summary>
        public static Material Distant(string name, Color foot, Color upper, Color haze, float hazeAmount, float height, float snowLine = 10000f)
        {
            string path = Folder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader s = Shader.Find("MageCast/Distant");
            if (m == null) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s;
            m.SetColor("_Base", foot);
            m.SetColor("_Top", upper);
            m.SetColor("_Haze", haze);
            m.SetFloat("_HazeAmount", hazeAmount);
            m.SetFloat("_Height", height);
            m.SetFloat("_SnowLine", snowLine);
            // snow in shade is blue-grey, not paper white
            m.SetColor("_Snow", new Color(0.82f, 0.86f, 0.94f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The Valley's crag texture for rock that is not cut stone (floating islands), or blocks before it exists.</summary>
        public static Texture2D Crag()
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Valley/Valley_Crag.png");
            return t != null ? t : ArenaTempleBuilder.PatternTexture("Assets/Textures/Temple/Temple_Blocks.png", 2, 4, true, 37);
        }

        /// <summary>
        /// A ring of hills or peaks round the map, <paramref name="radius"/> out, facing in: a jagged skyline of
        /// <paramref name="peaks"/> summits between <paramref name="low"/> and <paramref name="high"/> metres, one
        /// mesh (kept as an asset under the map's name), no collider, no shadow.
        /// </summary>
        public static GameObject Ridge(string name, Transform parent, float radius, float depth, float low, float high, int peaks, int seed, Material m,
                                       bool jagged = true)
        {
            var rnd = new System.Random(seed);
            int n = peaks * 8;
            float[] h = new float[n];
            float[] phase = { (float)rnd.NextDouble() * 6.28f, (float)rnd.NextDouble() * 6.28f, (float)rnd.NextDouble() * 6.28f };
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float v = 0.55f * Mathf.Sin(a * peaks + phase[0]) + 0.3f * Mathf.Sin(a * peaks * 2.3f + phase[1])
                        + 0.15f * Mathf.Sin(a * peaks * 5.1f + phase[2]) + ((float)rnd.NextDouble() - 0.5f) * 0.25f;
                if (jagged)
                {
                    // ridged: sharp crests, broad saddles -- mountains
                    v = 1f - Mathf.Abs(v);
                    h[i] = Mathf.Lerp(low, high, Mathf.Clamp01(v * v));
                }
                else h[i] = Mathf.Lerp(low, high, Mathf.Clamp01(v * 0.5f + 0.5f));      // rolling: hills
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // three rows from the foot (further in, below the horizon) to the crest
            for (int i = 0; i <= n; i++)
            {
                int k = i % n;
                float a = i / (float)n * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(dir * (radius - depth) + Vector3.up * -10f);
                verts.Add(dir * (radius - depth * 0.45f) + Vector3.up * (h[k] * 0.55f));
                verts.Add(dir * radius + Vector3.up * h[k]);
            }
            for (int i = 0; i < n; i++)
                for (int r = 0; r < 2; r++)
                {
                    int a = i * 3 + r, b = a + 1, c = a + 3, d = a + 4;
                    Tri(verts, tris, a, c, b);
                    Tri(verts, tris, b, c, d);
                }
            var mesh = new Mesh { name = Prefix + "_" + name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = MeshFolder + "/" + Prefix + "_" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            GameObject go = Piece(name, parent, mesh, new[] { m }, Vector3.zero, Quaternion.identity, Vector3.one, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
            return go;
        }

        /// <summary>A triangle wound to face the middle of the map, whichever way its corners came.</summary>
        static void Tri(List<Vector3> v, List<int> tris, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            Vector3 centre = (v[a] + v[b] + v[c]) / 3f;
            if (Vector3.Dot(normal, new Vector3(-centre.x, 0f, -centre.z)) < 0f) { int s = b; b = c; c = s; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        /// <summary>Fire in a metal bowl, with its glow: the anime pack's flame kept burning.</summary>
        public static void FireBowl(Transform parent, Vector3 at, float size, Material metal)
        {
            Transform t = Group("FireBowl", parent);
            t.localPosition = at;
            Cylinder("Bowl", t, metal, new Vector3(0f, 0.09f * size, 0f), 0.75f * size, 0.18f * size, false);
            Effect("Flame", t, new Vector3(0f, 0.15f * size, 0f), Anime + "Shared/Particles/VFX_Fire.prefab", 1.35f * size, 14f + 4f * size);
            Billboard("Glow", t, new Vector3(0f, 0.6f * size, 0f), 2.6f * size, new Color(1f, 0.55f, 0.2f) * 0.4f);
        }

        // --- glowing lines (LineRenderers in world space, flat on a surface facing <normal>), baked by BakeGlows

        public static LineRenderer NewLine(string name, Transform parent, Color colour, float width, Vector3 normal, bool loop)
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
            l.sharedMaterial = GlowLine;
            l.startColor = l.endColor = colour;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.textureMode = LineTextureMode.Stretch;
            return l;
        }

        public static LineRenderer Line(string name, Transform parent, Vector3[] points, Color colour, float width, Vector3 normal)
        {
            LineRenderer l = NewLine(name, parent, colour, width, normal, false);
            l.positionCount = points.Length;
            l.SetPositions(points);
            return l;
        }

        public static LineRenderer Circle(string name, Transform parent, Vector3 centre, Vector3 normal, float radius, Color colour, float width)
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
        public static LineRenderer Glyph(string name, Transform parent, string rune, Vector3 centre, Vector3 right, Vector3 up, float half,
                                         Color colour, float width, Vector3 normal)
        {
            Vector2[] corners = GestureTemplates.Corners(rune);
            LineRenderer l = NewLine(name, parent, colour, width, normal, false);
            l.positionCount = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                l.SetPosition(i, centre + (right * corners[i].x + up * corners[i].y) * half);
            return l;
        }

        /// <summary>A group of rune lines that breathes and flares when struck (RuneGlow), on <paramref name="holder"/>.</summary>
        public static RuneGlow Runes(GameObject holder, Color rest, float phase, params LineRenderer[] lines)
        {
            RuneGlow g = holder.AddComponent<RuneGlow>();
            g.rest = rest;
            g.phase = phase;
            g.lines = lines;
            return g;
        }

        /// <summary>
        /// Every group of rune lines turned into one mesh -- a soft ribbon along each line, coloured like it --
        /// drawn in one go and lit up by its RuneGlow through a property block. As LineRenderers they were a
        /// draw each, some hundred and thirty of them in the Sanctum.
        /// </summary>
        public static void BakeGlows(Transform look)
        {
            Transform holder = Group("Glows", look);
            foreach (RuneGlow g in look.parent.GetComponentsInChildren<RuneGlow>(true))
            {
                if (g.lines == null || g.lines.Length == 0) continue;
                var v = new List<Vector3>();
                var uv = new List<Vector2>();
                var col = new List<Color>();
                var tris = new List<int>();
                foreach (LineRenderer l in g.lines) if (l != null) Ribbon(l, v, uv, col, tris);

                var mesh = new Mesh { name = "Runes_" + g.name };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetUVs(0, uv);
                mesh.SetColors(col);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();

                var go = new GameObject("Runes_" + g.name);
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = GlowLine;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                foreach (LineRenderer l in g.lines) if (l != null) Object.DestroyImmediate(l.gameObject);
                g.lines = null;
                g.glows = new Renderer[] { r };
            }
            // anything that moves cannot be in a static batch: it would be frozen where it was built
            foreach (FloatingBob b in look.parent.GetComponentsInChildren<FloatingBob>(true))
                foreach (Transform c in b.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(c.gameObject, 0);
        }

        /// <summary>A LineRenderer's line as a flat ribbon (uv.y across it), lying in the plane it faced.</summary>
        static void Ribbon(LineRenderer l, List<Vector3> v, List<Vector2> uv, List<Color> col, List<int> tris)
        {
            int n = l.positionCount;
            if (n < 2) return;
            var p = new Vector3[n];
            l.GetPositions(p);
            bool loop = l.loop;
            Vector3 normal = l.transform.forward;
            float half = l.widthMultiplier * 0.5f;
            Color c = l.startColor;
            int count = loop ? n + 1 : n;
            int start = v.Count;
            for (int i = 0; i < count; i++)
            {
                int k = i % n;
                Vector3 prev = loop ? p[(k - 1 + n) % n] : p[Mathf.Max(0, k - 1)];
                Vector3 next = loop ? p[(k + 1) % n] : p[Mathf.Min(n - 1, k + 1)];
                Vector3 dir = next - prev;
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.right;
                dir.Normalize();
                Vector3 side = Vector3.Cross(normal, dir).normalized;
                float widen = 1f;
                bool end = !loop && (k == 0 || k == n - 1);
                if (!end)
                {
                    // across a corner the ribbon keeps its width
                    Vector3 before = Vector3.Cross(normal, (p[k] - prev).normalized).normalized;
                    widen = 1f / Mathf.Max(0.4f, Vector3.Dot(before, side));
                }
                Vector3 at = p[k];
                // the tips stick out half a width, so a rune's ends are not cut square
                if (!loop && k == 0) at -= dir * half;
                if (!loop && k == n - 1) at += dir * half;
                v.Add(at - side * half * widen);
                v.Add(at + side * half * widen);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(0f, 1f));
                col.Add(c);
                col.Add(c);
                if (i > 0)
                {
                    int a = start + (i - 1) * 2;
                    tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
                }
            }
        }

        // ------------------------------------------------------------------ beyond the walls

        /// <summary>The effect pack's pointed rock, the floating islands' underside.</summary>
        public static Mesh SpikeMesh()
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AoE + "Earth/Meshes/SM_VFX_Pointy_Rock_01.FBX"))
                if (o is Mesh) return (Mesh)o;
            return null;
        }

        /// <summary>A floating rock: the pack's spike turned point-down under a flat top, a crystal on some.</summary>
        public static void Island(Transform t, Vector3 at, float size, System.Random rnd, Material rock, Material crystal, Color glow)
        {
            Mesh spike = SpikeMesh();
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
                Static(go);
            }
            GameObject top = Cylinder("Top", isl, rock, Vector3.zero, size * 1.05f, size * 0.12f, false);
            bool crystalOnTop = crystal != null;
            if (crystalOnTop)
            {
                Crystal("Crystal", isl, crystal, new Vector3(size * 0.15f, size * 0.22f, 0f), new Vector3(size * 0.12f, size * 0.3f, size * 0.12f));
                Billboard("Glow", isl, new Vector3(size * 0.15f, size * 0.25f, 0f), size * 0.6f, glow);
            }
            float height = 0.4f + (float)rnd.NextDouble() * 0.8f, period = 7f + (float)rnd.NextDouble() * 6f;
            float spin = (float)rnd.NextDouble() * 2f - 1f, phase = (float)rnd.NextDouble();
            // only the ones with a crystal drift; the rest stand still, and so batch with each other
            if (!crystalOnTop) return;
            FloatingBob bob = isl.gameObject.AddComponent<FloatingBob>();
            bob.height = height;
            bob.period = period;
            bob.spin = spin;
            bob.phase = phase;
        }

        /// <summary>Specks of light drifting up over the arena, in a box <paramref name="area"/> wide round <paramref name="centre"/>.</summary>
        public static void Motes(Transform t, Vector3 centre, Vector3 area, float rate, int max, float size, params Color[] colours)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(t, false);
            go.transform.localPosition = centre;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size * 1.4f);
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var grad = new Gradient();
            var keys = new GradientColorKey[colours.Length];
            for (int i = 0; i < colours.Length; i++) keys[i] = new GradientColorKey(colours[i], colours.Length == 1 ? 0f : i / (colours.Length - 1f));
            grad.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
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
            r.sharedMaterial = Mote;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ atmosphere

        /// <summary>The one sun, replacing any directional light there was.</summary>
        public static Light Sun(Color colour, float intensity, float shadowStrength, Vector3 euler)
        {
            GameObject oldSun = GameObject.Find("Sun");
            if (oldSun != null) Object.DestroyImmediate(oldSun);
            foreach (Light l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);

            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = colour;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = shadowStrength;
            sun.transform.rotation = Quaternion.Euler(euler);
            RenderSettings.sun = light;
            return light;
        }

        public static void Ambient(Color sky, Color equator, Color ground, float reflections)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            // No fog: the packs' particle shaders are surface shaders with their own blend mode, and Unity fogs
            // them as if they were opaque -- in the distance every particle shows as a square.
            RenderSettings.fog = false;
            // A pale sky mirrored in everything at a glancing angle washes the picture out -- the far floor goes
            // cream and the characters lose their colour. Keep it under half.
            RenderSettings.reflectionIntensity = reflections;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        }

        public struct SkyColours
        {
            public Color zenith, middle, horizon, below, sun, cloud;
            public float sunSize, stars, clouds, cloudCover, cloudHeight, cloudScale, cloudStretch;
        }

        /// <summary>The painted sky (SanctumSky), its sun where the light comes from.</summary>
        public static Material Sky(string name, Light sun, SkyColours c)
        {
            string skyPath = Folder + "/" + name + ".mat";
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            Shader s = Shader.Find("MageCast/SanctumSky");
            if (sky == null) { sky = new Material(s); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.shader = s;
            Vector3 toSun = -sun.transform.forward;
            sky.SetVector("_SunDirection", new Vector4(toSun.x, toSun.y, toSun.z, 0f));
            sky.SetFloat("_SunSize", c.sunSize);
            sky.SetColor("_Zenith", c.zenith);
            sky.SetColor("_Middle", c.middle);
            sky.SetColor("_Horizon", c.horizon);
            sky.SetColor("_Below", c.below);
            sky.SetColor("_SunColour", c.sun);
            sky.SetColor("_CloudColour", c.cloud);
            sky.SetFloat("_Stars", c.stars);
            sky.SetFloat("_Clouds", c.clouds);
            sky.SetFloat("_CloudCover", c.cloudCover);
            sky.SetFloat("_CloudHeight", c.cloudHeight);
            sky.SetFloat("_CloudScale", c.cloudScale);
            sky.SetFloat("_CloudStretch", c.cloudStretch);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            return sky;
        }

        /// <summary>
        /// The scene's ambient light and sky reflection, worked out and stored with it. The editor works them
        /// out by itself as you look; a build has only what was stored, and without this the web build had
        /// neither -- every face out of the sun went near black (21 Oct). Global illumination is off, so this
        /// is the sky alone and takes a second; no lightmaps.
        /// </summary>
        public static void BakeEnvironment(string lightingPath)
        {
            LightingSettings s = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if (s == null)
            {
                s = new LightingSettings { name = System.IO.Path.GetFileNameWithoutExtension(lightingPath) };
                AssetDatabase.CreateAsset(s, lightingPath);
            }
            s.bakedGI = false;
            s.realtimeGI = false;
            s.autoGenerate = false;
            Lightmapping.lightingSettings = s;
            if (!Lightmapping.Bake()) Debug.LogWarning("[" + Prefix + "] could not bake the environment lighting");
            // the materials, meshes and textures made along the way, written out with the scene
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ meshes

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

        /// <summary>Static, so it batches with everything else in its material.</summary>
        public static void Static(GameObject g)
        {
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                                                      | StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>
        /// A block made ready: static, and if its stone has worn edges, its own copy of the cube carrying where
        /// each corner sits in the cube (uv3) and the block's size (uv4) -- SanctumStone reads its edges from
        /// these, since in a static batch the block's own matrix is gone.
        /// </summary>
        public static void Prepare(GameObject g)
        {
            Static(g);
            Renderer r = g.GetComponent<Renderer>();
            MeshFilter f = g.GetComponent<MeshFilter>();
            if (r == null || f == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_EdgeWidth")) return;
            if (r.sharedMaterial.GetFloat("_EdgeWidth") <= 0.001f) return;
            if (f.sharedMesh == null || f.sharedMesh.vertexCount != 24) return;     // a cube, not a cylinder
            f.sharedMesh = BoxMesh(g.transform.lossyScale);
        }

        static Mesh BoxMesh(Vector3 scale)
        {
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            string key = scale.x.ToString("0.###") + "x" + scale.y.ToString("0.###") + "x" + scale.z.ToString("0.###");
            Mesh m;
            if (boxes.TryGetValue(key, out m) && m != null) return m;
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            m = Object.Instantiate(cube);
            m.name = "Block_" + key;
            var at = new List<Vector3>(m.vertices);
            var size = new List<Vector3>();
            for (int i = 0; i < at.Count; i++) size.Add(scale);
            m.SetUVs(2, at);
            m.SetUVs(3, size);
            boxes[key] = m;
            return m;
        }

        /// <summary>Re-dresses every block under <paramref name="root"/> in stone: static, with worn edges where the material has them.</summary>
        public static void PrepareAll(Transform root)
        {
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.shader == Shader.Find("MageCast/SanctumStone")) Prepare(r.gameObject);
        }

        public static void MakeFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            MakeFolder(path.Substring(0, cut));
            AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1));
        }
    }
}
