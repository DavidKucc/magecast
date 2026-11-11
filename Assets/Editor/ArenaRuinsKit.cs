using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// The Ruins' building set: David's own models (made from pictures in ComfyUI, Nov 2026), each one
    /// brought down in Blender from 50-100 thousand triangles to a few thousand with its detail baked into
    /// a colour and a normal texture (tools/ruins_kit_bake.py does it, and writes the measurements used
    /// below), and here given a size in metres and the solids that stand in for it.
    ///
    /// The solids are not the mesh. A model is thousands of triangles of chipped stone; the game wants a
    /// handful of boxes a player slides along and a shot stops at. Their numbers were measured on the
    /// original models (rays down, rays up, and the outline per band of height), in the model's own axes
    /// as Blender has them -- x along, y across, z up, the model about 1 unit big, standing on z = 0 --
    /// and every piece below takes them in that form. The import turns the axes: X = -x, Y = z, Z = -y.
    /// How well they fit is itself measured: Tools > Arena > Audit Ruins Colliders shoots a few thousand
    /// rays at the mesh and at the solids and reports how far apart the two answers are.
    ///
    /// Heights follow the scale contract: 1.2 a jump, 1.6 chest cover, 2.4 a ledge you climb, 2.8 a wall
    /// you do not. Tops nobody is meant to stand on (a capital, a roof) go under a "Look" child, which the
    /// map audit leaves out of its standable spots -- they stay solid.
    /// </summary>
    public static class ArenaRuinsKit
    {
        public const string MeshFolder = "Assets/Meshes/Ruins";
        public const string TexFolder = "Assets/Textures/Ruins";
        public const string MatFolder = "Assets/Materials/Ruins";

        static readonly string[] Models =
        {
            "House", "HouseFar", "Bridge", "ColumnBanded", "ColumnCarved", "WallLow", "WallTall", "WallBrick",
            "StairsMoss", "StairsStone", "PlatformSand", "PlatformBrick", "PlatformMoss",
        };

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        // ------------------------------------------------------------------ import

        /// <summary>Import settings and one material per model. Safe to run again: it only writes what differs.</summary>
        public static void Import()
        {
            ArenaKit.MakeFolder(MatFolder);
            meshes.Clear();
            materials.Clear();
            foreach (string name in Models)
            {
                string path = MeshFolder + "/" + name + ".fbx";
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null) { Debug.LogError("[Ruins] missing model " + path); continue; }
                if (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.importAnimation || mi.importCameras || mi.importLights
                    || mi.importNormals != ModelImporterNormals.Import || mi.importTangents != ModelImporterTangents.CalculateMikk
                    || mi.animationType != ModelImporterAnimationType.None)
                {
                    mi.materialImportMode = ModelImporterMaterialImportMode.None;
                    mi.importAnimation = false;
                    mi.importCameras = false;
                    mi.importLights = false;
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.importTangents = ModelImporterTangents.CalculateMikk;
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.SaveAndReimport();
                }
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Mesh) { meshes[name] = (Mesh)o; break; }

                // the two big ones keep their colour at full size: a 10 m house on a 1024 texture is 100 px a metre
                bool big = name == "House" || name == "Bridge";
                Texture2D albedo = Texture(name + "_albedo.png", false, big ? 2048 : 1024);
                Texture2D normal = Texture(name + "_normal.png", true, name == "HouseFar" ? 512 : 1024);

                string matPath = MatFolder + "/Ruins_" + name + ".mat";
                Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, matPath); }
                m.shader = Shader.Find("Standard");
                m.color = Color.white;
                m.SetTexture("_MainTex", albedo);
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
                m.SetFloat("_BumpScale", 1f);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Glossiness", 0.08f);
                // painted, not polished: no sky mirrored in it, no highlight (a pale sky at a glancing angle
                // washed the Sanctum's stone out the same way)
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_GlossyReflections", 0f);
                m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                EditorUtility.SetDirty(m);
                materials[name] = m;
            }
            AssetDatabase.SaveAssets();
        }

        public static Texture2D Texture(string file, bool normal, int size, bool repeat = false)
        {
            string path = TexFolder + "/" + file;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogError("[Ruins] missing texture " + path); return null; }
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            TextureWrapMode wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            int aniso = repeat ? 8 : 4;
            if (ti.textureType != type || ti.maxTextureSize != size || ti.wrapMode != wrap || ti.anisoLevel != aniso || !ti.mipmapEnabled)
            {
                ti.textureType = type;
                ti.maxTextureSize = size;
                ti.wrapMode = wrap;
                ti.anisoLevel = aniso;
                ti.mipmapEnabled = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Mesh MeshOf(string name)
        {
            Mesh m;
            if (!meshes.TryGetValue(name, out m) || m == null)
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MeshFolder + "/" + name + ".fbx"))
                    if (o is Mesh) { m = (Mesh)o; break; }
                meshes[name] = m;
            }
            return m;
        }

        static Material MaterialOf(string name)
        {
            Material m;
            if (!materials.TryGetValue(name, out m) || m == null)
            {
                m = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Ruins_" + name + ".mat");
                materials[name] = m;
            }
            return m;
        }

        // ------------------------------------------------------------------ a piece being put together

        /// <summary>The model at its size, and the solids added to it one by one, in the model's own axes.</summary>
        public sealed class Body
        {
            public readonly Transform root;
            readonly Vector3 s;        // metres per model unit along the root's x, y, z
            readonly float sink;       // how far the model stands below the root, in metres
            readonly bool solid;
            Transform look;

            internal Body(Transform root, Vector3 scale, float sink, bool solid)
            {
                this.root = root;
                s = scale;
                this.sink = sink;
                this.solid = solid;
            }

            Vector3 At(float x, float y, float z) { return new Vector3(-x * s.x, z * s.y - sink, -y * s.z); }

            GameObject New(bool place)
            {
                var g = new GameObject("Solid");
                if (!place && look == null)
                {
                    look = new GameObject("Look").transform;
                    look.SetParent(root, false);
                }
                g.transform.SetParent(place ? root : look, false);
                return g;
            }

            /// <summary>A box by its extents. <paramref name="place"/>: whether its top is somewhere to stand.</summary>
            public void Box(float x0, float x1, float y0, float y1, float z0, float z1, bool place = true)
            {
                if (!solid) return;
                GameObject g = New(place);
                g.transform.localPosition = At((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                g.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(x1 - x0) * s.x, Mathf.Abs(z1 - z0) * s.y, Mathf.Abs(y1 - y0) * s.z);
            }

            /// <summary>A box centred on the model's axis, <paramref name="half"/> to every side.</summary>
            public void Square(float half, float z0, float z1, bool place = true) { Box(-half, half, -half, half, z0, z1, place); }

            /// <summary>The four strips of a rim between <paramref name="inner"/> and <paramref name="outer"/>.</summary>
            public void Rim(float inner, float outer, float z0, float z1)
            {
                Box(-outer, -inner, -outer, outer, z0, z1);
                Box(inner, outer, -outer, outer, z0, z1);
                Box(-inner, inner, -outer, -inner, z0, z1);
                Box(-inner, inner, inner, outer, z0, z1);
            }

            /// <summary>Four corner posts.</summary>
            public void Corners(float inner, float outer, float z0, float z1)
            {
                foreach (float a in new[] { -1f, 1f })
                    foreach (float b in new[] { -1f, 1f })
                        Box(a * inner, a * outer, b * inner, b * outer, z0, z1);
            }

            /// <summary>An upright round shaft on the model's axis.</summary>
            public void Shaft(float z0, float z1, float radius, bool place = false)
            {
                if (!solid) return;
                GameObject g = New(place);
                g.transform.localPosition = At(0f, 0f, (z0 + z1) * 0.5f);
                var c = g.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = radius * (s.x + s.z) * 0.5f;
                c.height = (z1 - z0) * s.y;
            }

            /// <summary>A round swelling on the model's axis: a column's foot.</summary>
            public void Ball(float z, float radius)
            {
                if (!solid) return;
                GameObject g = New(false);
                g.transform.localPosition = At(0f, 0f, z);
                g.AddComponent<SphereCollider>().radius = radius * (s.x + s.z) * 0.5f;
            }

            /// <summary>
            /// A stair's walking surface: a slab whose top runs straight from one point to the other, over the
            /// noses of the steps. The steps under it stay as solids (the stair's sides and face), but a body
            /// going up 20 cm risers one by one is jolted at every one of them, the map audit cannot stand it
            /// anywhere on a flight (its body test touches the next riser), and a slope is what every other
            /// stair in the game walks like.
            /// </summary>
            public void RampAlongY(float ya, float za, float yb, float zb, float xHalf) { Ramp(At(0f, ya, za), At(0f, yb, zb), 2f * xHalf * s.x); }
            public void RampAlongX(float xa, float za, float xb, float zb, float yHalf) { Ramp(At(xa, 0f, za), At(xb, 0f, zb), 2f * yHalf * s.z); }

            void Ramp(Vector3 a, Vector3 b, float width)
            {
                if (!solid) return;
                const float thick = 0.3f;
                GameObject g = New(true);
                Quaternion rot = Quaternion.LookRotation(b - a, Vector3.up);
                g.transform.localRotation = rot;
                g.transform.localPosition = (a + b) * 0.5f - rot * Vector3.up * (thick * 0.5f);
                g.AddComponent<BoxCollider>().size = new Vector3(width, thick, (b - a).magnitude);
            }

            /// <summary>A slanted slab running from (xa, za) to (xb, zb), between y0 and y1 across.</summary>
            public void Slab(float xa, float za, float xb, float zb, float y0, float y1, float thick)
            {
                if (!solid) return;
                GameObject g = New(false);
                Vector3 a = At(xa, (y0 + y1) * 0.5f, za), b = At(xb, (y0 + y1) * 0.5f, zb);
                g.transform.localPosition = (a + b) * 0.5f;
                g.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
                g.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(y1 - y0) * s.z, thick * s.y, (b - a).magnitude);
            }
        }

        static Body Make(Transform parent, string name, string model, Vector3 pos, float yaw, Vector3 scale, bool solid, float sink = 0f)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var m = new GameObject("Model");
            m.transform.SetParent(root, false);
            m.transform.localPosition = new Vector3(0f, -sink, 0f);
            m.transform.localScale = scale;
            m.AddComponent<MeshFilter>().sharedMesh = MeshOf(model);
            m.AddComponent<MeshRenderer>().sharedMaterial = MaterialOf(model);
            ArenaKit.Static(m);
            return new Body(root, scale, sink, solid);
        }

        // ------------------------------------------------------------------ the pieces

        /// <summary>The house is 10 m to its ridge; its paving slab, half a metre thick in the model, is sunk to a kerb.</summary>
        public const float HouseScale = 10.04f, HouseSink = 0.42f;
        /// <summary>Its footprint, slab included: 8.6 m along x, 9.2 m along z. The door looks along +z.</summary>
        public static readonly Vector2 HouseFootprint = new Vector2(0.86f * HouseScale, 0.918f * HouseScale);

        public static Body House(Transform parent, Vector3 pos, float yaw, bool solid, float scale = HouseScale)
        {
            Body b = Make(parent, "House", solid ? "House" : "HouseFar", pos, yaw, Vector3.one * scale, solid, HouseSink * scale / HouseScale);
            b.Box(-0.43f, 0.43f, -0.458f, 0.458f, 0f, 0.05f);                       // the slab: a kerb you walk onto
            // the stone ground floor: its walls stand at 0.25 (most of their area, measured) with door posts,
            // sills and a sign out to 0.29, the piers on its corners at 0.29, and its top third steps out to
            // carry the floor above. One box between the two: at 0.25 shots sank 0.32 m into the stonework,
            // at 0.29 they stopped 0.46 m short of the wall.
            b.Box(-0.27f, 0.27f, -0.265f, 0.265f, 0.05f, 0.36f, false);
            b.Corners(0.19f, 0.29f, 0.05f, 0.36f);
            b.Box(-0.29f, 0.29f, -0.285f, 0.285f, 0.36f, 0.49f, false);
            b.Box(-0.33f, 0.33f, -0.325f, 0.325f, 0.49f, 0.62f, false);             // the timbered floor, to the eaves
            // the roof, a gable along y, in steps; the dormers' ridge across it; the chimney
            b.Box(-0.26f, 0.26f, -0.37f, 0.37f, 0.62f, 0.70f, false);
            b.Box(-0.17f, 0.17f, -0.37f, 0.37f, 0.70f, 0.80f, false);
            b.Box(-0.09f, 0.09f, -0.37f, 0.37f, 0.80f, 0.90f, false);
            b.Box(-0.035f, 0.035f, -0.37f, 0.37f, 0.90f, 0.985f, false);
            b.Box(-0.345f, 0.345f, -0.07f, 0.07f, 0.62f, 0.80f, false);
            b.Box(0.11f, 0.22f, 0.05f, 0.19f, 0.75f, 0.96f, false);
            return b;
        }

        /// <summary>
        /// The bridge at the size where the stone sills at its two ends stand 2.4 m up: 8.5 m long, 4.3 m
        /// wide, its deck at 2.2 m, its corner towers at 3.3 m. Under the deck two timber arches spring from
        /// the legs; between the legs it is open across -- 1.5 m of headroom at the middle, so the way under
        /// it has to be dug (the Ruins' sunken lane, 1.2 m down).
        /// </summary>
        public const float BridgeScale = 2.4f / 0.281f;
        public const float BridgeDeck = 0.259f * BridgeScale, BridgeLength = 1f * BridgeScale, BridgeWidth = 0.507f * BridgeScale;
        /// <summary>Where the sill ends, measured from the middle: a stair's top edge goes here.</summary>
        public const float BridgeSillEnd = 0.462f * BridgeScale;
        /// <summary>Clear ground between the feet, from the middle.</summary>
        public const float BridgeLegInner = 0.33f * BridgeScale;

        public static Body Bridge(Transform parent, Vector3 pos, float yaw)
        {
            Body b = Make(parent, "Bridge", "Bridge", pos, yaw, Vector3.one * BridgeScale, true);
            foreach (float e in new[] { -1f, 1f })
            {
                // an end: a wall from the ground to the deck between two towers, the sill on it reaching a
                // little further out, a foot under each tower
                b.Box(e * 0.335f, e * 0.44f, -0.10f, 0.10f, 0f, 0.259f);
                b.Box(e * 0.335f, e * 0.462f, -0.10f, 0.10f, 0.259f, 0.281f);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Box(e * 0.34f, e * 0.47f, side * 0.10f, side * 0.22f, 0f, 0.385f);
                    b.Box(e * 0.33f, e * 0.50f, side * 0.09f, side * 0.253f, 0f, 0.10f, false);
                }
            }
            b.Box(-0.335f, 0.335f, -0.19f, 0.19f, 0.232f, 0.259f);                  // the deck
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(-0.34f, 0.34f, side * 0.139f, side * 0.19f, 0.259f, 0.324f, false);       // a parapet
                // an arch under it: measured underside 0.178 at the middle, 0.122 a quarter of the way out
                foreach (float e in new[] { -1f, 1f })
                {
                    b.Slab(e * 0.335f, 0.07f, e * 0.25f, 0.137f, side * 0.14f, side * 0.19f, 0.03f);
                    b.Slab(e * 0.25f, 0.137f, 0f, 0.193f, side * 0.14f, side * 0.19f, 0.03f);
                }
            }
            return b;
        }

        /// <summary>A column: 5.2 m, its base 2 m square, its shaft 1.2-1.3 m through.</summary>
        public static Body Column(Transform parent, Vector3 pos, float yaw, bool banded, float height = 5.2f)
        {
            float w = (banded ? 4.2f : 4.6f) * height / 5.2f;
            Body b = Make(parent, banded ? "Column_Banded" : "Column_Carved", banded ? "ColumnBanded" : "ColumnCarved", pos, yaw,
                          new Vector3(w, height, w), true);
            if (banded)
            {
                b.Square(0.247f, 0f, 0.14f);
                b.Ball(0.19f, 0.20f);
                b.Shaft(0.20f, 0.92f, 0.158f);
                b.Square(0.201f, 0.85f, 0.999f, false);
            }
            else
            {
                b.Square(0.214f, 0f, 0.10f);
                b.Square(0.187f, 0.10f, 0.145f);
                b.Ball(0.19f, 0.172f);
                b.Shaft(0.20f, 0.90f, 0.136f);
                b.Square(0.179f, 0.85f, 0.997f, false);
            }
            return b;
        }

        /// <summary>The long low wall: 4.6 m by 0.8 m at chest height. Runs along local x.</summary>
        public static Body WallLow(Transform parent, Vector3 pos, float yaw, float height = 1.6f)
        {
            Body b = Make(parent, "Wall_Low", "WallLow", pos, yaw, Vector3.one * (height / 0.344f), true);
            b.Box(-0.497f, 0.497f, -0.09f, 0.09f, 0f, 0.045f);
            b.Box(-0.479f, 0.479f, -0.072f, 0.072f, 0.045f, 0.305f);
            b.Box(-0.497f, 0.497f, -0.09f, 0.09f, 0.305f, 0.344f);
            return b;
        }

        /// <summary>
        /// The brick wall, 1.25 m by default: something to hop over, and the lining of the sunken lane.
        /// Runs along local x, 2.9 m long unless <paramref name="length"/> says otherwise.
        /// </summary>
        public static Body WallBrick(Transform parent, Vector3 pos, float yaw, float height = 1.25f, float length = 0f)
        {
            float k = height / 0.427f;
            Body b = Make(parent, "Wall_Brick", "WallBrick", pos, yaw, new Vector3(length > 0f ? length / 1.001f : k, k, k), true);
            b.Box(-0.5f, 0.5f, -0.129f, 0.129f, 0f, 0.08f);
            b.Box(-0.474f, 0.474f, -0.105f, 0.105f, 0.08f, 0.38f);
            b.Box(-0.5f, 0.5f, -0.129f, 0.129f, 0.38f, 0.427f);
            return b;
        }

        /// <summary>The tall plastered piece: 2.8 m, too high to climb, 1.4 m wide along local z, 0.6 m thick.</summary>
        public static Body WallTall(Transform parent, Vector3 pos, float yaw, float height = 2.8f)
        {
            Body b = Make(parent, "Wall_Tall", "WallTall", pos, yaw, Vector3.one * (height / 0.996f), true);
            b.Box(-0.107f, 0.107f, -0.25f, 0.25f, 0f, 0.19f);
            b.Box(-0.078f, 0.078f, -0.224f, 0.224f, 0.19f, 0.94f, false);
            b.Box(-0.106f, 0.106f, -0.256f, 0.256f, 0.94f, 0.996f, false);
            return b;
        }

        /// <summary>
        /// The grey stair: nine steps and a landing. Its top is at local -z, the foot at +z; <paramref name="pos"/>
        /// is the middle of its footprint.
        /// </summary>
        public static Body StairsStone(Transform parent, Vector3 pos, float yaw, float width, float length, float height)
        {
            Body b = Make(parent, "Stairs_Stone", "StairsStone", pos, yaw, new Vector3(width / 0.553f, height / 0.753f, length / 0.999f), true);
            float[] front = { -0.499f, -0.411f, -0.337f, -0.254f, -0.176f, -0.112f, -0.034f, 0.044f, 0.119f, 0.196f };
            float[] top = { 0.091f, 0.170f, 0.241f, 0.317f, 0.392f, 0.460f, 0.533f, 0.604f, 0.678f, 0.753f };
            for (int k = 0; k < front.Length; k++)
                b.Box(-0.276f, 0.276f, front[k], k + 1 < front.Length ? front[k + 1] : 0.499f, 0f, top[k]);
            // the slope over the steps' noses, carried on down to the ground in front of the first step
            float rise = (top[top.Length - 1] - top[0]) / (front[front.Length - 1] - front[0]);
            b.RampAlongY(front[0] - top[0] / rise, 0f, front[front.Length - 1], top[top.Length - 1], 0.276f);
            return b;
        }

        /// <summary>
        /// The mossy stair between two kerbs: eleven steps and a landing. Its top is at local +x, the foot at
        /// -x; <paramref name="pos"/> is the middle of its footprint.
        /// </summary>
        public static Body StairsMoss(Transform parent, Vector3 pos, float yaw, float width, float length, float height)
        {
            Body b = Make(parent, "Stairs_Moss", "StairsMoss", pos, yaw, new Vector3(length / 0.995f, height / 0.585f, width / 0.393f), true);
            float[] end = { -0.193f, -0.155f, -0.093f, -0.031f, 0.031f, 0.093f, 0.143f, 0.218f, 0.280f, 0.342f, 0.392f, 0.466f };
            float[] top = { 0.585f, 0.528f, 0.474f, 0.427f, 0.381f, 0.341f, 0.293f, 0.240f, 0.198f, 0.149f, 0.100f, 0.051f };
            for (int k = 0; k < end.Length; k++)
            {
                float x0 = k == 0 ? -0.4975f : end[k - 1];
                b.Box(x0, end[k], -0.14f, 0.14f, 0f, top[k]);
                // the kerbs follow the steps, a little above them
                float kerb = top[k] + (k == 0 ? 0.007f : 0.053f);
                b.Box(x0, end[k], -0.196f, -0.14f, 0f, kerb, k == 0);
                b.Box(x0, end[k], 0.14f, 0.196f, 0f, kerb, k == 0);
            }
            // the slope between the kerbs, from the landing's edge over the noses to the ground past the last step
            float fall = (top[0] - top[top.Length - 1]) / (end[end.Length - 1] - end[0]);
            b.RampAlongX(end[end.Length - 1] + top[top.Length - 1] / fall, 0f, end[0], top[0], 0.14f);
            return b;
        }

        /// <summary>The sandstone terrace: a floor with a low rim, <paramref name="height"/> to the rim's top.</summary>
        public static Body PlatformSand(Transform parent, Vector3 pos, float yaw, float size = 6f, float height = 2.4f)
        {
            Body b = Make(parent, "Platform_Sand", "PlatformSand", pos, yaw, new Vector3(size / 0.995f, height / 0.528f, size / 0.995f), true);
            b.Square(0.492f, 0f, 0.115f);
            // the panels between the corner piers stand 22 cm back from them
            b.Square(0.433f, 0.115f, 0.40f);
            b.Corners(0.36f, 0.466f, 0.115f, 0.40f);
            b.Square(0.497f, 0.40f, 0.514f);
            b.Rim(0.345f, 0.497f, 0.514f, 0.528f);
            b.Corners(0.44f, 0.497f, 0.528f, 0.552f);
            return b;
        }

        /// <summary>The mossy terrace: its rim stands 18 cm over the floor inside, <paramref name="height"/> to the rim's top.</summary>
        public static Body PlatformMoss(Transform parent, Vector3 pos, float yaw, float size = 6f, float height = 2.4f)
        {
            Body b = Make(parent, "Platform_Moss", "PlatformMoss", pos, yaw, new Vector3(size / 0.999f, height / 0.489f, size / 0.998f), true);
            b.Square(0.495f, 0f, 0.085f);
            b.Square(0.45f, 0.085f, 0.125f);
            // as on the sandstone one, the panels stand back from the corner piers
            b.Square(0.426f, 0.125f, 0.29f);
            b.Corners(0.37f, 0.46f, 0.125f, 0.29f);
            b.Square(0.452f, 0.29f, 0.375f);
            b.Corners(0.37f, 0.473f, 0.29f, 0.375f);
            b.Square(0.499f, 0.375f, 0.452f);
            b.Rim(0.395f, 0.499f, 0.452f, 0.489f);
            return b;
        }

        /// <summary>The low brick dais with corner posts: something to jump onto, <paramref name="height"/> to its rim.</summary>
        public static Body PlatformBrick(Transform parent, Vector3 pos, float yaw, float size = 4f, float height = 1.2f)
        {
            Body b = Make(parent, "Platform_Brick", "PlatformBrick", pos, yaw, new Vector3(size / 0.993f, height / 0.380f, size / 0.993f), true);
            b.Square(0.465f, 0f, 0.361f);
            b.Rim(0.37f, 0.47f, 0.361f, 0.380f);
            b.Corners(0.35f, 0.496f, 0f, 0.407f);          // the corner posts, from the ground up
            return b;
        }

        // ------------------------------------------------------------------ how well the solids fit

        /// <summary>
        /// Measures every piece's solids against its mesh, at the points of the mesh itself:
        ///   deep   the point is outside every solid -- how far a shot travels into visible stone before it stops
        ///   early  the point is inside a solid -- how deep, which is how far in front of it a shot stops in clear air
        /// Both in metres. "in play" is the part up to 3 m, where spells fly.
        /// </summary>
        [MenuItem("Tools/Arena/Audit Ruins Colliders")]
        public static void AuditFitMenu() { Debug.Log(AuditFit()); }

        public static string AuditFit()
        {
            Import();
            var holder = new GameObject("RuinsKit_FitAudit");
            holder.transform.position = new Vector3(900f, 0f, 900f);
            var sb = new System.Text.StringBuilder("[Ruins] solids against the mesh, at the middle of every triangle that can be seen (metres): 95% / 99% of them\n");
            try
            {
                Transform t = holder.transform;
                var pieces = new List<Body>
                {
                    House(t, Vector3.zero, 0f, true), Bridge(t, new Vector3(40f, 0f, 0f), 0f),
                    Column(t, new Vector3(80f, 0f, 0f), 0f, true), Column(t, new Vector3(120f, 0f, 0f), 0f, false),
                    WallLow(t, new Vector3(160f, 0f, 0f), 0f), WallBrick(t, new Vector3(200f, 0f, 0f), 0f), WallTall(t, new Vector3(240f, 0f, 0f), 0f),
                    StairsStone(t, new Vector3(280f, 0f, 0f), 0f, 1.8f, 4.4f, 2.4f), StairsMoss(t, new Vector3(320f, 0f, 0f), 0f, 2.4f, 5f, 2.4f),
                    PlatformSand(t, new Vector3(360f, 0f, 0f), 0f), PlatformMoss(t, new Vector3(400f, 0f, 0f), 0f), PlatformBrick(t, new Vector3(440f, 0f, 0f), 0f),
                };
                Physics.SyncTransforms();
                const float probe = 1.5f;
                foreach (Body b in pieces)
                {
                    Transform model = b.root.Find("Model");
                    Collider[] solids = b.root.GetComponentsInChildren<Collider>();
                    Mesh mesh = model.GetComponent<MeshFilter>().sharedMesh;
                    // sampled at the middle of every triangle, along the triangle's own facing (vertex normals are
                    // smoothed across edges and point every which way at a crease)
                    Vector3[] mv = mesh.vertices; int[] mt = mesh.triangles;
                    var vertsList = new List<Vector3>(); var normalsList = new List<Vector3>();
                    for (int k = 0; k < mt.Length; k += 3)
                    {
                        Vector3 a = model.TransformPoint(mv[mt[k]]), b1 = model.TransformPoint(mv[mt[k + 1]]), c1 = model.TransformPoint(mv[mt[k + 2]]);
                        Vector3 g = Vector3.Cross(b1 - a, c1 - a);
                        if (g.sqrMagnitude < 1e-10f) continue;
                        vertsList.Add((a + b1 + c1) / 3f); normalsList.Add(g.normalized);
                    }
                    Vector3[] verts = vertsList.ToArray(), normals = normalsList.ToArray();
                    float floor = b.root.position.y + 0.03f;

                    // Only what can be seen: the slab under the house's walls, the foot of a wall inside its
                    // plinth are mesh too, and deep inside the solids by rights. A point counts if a ray from
                    // it gets clear of the mesh, straight out or 60 degrees off that.
                    foreach (Collider c in solids) c.enabled = false;
                    MeshCollider skin = model.gameObject.AddComponent<MeshCollider>();
                    skin.sharedMesh = mesh;
                    Physics.SyncTransforms();
                    var seen = new bool[verts.Length];
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 p = verts[i];
                        Vector3 n = normals[i];
                        Vector3 side = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                        for (int k = 0; k < 7 && !seen[i]; k++)
                        {
                            Vector3 d = k == 0 ? n : Quaternion.AngleAxis(k * 60f, n) * (Quaternion.AngleAxis(60f, side) * n);
                            if (d.y < -0.2f) continue;              // not out through the ground
                            seen[i] = !Physics.Raycast(p + n * 0.01f, d, 6f, ~0, QueryTriggerInteraction.Ignore);
                        }
                    }
                    Object.DestroyImmediate(skin);
                    foreach (Collider c in solids) c.enabled = true;
                    Physics.SyncTransforms();

                    var deepPlay = new List<float>(); var earlyPlay = new List<float>();
                    var deepAll = new List<float>(); var earlyAll = new List<float>();
                    for (int i = 0; i < verts.Length; i++)
                    {
                        if (!seen[i]) continue;
                        Vector3 p = verts[i];
                        if (p.y < floor) continue;                  // under the ground: not there to be hit
                        Vector3 n = normals[i];
                        // skip what faces the ground: nothing reaches the underside of a foot
                        if (p.y < floor + 0.1f && n.y < -0.5f) continue;
                        bool play = p.y - b.root.position.y <= 3f;
                        float nearest = float.MaxValue;
                        foreach (Collider c in solids)
                        {
                            float d = Vector3.Distance(c.ClosestPoint(p), p);
                            if (d < nearest) nearest = d;
                        }
                        if (nearest > 0.0005f)
                        {
                            deepAll.Add(nearest);
                            if (play) deepPlay.Add(nearest);
                            earlyAll.Add(0f);
                            if (play) earlyPlay.Add(0f);
                            continue;
                        }
                        // inside a solid: how deep, the shortest way out in any of 26 directions, walked in 2 cm
                        // steps. (Not along the surface's own facing: a window sill faces up, and up from a sill
                        // is three metres of wall.)
                        deepAll.Add(0f);
                        if (play) deepPlay.Add(0f);
                        float outBy = probe;
                        for (float d = 0.02f; d < probe && outBy >= probe; d += 0.02f)
                            foreach (Vector3 dir in Ways)
                            {
                                Vector3 q = p + dir * d;
                                bool inside = false;
                                foreach (Collider c in solids)
                                    if ((c.ClosestPoint(q) - q).sqrMagnitude < 1e-8f) { inside = true; break; }
                                if (!inside) { outBy = d; break; }
                            }
                        earlyAll.Add(outBy);
                        if (play) earlyPlay.Add(outBy);
                    }
                    deepPlay.Sort(); earlyPlay.Sort(); deepAll.Sort(); earlyAll.Sort();
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "  {0,-15} in play: deep {1:F2} / {2:F2}, early {3:F2} / {4:F2}     whole: deep {5:F2} / {6:F2}, early {7:F2} / {8:F2}   ({9} points)\n",
                        b.root.name, Pct(deepPlay, 0.95f), Pct(deepPlay, 0.99f), Pct(earlyPlay, 0.95f), Pct(earlyPlay, 0.99f),
                        Pct(deepAll, 0.95f), Pct(deepAll, 0.99f), Pct(earlyAll, 0.95f), Pct(earlyAll, 0.99f), deepAll.Count);
                }
            }
            finally { Object.DestroyImmediate(holder); }
            return sb.ToString();
        }

        static Vector3[] ways;
        static Vector3[] Ways
        {
            get
            {
                if (ways != null) return ways;
                var list = new List<Vector3>();
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++)
                            if (x != 0 || y != 0 || z != 0) list.Add(new Vector3(x, y, z).normalized);
                return ways = list.ToArray();
            }
        }

        static float Pct(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0f;
            return sorted[Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
        }
    }
}
