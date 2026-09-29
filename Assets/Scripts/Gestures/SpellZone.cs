using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A patch a spell leaves on the floor: fire burns, ice takes your grip away, air throws you up.
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

        /// <summary>Every patch on this machine, oldest first.</summary>
        static readonly List<SpellZone> all = new List<SpellZone>();

        /// <summary>
        /// Patches one player may have down at once. The third takes the oldest away -- the rule that
        /// keeps a busy arena from turning into a floor nobody can read.
        /// </summary>
        public const int MaxPerPlayer = 2;

        /// <summary>Who laid it, for the per-player limit. Known on every machine, unlike who it hurts.</summary>
        ulong owner = Health.NoAttacker;

        float flashUntil = -1f;

        /// <summary>The pack's area effect on top of the outline, where the spell has one.</summary>
        ZoneFx fx;
        MeshRenderer outline;

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
        public static float ReachTowards(float[] reach, Vector3 offset)
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
                                      bool authoritative = true, ulong attacker = Health.NoAttacker,
                                      ulong owner = Health.NoAttacker)
        {
            // The per-player limit, applied the same way on every machine: they all see the same patches
            // arrive in the same order, so they all take away the same oldest one.
            var mine = all.FindAll(z => z != null && z.owner == owner);
            for (int i = 0; i <= mine.Count - MaxPerPlayer; i++) mine[i].Remove();

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
            z.owner = owner;
            z.reach = reach;
            z.fx = ZoneFx.Play(SpellFx.ForZone(effect), go.transform.position, radius, lifetime, reach);
            // Under a real effect the plain outline would only wash it out; it stays for the flash.
            z.outline = renderer;
            if (z.fx != null) renderer.enabled = false;
            all.Add(z);
            return z;
        }

        /// <summary>
        /// The newest patch of one kind under a point on the floor, or null. <paramref name="reachOut"/>
        /// widens the test by the size of whatever landed: a spell is a ball, not a point, and one whose
        /// edge falls in the patch has landed in it -- a big gust of air aimed at a fire lands touching
        /// it long before its centre does.
        /// </summary>
        public static SpellZone At(Vector3 point, GroundEffect kind, float reachOut = 0f)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                SpellZone z = all[i];
                if (z == null || z.effect != kind) continue;
                if (z.Contains(point, 0.6f, reachOut)) return z;
            }
            return null;
        }

        /// <summary>The newest patch of any kind under a point, or null.</summary>
        public static SpellZone AnyAt(Vector3 point, float reachOut = 0f)
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                SpellZone z = all[i];
                if (z != null && z.Contains(point, 0.6f, reachOut)) return z;
            }
            return null;
        }

        /// <summary>A patch that carries lightning -- ice or water -- under a point, or null.</summary>
        public static SpellZone ConductorAt(Vector3 point)
        {
            SpellZone ice = At(point, GroundEffect.Ice);
            SpellZone water = At(point, GroundEffect.Water);
            if (ice == null) return water;
            if (water == null) return ice;
            return all.IndexOf(ice) > all.IndexOf(water) ? ice : water;   // the newer one
        }

        /// <summary>
        /// The same patch on another machine: every copy of a patch sits at the same centre, because the
        /// server sends the centre its own copy settled on. Used to change or remove it everywhere.
        /// </summary>
        public static SpellZone Find(Vector3 centre, GroundEffect kind)
        {
            SpellZone best = null;
            float bestDistance = 0.5f;
            foreach (SpellZone z in all)
            {
                if (z == null || z.effect != kind) continue;
                float d = Vector3.Distance(z.transform.position, centre);
                if (d < bestDistance) { bestDistance = d; best = z; }
            }
            return best;
        }

        /// <summary>
        /// An updraft that a projectile at <paramref name="point"/> is flying through, or null. The air
        /// column over the patch reaches <see cref="DraftHeight"/> up.
        /// </summary>
        public static SpellZone DraftAt(Vector3 point)
        {
            foreach (SpellZone z in all)
            {
                if (z == null || z.effect != GroundEffect.Updraft) continue;
                Vector3 offset = point - z.transform.position;
                if (offset.y < 0f || offset.y > DraftHeight) continue;
                if (new Vector2(offset.x, offset.z).magnitude <= ReachTowards(z.reach, offset)) return z;
            }
            return null;
        }

        /// <summary>How high the updraft rises -- high enough to catch a shot aimed at somebody's head.</summary>
        public const float DraftHeight = 4f;

        public ulong Owner { get { return owner; } }
        public ulong Attacker { get { return attacker; } }
        public float Remaining { get { return remaining; } }
        public float Strength { get { return strength; } }
        public Color Colour { get { return colour; } }

        /// <summary>A fire already fanned by air cannot be fanned again -- one push, then it stays put.</summary>
        public bool Fanned { get; private set; }
        public void MarkFanned() { Fanned = true; }

        /// <summary>Takes this patch away, on this machine.</summary>
        public void Remove()
        {
            all.Remove(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// Everybody standing on the patch, by their motor -- the ones air can send sliding across ice.
        /// Dummies have no motor and stay where they are.
        /// </summary>
        public List<PlayerMotor> MotorsTouching()
        {
            var found = new List<PlayerMotor>();
            foreach (Target t in Occupants())
                if (t.Touching && t.Motor != null) found.Add(t.Motor);
            return found;
        }

        bool Contains(Vector3 point, float verticalSlack, float reachOut = 0f)
        {
            Vector3 offset = point - transform.position;
            if (Mathf.Abs(offset.y) > verticalSlack) return false;
            return new Vector2(offset.x, offset.z).magnitude <= ReachTowards(reach, offset) + reachOut;
        }

        /// <summary>
        /// Lightning into ice: the whole patch carries the charge, and everyone TOUCHING it takes the hit
        /// -- standing on it, not jumping over it. The caster's own ice included; it is their risk.
        ///
        /// Server only; everyone else is sent the picture (PlayerNet.BroadcastCharge). Returns where the
        /// victims were, for that picture.
        /// </summary>
        public List<Vector3> Discharge(float damage, ulong byWhom)
        {
            var hit = new List<Vector3>();
            foreach (Target t in Occupants())
            {
                if (!t.Touching) continue;
                if (t.Health != null && authoritative) t.Health.TakeDamage(damage, byWhom);
                hit.Add(t.Root.position + Vector3.up * 1f);
            }
            return hit;
        }

        /// <summary>The visible half of a discharge: the patch flares, arcs run to whoever it caught.</summary>
        public void ShowDischarge(Vector3 struck, List<Vector3> victims)
        {
            flashUntil = Time.time + 0.45f;
            Vector3 centre = transform.position + Vector3.up * 0.05f;
            ChargeArc.Spawn(struck, centre);
            foreach (Vector3 v in victims) ChargeArc.Spawn(centre, v);
        }

        void OnDestroy()
        {
            all.Remove(this);
            // taken away before its time -- over the limit, or turned into something else
            if (fx != null && remaining > 0.3f) fx.Fade();
            MeshFilter f = GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
        }

        void Update()
        {
            remaining -= Time.deltaTime;

            // Dims over its last second so it never vanishes mid-fight without warning. Flares white
            // for a moment when lightning runs through it.
            float alpha = Mathf.Clamp01(remaining);
            if (fx != null) outline.enabled = Time.time < flashUntil;
            if (Time.time < flashUntil)
                material.SetColor("_EmissionColor", Color.Lerp(colour, Color.white, 0.7f) * 3.5f);
            else
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

                    case GroundEffect.Ice:
                        // Grip, not speed. You keep whatever you were doing when you stepped on, and
                        // changing it takes most of a second -- which in a game about sidestepping is the
                        // thing that matters. Re-applied each tick for a little longer than a tick, so
                        // it holds while you are on it and lets go almost as you step off.
                        if (t.Motor != null && t.Motor.IsLocallyControlled && t.Touching)
                            t.Motor.ApplySlippery(strength, TickInterval + 0.15f);
                        break;

                    case GroundEffect.Shock:
                        // Stings, and takes the glyph out of the hand of anyone drawing in it: to cast,
                        // step out first. Standing in it, not jumping over it.
                        if (authoritative && t.Health != null && t.Touching) t.Health.TakeDamage(strength * TickInterval, attacker);
                        if (t.Motor != null && t.Motor.IsLocallyControlled && t.Touching)
                        {
                            GestureCaster hands = t.Motor.GetComponent<GestureCaster>();
                            if (hands != null) hands.Interrupt();
                        }
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

            /// <summary>Feet on the patch, as opposed to above it in a jump.</summary>
            public bool Touching;
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

                found.Add(new Target { Root = root, Health = hp, Motor = motor, Touching = IsTouching(c, motor, centre) });
            }
            return found;
        }

        /// <summary>
        /// Standing on the patch. For a player that is their grounded flag -- read off the network for
        /// somebody else's body, whose motor is not running here -- and feet at the patch's height. A
        /// dummy has no motor and never leaves the floor.
        /// </summary>
        static bool IsTouching(Collider c, PlayerMotor motor, Vector3 centre)
        {
            if (motor == null) return Mathf.Abs(c.bounds.min.y - centre.y) < 0.35f;

            PlayerNet net = motor.GetComponent<PlayerNet>();
            bool grounded = net != null && net.IsRemote ? net.Grounded.Value : motor.IsGrounded;
            return grounded && Mathf.Abs(motor.transform.position.y - centre.y) < 0.35f;
        }
    }
}
