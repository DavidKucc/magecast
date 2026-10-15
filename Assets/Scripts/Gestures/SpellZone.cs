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

        /// <summary>
        /// How much the ground may rise or fall between two probes a step apart and still be the same
        /// surface -- a slope of about 40 degrees. The lip of a platform or a step is more, and ends it.
        /// </summary>
        const float StepRise = 0.2f;
        const float ProbeStep = 0.25f;

        /// <summary>Rings of the patch along each spoke, so it lies on a hill instead of cutting through it.</summary>
        const int Rings = 5;

        /// <summary>How far the patch reaches in each spoke direction, after the ground has run out.</summary>
        float[] reach;

        /// <summary>The ground's height under the patch, relative to its centre: spoke * Rings + ring - 1.</summary>
        float[] heights;

        /// <summary>The ground a patch lies on: its reach along each spoke and its height at each ring.</summary>
        public struct Shape
        {
            public float[] Reach;
            public float[] Heights;
        }

        /// <summary>
        /// How far a patch centred on <paramref name="ground"/> can spread in each direction before the
        /// surface under it ends -- the edge of a platform, the lip of a step -- and the shape of that
        /// surface on the way, for lying on it.
        ///
        /// A patch is a thing lying on the ground, so where there is no ground there is no patch. It used
        /// to be a flat disc of the full radius, and one landing near the edge of a platform hung out over
        /// the drop like a tray. Probed straight down, walking outwards along each spoke: each probe may be
        /// a little higher or lower than the one before (a hill), not a step higher or lower (a ledge).
        /// Until 13 Oct every probe had to be within 0.15 m of the centre's height, and on the Valley's
        /// grass a patch was cut to a strip along the slope and lay flat, half buried in the hill.
        /// </summary>
        public static Shape SupportedShape(Vector3 ground, float radius)
        {
            return SupportedShape(ground, radius, Vector3.zero, 1f);
        }

        /// <summary>
        /// The same, for an oval: <paramref name="radius"/> along <paramref name="axis"/>, that times
        /// <paramref name="across"/> across it (1 is a circle). Meltwater from a wall lies along the wall.
        /// </summary>
        public static Shape SupportedShape(Vector3 ground, float radius, Vector3 axis, float across)
        {
            var shape = new Shape { Reach = new float[Spokes], Heights = new float[Spokes * Rings] };
            axis.y = 0f;
            bool oval = across < 0.999f && axis.sqrMagnitude > 1e-4f;
            if (oval) axis.Normalize();
            float full = radius;
            var rs = new List<float>();
            var ys = new List<float>();

            for (int i = 0; i < Spokes; i++)
            {
                float a = i / (float)Spokes * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                radius = oval ? full * OvalReach(dir, axis, across) : full;
                rs.Clear();
                ys.Clear();
                rs.Add(0f);
                ys.Add(0f);

                // coarse steps to find the gap, then halving to put the edge within a centimetre
                float good = 0f, goodY = 0f, reached = radius, y;
                for (float r = ProbeStep; ; r += ProbeStep)
                {
                    float at = Mathf.Min(r, radius);
                    if (!Probe(ground, dir, at, goodY, Rise(at - good), out y))
                    {
                        float bad = at;
                        for (int k = 0; k < 5; k++)
                        {
                            float mid = (good + bad) * 0.5f;
                            if (Probe(ground, dir, mid, goodY, Rise(mid - good), out y))
                            {
                                good = mid;
                                goodY = y;
                                rs.Add(mid);
                                ys.Add(y);
                            }
                            else bad = mid;
                        }
                        reached = good;
                        break;
                    }
                    good = at;
                    goodY = y;
                    rs.Add(at);
                    ys.Add(y);
                    if (at >= radius) break;
                }

                shape.Reach[i] = reached;
                for (int k = 1; k <= Rings; k++)
                    shape.Heights[i * Rings + k - 1] = Along(rs, ys, reached * k / Rings);
            }
            return shape;
        }

        /// <summary>An ellipse's radius in direction <paramref name="dir"/>, as a share of its long half-axis.</summary>
        static float OvalReach(Vector3 dir, Vector3 axis, float across)
        {
            float c = Vector3.Dot(dir, axis);
            float s2 = Mathf.Max(0f, 1f - c * c);
            return across / Mathf.Sqrt(across * across * c * c + s2);
        }

        /// <summary>How far the patch reaches along each spoke -- for the aim marker, which shows the patch ahead.</summary>
        public static float[] SupportedReach(Vector3 ground, float radius)
        {
            return SupportedShape(ground, radius).Reach;
        }

        static float Rise(float distance)
        {
            return Mathf.Max(0.05f, distance / ProbeStep * StepRise);
        }

        /// <summary>
        /// The ground <paramref name="r"/> out along a spoke, if it is within <paramref name="tolerance"/>
        /// of the height expected there. <paramref name="y"/> is relative to the centre.
        /// </summary>
        static bool Probe(Vector3 ground, Vector3 dir, float r, float expected, float tolerance, out float y)
        {
            y = expected;
            Vector3 from = ground + dir * r + Vector3.up * (expected + 0.8f);
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 0.8f + tolerance + 0.05f, ~0,
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
            if (nearest == float.MaxValue || best.normal.y <= Projectile.FloorNormal) return false;

            y = best.point.y - ground.y;
            return Mathf.Abs(y - expected) <= tolerance;
        }

        /// <summary>The height at <paramref name="r"/> along a spoke, between the probes taken.</summary>
        static float Along(List<float> rs, List<float> ys, float r)
        {
            for (int i = 1; i < rs.Count; i++)
                if (rs[i] >= r)
                {
                    float span = rs[i] - rs[i - 1];
                    return span > 1e-4f ? Mathf.Lerp(ys[i - 1], ys[i], (r - rs[i - 1]) / span) : ys[i];
                }
            return ys[ys.Count - 1];
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

        /// <summary>The height of the ground the patch lies on under a point, relative to its centre.</summary>
        float HeightAt(Vector3 offset)
        {
            if (heights == null) return 0f;
            float a = Mathf.Atan2(offset.z, offset.x);
            if (a < 0f) a += Mathf.PI * 2f;
            float f = a / (Mathf.PI * 2f) * Spokes;
            int i0 = Mathf.FloorToInt(f) % Spokes;
            int i1 = (i0 + 1) % Spokes;
            float flat = new Vector2(offset.x, offset.z).magnitude;
            return Mathf.Lerp(SpokeHeight(i0, flat), SpokeHeight(i1, flat), f - Mathf.Floor(f));
        }

        float SpokeHeight(int spoke, float flat)
        {
            float r = reach[spoke];
            if (r < 1e-3f) return 0f;
            float ring = Mathf.Clamp(flat / r * Rings, 0f, Rings);
            int k = Mathf.Min(Mathf.FloorToInt(ring), Rings - 1);
            float inner = k == 0 ? 0f : heights[spoke * Rings + k - 1];
            float outer = heights[spoke * Rings + k];
            return Mathf.Lerp(inner, outer, ring - k);
        }

        /// <summary>
        /// The patch as a mesh: a fan of spokes cut to the reach, each with rings that follow the ground,
        /// lifted a hair so it does not z-fight the floor.
        /// </summary>
        static Mesh BuildMesh(Shape shape)
        {
            var vertices = new Vector3[1 + Spokes * Rings];
            var triangles = new List<int>(Spokes * (1 + 2 * (Rings - 1)) * 3);
            vertices[0] = Vector3.zero;
            for (int i = 0; i < Spokes; i++)
            {
                float a = i / (float)Spokes * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int k = 1; k <= Rings; k++)
                    vertices[1 + i * Rings + k - 1] = dir * (shape.Reach[i] * k / Rings)
                                                      + Vector3.up * shape.Heights[i * Rings + k - 1];
            }

            // wound so the faces point up
            for (int i = 0; i < Spokes; i++)
            {
                int next = (i + 1) % Spokes;
                triangles.Add(0);
                triangles.Add(1 + next * Rings);
                triangles.Add(1 + i * Rings);
                for (int k = 1; k < Rings; k++)
                {
                    int innerCur = 1 + i * Rings + k - 1, innerNext = 1 + next * Rings + k - 1;
                    int outerCur = innerCur + 1, outerNext = innerNext + 1;
                    triangles.Add(innerCur); triangles.Add(innerNext); triangles.Add(outerNext);
                    triangles.Add(innerCur); triangles.Add(outerNext); triangles.Add(outerCur);
                }
            }

            var mesh = new Mesh { name = "SpellZone" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static SpellZone Spawn(Vector3 at, GroundEffect effect, float radius, float lifetime,
                                      float strength, Color colour,
                                      bool authoritative = true, ulong attacker = Health.NoAttacker,
                                      ulong owner = Health.NoAttacker,
                                      Vector3 ovalAxis = default(Vector3), float ovalAcross = 1f)
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

            // Cut to the ground it actually lies on, so it ends where the platform ends, and shaped to it,
            // so it lies on a hill rather than cutting through one.
            Shape shape = SupportedShape(ground, radius, ovalAxis, ovalAcross);
            float[] reach = shape.Reach;
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(shape);
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
            z.heights = shape.Heights;
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
                float above = offset.y - z.HeightAt(offset);
                if (above < 0f || above > DraftHeight) continue;
                if (new Vector2(offset.x, offset.z).magnitude <= ReachTowards(z.reach, offset)) return z;
            }
            return null;
        }

        /// <summary>How high the updraft rises -- high enough to catch a shot aimed at somebody's head.</summary>
        public const float DraftHeight = 4f;

        public ulong Owner { get { return owner; } }
        public ulong Attacker { get { return attacker; } }

        /// <summary>Whose patch it is, on every machine (the attacker is known only on the server).</summary>
        ulong Laid { get { return owner != Health.NoAttacker ? owner : attacker; } }
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
            if (Mathf.Abs(offset.y - HeightAt(offset)) > verticalSlack) return false;
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
                        // Wet feet freeze to it -- whether they walked on or it was laid under them. On
                        // every machine (each sees the same wet body touch the same ice); Frozen holds
                        // the body still only where it is moved from.
                        if (t.Touching && Wet.Is(t.Health != null ? (Component)t.Health : t.Root))
                            Frozen.Apply(t.Health, t.Motor, t.Root, SpellTiers.WetFreezeSeconds);
                        break;

                    case GroundEffect.Shock:
                        // Stings, and takes the glyph out of the hand of anyone drawing in it: to cast,
                        // step out first. Standing in it, not jumping over it.
                        if (authoritative && t.Health != null && t.Touching) t.Health.TakeDamage(strength * TickInterval, attacker);
                        if (t.Motor != null && t.Motor.IsLocallyControlled && t.Touching
                            && !MageCast.Teams.Spares(Laid, t.Motor))
                        {
                            GestureCaster hands = t.Motor.GetComponent<GestureCaster>();
                            if (hands != null) hands.Interrupt(true);
                        }
                        break;

                    case GroundEffect.Updraft:
                        // Once per person per patch. Without that, a two-second updraft is a trampoline,
                        // and "you walked into it" turns into "you cannot get out of it".
                        if (t.Motor != null && t.Motor.IsLocallyControlled && t.Motor.IsGrounded
                            && !launched.Contains(t.Motor))
                        {
                            // Your own updraft throws you twice as high: 4 m, onto a roof or a bridge.
                            // Air as a way up, not only a way to move other people. A teammate's
                            // does the same for you -- a lift up for the side.
                            PlayerNet who = t.Motor.GetComponent<PlayerNet>();
                            ulong mine = Laid;
                            bool own = who != null ? who.IsSpawned && (who.AttackerId == mine || MageCast.Teams.Spares(mine, who))
                                                   : mine == Health.NoAttacker;
                            t.Motor.Launch(own ? SpellTiers.UpdraftSelfLaunch : strength);
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
                float above = offset.y - HeightAt(offset);              // over the ground the patch lies on
                if (above < -0.4f || above > Height) continue;

                found.Add(new Target { Root = root, Health = hp, Motor = motor,
                                       Touching = IsTouching(c, motor, centre.y + HeightAt(offset)) });
            }
            return found;
        }

        /// <summary>
        /// Standing on the patch. For a player that is their grounded flag -- read off the network for
        /// somebody else's body, whose motor is not running here -- and feet at the patch's height where
        /// they stand (<paramref name="surface"/>: on a hill, not the height of its centre). A dummy has
        /// no motor and never leaves the floor.
        /// </summary>
        static bool IsTouching(Collider c, PlayerMotor motor, float surface)
        {
            if (motor == null) return Mathf.Abs(c.bounds.min.y - surface) < 0.35f;

            PlayerNet net = motor.GetComponent<PlayerNet>();
            bool grounded = net != null && net.IsRemote ? net.Grounded.Value : motor.IsGrounded;
            return grounded && Mathf.Abs(motor.transform.position.y - surface) < 0.35f;
        }
    }
}
