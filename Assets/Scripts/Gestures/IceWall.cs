using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Ice aimed at the floor close in front of you: instead of flying, it bursts up out of the ground as
    /// a wall of ice spikes. Cover that is quick and cheap -- it costs your ice (its cooldown, the global
    /// cooldown) -- and fragile: the first spell to hit it shatters it, and it melts after a few seconds.
    /// The barrier is the strong, lasting one; this is the moment's cover, and the choice between
    /// attacking with your ice and hiding behind it.
    ///
    /// Solid, both ways: it stops shots (yours too), people and sight. A fire that breaks it leaves the
    /// meltwater behind as a water patch -- fire into ice, as everywhere else.
    ///
    /// Every machine raises its own copy where the caster said; only the server decides that a hit broke
    /// it, and tells everyone (PlayerNet.BroadcastIceWallBreak), so it shatters for all at once.
    ///
    /// The look is the Stylized AoE pack's pointy rock in its ice material: spikes of varied height in
    /// two staggered rows, bursting up out of the floor from the middle outwards, over the pack's ice
    /// burst. Without the pack, plain crystals of our own.
    ///
    /// What is solid: an invisible box the wall's width and height (no gaps between the spikes to shoot
    /// through), and every spike in its own shape on top -- the needles of a tier III stand above the box,
    /// and a shot at the ice you see must stop on it. Any of it struck is the wall struck.
    /// </summary>
    public class IceWall : MonoBehaviour
    {
        static readonly List<IceWall> all = new List<IceWall>();

        static readonly Color IceColour = new Color(0.55f, 0.85f, 1f);
        static readonly Color WaterColour = new Color(0.2f, 0.45f, 0.95f);

        const float RiseTime = 0.24f;
        const float MeltTime = 0.7f;

        ulong owner = Health.NoAttacker;
        bool authoritative = true;
        float remaining, age;
        float width, height;
        bool broken;

        BoxCollider box;
        Transform[] pieces;
        Vector3[] pieceAt;
        float[] pieceLength, pieceDelay;
        Mesh pieceMesh;
        Material pieceMaterial;
        SpellFx.Entry fx;

        /// <summary>For measuring: walls raised and walls shattered on this machine since the start.</summary>
        public static int Made, Shattered;

        public float Width { get { return width; } }
        public float Height { get { return height; } }

        /// <summary>
        /// Raises a wall standing on <paramref name="ground"/>, its face turned along <paramref name="facing"/>
        /// (from the caster towards it), sized by the tier the rune was drawn at.
        /// </summary>
        public static IceWall Spawn(Vector3 ground, Vector3 facing, int tier, ulong owner, bool authoritative)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            Vector3 flat = new Vector3(facing.x, 0f, facing.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;

            // only as wide as the floor it stands on: a narrow wall top gets a narrow wall
            float width;
            Placement.Fit(ground, flat, SpellTiers.IceWallWidth[tier], out ground, out width);

            var go = new GameObject("IceWall");
            go.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(flat.normalized, Vector3.up));

            IceWall w = go.AddComponent<IceWall>();
            w.owner = owner;
            w.authoritative = authoritative;
            w.width = width;
            w.height = SpellTiers.IceWallHeight[tier];
            w.remaining = SpellTiers.IceWallSeconds;

            // the solid part: a plain box the size of the wall, there from the first frame, so nothing
            // slips through while the spikes are still coming up
            // invisible: the wall is the ice you see, and this is what makes all of it solid between the spikes
            var box = go.AddComponent<BoxCollider>();
            w.box = box;
            box.center = new Vector3(0f, w.height * 0.5f, 0f);
            box.size = new Vector3(w.width, w.height, SpellTiers.IceWallThickness);

            w.fx = SpellFx.ByName("ICE");
            w.Build();
            w.Raised(ground);
            Made++;
            all.Add(w);
            return w;
        }

        /// <summary>The wall standing at <paramref name="centre"/> on this machine, or null.</summary>
        public static IceWall Find(Vector3 centre)
        {
            IceWall best = null;
            float bestDistance = 0.75f;
            foreach (IceWall w in all)
            {
                if (w == null || w.broken) continue;
                float d = Vector3.Distance(w.transform.position, centre);
                if (d < bestDistance) { bestDistance = d; best = w; }
            }
            return best;
        }

        // ---------------------------------------------------------------- the look

        void Build()
        {
            pieceMesh = fx != null ? fx.wallPiece : null;
            pieceMaterial = fx != null ? Toned(fx.wallPieceMaterial) : null;
            if (pieceMesh != null && pieceMaterial != null) pieceMesh = Tinted(pieceMesh);
            else
            {
                pieceMesh = Crystal();
                pieceMaterial = RuntimeMaterials.Emissive(IceColour, 0.6f);
            }

            // Two staggered rows: tall spikes along the back, shorter ones in front filling the gaps, so it
            // reads as one jagged wall rather than a fence -- tallest in the middle, lower at the ends.
            var rnd = new System.Random(Mathf.RoundToInt(transform.position.x * 73f + transform.position.z * 131f));
            int back = Mathf.Max(4, Mathf.RoundToInt(width / 0.34f));
            int n = back + back - 1;
            pieces = new Transform[n];
            pieceAt = new Vector3[n];
            pieceLength = new float[n];
            pieceDelay = new float[n];

            // the pack's spike points along its Z; ours along Y
            Bounds b = pieceMesh.bounds;
            bool alongZ = b.size.z > b.size.y;
            float length = alongZ ? b.size.z : b.size.y;
            float baseAt = alongZ ? b.min.z : b.min.y;
            float girth = alongZ ? Mathf.Max(b.size.x, b.size.y) : Mathf.Max(b.size.x, b.size.z);
            Quaternion upright = alongZ ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;

            for (int i = 0; i < n; i++)
            {
                bool isBack = i < back;
                int k = isBack ? i : i - back;
                float slot = width / back;
                // the back row runs end to end -- the wall stops shots right to its edges, and must look it
                float x = isBack ? -width * 0.5f + width * (k + 0.5f) / back * 0.96f + width * 0.02f + Rand(rnd, -0.05f, 0.05f)
                                 : -width * 0.5f + slot * (k + 1f) + Rand(rnd, -0.08f, 0.08f);
                float edge = Mathf.Clamp01(Mathf.Abs(x) / (width * 0.5f));
                float h = height * (1f - 0.3f * edge * edge) * (isBack ? Rand(rnd, 0.88f, 1.06f) : Rand(rnd, 0.5f, 0.72f));
                float z = (isBack ? 0.1f : -0.2f) + Rand(rnd, -0.06f, 0.06f);

                var go = new GameObject("Spike");
                go.transform.SetParent(transform, false);
                // upright, then a little lean -- outwards at the ends, the rows apart -- and a random turn
                // about its own axis, so no two catch the light alike
                Quaternion lean = Quaternion.Euler(Rand(rnd, -8f, 8f) + (isBack ? 5f : -7f), 0f,
                                                   -Mathf.Sign(x) * edge * 14f + Rand(rnd, -5f, 5f));
                Quaternion spin = Quaternion.AngleAxis(Rand(rnd, 0f, 360f), Vector3.up);
                go.transform.localRotation = lean * spin * upright;

                // as tall as wanted, about half a metre thick whatever the height
                float s = h / length;
                float across = Mathf.Clamp(s * girth, 0.5f, 0.9f) / girth;
                go.transform.localScale = alongZ ? new Vector3(across, across, s) : new Vector3(across, s, across);

                // the foot on the floor, sunk a hair so no gap shows on a slope
                pieceAt[i] = new Vector3(x, -baseAt * s - 0.05f, z);
                go.transform.localPosition = pieceAt[i] - Vector3.up * (h * 1.1f);

                go.AddComponent<MeshFilter>().sharedMesh = pieceMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = pieceMaterial;

                // Every spike is solid in its own shape: a tier III's needles stand well above the box, and
                // a shot at one must stop on the ice you see, not fly through it into you. Hitting a spike
                // is hitting the wall -- the same shatter, whatever part was struck.
                var hull = go.AddComponent<MeshCollider>();
                hull.sharedMesh = pieceMesh;
                hull.convex = true;

                pieces[i] = go.transform;
                pieceLength[i] = h;
                // bursting up from the middle outwards, the front row a beat behind the back
                pieceDelay[i] = edge * 0.1f + (isBack ? 0f : 0.05f) + Rand(rnd, 0f, 0.02f);
            }
        }

        static float Rand(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        /// <summary>
        /// The pack's ice, toned down for something that stands for seconds rather than flashes past: it
        /// glows at ten times its colour for a burst, which on a wall is a white blob. A third of that,
        /// and glossier, reads as ice -- blue edges, a lit face.
        /// </summary>
        static Material toned;
        static Material Toned(Material source)
        {
            if (source == null) return null;
            if (toned != null) return toned;
            toned = new Material(source) { name = source.name + "_Wall" };
            if (toned.HasProperty("_Emissive")) toned.SetFloat("_Emissive", 3f);
            if (toned.HasProperty("_BaseEmissionMultiply")) toned.SetFloat("_BaseEmissionMultiply", 0.3f);
            if (toned.HasProperty("_Smoothness")) toned.SetFloat("_Smoothness", 0.75f);
            return toned;
        }

        /// <summary>The pack's spike, coloured: its glow takes the vertex colour, which a particle gives it and a mesh does not.</summary>
        static Mesh tinted;
        static Mesh Tinted(Mesh source)
        {
            if (tinted != null) return tinted;
            if (!source.isReadable) return source;
            tinted = Object.Instantiate(source);
            tinted.name = source.name + "_Ice";
            var colours = new Color[tinted.vertexCount];
            for (int i = 0; i < colours.Length; i++) colours[i] = new Color(0.25f, 0.62f, 1f, 1f);
            tinted.colors = colours;
            return tinted;
        }

        /// <summary>Our own crystal, for when the pack is not there: a six-sided spike, 1 m long.</summary>
        static Mesh crystal;
        static Mesh Crystal()
        {
            if (crystal != null) return crystal;
            const int Sides = 6;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < Sides; i++)
            {
                float a0 = i / (float)Sides * Mathf.PI * 2f, a1 = (i + 1) / (float)Sides * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.22f;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.22f;
                Vector3 q0 = p0 * 0.8f + Vector3.up * 0.75f, q1 = p1 * 0.8f + Vector3.up * 0.75f;
                int s = v.Count;
                v.AddRange(new[] { p0, p1, q1, q0, q0, q1, Vector3.up });
                t.AddRange(new[] { s, s + 3, s + 2, s, s + 2, s + 1, s + 4, s + 6, s + 5 });
            }
            crystal = new Mesh { name = "IceCrystal" };
            crystal.SetVertices(v);
            crystal.SetTriangles(t, 0);
            crystal.RecalculateNormals();
            crystal.RecalculateBounds();
            return crystal;
        }

        /// <summary>Going up: the pack's ice burst at its foot, and the crack of ice.</summary>
        void Raised(Vector3 ground)
        {
            if (fx == null) { Projectile.FlashAt(ground + Vector3.up * 0.5f, IceColour, width, 1.8f); return; }
            if (fx.wallRaise != null)
                SpellFx.PlayBurst(fx.wallRaise, ground, fx.wallRaiseScale * width / 2.8f, fx.wallRaiseLeadIn);
            if (fx.wallRaiseSound != null) SpellFx.OneShot(fx.wallRaiseSound, ground + Vector3.up, 0.9f);
        }

        // ---------------------------------------------------------------- breaking

        /// <summary>How far the meltwater reaches either side of where the wall stood.</summary>
        const float MeltDepth = 0.9f;

        /// <summary>
        /// Server (or offline): a spell has hit it. It breaks, for everyone; a fire leaves the meltwater as
        /// a water patch, owned by whoever threw it.
        /// </summary>
        public void Hit(bool byFire, PlayerNet by, ulong attacker)
        {
            if (!authoritative || broken) return;
            Vector3 centre = transform.position;
            Shatter();

            PlayerNet teller = PlayerNet.Find(owner);
            if (teller == null || !teller.IsSpawned) teller = by;
            if (teller != null && teller.IsSpawned) teller.BroadcastIceWallBreak(centre);

            if (byFire)
            {
                // the meltwater lies where the wall stood: an oval along it, not a round puddle
                float along = width * 0.5f + 0.3f;
                float across = Mathf.Clamp01(MeltDepth / along);
                Vector3 axis = transform.right;
                SpellZone water = SpellZone.Spawn(centre, GroundEffect.Water, along, 6f, 0f, WaterColour, true, attacker, attacker,
                                                  axis, across);
                if (by != null && by.IsSpawned)
                    by.BroadcastZone(water.transform.position, GroundEffect.Water, along, 6f, 0f, WaterColour, axis, across);
            }
        }

        /// <summary>Flies apart into shards of itself, with the ice burst and its crack. On every machine.</summary>
        public void Shatter()
        {
            if (broken) return;
            broken = true;
            // gone at once, not at the end of the frame: the meltwater laid right after must not be cut
            // to the shape of spikes that are no longer there
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Shattered++;
            Vector3 middle = transform.position + Vector3.up * (height * 0.45f);
            Shards();
            if (fx != null && fx.wallBreak != null)
                SpellFx.Play(fx.wallBreak, middle, Quaternion.identity, fx.wallBreakScale * width / 2.8f);
            else Projectile.FlashAt(middle, IceColour, width, 2f);
            if (fx != null && fx.wallBreakSound != null) SpellFx.OneShot(fx.wallBreakSound, middle, 1f);
            Destroy(gameObject);
        }

        void Shards()
        {
            var go = new GameObject("IceShards");
            go.transform.SetPositionAndRotation(transform.position + Vector3.up * (height * 0.5f), transform.rotation);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            // a shard is a small copy of a spike: its mesh, a tenth to a quarter of a metre long
            float unit = Mathf.Max(0.01f, pieceMesh.bounds.size.magnitude);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f / unit, 0.5f / unit);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.65f, 0.9f, 1f, 1f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(width * height * 8f, 24f, 60f)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, height, 0.4f);
            shape.randomDirectionAmount = 1f;

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
            spin.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
            spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = pieceMesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = pieceMaterial;

            ps.Play();
            Destroy(go, 1.6f);
        }

        // ---------------------------------------------------------------- living

        void Update()
        {
            age += Time.deltaTime;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Destroy(gameObject); return; }

            // Up out of the floor, overshooting a touch and settling; at the end, back down into it. Moved,
            // not scaled: the floor hides what is still under it, and it looks like ice breaking through.
            float melt = 1f - Mathf.Clamp01(remaining / MeltTime);
            for (int i = 0; i < pieces.Length; i++)
            {
                float t = Mathf.Clamp01((age - pieceDelay[i]) / RiseTime);
                float up = t < 1f ? EaseOutBack(t) : 1f;
                float sunk = (1f - up) + melt * melt;
                pieces[i].localPosition = pieceAt[i] - Vector3.up * (pieceLength[i] * 1.1f * sunk);
            }
            // the invisible box goes down with the ice as it melts, so it stops nothing that is no longer there
            if (melt > 0f && box != null)
            {
                float left = height * (1f - melt * melt);
                box.size = new Vector3(box.size.x, Mathf.Max(0.01f, left), box.size.z);
                box.center = new Vector3(0f, left * 0.5f, 0f);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.2f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        void OnDestroy()
        {
            all.Remove(this);
        }
    }
}
