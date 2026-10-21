using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Finds z-fighting: two surfaces lying in the same plane, facing the same way, overlapping. The depth
    /// buffer cannot tell which is in front, so as the camera moves they flicker through each other in
    /// stripes -- the usual cause is two generated blocks that share a corner (two walls both running to
    /// it) or a trim laid flush on a face.
    ///
    /// Every triangle of every mesh in the open scene, in world space, is bucketed by its plane; within a
    /// bucket, triangles of different renderers are tested for overlap in that plane (separating axes),
    /// counting only overlaps deeper than a couple of centimetres, so blocks that merely touch along an edge
    /// do not count. Additive glows count too: a glow flush on stone flickers the same way.
    /// </summary>
    public static class ArenaOverlapAudit
    {
        /// <summary>Further apart than this, the depth buffer separates them (24-bit, 0.05 m near plane: ~2 mm at 40 m).</summary>
        const float PlaneTolerance = 0.006f;
        /// <summary>Overlaps shallower than this are blocks touching along an edge, not a fight.</summary>
        const float MinOverlap = 0.02f;

        static readonly string[] Scenes =
        {
            "Assets/Scenes/Arena_Blockout.unity", "Assets/Scenes/Arena_Temple.unity",
            "Assets/Scenes/Arena_Crossing.unity", "Assets/Scenes/Arena_Valley.unity",
        };

        struct Tri
        {
            public Vector3 a, b, c, n;
            public float d;
            public int owner;
        }

        [MenuItem("Tools/Arena/Audit Overlaps (open scene)")]
        public static void AuditOpenMenu() { Debug.Log(AuditOpen()); }

        [MenuItem("Tools/Arena/Audit Overlaps (all maps)")]
        public static void AuditAllMenu()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogWarning("[Overlaps] save the open scene first"); return; }
            Debug.Log(AuditAll());
        }

        public static string AuditAll()
        {
            var sb = new StringBuilder();
            foreach (string path in Scenes)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine(AuditOpen());
            }
            return sb.ToString();
        }

        /// <summary>The fights in the open scene, one line per pair of objects, worst (largest) first.</summary>
        public static string AuditOpen(int listAtMost = 40)
        {
            var renderers = new List<Renderer>();
            var seeThrough = new List<bool>();
            var tris = new List<Tri>();
            Terrain ground = Terrain.activeTerrain;      // what is under the turf nobody sees
            foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                MeshFilter f = r.GetComponent<MeshFilter>();
                if (f == null || f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                int owner = renderers.Count;
                renderers.Add(r);
                seeThrough.Add(r.sharedMaterial != null && r.sharedMaterial.renderQueue >= 2500);
                Matrix4x4 m = r.transform.localToWorldMatrix;
                Vector3[] v = f.sharedMesh.vertices;
                int[] t = f.sharedMesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-8f) continue;
                    n.Normalize();
                    tris.Add(new Tri { a = a, b = b, c = c, n = n, d = Vector3.Dot(n, a), owner = owner });
                }
            }

            // bucket by plane: the normal to 1/40, the offset to the tolerance (and look in the neighbours)
            var buckets = new Dictionary<long, List<int>>();
            for (int i = 0; i < tris.Count; i++)
            {
                long key = Key(tris[i].n, Mathf.RoundToInt(tris[i].d / PlaneTolerance));
                List<int> list;
                if (!buckets.TryGetValue(key, out list)) buckets[key] = list = new List<int>();
                list.Add(i);
            }

            var pairs = new Dictionary<long, Vector4>();     // owner pair -> a point on it, and the overlap depth
            foreach (var kv in buckets)
            {
                List<int> here = kv.Value;
                Tri first = tris[here[0]];
                int slot = Mathf.RoundToInt(first.d / PlaneTolerance);
                foreach (int offset in new[] { 0, 1 })
                {
                    List<int> there;
                    if (offset == 0) there = here;
                    else if (!buckets.TryGetValue(Key(first.n, slot + 1), out there)) continue;
                    for (int i = 0; i < here.Count; i++)
                        for (int j = offset == 0 ? i + 1 : 0; j < there.Count; j++)
                        {
                            Tri p = tris[here[i]], q = tris[there[j]];
                            if (p.owner == q.owner) continue;
                            // two glows that write no depth are both drawn whatever their order: no flicker
                            if (seeThrough[p.owner] && seeThrough[q.owner]) continue;
                            if (Vector3.Dot(p.n, q.n) < 0.9995f || Mathf.Abs(p.d - q.d) > PlaneTolerance) continue;
                            float depth = Overlap(p, q);
                            if (depth < MinOverlap) continue;
                            Vector3 at = Middle(p, q);
                            if (ground != null && at.y < ground.SampleHeight(at) + ground.transform.position.y - 0.05f) continue;
                            // the bottom of a block standing on the floor shares its plane with the next
                            // block's bottom, but the floor's top covers both: nobody sees it
                            if (Covered(at, p, tris, buckets)) continue;
                            long pk = p.owner < q.owner ? ((long)p.owner << 32) | (uint)q.owner : ((long)q.owner << 32) | (uint)p.owner;
                            Vector4 old;
                            if (!pairs.TryGetValue(pk, out old) || old.w < depth)
                                pairs[pk] = new Vector4(at.x, at.y, at.z, depth);
                        }
                }
            }

            var sorted = new List<KeyValuePair<long, Vector4>>(pairs);
            sorted.Sort((x, y) => y.Value.w.CompareTo(x.Value.w));
            var sb = new StringBuilder();
            string scene = EditorSceneManager.GetActiveScene().name;
            sb.AppendLine("[Overlaps] " + scene + ": " + sorted.Count + " pairs of surfaces in the same place ("
                          + renderers.Count + " meshes, " + tris.Count + " triangles)");
            for (int i = 0; i < sorted.Count && i < listAtMost; i++)
            {
                long k = sorted[i].Key;
                Vector4 v = sorted[i].Value;
                sb.AppendLine(string.Format("  {0}  <>  {1}   at ({2:0.0}, {3:0.0}, {4:0.0}), {5:0.00} m deep",
                    PathOf(renderers[(int)(k >> 32)].transform), PathOf(renderers[(int)(k & 0xffffffff)].transform),
                    v.x, v.y, v.z, v.w));
            }
            return sb.ToString();
        }

        static long Key(Vector3 n, int slot)
        {
            long x = Mathf.RoundToInt(n.x * 40f) + 64, y = Mathf.RoundToInt(n.y * 40f) + 64, z = Mathf.RoundToInt(n.z * 40f) + 64;
            return (((x * 128 + y) * 128 + z) << 32) ^ (uint)slot;
        }

        /// <summary>
        /// How far two coplanar triangles overlap, in metres: the smallest overlap over the separating axes
        /// (each edge's normal in the plane). 0 or less when they do not.
        /// </summary>
        static float Overlap(Tri p, Tri q)
        {
            Vector3 n = p.n;
            Vector3[] A = { p.a, p.b, p.c }, B = { q.a, q.b, q.c };
            float least = float.MaxValue;
            for (int s = 0; s < 2; s++)
            {
                Vector3[] T = s == 0 ? A : B;
                for (int e = 0; e < 3; e++)
                {
                    Vector3 axis = Vector3.Cross(n, T[(e + 1) % 3] - T[e]);
                    if (axis.sqrMagnitude < 1e-10f) continue;
                    axis.Normalize();
                    float amin = float.MaxValue, amax = float.MinValue, bmin = float.MaxValue, bmax = float.MinValue;
                    foreach (Vector3 a in A) { float d = Vector3.Dot(axis, a); amin = Mathf.Min(amin, d); amax = Mathf.Max(amax, d); }
                    foreach (Vector3 b in B) { float d = Vector3.Dot(axis, b); bmin = Mathf.Min(bmin, d); bmax = Mathf.Max(bmax, d); }
                    float o = Mathf.Min(amax, bmax) - Mathf.Max(amin, bmin);
                    if (o < least) least = o;
                    if (least <= 0f) return least;
                }
            }
            return least;
        }

        /// <summary>A point inside both triangles: the middle of p clipped to q (q's edges as half-planes).</summary>
        static Vector3 Middle(Tri p, Tri q)
        {
            var poly = new List<Vector3> { p.a, p.b, p.c };
            Vector3[] Q = { q.a, q.b, q.c };
            // q's corners in order round its own normal, which is p's
            for (int e = 0; e < 3 && poly.Count > 0; e++)
            {
                Vector3 e0 = Q[e], e1 = Q[(e + 1) % 3];
                Vector3 inward = Vector3.Cross(q.n, e1 - e0);
                var next = new List<Vector3>();
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector3 s = poly[i], f = poly[(i + 1) % poly.Count];
                    float ds = Vector3.Dot(inward, s - e0), df = Vector3.Dot(inward, f - e0);
                    if (ds >= 0f) next.Add(s);
                    if ((ds >= 0f) != (df >= 0f)) next.Add(Vector3.Lerp(s, f, ds / (ds - df)));
                }
                poly = next;
            }
            if (poly.Count == 0) return (p.a + p.b + p.c) / 3f;
            Vector3 sum = Vector3.zero;
            foreach (Vector3 v in poly) sum += v;
            return sum / poly.Count;
        }

        /// <summary>Whether a surface of another mesh faces this one, flush against it, at <paramref name="at"/>.</summary>
        static bool Covered(Vector3 at, Tri p, List<Tri> tris, Dictionary<long, List<int>> buckets)
        {
            Vector3 n = -p.n;
            int slot = Mathf.RoundToInt(-p.d / PlaneTolerance);
            for (int s = slot - 1; s <= slot + 1; s++)
            {
                List<int> list;
                if (!buckets.TryGetValue(Key(n, s), out list)) continue;
                foreach (int i in list)
                {
                    Tri o = tris[i];
                    if (Mathf.Abs(o.d + p.d) > PlaneTolerance) continue;
                    if (Inside(at, o)) return true;
                }
            }
            return false;
        }

        static bool Inside(Vector3 x, Tri t)
        {
            Vector3 c0 = Vector3.Cross(t.b - t.a, x - t.a), c1 = Vector3.Cross(t.c - t.b, x - t.b), c2 = Vector3.Cross(t.a - t.c, x - t.c);
            return Vector3.Dot(c0, t.n) >= -1e-5f && Vector3.Dot(c1, t.n) >= -1e-5f && Vector3.Dot(c2, t.n) >= -1e-5f;
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            for (Transform p = t.parent; p != null && p.parent != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }
    }
}
