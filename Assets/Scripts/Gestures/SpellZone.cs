using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A patch a spell leaves on the floor: fire burns, ice slows, air throws you up.
    ///
    /// These are what give the arena temporary territory. It has no objectives, so until now there was
    /// nowhere worth being and nowhere to avoid -- movement was only ever about dodging. A patch is a
    /// place you do not want to step, and a place you can herd someone away from.
    ///
    /// Written fresh rather than grown out of PoisonPuddle, because that one found its occupants by
    /// PlayerMotor alone -- which the training dummy does not have. A burn you cannot test on the dummy
    /// is a burn nobody will ever see working. This one takes anything with Health OR a motor.
    ///
    /// It affects its own caster too, deliberately. A misfire already can, and fire dropped at your own
    /// feet as free cover is exactly the kind of thing that should cost you.
    /// </summary>
    public class SpellZone : MonoBehaviour
    {
        const float TickInterval = 0.25f;

        /// <summary>How high above the floor the patch reaches. A body standing in it, not a bird over it.</summary>
        const float Height = 2.3f;

        GroundEffect effect;
        float radius;
        float lifetime;
        float remaining;
        float strength;
        float nextTick;
        Material material;
        Color colour;

        readonly HashSet<PlayerMotor> launched = new HashSet<PlayerMotor>();

        /// <summary>
        /// The server's patch deals the damage. Being slowed or thrown is done by each player's own
        /// copy of the patch to their own body -- the body is theirs to move, and waiting on the server
        /// to tell them would put a round trip between stepping in and being thrown.
        /// </summary>
        bool authoritative = true;
        ulong attacker = Health.NoAttacker;

        public GroundEffect Effect { get { return effect; } }
        public float Radius { get { return radius; } }

        /// <summary>Directions the edge is measured in. 64 keeps a straight platform edge straight.</summary>
        public const int Spokes = 64;

        /// <summary>How far the ground may step and still count as the same surface.</summary>
        const float SameSurface = 0.15f;

        /// <summary>How far the patch reaches in each spoke direction, after the ground has run out.</summary>
        float[] reach;

        /// <summary>
        /// How far a patch centred on <paramref name="ground"/> can spread in each direction before the
        /// surface under it ends -- the edge of a platform, the lip of a step.
        ///
        /// A patch is a thing lying on the ground, so where there is no ground at its height there is no
        /// patch. It used to be a flat disc of the full radius, and one landing near the edge of a
        /// platform hung out over the drop like a tray. Probed straight down along each spoke; a box top
        /// seen from a point on it is always reachable in a straight line, so walking outwards and
        /// stopping at the first miss finds the edge exactly.
        /// </summary>
        public static float[] SupportedReach(Vector3 ground, float radius)
        {
            var result = new float[Spokes];
            const float Step = 0.25f;

            for (int i = 0; i < Spokes; i++)
            {
                float a = i / (float)Spokes * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

                // coarse steps to find the gap, then halving to put the edge within a centimetre
                float good = 0f, reached = radius;
                for (float r = Step; ; r += Step)
                {
                    float at = Mathf.Min(r, radius);
                    if (!Supported(ground, dir, at))
                    {
                        float bad = at;
                        for (int k = 0; k < 5; k++)
                        {
                            float mid = (good + bad) * 0.5f;
                            if (Supported(ground, dir, mid)) good = mid; else bad = mid;
                        }
                        reached = good;
                        break;
                    }
                    good = at;
                    if (at >= radius) break;
                }
                result[i] = reached;
            }
            return result;
        }

        static bool Supported(Vector3 ground, Vector3 dir, float r)
        {
            Vector3 from = ground + dir * r + Vector3.up * 0.6f;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 0.6f + SameSurface + 0.05f, ~0,
                                                   QueryTriggerInteraction.Ignore);

            // The nearest SURFACE below, looking through anybody standing there. Somebody standing in
            // the patch is not the ground, and counting their head as a ledge would punch a hole in it.
            float nearest = float.MaxValue;
            RaycastHit best = new RaycastHit();
            foreach (RaycastHit h in hits)
            {
                if (h.collider.GetComponentInParent<Health>() != null) continue;
                if (h.collider.GetComponentInParent<PlayerMotor>() != null) continue;
                if (h.distance < nearest) { nearest = h.distance; best = h; }
            }

            return nearest < float.MaxValue
                   && Mathf.Abs(best.point.y - ground.y) <= SameSurface
                   && best.normal.y > Projectile.FloorNormal;
        }

        /// <summary>The patch's reach towards a point, read off the nearest spokes.</summary>
        static float ReachTowards(float[] reach, Vector3 offset)
        {
            float a = Mathf.Atan2(offset.z, offset.x);
            if (a < 0f) a += Mathf.PI * 2f;
            float f = a / (Mathf.PI * 2f) * Spokes;
            int i0 = Mathf.FloorToInt(f) % Spokes;
            int i1 = (i0 + 1) % Spokes;
            return Mathf.Lerp(reach[i0], reach[i1], f - Mathf.Floor(f));
        }

        /// <summary>A flat fan following the reach, lifted a hair so it does not z-fight the floor.</summary>
        static Mesh BuildMesh(float[] reach)
        {
            var vertices = new Vector3[Spokes + 1];
            var triangles = new int[Spokes * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < Spokes; i++)
            {
                float a = i / (float)Spokes * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reach[i];

                // wound so the face points up
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % Spokes + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "SpellZone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static SpellZone Spawn(Vector3 at, GroundEffect effect, float radius, float lifetime,
                                      float strength, Color colour,
                                      bool authoritative = true, ulong attacker = Health.NoAttacker)
        {
            // no collider at all: a patch must never block a projectile
            GameObject go = new GameObject("SpellZone_" + effect);

            // Settled onto the floor under the impact. The spell may have struck a step or a ledge, and
            // a disc floating in the air is plainly a bug.
            Vector3 ground = at;
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out hit, 12f, ~0,
                                QueryTriggerInteraction.Ignore))
                ground = hit.point;

            // a hair off the floor, or the two surfaces z-fight and the patch flickers
            go.transform.position = ground + Vector3.up * 0.03f;

            // Cut to the ground it actually lies on, so it ends where the platform ends.
            float[] reach = SupportedReach(ground, radius);
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(reach);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Solid and emissive, with a hard edge. A patch has to say exactly where it ends -- a soft
            // glow would leave the player guessing whether the step they are about to take is safe.
            Material m = RuntimeMaterials.Emissive(colour, 1.1f);
            renderer.material = m;

            SpellZone z = go.AddComponent<SpellZone>();
            z.effect = effect;
            z.radius = radius;
            z.lifetime = lifetime;
            z.remaining = lifetime;
            z.strength = strength;
            z.material = m;
            z.colour = colour;
            z.authoritative = authoritative;
            z.attacker = attacker;
            z.reach = reach;
            return z;
        }

        void OnDestroy()
        {
            MeshFilter f = GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
        }

        void Update()
        {
            remaining -= Time.deltaTime;

            // Dims over its last second so it never vanishes mid-fight without warning.
            float alpha = Mathf.Clamp01(remaining);
            material.SetColor("_EmissionColor", colour * (0.35f + 0.75f * alpha));

            if (remaining <= 0f) { Destroy(gameObject); return; }

            if (Time.time < nextTick) return;
            nextTick = Time.time + TickInterval;

            foreach (Target t in Occupants())
            {
                switch (effect)
                {
                    case GroundEffect.Burn:
                        if (authoritative && t.Health != null) t.Health.TakeDamage(strength * TickInterval, attacker);
                        break;

                    case GroundEffect.Slow:
                        // re-applied every tick for a little longer than a tick, so it holds while you
                        // stand in it and lets go almost as soon as you step out
                        if (t.Motor != null && t.Motor.IsLocallyControlled)
                            t.Motor.ApplySlow(strength, TickInterval + 0.15f);
                        break;

                    case GroundEffect.Updraft:
                        // Once per person per patch. Without that, a two-second updraft is a trampoline,
                        // and "you walked into it" turns into "you cannot get out of it".
                        if (t.Motor != null && t.Motor.IsLocallyControlled && t.Motor.IsGrounded
                            && !launched.Contains(t.Motor))
                        {
                            t.Motor.Launch(strength);
                            launched.Add(t.Motor);
                        }
                        break;
                }
            }
        }

        struct Target
        {
            public Transform Root;
            public Health Health;
            public PlayerMotor Motor;
        }

        /// <summary>
        /// Who is standing in it -- tested as a cylinder, not the sphere OverlapSphere gives, or a patch
        /// 2.5 m wide would also catch somebody jumping 2 m above it.
        /// </summary>
        List<Target> Occupants()
        {
            var found = new List<Target>();
            var seen = new HashSet<Transform>();
            Vector3 centre = transform.position;

            Collider[] near = Physics.OverlapSphere(centre + Vector3.up * (Height * 0.5f),
                                                    radius + Height, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider c in near)
            {
                Health hp = c.GetComponentInParent<Health>();
                PlayerMotor motor = c.GetComponentInParent<PlayerMotor>();
                if (hp == null && motor == null) continue;          // scenery

                Transform root = motor != null ? motor.transform : hp.transform;
                if (!seen.Add(root)) continue;

                Vector3 offset = root.position - centre;
                float flat = new Vector2(offset.x, offset.z).magnitude;
                // inside the patch as cut, not the full circle -- standing below a platform's edge is
                // not standing in the fire on top of it
                if (flat > ReachTowards(reach, offset)) continue;
                if (offset.y < -0.4f || offset.y > Height) continue;

                found.Add(new Target { Root = root, Health = hp, Motor = motor });
            }
            return found;
        }
    }
}
