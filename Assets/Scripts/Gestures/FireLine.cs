using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Fire aimed at the floor close in front of you: instead of flying, it lays a line of fire across
    /// the way -- the fire's answer to the ice wall, and nothing like a shield. It stops nobody and no
    /// shot; whoever walks through it catches fire the way a tier II hit sets you burning, and again
    /// every second they stay in it. A line across a doorway is a door you pay to use.
    ///
    /// And ice thrown through it comes out the other side as water (Projectile): a bolt that soaks
    /// whoever it hits (Wet) -- lightning into somebody wet strikes twice as hard, fire on them only
    /// steams. Anybody's ice, through anybody's line: you can be soaked by your own.
    ///
    /// Every machine lays its own copy where the caster said and sees the same bodies walk through
    /// it; only the server's burning deals the damage (see Burning). The look is a wall of the packs'
    /// own fire -- the fireball's flames, hot smoke, embers and a glow on the floor -- catching from the
    /// middle out, with the fire patch's sound; without the packs, simple flames over a band of embers.
    /// </summary>
    public class FireLine : MonoBehaviour
    {
        static readonly List<FireLine> all = new List<FireLine>();

        /// <summary>Lines laid since the counter was last zeroed -- for measuring how bots use them.</summary>
        public static int Laid;

        static readonly Color Ember = new Color(1f, 0.45f, 0.12f);

        const int Segments = 12;

        /// <summary>
        /// The fire patch's loop under it, quietly: the clip is loud all the way through (RMS 0.27), and
        /// at the 0.6 it had at first (15 Oct) a line drowned out the fight around it.
        /// </summary>
        const float CrackleVolume = 0.2f;
        const float CheckEvery = 0.1f;

        float length, width, remaining, age;
        ulong attacker;
        bool authoritative;
        float nextCheck;
        float[] ground;               // the floor's height under the line, Segments + 1 points, relative to its middle
        readonly Dictionary<Health, float> burntAt = new Dictionary<Health, float>();
        readonly List<GameObject> flames = new List<GameObject>();
        readonly List<float> lightsAt = new List<float>();      // when each piece catches, from the middle out
        Light glow;
        Material band;
        Mesh bandMesh;
        AudioSource crackle;
        SpellFx.Entry fx;

        /// <summary>
        /// Lays a line across <paramref name="facing"/> (from the caster towards it) through
        /// <paramref name="spot"/>, as long as the tier the rune was drawn at.
        /// </summary>
        public static FireLine Spawn(Vector3 spot, Vector3 facing, int tier, ulong attacker, bool authoritative)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            Vector3 flat = new Vector3(facing.x, 0f, facing.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;

            var go = new GameObject("FireLine");
            go.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(flat.normalized, Vector3.up));
            FireLine f = go.AddComponent<FireLine>();
            f.length = SpellTiers.FireLineLength[tier];
            f.width = SpellTiers.FireLineWidth;
            f.remaining = SpellTiers.FireLineSeconds;
            f.attacker = attacker;
            f.authoritative = authoritative;
            f.fx = SpellFx.ByName("FIRE");
            f.Build();
            all.Add(f);
            Laid++;
            return f;
        }

        // ---------------------------------------------------------------- the look

        void Build()
        {
            // the floor along the line, so it lies on a slope instead of cutting through it
            ground = new float[Segments + 1];
            for (int i = 0; i <= Segments; i++)
            {
                Vector3 p = transform.TransformPoint(new Vector3(X(i), 0f, 0f));
                float y = 0f;
                RaycastHit h;
                if (Ground(p + Vector3.up * 1.5f, 3.5f, out h)) y = h.point.y - transform.position.y;
                else if (i > 0) y = ground[i - 1];
                ground[i] = y;
            }

            if (fx != null && fx.linePiece != null) { BuildWall(); return; }

            BuildBand();

            // without the packs: flames standing along it, a little apart, a little different each
            int count = Mathf.Max(3, Mathf.RoundToInt(length / 0.55f));
            var rnd = new System.Random(Mathf.RoundToInt(transform.position.x * 31f + transform.position.z * 17f));
            for (int i = 0; i < count; i++)
            {
                float x = -length * 0.5f + length * (i + 0.5f) / count + (float)(rnd.NextDouble() - 0.5) * 0.15f;
                Vector3 at = transform.TransformPoint(new Vector3(x, GroundAt(x) + 0.02f, (float)(rnd.NextDouble() - 0.5) * 0.25f));
                if (fx != null && fx.status != null)
                {
                    float scale = fx.statusScale * (2.1f + (float)rnd.NextDouble() * 0.6f);   // a wall of flame, not a person alight
                    GameObject flame = SpellFx.Play(fx.status, at, Quaternion.identity, scale, transform, false);
                    SpellFx.KeepBurning(flame, 7f + (float)rnd.NextDouble() * 2f);
                    if (flame != null) flames.Add(flame);
                }
            }

            // lit: the fire's own burst where it starts, and its patch's crackle while it burns
            Vector3 middle = transform.position + Vector3.up * 0.3f;
            if (fx != null && fx.impact != null) SpellFx.Play(fx.impact, middle, Quaternion.identity, fx.impactScale * 1.2f);
            else Projectile.FlashAt(middle, Ember, length * 0.6f, 2f);
            if (fx != null && fx.zoneLoop != null)
            {
                crackle = gameObject.AddComponent<AudioSource>();
                SpellFx.MakeSpatial(crackle);
                crackle.clip = fx.zoneLoop;
                crackle.loop = true;
                crackle.volume = CrackleVolume * SpellFx.Volume;
                crackle.Play();
            }
        }

        /// <summary>How fast it catches along its length from where it was thrown, m/s: a 6 m line in a quarter second.</summary>
        const float Spread = 12f;

        /// <summary>
        /// The look with the packs: a wall of the fire's own flames, hot smoke and embers over a soft glow on
        /// the floor (see SpellFxBuilder.BuildFireLinePiece), one piece per metre or so, each standing on the
        /// floor under it. It catches from the middle out, with the fire burst's whoomph, and lights what
        /// is around it.
        /// </summary>
        void BuildWall()
        {
            int count = Mathf.Max(2, Mathf.RoundToInt(length));
            float spacing = length / count;
            for (int i = 0; i < count; i++)
            {
                float x = -length * 0.5f + spacing * (i + 0.5f);
                Vector3 at = transform.TransformPoint(new Vector3(x, GroundAt(x) + 0.02f, 0f));
                if (InsideWall(at)) continue;      // laid against a wall: no flames standing out of the stone
                GameObject piece = SpellFx.Play(fx.linePiece, at, transform.rotation, 1f, transform, false);
                if (piece == null) continue;
                foreach (ParticleSystem ps in piece.GetComponentsInChildren<ParticleSystem>(true))
                {
                    // the piece is a metre; this one covers its own share of the line
                    var shape = ps.shape;
                    shape.scale = new Vector3(spacing, shape.scale.y, shape.scale.z);
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier *= spacing;
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                flames.Add(piece);
                lightsAt.Add(Mathf.Abs(x) / Spread);
            }

            Vector3 middle = transform.position + Vector3.up * 0.3f;
            if (fx.specialSound != null) SpellFx.OneShot(fx.specialSound, middle, 0.35f);

            var lit = new GameObject("Glow");
            lit.transform.SetParent(transform, false);
            lit.transform.localPosition = Vector3.up * 0.9f;
            glow = lit.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.2f);
            glow.range = length * 0.5f + 3f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;

            if (fx.zoneLoop != null)
            {
                crackle = gameObject.AddComponent<AudioSource>();
                SpellFx.MakeSpatial(crackle);
                crackle.clip = fx.zoneLoop;
                crackle.loop = true;
                crackle.volume = CrackleVolume * SpellFx.Volume;
                crackle.Play();
            }
        }

        /// <summary>Whether a piece standing at <paramref name="foot"/> would be inside the map (a wall, a pillar).</summary>
        static bool InsideWall(Vector3 foot)
        {
            foreach (Collider c in Physics.OverlapSphere(foot + Vector3.up * 0.9f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<Health>() != null || c.GetComponentInParent<PlayerMotor>() != null
                    || c.GetComponentInParent<CastShield>() != null || c.GetComponentInParent<IceWall>() != null) continue;
                return true;
            }
            return false;
        }

        /// <summary>A glowing band of embers on the floor under the flames -- exactly where it burns.</summary>
        void BuildBand()
        {
            var v = new Vector3[(Segments + 1) * 2];
            var t = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float y = ground[i] + 0.04f;
                v[i * 2] = new Vector3(X(i), y, -width * 0.5f);
                v[i * 2 + 1] = new Vector3(X(i), y, width * 0.5f);
            }
            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2;
                t[i * 6] = a; t[i * 6 + 1] = a + 1; t[i * 6 + 2] = a + 3;
                t[i * 6 + 3] = a; t[i * 6 + 4] = a + 3; t[i * 6 + 5] = a + 2;
            }
            bandMesh = new Mesh { name = "FireLine" };
            bandMesh.vertices = v;
            bandMesh.triangles = t;
            bandMesh.RecalculateNormals();
            bandMesh.RecalculateBounds();

            var go = new GameObject("Embers");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = bandMesh;
            var r = go.AddComponent<MeshRenderer>();
            band = RuntimeMaterials.Fade(Ember, 0.55f, 1.6f);
            r.material = band;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        float X(int i) { return -length * 0.5f + length * i / Segments; }

        /// <summary>The floor's height under a point along the line, relative to its middle.</summary>
        float GroundAt(float x)
        {
            float f = Mathf.Clamp01((x + length * 0.5f) / length) * Segments;
            int i = Mathf.Min(Mathf.FloorToInt(f), Segments - 1);
            return Mathf.Lerp(ground[i], ground[i + 1], f - i);
        }

        /// <summary>The map straight down, looking through people, walls of ice and barriers.</summary>
        public static bool Ground(Vector3 from, float depth, out RaycastHit best)
        {
            best = new RaycastHit();
            float nearest = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(from, Vector3.down, depth, ~0, QueryTriggerInteraction.Ignore))
            {
                Collider c = h.collider;
                if (c.GetComponentInParent<Health>() != null || c.GetComponentInParent<PlayerMotor>() != null
                    || c.GetComponentInParent<CastShield>() != null || c.GetComponentInParent<IceWall>() != null) continue;
                if (h.distance < nearest) { nearest = h.distance; best = h; }
            }
            return nearest < float.MaxValue;
        }

        // ---------------------------------------------------------------- what it does

        /// <summary>
        /// Whether a shot moving from <paramref name="a"/> to <paramref name="b"/> goes through the flames
        /// of any line on this machine -- where ice turns to water.
        /// </summary>
        public static bool Crosses(Vector3 a, Vector3 b)
        {
            foreach (FireLine f in all)
                if (f != null && f.remaining > 0f && f.Through(a, b)) return true;
            return false;
        }

        /// <summary>The segment against the box the flames fill (the slab test, in the line's own frame).</summary>
        bool Through(Vector3 a, Vector3 b)
        {
            Vector3 la = transform.InverseTransformPoint(a), lb = transform.InverseTransformPoint(b);
            Vector3 d = lb - la;
            float enter = 0f, exit = 1f;
            if (!Slab(la.x, d.x, -length * 0.5f, length * 0.5f, ref enter, ref exit)) return false;
            if (!Slab(la.z, d.z, -width * 0.5f - 0.15f, width * 0.5f + 0.15f, ref enter, ref exit)) return false;
            Vector3 mid = la + d * ((enter + exit) * 0.5f);
            float above = mid.y - GroundAt(mid.x);
            return above > -0.2f && above < SpellTiers.FireLineFlameHeight;
        }

        static bool Slab(float start, float delta, float min, float max, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 1e-6f) return start >= min && start <= max;
            float t0 = (min - start) / delta, t1 = (max - start) / delta;
            if (t0 > t1) { float s = t0; t0 = t1; t1 = s; }
            enter = Mathf.Max(enter, t0);
            exit = Mathf.Min(exit, t1);
            return enter <= exit;
        }

        void Update()
        {
            age += Time.deltaTime;
            remaining -= Time.deltaTime;

            // catching, from the middle out
            for (int i = 0; i < lightsAt.Count && i < flames.Count; i++)
            {
                if (lightsAt[i] < 0f || age < lightsAt[i]) continue;
                lightsAt[i] = -1f;
                if (flames[i] != null)
                    foreach (ParticleSystem ps in flames[i].GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
            }
            if (glow != null)
            {
                // a fire's light: never quite still, and dying with it
                float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 7f, transform.position.x);
                glow.intensity = 1.6f * flicker * Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01(remaining / 0.6f);
            }

            // the last half second: flames out, embers fading
            if (remaining <= 0.5f && flames.Count > 0)
            {
                foreach (GameObject f in flames) SpellFx.Release(f);
                flames.Clear();
                lightsAt.Clear();
            }
            if (band != null)
            {
                float a = Mathf.Clamp01(remaining / 0.5f) * Mathf.Clamp01(age / 0.15f);
                Color c = band.color;
                c.a = 0.55f * a;
                band.color = c;
                band.SetColor("_EmissionColor", Ember * (1.6f * a));
            }
            if (crackle != null) crackle.volume = CrackleVolume * SpellFx.Volume * Mathf.Clamp01(remaining / 0.5f);
            if (remaining <= 0f) { Destroy(gameObject); return; }

            if (Time.time < nextCheck || remaining <= 0.4f) return;
            nextCheck = Time.time + CheckEvery;
            BurnWhoeverIsIn();
        }

        /// <summary>
        /// Whoever is standing in the flames catches fire -- as a tier II hit sets you burning -- and again
        /// every SpellTiers.FireLineReburn seconds while they stay. A wet body only steams (see Burning).
        /// Its caster's teammates walk through it; its caster does not.
        /// </summary>
        void BurnWhoeverIsIn()
        {
            Vector3 centre = transform.position + Vector3.up * 1f;
            Vector3 half = new Vector3(length * 0.5f, 1.6f, width * 0.5f + 0.3f);
            var seen = new HashSet<Health>();
            foreach (Collider c in Physics.OverlapBox(centre, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead || !seen.Add(h)) continue;
                Vector3 local = transform.InverseTransformPoint(h.transform.position);
                float above = local.y - GroundAt(local.x);
                if (above < -0.6f || above > 1.4f) continue;           // over it in a jump, or on a ledge below
                if (MageCast.Teams.Spares(attacker, h)) continue;

                float last;
                if (burntAt.TryGetValue(h, out last) && Time.time - last < SpellTiers.FireLineReburn) continue;
                burntAt[h] = Time.time;
                Burning.Apply(h, SpellTiers.BurnPerSecond, SpellTiers.BurnSeconds, attacker, authoritative, fx);
            }
        }

        void OnDestroy()
        {
            all.Remove(this);
            foreach (GameObject f in flames) SpellFx.Release(f);
            if (band != null) Destroy(band);
            if (bandMesh != null) Destroy(bandMesh);
        }
    }
}
