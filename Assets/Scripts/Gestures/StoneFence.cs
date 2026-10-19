using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Earth's patch: a ring of stone spikes about a metre high round where it landed (19 Oct, in place
    /// of spikes that cut and slowed -- one more patch that took life a second). It is ground made into a
    /// shape, not harm: nobody walks through it, out of it or into it, and it stops spells flying low.
    /// It can be jumped -- a running jump clears it -- but not while drawing. Earth throws over the
    /// ground, and then changes it.
    ///
    /// Built by its patch (SpellZone, Rubble) on every machine from the same centre and the same outline,
    /// so it stands in the same place everywhere: each machine flies its own copies of spells and each
    /// player's machine moves their own body, so each needs the stones. It runs along the patch's edge --
    /// cut where the ground ends, so against a wall it hugs the wall's foot and on a ledge it is a rail --
    /// and goes back into the ground with its patch.
    ///
    /// Its top is not floor: a spell landing on it hits a wall (Projectile), so nothing is left lying on
    /// top of stones that are about to sink.
    /// </summary>
    public class StoneFence : MonoBehaviour
    {
        public const float Height = 1f;
        const float Thickness = 0.3f;
        const int Segments = 32;
        const float RiseTime = 0.25f;
        const float SinkTime = 0.45f;

        /// <summary>A spike every this many metres along the ring.</summary>
        const float SpikeEvery = 0.36f;

        SpellZone zone;
        readonly List<Transform> pieces = new List<Transform>();
        readonly List<Vector3> pieceAt = new List<Vector3>();
        readonly List<float> pieceLength = new List<float>();
        readonly List<float> pieceDelay = new List<float>();
        readonly List<BoxCollider> walls = new List<BoxCollider>();
        float age;
        bool standing;

        public static StoneFence Build(SpellZone zone, Vector3 centre, float[] reach, float[] heights, int rings)
        {
            var go = new GameObject("StoneFence");
            go.transform.SetParent(zone.transform, true);
            go.transform.position = centre;
            StoneFence f = go.AddComponent<StoneFence>();
            f.zone = zone;

            int spokes = reach.Length;
            int step = Mathf.Max(1, spokes / Segments);
            var corners = new List<Vector3>();
            for (int i = 0; i < spokes; i += step)
            {
                float a = i / (float)spokes * Mathf.PI * 2f;
                float y = heights != null && heights.Length >= (i + 1) * rings ? heights[i * rings + rings - 1] : 0f;
                corners.Add(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach[i] + Vector3.up * y);
            }

            SpellFx.Entry fx = SpellFx.ByName("EARTH");
            Mesh mesh = fx != null ? fx.wallPiece : null;
            Material material = fx != null ? fx.wallPieceMaterial : null;
            if (mesh != null && material != null) { mesh = Tinted(mesh); material = Toned(material); }
            else { mesh = null; material = null; }

            var rnd = new System.Random(Mathf.RoundToInt(centre.x * 73f + centre.z * 131f));
            for (int k = 0; k < corners.Count; k++)
            {
                Vector3 a = corners[k], b = corners[(k + 1) % corners.Count];
                Vector3 along = b - a;
                along.y = 0f;
                float length = along.magnitude;
                if (length < 0.05f) continue;
                along /= length;
                Vector3 mid = (a + b) * 0.5f;

                // the solid part: a slab along this stretch of the ring, a little long so the joints are closed
                var wall = new GameObject("Stretch");
                wall.transform.SetParent(go.transform, false);
                wall.transform.SetPositionAndRotation(mid + Vector3.up * (Height * 0.5f - 0.05f), Quaternion.LookRotation(along, Vector3.up));
                BoxCollider box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(Thickness, Height + 0.1f, length + 0.12f);
                box.enabled = false;
                f.walls.Add(box);

                // the look: spikes along it, out of the ground
                int count = Mathf.Max(1, Mathf.RoundToInt(length / SpikeEvery));
                for (int s = 0; s < count; s++)
                {
                    Vector3 at = Vector3.Lerp(a, b, (s + 0.5f) / count) + along * Rand(rnd, -0.05f, 0.05f);
                    f.Spike(mesh, material, at, centre, rnd);
                }
            }
            return f;
        }

        void Spike(Mesh mesh, Material material, Vector3 at, Vector3 centre, System.Random rnd)
        {
            float h = Height * Rand(rnd, 0.95f, 1.3f);
            var go = new GameObject("Spike");
            go.transform.SetParent(transform, false);
            Vector3 outward = at - centre;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Vector3.forward;

            if (mesh == null)
            {
                // without the pack: a plain stone post
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(go.transform, false);
                post.transform.localScale = new Vector3(0.3f, h, 0.3f);
                post.transform.localPosition = Vector3.up * (h * 0.5f);
                post.GetComponent<Renderer>().material = RuntimeMaterials.Emissive(new Color(0.55f, 0.42f, 0.3f), 0.2f);
                go.transform.position = at;
                Keep(go.transform, at, h, Rand(rnd, 0f, 0.06f));
                return;
            }

            // the pack's spike points along its Z (as the ice wall finds it); ours stands up, leaning out
            Bounds bounds = mesh.bounds;
            bool alongZ = bounds.size.z > bounds.size.y;
            float length = alongZ ? bounds.size.z : bounds.size.y;
            float baseAt = alongZ ? bounds.min.z : bounds.min.y;
            float girth = alongZ ? Mathf.Max(bounds.size.x, bounds.size.y) : Mathf.Max(bounds.size.x, bounds.size.z);
            Quaternion upright = alongZ ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
            Quaternion lean = Quaternion.AngleAxis(Rand(rnd, 6f, 16f), Vector3.Cross(Vector3.up, outward))
                              * Quaternion.AngleAxis(Rand(rnd, -6f, 6f), outward);
            Quaternion spin = Quaternion.AngleAxis(Rand(rnd, 0f, 360f), Vector3.up);
            go.transform.rotation = lean * spin * upright;

            float scale = h / length;
            float across = Mathf.Clamp(scale * girth, 0.32f, 0.5f) / girth;
            go.transform.localScale = alongZ ? new Vector3(across, across, scale) : new Vector3(across, scale, across);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            // the foot on the floor, sunk a hair so no gap shows on a slope
            Keep(go.transform, at + Vector3.up * (-baseAt * scale - 0.06f), h, Rand(rnd, 0f, 0.06f));
        }

        void Keep(Transform piece, Vector3 standingAt, float length, float delay)
        {
            pieces.Add(piece);
            pieceAt.Add(standingAt);
            pieceLength.Add(length);
            pieceDelay.Add(delay);
            piece.position = standingAt - Vector3.up * (length * 1.15f);
        }

        void Start()
        {
            // Whoever stands right where the stones come up is put to whichever side they stood on, before the
            // stones are there to stop them -- by their own machine, which moves them (bots by the server).
            float clear = Thickness * 0.5f + 0.45f;
            Vector3 centre = transform.position;
            foreach (PlayerMotor m in FindObjectsOfType<PlayerMotor>())
            {
                if (m == null || !m.IsLocallyControlled) continue;
                Vector3 offset = m.transform.position - centre;
                if (Mathf.Abs(offset.y) > 2.5f) continue;
                offset.y = 0f;
                float d = offset.magnitude;
                float edge = zone.EdgeTowards(offset);
                if (d < 1e-3f || Mathf.Abs(d - edge) >= clear) continue;
                float to = d < edge ? Mathf.Max(0f, edge - clear) : edge + clear;
                m.Nudge(offset / d * (to - d));
            }
            foreach (BoxCollider w in walls) w.enabled = true;
            standing = true;
        }

        void Update()
        {
            age += Time.deltaTime;
            float left = zone != null ? zone.Remaining : 0f;
            float sink = 1f - Mathf.Clamp01(left / SinkTime);
            if (standing && sink > 0.4f)
            {
                // down far enough to step over: it stops nothing any more
                foreach (BoxCollider w in walls) if (w != null) w.enabled = false;
                standing = false;
            }
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                float t = Mathf.Clamp01((age - pieceDelay[i]) / RiseTime);
                float up = t < 1f ? EaseOutBack(t) : 1f;
                float down = (1f - up) + sink * sink;
                pieces[i].position = pieceAt[i] - Vector3.up * (pieceLength[i] * 1.15f * Mathf.Clamp01(down));
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        static float Rand(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        /// <summary>Whether a collider is part of a fence -- a wall to anything that lands on it, never floor.</summary>
        public static bool Is(Collider c)
        {
            return c != null && c.GetComponentInParent<StoneFence>() != null;
        }

        /// <summary>
        /// The pack's stone, dimmed: made to flash up for a moment, it glows at five times its colour with a
        /// bright rim, and a ring of it standing for seconds read as white-hot crystal rather than rock.
        /// </summary>
        static Material toned;
        static Material Toned(Material source)
        {
            if (toned != null) return toned;
            toned = new Material(source) { name = source.name + "_Fence" };
            if (toned.HasProperty("_Emissive")) toned.SetFloat("_Emissive", StoneGlow);
            if (toned.HasProperty("_FresnelMultiply")) toned.SetFloat("_FresnelMultiply", 0.35f);
            return toned;
        }

        const float StoneGlow = 1f;

        /// <summary>The pack's spike, coloured: its shading takes the vertex colour, which a particle gives it and a mesh does not.</summary>
        static Mesh tinted;
        static Mesh Tinted(Mesh source)
        {
            if (tinted != null) return tinted;
            if (!source.isReadable) return source;
            tinted = Instantiate(source);
            tinted.name = source.name + "_Fence";
            var colours = new Color[tinted.vertexCount];
            // the brown of the stone (the pack gives its particles a paler tan, which on a mesh reads pink)
            for (int i = 0; i < colours.Length; i++) colours[i] = new Color(0.78f, 0.56f, 0.38f, 1f);
            tinted.colors = colours;
            return tinted;
        }
    }
}
