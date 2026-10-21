using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Trees for the maps, made here as meshes and kept as assets: a broad-leaved tree (a crown of lumpy
    /// balls on a tapering trunk), a pine (stacked cones), a bush, and ivy hanging down a wall. A tree has
    /// two parts, bark and leaves (submeshes 0 and 1); a bush and ivy are leaves only. All for the Foliage
    /// shader, which reads how high a point sits in the crown from the vertex colour's red, and a random
    /// shade per mesh from its green.
    ///
    /// Low on purpose (a broad tree is about 400 triangles): they stand outside the fight in their dozens,
    /// static, so the web build draws all of one kind in a batch or two.
    /// </summary>
    public static class ArenaTrees
    {
        const string Folder = "Assets/Meshes/Trees";

        public enum Kind { Broad, Pine, Bush, Vine, Reeds }

        /// <summary>One of a few variants of each kind, made once.</summary>
        public static Mesh Get(Kind kind, int variant)
        {
            ArenaKit.MakeFolder(Folder);
            string path = Folder + "/" + kind + "_" + variant + ".asset";
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            var rnd = new System.Random(1000 * (int)kind + variant * 17 + 3);
            var b = new Builder();
            switch (kind)
            {
                case Kind.Broad: Broad(b, rnd); break;
                case Kind.Pine: Pine(b, rnd); break;
                case Kind.Vine: Vine(b, rnd); break;
                case Kind.Reeds: Reeds(b, rnd); break;
                default: Bush(b, rnd); break;
            }
            m = b.Finish(kind + "_" + variant, (float)rnd.NextDouble());
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>A tree standing at <paramref name="at"/> (local), turned and sized, with no collider.</summary>
        public static GameObject Plant(Transform parent, Kind kind, int variant, Vector3 at, float yaw, float scale, Material bark, Material leaves)
        {
            Mesh m = Get(kind, variant);
            return ArenaKit.Piece(kind.ToString(), parent, m, m.subMeshCount > 1 ? new[] { bark, leaves } : new[] { leaves }, at,
                                  Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
        }

        /// <summary>Ivy hanging from <paramref name="top"/> (local) down a wall whose face looks along <paramref name="outward"/>.</summary>
        public static GameObject Ivy(Transform parent, int variant, Vector3 top, Vector3 outward, float scale, Material leaves)
        {
            return ArenaKit.Piece("Ivy", parent, Get(Kind.Vine, variant), new[] { leaves }, top,
                                  Quaternion.LookRotation(outward, Vector3.up), Vector3.one * scale, false);
        }

        // ------------------------------------------------------------------ shapes, about 1 unit = 1 m at scale 1

        static void Broad(Builder b, System.Random rnd)
        {
            float height = 3.2f + (float)rnd.NextDouble() * 1.2f;
            b.Trunk(Vector3.zero, Vector3.up * (height + 0.6f), 0.28f, 0.12f, 6);
            // a branch or two reaching out into the crown
            for (int i = 0; i < 2; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                Vector3 from = Vector3.up * (height * 0.65f);
                b.Trunk(from, from + new Vector3(Mathf.Cos(a) * 1.1f, 1.1f, Mathf.Sin(a) * 1.1f), 0.1f, 0.05f, 5);
            }
            // the crown: one big ball and four or five smaller ones round and over it
            b.leafFrom = height - 0.6f;
            b.leafTo = height + 3.4f;
            b.Blob(new Vector3(0f, height + 1.2f, 0f), new Vector3(2.0f, 1.6f, 2.0f), rnd);
            int n = 4 + rnd.Next(2);
            for (int i = 0; i < n; i++)
            {
                float a = (i + (float)rnd.NextDouble() * 0.5f) / n * Mathf.PI * 2f;
                float r = 1.3f + (float)rnd.NextDouble() * 0.5f;
                float s = 1.0f + (float)rnd.NextDouble() * 0.5f;
                b.Blob(new Vector3(Mathf.Cos(a) * r, height + 0.6f + (float)rnd.NextDouble() * 1.4f, Mathf.Sin(a) * r),
                       new Vector3(s, s * 0.85f, s), rnd);
            }
            b.Blob(new Vector3(0.2f, height + 2.5f, -0.1f), new Vector3(1.2f, 1.0f, 1.2f), rnd);
        }

        static void Pine(Builder b, System.Random rnd)
        {
            float height = 6f + (float)rnd.NextDouble() * 2.5f;
            b.Trunk(Vector3.zero, Vector3.up * (height * 0.8f), 0.24f, 0.08f, 6);
            b.leafFrom = height * 0.18f;
            b.leafTo = height;
            int tiers = 4;
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)tiers;
                float baseY = height * (0.18f + t * 0.62f);
                float radius = Mathf.Lerp(1.9f, 0.7f, t) * (0.9f + (float)rnd.NextDouble() * 0.2f);
                float tall = Mathf.Lerp(2.4f, 1.7f, t);
                b.Cone(Vector3.up * baseY, radius, tall, 9, rnd);
            }
        }

        /// <summary>
        /// Ivy: strands of small leaves hanging from a mat along the top, flat in the plane of a wall -- the top
        /// at the origin, hanging down, the leaves no more than 0.1 m out along +z, so it hangs just off the wall.
        /// </summary>
        static void Vine(Builder b, System.Random rnd)
        {
            b.leafFrom = -4f;
            b.leafTo = 0f;
            int strands = 4 + rnd.Next(3);
            for (int s = 0; s < strands; s++)
            {
                float x = (s - (strands - 1) * 0.5f) * 0.32f + ((float)rnd.NextDouble() - 0.5f) * 0.15f;
                float length = 1.4f + (float)rnd.NextDouble() * 2.4f;
                int leaves = Mathf.RoundToInt(length / 0.2f);
                for (int i = 0; i < leaves; i++)
                {
                    float t = i / (float)leaves;
                    float sway = Mathf.Sin(t * 5f + s * 1.7f) * 0.07f;
                    float size = Mathf.Lerp(0.2f, 0.11f, t) * (0.8f + (float)rnd.NextDouble() * 0.4f);
                    b.Blob(new Vector3(x + sway + ((float)rnd.NextDouble() - 0.5f) * 0.08f, -t * length, size * 0.18f + 0.02f),
                           new Vector3(size * 1.15f, size, size * 0.18f), rnd, 0);      // flat, as a leaf is
                }
            }
            for (int i = 0; i < 4; i++)
                b.Blob(new Vector3((i - 1.5f) * 0.4f, -0.06f, 0.07f), new Vector3(0.2f, 0.13f, 0.06f), rnd, 0);
        }

        /// <summary>A tuft of reeds or tall dry grass: a dozen thin blades leaning out from one spot, about a metre high.</summary>
        static void Reeds(Builder b, System.Random rnd)
        {
            b.leafFrom = 0f;
            b.leafTo = 1.1f;
            int blades = 10 + rnd.Next(5);
            for (int i = 0; i < blades; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = (float)rnd.NextDouble() * 0.12f;
                float lean = 0.1f + (float)rnd.NextDouble() * 0.3f;
                float tall = 0.6f + (float)rnd.NextDouble() * 0.5f;
                Vector3 foot = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 tip = foot + new Vector3(Mathf.Cos(a) * lean, tall, Mathf.Sin(a) * lean);
                b.Blade(foot, tip, 0.025f + (float)rnd.NextDouble() * 0.015f);
            }
        }

        static void Bush(Builder b, System.Random rnd)
        {
            b.leafFrom = 0f;
            b.leafTo = 1.6f;
            int n = 3 + rnd.Next(2);
            for (int i = 0; i < n; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = i == 0 ? 0f : 0.55f + (float)rnd.NextDouble() * 0.3f;
                float s = i == 0 ? 0.9f : 0.55f + (float)rnd.NextDouble() * 0.25f;
                b.Blob(new Vector3(Mathf.Cos(a) * r, s * 0.55f, Mathf.Sin(a) * r), new Vector3(s, s * 0.8f, s), rnd);
            }
        }

        // ------------------------------------------------------------------ mesh building

        class Builder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Color> c = new List<Color>();
            readonly List<int> bark = new List<int>(), leaves = new List<int>();
            public float leafFrom, leafTo = 1f;

            /// <summary>A tapering tube from a to b, its ends open (they are buried in the ground and the crown).</summary>
            public void Trunk(Vector3 a, Vector3 b, float r0, float r1, int sides)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 x = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 z = Vector3.Cross(axis, x);
                int start = v.Count;
                for (int ring = 0; ring < 2; ring++)
                    for (int i = 0; i < sides; i++)
                    {
                        float t = i / (float)sides * Mathf.PI * 2f;
                        Vector3 dir = x * Mathf.Cos(t) + z * Mathf.Sin(t);
                        v.Add((ring == 0 ? a : b) + dir * (ring == 0 ? r0 : r1));
                        n.Add(dir);
                        c.Add(new Color(ring * 0.6f, 0f, 0f));
                    }
                for (int i = 0; i < sides; i++)
                {
                    int i0 = start + i, i1 = start + (i + 1) % sides, j0 = i0 + sides, j1 = i1 + sides;
                    bark.Add(i0); bark.Add(i1); bark.Add(j0);
                    bark.Add(i1); bark.Add(j1); bark.Add(j0);
                }
            }

            /// <summary>A lumpy ball of leaves: an icosphere (subdivided once, or not at all for a small leaf), its corners pushed in and out at random.</summary>
            public void Blob(Vector3 centre, Vector3 radius, System.Random rnd, int detail = 1)
            {
                List<Vector3> sv; List<int> st;
                Icosphere(detail, out sv, out st);
                int start = v.Count;
                foreach (Vector3 p in sv)
                {
                    float bump = 0.82f + (float)rnd.NextDouble() * 0.3f;
                    Vector3 at = centre + Vector3.Scale(p * bump, radius);
                    v.Add(at);
                    n.Add(Vector3.Scale(p, new Vector3(1f / radius.x, 1f / radius.y, 1f / radius.z)).normalized);
                    c.Add(new Color(Mathf.InverseLerp(leafFrom, leafTo, at.y), 0f, 0f));
                }
                foreach (int i in st) leaves.Add(start + i);
            }

            /// <summary>A blade of grass: a thin three-sided spike from <paramref name="foot"/> to <paramref name="tip"/>.</summary>
            public void Blade(Vector3 foot, Vector3 tip, float width)
            {
                Vector3 axis = (tip - foot).normalized;
                Vector3 x = Vector3.Cross(axis, Vector3.forward).normalized;
                if (x.sqrMagnitude < 0.01f) x = Vector3.right;
                Vector3 z = Vector3.Cross(axis, x);
                int start = v.Count;
                for (int i = 0; i < 3; i++)
                {
                    float t = i / 3f * Mathf.PI * 2f;
                    Vector3 dir = x * Mathf.Cos(t) + z * Mathf.Sin(t);
                    v.Add(foot + dir * width);
                    n.Add(dir);
                    c.Add(new Color(0f, 0f, 0f));
                }
                int top = v.Count;
                v.Add(tip);
                n.Add(axis);
                c.Add(new Color(Mathf.InverseLerp(leafFrom, leafTo, tip.y), 0f, 0f));
                for (int i = 0; i < 3; i++)
                {
                    int a = start + i, bb = start + (i + 1) % 3;
                    // wound to face out either way round: a blade is thin, it is seen from all sides
                    leaves.Add(a); leaves.Add(top); leaves.Add(bb);
                    leaves.Add(a); leaves.Add(bb); leaves.Add(top);
                }
            }

            /// <summary>A cone of needles, its rim dipping and rising a little so the tiers do not look turned.</summary>
            public void Cone(Vector3 baseCentre, float radius, float height, int sides, System.Random rnd)
            {
                int start = v.Count;
                for (int i = 0; i < sides; i++)
                {
                    float t = i / (float)sides * Mathf.PI * 2f;
                    float r = radius * (0.85f + (float)rnd.NextDouble() * 0.3f);
                    Vector3 at = baseCentre + new Vector3(Mathf.Cos(t) * r, -(float)rnd.NextDouble() * 0.25f, Mathf.Sin(t) * r);
                    v.Add(at);
                    n.Add(new Vector3(Mathf.Cos(t), radius / height, Mathf.Sin(t)).normalized);
                    c.Add(new Color(Mathf.InverseLerp(leafFrom, leafTo, at.y), 0f, 0f));
                }
                Vector3 tip = baseCentre + Vector3.up * height;
                int apex = v.Count;
                v.Add(tip);
                n.Add(Vector3.up);
                c.Add(new Color(Mathf.InverseLerp(leafFrom, leafTo, tip.y), 0f, 0f));
                // the underside, darker: a flat ring closed at the trunk
                int under = v.Count;
                v.Add(baseCentre + Vector3.up * 0.1f);
                n.Add(Vector3.down);
                c.Add(new Color(0f, 0f, 0f));
                for (int i = 0; i < sides; i++)
                {
                    int a = start + i, b = start + (i + 1) % sides;
                    leaves.Add(a); leaves.Add(apex); leaves.Add(b);
                    leaves.Add(a); leaves.Add(b); leaves.Add(under);
                }
            }

            public Mesh Finish(string name, float shade)
            {
                for (int i = 0; i < c.Count; i++) c[i] = new Color(c[i].r, shade, 0f, 1f);
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetColors(c);
                if (bark.Count == 0) m.SetTriangles(leaves, 0);       // leaves only: one part, one material
                else
                {
                    m.subMeshCount = 2;
                    m.SetTriangles(bark, 0);
                    m.SetTriangles(leaves, 1);
                }
                m.RecalculateBounds();
                return m;
            }
        }

        static List<Vector3> icoV, icoV0;
        static List<int> icoT, icoT0;

        /// <summary>
        /// A unit icosphere, each face's cross product pointing out (Unity's front): subdivided once (42 corners,
        /// 80 faces) at detail 1, the plain icosahedron (12, 20) at 0.
        /// </summary>
        static void Icosphere(int detail, out List<Vector3> verts, out List<int> tris)
        {
            if (icoV == null)
            {
                float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
                var p = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < p.Count; i++) p[i] = p[i].normalized;
                int[] f =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                icoV0 = new List<Vector3>(p);
                icoT0 = new List<int>(f);
                var mid = new Dictionary<long, int>();
                var tl = new List<int>();
                System.Func<int, int, int> middle = (a, b) =>
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int idx;
                    if (mid.TryGetValue(key, out idx)) return idx;
                    p.Add(((p[a] + p[b]) * 0.5f).normalized);
                    mid[key] = p.Count - 1;
                    return p.Count - 1;
                };
                for (int i = 0; i < f.Length; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = middle(a, b), bc = middle(b, c), ca = middle(c, a);
                    tl.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                icoV = p;
                icoT = tl;
            }
            verts = detail > 0 ? icoV : icoV0;
            tris = detail > 0 ? icoT : icoT0;
        }
    }
}
