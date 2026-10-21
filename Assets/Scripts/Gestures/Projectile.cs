using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Placeholder projectile: swept by hand with a SphereCast rather than given a Rigidbody, because
    /// a fast rigidbody tunnels through thin geometry and this arena is full of 1 m fins.
    ///
    /// What it does on impact depends on WHAT it hit, and that decision lives here:
    ///
    ///   a person   damage, knockback, and whatever the spell does on a direct hit
    ///   a wall     bounce, burst, or nothing -- per spell
    ///   the floor  leave a patch, or nothing -- per spell
    ///
    /// Wall and floor are told apart by the surface normal the sweep already reports, so it is
    /// deterministic and needs no tagging of the level: a surface facing up is floor, anything else is
    /// wall. Which also means an enemy's barrier counts as a wall, so fire can be bounced off one.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        /// <summary>
        /// Above this, a surface counts as floor. 0.6 is about 53 degrees from vertical: ramps and steps
        /// are floor, the sides of cover are wall.
        /// </summary>
        public const float FloorNormal = 0.6f;

        Vector3 velocity;
        float gravity;             // 0 = flies straight; negative = arcs
        float radius;
        float remaining;
        Transform owner;
        Color colour;
        float areaRadius;          // legacy ballistic puddle
        float areaLifetime;
        float knockback;
        float damage;
        float power = 1f;
        float sizeScale = 1f;

        Spell spell;               // surface behaviour; null for anything that has none
        int bouncesLeft;

        /// <summary>
        /// Whether this copy may do anything to anybody. In a networked game every machine flies its
        /// own copy of each spell so everyone sees it, but only the server's hurts, shoves or leaves a
        /// patch -- otherwise a hit would land once per machine watching it.
        /// </summary>
        bool authoritative = true;
        ulong attacker = Health.NoAttacker;

        /// <summary>
        /// The collider just bounced off, ignored for one sweep. The projectile leaves the bounce
        /// touching that wall, and a sweep that starts touching a collider reports a hit at distance
        /// zero -- it would bounce again on the spot, forever.
        /// </summary>
        Collider ignoreOnce;

        /// <summary>I, II or III -- see SpellTiers. II until told otherwise: the spell as designed.</summary>
        int tier = 2;

        /// <summary>Sets the tier. A tier I spell is the plain hit: it does not bounce.</summary>
        /// <summary>Set on the caster's own copy only: the shot's fate goes into <see cref="HitStats"/>.</summary>
        string statsName;
        bool statsDone;

        public void CountForStats(string spellName) { statsName = spellName; }

        void Tally(ShotOutcome outcome)
        {
            if (statsName == null || statsDone) return;
            statsDone = true;
            HitStats.Record(statsName, outcome);
        }

        /// <summary>A shot that hit nothing it wanted: near somebody, or a clean miss.</summary>
        void TallyMiss(Vector3 at)
        {
            if (statsName == null || statsDone) return;
            foreach (Collider c in Physics.OverlapSphere(at, HitStats.NearRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                if (owner != null && c.transform.IsChildOf(owner)) continue;
                if (MageCast.Teams.Spares(owner, c)) continue;
                Tally(ShotOutcome.Near);
                return;
            }
            Tally(ShotOutcome.Miss);
        }

        public void SetTier(int value)
        {
            tier = Mathf.Clamp(value, 1, 3);
            if (tier < 2) bouncesLeft = 0;
        }

        bool IsFire { get { return spell != null && spell.groundEffect == GroundEffect.Burn; } }
        bool IsIce { get { return spell != null && spell.groundEffect == GroundEffect.Ice && !watery; } }

        /// <summary>An ice bolt that went through a line of fire: the ice's hit, no slow, and it soaks (Wet).</summary>
        bool watery;
        public bool IsWater { get { return watery; } }
        bool IsAir { get { return spell != null && spell.groundEffect == GroundEffect.Updraft; } }
        bool IsLightning { get { return spell != null && spell.chargeDamage > 0f; } }
        bool IsEarth { get { return spell != null && spell.groundEffect == GroundEffect.Rubble; } }

        /// <summary>A lobbed earth's ring on the floor where it will land (LandingMark), gone when it lands.</summary>
        LandingMark landing;

        SpellFx.Entry fxEntry;     // this spell's real effects, or null for the placeholder look
        GameObject fx;             // the flying effect, riding on this object
        bool impactShown;

        /// <summary>
        /// How hard an updraft pushes a shot upwards, m/s^2. Across a 4 m draft lightning (44 m/s) gains
        /// about 6 m/s of climb, fire about 10, ice about 16 -- enough that none of them arrive where
        /// they were aimed, and the slower the shot, the more completely it is thrown off.
        /// </summary>
        const float DraftLift = 70f;

        // ---- aim assist
        // A shot that is about to miss somebody by a little bends towards them -- gently. A target is
        // taken only if it is close to the shot (AssistRange), nearly straight ahead of it (within
        // AssistCone of where it is flying), in the open (nothing solid between) and not on the caster's
        // side. The turn is slow and capped for the whole flight (AssistBudget), so a near miss becomes a
        // hit while a shot aimed elsewhere stays aimed elsewhere. It turns by the metre flown, not by the
        // second, so a slow ice is helped exactly as much as a fast lightning -- by time, the slow one
        // had three times as long to bend and became hard to dodge. About 0.15 m of help over the last
        // 6 m, about 0.45 m over the last 10 -- a nudge, never round a corner.
        //
        // People's shots only, every person's alike; not bots'. Bots fire from close range and roughly
        // on target, so the assist landed nearly all of it: six 60 % bots went from 43 % to 61-66 % hits
        // at twice this strength and still 57 % at this one (16 Oct), and their difficulty is tuned on
        // their own aim (BotBrain.AimFor). Aim assist is for hands, as in any shooter.
        public static bool AimAssist = true;
        public const float AssistRange = 10f;
        // (17 Oct: 14 deg / 0.7 deg/m / 7 deg bent too visibly for the player -- a third less)
        public const float AssistCone = 10f;
        public const float AssistPerMetre = 0.45f;   // degrees of turn per metre flown, at most
        public const float AssistBudget = 4.5f;
        const float ArcStart = 0.2f;                 // the pull grows over the outer fifth of the range
        const float ArcEase = 12f;                   // how quickly what is left of the angle is taken, per second
        float assisted;
        int assistFor = -1;          // -1 not yet known, 0 a bot's (or nobody's) shot, 1 a person's

        public static Projectile Spawn(Vector3 origin, Vector3 direction, float speed, float radius,
                                       float lifetime, Color colour, Transform owner, float power = 1f,
                                       float gravity = 0f, float areaRadius = 0f, float areaLifetime = 0f,
                                       float knockback = 0f, float damage = 0f,
                                       Spell spell = null, float sizeScale = 1f,
                                       bool authoritative = true, ulong attacker = Health.NoAttacker)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            Destroy(go.GetComponent<Collider>());          // we sweep it ourselves
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (radius * 2f);

            Renderer r = go.GetComponent<Renderer>();
            // Bloom (World.PostFx) can be switched off, so a crit has to read as a crit by raw emission
            // and size alone. Both scale with the quality of the stroke that produced it.
            Material m = RuntimeMaterials.Emissive(colour, 1.2f + power * 0.9f);
            r.material = m;

            Projectile p = go.AddComponent<Projectile>();
            p.velocity = direction.normalized * speed;
            p.gravity = gravity;
            p.radius = radius;
            p.remaining = lifetime;
            p.owner = owner;
            p.colour = colour;
            p.areaRadius = areaRadius;
            p.areaLifetime = areaLifetime;
            p.knockback = knockback;
            p.damage = damage;
            p.power = power;
            p.sizeScale = sizeScale;
            p.spell = spell;
            p.bouncesLeft = spell != null ? spell.wallBounces : 0;
            p.authoritative = authoritative;
            p.attacker = attacker;
            Live.Add(p);

            // The real effect, where the spell has one: the sphere stays as the thing that collides and
            // stops being the thing you see. Sized with the sphere, so a big draw still looks big.
            p.fxEntry = SpellFx.For(spell);
            if (p.fxEntry != null && p.fxEntry.projectile != null)
            {
                r.enabled = false;
                p.fx = SpellFx.Play(p.fxEntry.projectile, origin, Quaternion.LookRotation(direction),
                                    p.fxEntry.projectileScale * radius, go.transform);
                SpellFx.OneShot(p.fxEntry.launch, origin);
            }
            return p;
        }

        /// <summary>Gone: hit something for good, or ran out of time. Destroy only takes effect later.</summary>
        bool ended;

        void End()
        {
            TallyMiss(transform.position);      // anything not already counted as a hit
            ended = true;
            Destroy(gameObject);
        }

        /// <summary>The most a copy is moved on to make up for the time its cast spent on the network.</summary>
        public const float MaxCatchUp = 0.4f;

        /// <summary>
        /// Moves a copy on by the time its cast spent reaching this machine, in small steps that still
        /// hit whatever lies on the way -- so it flies where the caster's own shot is now, instead of
        /// setting off from the hand a ping late and landing (and hitting, and sounding) a ping late.
        /// </summary>
        public void CatchUp(float seconds)
        {
            seconds = Mathf.Min(seconds, MaxCatchUp);
            const float Slice = 1f / 60f;
            while (seconds > 1e-4f && !ended)
            {
                float dt = Mathf.Min(Slice, seconds);
                Step(dt);
                seconds -= dt;
            }
        }

        void Update()
        {
            if (!ended) Step(Time.deltaTime);
        }

        void Step(float dt)
        {
            // Integrated as a velocity rather than a fixed heading so the same component covers both
            // a flat bolt and a lob, and so a bounce is just a change of velocity.
            if (gravity != 0f) velocity += Vector3.up * (gravity * dt);

            Assist(dt);

            // Over an updraft the rising air bends the shot upwards and it sails over whoever stood
            // behind it -- a screen you can put down, not a mirror. Every shot, whoever cast it and
            // whoever laid the draft; a fast one is only nudged, a slow one is lifted clean away.
            if (SpellZone.DraftAt(transform.position) != null)
                velocity += Vector3.up * (DraftLift * dt);

            Vector3 step = velocity * dt;

            // Ice through a line of fire comes out the other side as water. Anybody's ice through anybody's
            // line -- every machine's copy flies the same path through the same line, so all of them turn.
            if (IsIce && step.sqrMagnitude > 1e-8f && FireLine.Crosses(transform.position, transform.position + step))
                BecomeWater();
            float distance = step.magnitude;
            if (distance > 1e-5f)
            {
                Vector3 heading = step / distance;
                // Two sweeps. People and barriers are hit by the whole ball -- that is what makes a slow
                // fat spell easy to land. The map is hit only by its core: by the whole ball, a 0.8 m air
                // spell aimed past the edge of a pillar stopped on it while the crosshair was clearly
                // beside it, and a shot over cover clipped the top.
                RaycastHit[] full = Physics.SphereCastAll(transform.position, radius, heading, distance,
                                                          ~0, QueryTriggerInteraction.Ignore);
                float coreRadius = Mathf.Min(radius, WallCore);
                RaycastHit[] core = Physics.SphereCastAll(transform.position, coreRadius, heading, distance,
                                                          ~0, QueryTriggerInteraction.Ignore);
                RaycastHit[] hits = new RaycastHit[full.Length + core.Length];
                full.CopyTo(hits, 0);
                core.CopyTo(hits, full.Length);
                float nearest = float.MaxValue;
                RaycastHit best = new RaycastHit();
                bool hitSomething = false;

                for (int i = 0; i < hits.Length; i++)
                {
                    // whole ball for targets, core for everything else
                    if (IsTarget(hits[i].collider) != (i < full.Length)) continue;
                    Transform t = hits[i].transform;
                    // Destroy() on our own collider only takes effect at the end of the frame, so on the
                    // first Update it is still there and a sweep from inside it reports a zero-distance
                    // hit -- the projectile would blow up on itself the instant it spawned.
                    if (t == transform) continue;
                    if (owner != null && (t == owner || t.IsChildOf(owner))) continue;
                    if (ignoreOnce != null && hits[i].collider == ignoreOnce) continue;
                    // through a teammate, as through yourself: they neither stop it nor feel it
                    if (MageCast.Teams.Spares(owner, hits[i].collider)) continue;
                    // a person (or a dummy wearing a person's body) is hit by the body (Hurtbox, below),
                    // not the capsule they walk with
                    if (hits[i].collider.GetComponentInParent<Hurtbox>() != null
                        || hits[i].collider.GetComponentInParent<PlayerMotor>() != null) continue;

                    // the caster's own dome lets the caster's spells out (and only theirs)
                    CastShield dome = hits[i].collider.GetComponentInParent<CastShield>();
                    if (dome != null && dome.IsDome && owner != null && dome.Follows == owner) continue;

                    // The floor is not found by the ball's edge -- see below. That includes the ball
                    // already dipping into the floor: a sweep that starts overlapping reports no normal at
                    // all, and read as a wall it bounced a fire straight back off the ground.
                    if (!IsTarget(hits[i].collider) && IsFloorContact(hits[i])) continue;

                    if (hits[i].distance < nearest)
                    {
                        nearest = hits[i].distance;
                        best = hits[i];
                        hitSomething = true;
                    }
                }
                ignoreOnce = null;

                // Where a spell lands on the floor is where its CENTRE meets it, not where the edge of the
                // ball first grazes it. A fat spell aimed at the floor a few metres off at a shallow angle
                // used to touch down two to four metres short -- a gust of air aimed into a fire came down
                // beside it instead, and the combination never happened. People, walls and barriers are
                // still hit by the whole ball.
                RaycastHit ground;
                if (Physics.Raycast(transform.position, heading, out ground, distance, ~0, QueryTriggerInteraction.Ignore)
                    && ground.normal.y > FloorNormal && !IsTarget(ground.collider)
                    && ground.transform != transform
                    && (owner == null || !(ground.transform == owner || ground.transform.IsChildOf(owner)))
                    && ground.distance < nearest)
                {
                    nearest = ground.distance;
                    best = ground;
                    hitSomething = true;
                }

                // People: the ball against the body's own parts, trunk, head and limbs as they stand.
                Hurtbox struckBody = null;
                Vector3 bodyPoint = Vector3.zero, bodyNormal = Vector3.zero;
                float bodyR = PersonRadius;
                Vector3 to = transform.position + heading * distance;
                foreach (Hurtbox body in Hurtbox.All)
                {
                    if (body == null || body.Health == null || body.Health.IsDead) continue;
                    Transform bt = body.transform;
                    if (owner != null && (bt == owner || bt.IsChildOf(owner))) continue;
                    if (MageCast.Teams.Spares(owner, body)) continue;
                    float share;
                    Vector3 bp, bn;
                    if (!body.Sweep(transform.position, to, bodyR, out share, out bp, out bn)) continue;
                    if (share * distance < nearest)
                    {
                        nearest = share * distance;
                        struckBody = body;
                        bodyPoint = bp;
                        bodyNormal = bn;
                        hitSomething = true;
                    }
                }
                if (struckBody != null)
                {
                    Impact(bodyPoint, bodyNormal, transform.position + heading * nearest, struckBody.transform,
                           struckBody.GetComponent<Collider>(), heading);
                    return;
                }

                if (hitSomething)
                {
                    // A sweep that starts already touching something reports point and normal as zero.
                    Vector3 point = best.point == Vector3.zero ? transform.position : best.point;
                    Vector3 normal = best.normal.sqrMagnitude < 0.01f ? -heading : best.normal;
                    Vector3 centreAtContact = transform.position + heading * nearest;

                    // either finished, or bounced and flying on from the wall next frame
                    Impact(point, normal, centreAtContact, best.transform, best.collider, heading);
                    return;
                }

                transform.position += step;
                if (fx != null) fx.transform.rotation = Quaternion.LookRotation(heading);
                if (gravity != 0f) transform.forward = heading;
            }

            remaining -= dt;
            if (remaining <= 0f) End();
        }

        /// <summary>How fat a spell is to walls, pillars and cover, whatever its size to people.</summary>
        const float WallCore = 0.12f;

        /// <summary>
        /// Whether a sweep hit is the ball touching the floor. A normal hit says so by its normal; a hit
        /// the sweep STARTED inside has no normal, so the floor is recognised by lying under the centre.
        /// </summary>
        bool IsFloorContact(RaycastHit hit)
        {
            if (hit.normal.sqrMagnitude > 0.01f && hit.distance > 0f) return hit.normal.y > FloorNormal;

            // Whatever tops out at or below the centre is underfoot. Asked of the bounds because at the
            // moment of landing the centre sits ON the surface, where the closest point is the centre
            // itself and has no direction -- read as a wall there, a fire bounced straight back up.
            if (hit.collider.bounds.max.y <= transform.position.y + 0.05f) return true;

            // A surface lying right under the centre is the floor, whatever its shape. Asked of the
            // collider itself, by a ray straight down: a terrain's bounds reach up to the mountain tops,
            // and ClosestPoint does not work on a terrain at all -- the Valley's grass was taken for a
            // wall, so a fire skimming it bounced back at its caster and never left a patch.
            RaycastHit below;
            if (hit.collider.Raycast(new Ray(transform.position + Vector3.up * 0.3f, Vector3.down), out below, 0.3f + WallCore + 0.2f)
                && below.normal.y > FloorNormal) return true;

            Vector3 closest = hit.collider.ClosestPoint(transform.position);
            Vector3 up = transform.position - closest;
            return up.sqrMagnitude > 1e-6f && up.normalized.y > FloorNormal;
        }

        /// <summary>Something a spell hits with its whole body: a person, a dummy, a barrier.</summary>
        /// <summary>
        /// How big the ball is against a body: its core, about what is seen of it -- a fat ice or a wide
        /// gust still hits barriers and walls by the whole ball, but a person only where it visibly meets
        /// them (16 Oct: ice passing 0.9 m from somebody's spine used to hit them).
        /// </summary>
        float PersonRadius { get { return Mathf.Min(radius, IsAir ? 0.45f : 0.3f); } }

        static bool IsTarget(Collider c)
        {
            return c.GetComponentInParent<Health>() != null || c.GetComponentInParent<PlayerMotor>() != null
                   || c.GetComponentInParent<CastShield>() != null || c.GetComponentInParent<IceWall>() != null;
        }

        /// <summary>Resolves a hit. Returns true if the projectile is finished, false if it bounced on.</summary>
        bool Impact(Vector3 at, Vector3 normal, Vector3 centreAtContact, Transform struck,
                    Collider collider, Vector3 heading)
        {
            // GetComponentInParent, because the thing a sweep reports is usually a child renderer or
            // collider rather than the object that owns the health.
            Health hp = struck != null ? struck.GetComponentInParent<Health>() : null;
            PlayerMotor motor = struck != null ? struck.GetComponentInParent<PlayerMotor>() : null;

            // runes cut into what it struck light up in its colour (the Sanctum's pillars and walls)
            World.RuneGlow.Struck(collider, colour);

            // A barrier stops the shot where it stands and pays for it in durability. No bounce: a
            // barrier that threw fire back at whoever fired it would be a reward for being shot at.
            CastShield shield = struck != null ? struck.GetComponentInParent<CastShield>() : null;
            if (shield != null)
            {
                // the ripple first: a hit that breaks it should still be seen landing
                Tally(ShotOutcome.Barrier);
                shield.Struck(at, BarrierWear() / 30f);
                if (authoritative) shield.Wear(BarrierWear());
                Flash(at, 1.1f, 2.2f);
                if (IsFire && tier >= 3) Explode(at, null);
                if (IsEarth) Quake(at - heading * 0.3f, null);
                Splash(at, -heading);
                End();
                return true;
            }

            // An ice wall: the first spell to hit it shatters it, and stops there. The server's copy breaks
            // it for everyone; a fire leaves its meltwater behind. Everybody else's copy only stops.
            IceWall iceWall = struck != null ? struck.GetComponentInParent<IceWall>() : null;
            if (iceWall != null)
            {
                Tally(ShotOutcome.Barrier);
                Flash(at, 1.1f, 2.2f);
                if (authoritative)
                    iceWall.Hit(IsFire, owner != null ? owner.GetComponent<PlayerNet>() : null, attacker);
                if (IsEarth) Quake(at - heading * 0.3f, null);
                End();
                return true;
            }

            if (hp != null || motor != null)
            {
                Tally(motor == null && hp != null && hp.GetComponent<Combat.TrainingDummy>() != null
                      ? ShotOutcome.Dummy : ShotOutcome.Player);
                // your own shot: its damage numbers are yours to see from any distance
                if (owner != null && MageCast.PlayerNet.Local != null && owner == MageCast.PlayerNet.Local.transform)
                    MageCast.Sight.MarkMyHit(hp != null ? hp.transform : motor.transform);

                // lightning into somebody wet: twice the hit, and it dries them -- on every machine, so the
                // soak goes for everyone at once; only the deciding copy deals the damage
                bool soaked = IsLightning && hp != null && Wet.Is(hp);
                if (authoritative) HitPerson(hp, motor, heading, soaked ? damage * SpellTiers.WetShockMultiplier : damage);
                if (soaked)
                {
                    Wet.Remove(hp);
                    WorldPopups.Word(hp.transform, "SHOCK x" + SpellTiers.WetShockMultiplier.ToString("0"), colour);
                }
                if (watery && hp != null) Wet.Apply(hp, SpellTiers.WetSeconds);
                Flash(at, 1.4f, 2.5f);
                TierHit(hp, at);
                if (IsEarth) Quake(at, hp);
                // earth III on somebody: the fence comes up round their feet -- the hit shuts them in (as
                // lightning III lays its patch under whoever it hit). Below III only a stone on the ground
                // raises it, so a II is a choice: hit them, or land beside them and fence them in.
                if (IsEarth && tier >= 3 && authoritative) FenceIn(hp, motor);
                End();
                return true;
            }

            // the top of a stone fence is no floor: whatever lands there would be left lying on stones that sink
            bool floor = normal.y > FloorNormal && !StoneFence.Is(collider);
            return floor ? HitFloor(at) : HitWall(at, normal, centreAtContact, collider);
        }

        /// <summary>
        /// What this shot takes off a barrier: its damage times how hard its element is on barriers,
        /// and never less than the element's floor -- air deals no damage but still nudges the count.
        /// </summary>
        float BarrierWear()
        {
            if (spell == null) return damage;
            float wear = Mathf.Max(damage * spell.barrierWear, spell.barrierWearFlat);
            if (IsFire && tier >= 3) wear *= SpellTiers.ExplosionBarrierWear;
            return wear;
        }

        void HitPerson(Health hp, PlayerMotor motor, Vector3 heading, float amount)
        {
            if (hp != null && amount > 0f) hp.TakeDamage(amount, attacker);

            // Lightning into somebody standing on ice runs through the ice as well: everyone else on
            // it takes the charge. Them too -- they are touching it.
            if (spell != null && spell.chargeDamage > 0f && hp != null)
            {
                // feet, not the middle: a dummy's origin is at its waist, a player's at its soles
                Collider body = hp.GetComponentInChildren<Collider>();
                Vector3 feet = motor != null ? motor.transform.position
                             : body != null ? new Vector3(hp.transform.position.x, body.bounds.min.y, hp.transform.position.z)
                             : hp.transform.position;
                DischargeIceAt(feet, feet + Vector3.up * 1.2f);
            }

            if (motor != null)
            {
                // a direct hit costs whatever they hold a tier
                GestureCaster struck = motor.GetComponent<GestureCaster>();
                if (struck != null) struck.Staggered();

                if (knockback > 0f)
                    // Along the flight path with a bit of lift, so it reads as being blown off your feet
                    // rather than shunted sideways.
                    motor.AddImpulse((heading + Vector3.up * 0.45f).normalized * knockback, attacker);

                // ice: slowed at every tier (tier I's one effect besides the hit), frozen in place at
                // III -- which still lets you draw
                if (spell != null && spell.hitSlowDuration > 0f && spell.hitSlow < 1f && !watery)
                {
                    if (tier >= 3) motor.ApplySlow(0f, SpellTiers.FreezeSeconds);
                    else motor.ApplySlow(spell.hitSlow, spell.hitSlowDuration);
                }

                if (spell != null && spell.interruptsDrawing)
                {
                    // Being shoved takes the hand off the glyph -- there is nothing left to salvage. A
                    // spell already drawn and HELD is untouched: the risk lives in drawing, not holding.
                    GestureCaster caster = motor.GetComponent<GestureCaster>();
                    if (caster != null) caster.Interrupt(true);
                }

            }
        }

        /// <summary>
        /// What a tier adds to a hit on somebody, on every machine -- each copy shows it, only the
        /// deciding copy (authoritative) deals it.
        /// </summary>
        void TierHit(Health hp, Vector3 at)
        {
            if (hp == null) { if (IsFire && tier >= 3) Explode(at, null); return; }

            if (IsFire && tier >= 2)
                Burning.Apply(hp, SpellTiers.BurnPerSecond * power, SpellTiers.BurnSeconds, attacker, authoritative, fxEntry);
            if (IsFire && tier >= 3)
                Explode(at, hp);

            if (IsIce && tier >= 3)
            {
                WorldPopups.Word(hp.transform, "FROZEN", colour);
                if (fxEntry != null && fxEntry.status != null)
                    SpellFx.Play(fxEntry.status, hp.transform.position, Quaternion.identity, fxEntry.statusScale);
                if (fxEntry != null) SpellFx.OneShot(fxEntry.statusSound, hp.transform.position, 0.8f);
            }

            if (IsLightning && tier >= 2)
            {
                // on to the nearest other body within reach, at half strength
                Health next = NearestOther(hp, at, SpellTiers.ChainRange);
                if (next != null)
                {
                    ChargeArc.Spawn(Centre(hp), Centre(next));
                    if (authoritative) next.TakeDamage(damage * SpellTiers.ChainShare, attacker);
                }
            }
            // lightning III: the shock patch appears right under whoever it hit
            if (IsLightning && tier >= 3 && authoritative)
            {
                Collider body = hp.GetComponentInChildren<Collider>();
                Vector3 feet = body != null ? new Vector3(hp.transform.position.x, body.bounds.min.y, hp.transform.position.z)
                                            : hp.transform.position;
                LayShock(feet);
            }
        }

        static readonly Color ShockColour = new Color(1f, 0.9f, 0.35f);

        /// <summary>
        /// Lightning's patch: nobody standing in it can draw, and a spell in hand runs down in it (SpellZone).
        /// It does not hurt. Server only; sent to all.
        /// </summary>
        void LayShock(Vector3 at)
        {
            if (!authoritative) return;
            float r = SpellTiers.ShockRadius * sizeScale;
            SpellZone zone = SpellZone.Spawn(at, GroundEffect.Shock, r, SpellTiers.ShockSeconds, 0f, ShockColour,
                                             true, attacker, attacker);
            PlayerNet caster = owner != null ? owner.GetComponent<PlayerNet>() : null;
            if (caster != null && caster.IsSpawned)
                caster.BroadcastZone(zone.transform.position, GroundEffect.Shock, r, SpellTiers.ShockSeconds, 0f, ShockColour);
        }

        Health NearestOther(Health from, Vector3 at, float reach)
        {
            Health best = null;
            float bestDistance = reach;
            Health mine = owner != null ? owner.GetComponentInParent<Health>() : null;
            foreach (Health h in FindObjectsOfType<Health>())
            {
                if (h == from || h == mine || h.IsDead) continue;
                if (MageCast.Teams.Spares(owner, h)) continue;      // the arc jumps to enemies only
                float d = Vector3.Distance(Centre(h), at);
                if (d < bestDistance) { bestDistance = d; best = h; }
            }
            return best;
        }

        static Vector3 Centre(Health h)
        {
            Collider c = h.GetComponentInChildren<Collider>();
            return c != null ? c.bounds.center : h.transform.position + Vector3.up;
        }

        /// <summary>
        /// Fire III: a blast around where it struck. Everyone in reach but the one it hit directly takes
        /// half the hit -- the caster too, if they are that close: point-blank fire costs.
        /// </summary>
        /// <summary>
        /// Tier I fire or ice landing on the map: whoever is within SplashRadius takes a share of the
        /// hit, most in the middle, nothing at the edge -- the caster too. Anything solid in between
        /// (a wall, a barrier) shields them. Damage only, and it is not a direct hit: nobody's held
        /// spell drops a tier from it. <paramref name="outward"/> points away from the surface struck.
        /// </summary>
        void Splash(Vector3 at, Vector3 outward)
        {
            if (!authoritative || tier > 1 || damage <= 0f || !(IsFire || IsIce || watery)) return;
            Vector3 origin = at + outward.normalized * 0.25f;
            float reach = SpellTiers.SplashRadius;
            var done = new System.Collections.Generic.HashSet<Health>();
            foreach (Collider c in Physics.OverlapSphere(origin, reach + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health hp = c.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || !done.Add(hp)) continue;
                float d;
                if (!Exposed(origin, hp.transform, c, out d) || d >= reach) continue;
                hp.TakeDamage(damage * SpellTiers.SplashShare * (1f - d / reach), attacker);
            }
        }

        /// <summary>
        /// Whether a blast at <paramref name="origin"/> reaches somebody: how near it comes to their body
        /// (<paramref name="distance"/>), and whether any of it is in the open to the blast -- the nearest
        /// part of them, their chest or their head. Cover in front of the chest no longer shields a head
        /// showing over it (19 Oct: an earth landing on a wall right by somebody's head did nothing, the
        /// line to the middle of their body ran through the wall).
        /// </summary>
        static bool Exposed(Vector3 origin, Transform root, Collider c, out float distance)
        {
            Hurtbox body = root.GetComponentInParent<Hurtbox>();
            Vector3 nearest, chest, head;
            if (body != null)
            {
                nearest = body.Nearest(origin);
                chest = body.Centre;
                head = body.Head;
            }
            else
            {
                nearest = c.ClosestPoint(origin);
                chest = c.bounds.center;
                head = new Vector3(chest.x, c.bounds.max.y - 0.15f, chest.z);
            }
            distance = Vector3.Distance(origin, nearest);
            return !Shielded(origin, nearest, root) || !Shielded(origin, chest, root) || !Shielded(origin, head, root);
        }

        /// <summary>Whether something solid that is not <paramref name="target"/> lies between two points.</summary>
        static bool Shielded(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.01f) return false;
            foreach (RaycastHit h in Physics.RaycastAll(from, d / length, length, ~0, QueryTriggerInteraction.Ignore))
                if (!h.transform.IsChildOf(target)) return true;
            return false;
        }

        void Explode(Vector3 at, Health exclude)
        {
            if (fxEntry != null && fxEntry.special != null)
            {
                SpellFx.PlayBurst(fxEntry.special, at, fxEntry.specialScale * sizeScale, fxEntry.specialLeadIn);
                SpellFx.OneShot(fxEntry.specialSound, at);
            }
            else FlashAt(at, colour, SpellTiers.ExplosionRadius * 2f * sizeScale, 2.5f);

            if (!authoritative) return;
            float reach = SpellTiers.ExplosionRadius * sizeScale;
            var done = new System.Collections.Generic.HashSet<Health>();
            foreach (Collider c in Physics.OverlapSphere(at, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h == exclude || h.IsDead || !done.Add(h)) continue;
                h.TakeDamage(damage * SpellTiers.ExplosionShare, attacker);
            }
        }

        /// <summary>
        /// Air III landing in a patch: everyone in it or just outside it is thrown once into its middle
        /// -- into your fire, onto your ice. The caster too, if they are standing there.
        /// </summary>
        void AirBomb(SpellZone zone)
        {
            // Not at once: the bomb winds up for BombDelay first. It reaches 1.5 m past the patch, so
            // somebody walking round your fire a metre off was pulled in with no chance to react; now
            // they see the air land, the burst gather, and have that long to step out of reach.
            // The pack's own wind-up is the telegraph -- most of it is kept this time, so it ends on the bang.
            Vector3 centre = zone.transform.position;
            float radius = zone.Radius;
            if (fxEntry != null && fxEntry.special != null)
            {
                SpellFx.PlayBurst(fxEntry.special, centre, fxEntry.specialScale * radius / 3f,
                                  Mathf.Max(0f, fxEntry.specialLeadIn - SpellTiers.BombDelay));
                AudioClip bang = fxEntry.specialSound;
                Delayed.Run(SpellTiers.BombDelay, () => SpellFx.OneShot(bang, centre));
            }
            else FlashAt(centre, colour, radius * 2f, 1.6f);

            if (!authoritative) return;
            Transform by = owner;
            Delayed.Run(SpellTiers.BombDelay, () => BombToss(centre, radius, by));
        }

        /// <summary>The bang itself: everyone in reach thrown once into the middle -- but the bomber's teammates.</summary>
        static void BombToss(Vector3 centre, float radius, Transform by)
        {
            float reach = radius + SpellTiers.BombReachBeyondPatch;
            float airTime = 2f * SpellTiers.BombLift / PlayerMotor.Gravity;
            foreach (PlayerMotor m in FindObjectsOfType<PlayerMotor>())
            {
                if (MageCast.Teams.Spares(by, m)) continue;
                Vector3 offset = centre - m.transform.position;
                if (Mathf.Abs(offset.y) > 2f) continue;
                Vector3 flat = new Vector3(offset.x, 0f, offset.z);
                if (flat.magnitude > reach) continue;
                m.Toss(flat / airTime, SpellTiers.BombLift);
            }
        }

        bool HitWall(Vector3 at, Vector3 normal, Vector3 centreAtContact, Collider collider)
        {
            if (bouncesLeft > 0)
            {
                bouncesLeft--;
                velocity = Vector3.Reflect(velocity, normal);
                damage *= spell != null ? spell.bounceDamageKeep : 1f;

                // Stepped a hair off the wall so the next sweep does not start inside it.
                transform.position = centreAtContact + normal * 0.03f;
                ignoreOnce = collider;

                // a small spark, so a bounce is seen as a bounce and not as the shot changing its mind --
                // never the full impact, which would read as the spell having ended there
                FlashAt(at, colour, 0.8f, 2f);
                return false;
            }

            if (spell != null && spell.wallBurstRadius > 0f)
                WallBurst(at, normal, spell.wallBurstRadius * sizeScale);
            if (IsFire && tier >= 3) Explode(at, null);
            if (IsEarth) Quake(at + normal * 0.3f, null);
            Splash(at, normal);

            if (areaRadius > 0f && authoritative)
                PoisonPuddle.Spawn(at, areaRadius, areaLifetime, colour);
            else
                Flash(at, 1.4f, 2.5f);

            End();
            return true;
        }

        /// <summary>
        /// Runs a lightning charge through the ice patch under <paramref name="floorPoint"/>, if there is
        /// one. Server only; everyone else is shown it. Returns whether there was ice to charge.
        /// </summary>
        bool DischargeIceAt(Vector3 floorPoint, Vector3 struck)
        {
            if (!authoritative) return false;
            SpellZone ice = SpellZone.ConductorAt(floorPoint);
            if (ice == null) return false;

            // Scaled by how well the lightning was drawn, like every other hit. It is the strongest
            // combination in the game on purpose: it costs two casts, two windows of being exposed, and
            // the ice gave the target seconds of warning -- it has to be worth more than two hits.
            float charge = spell.chargeDamage * power;
            var victims = ice.Discharge(charge, attacker);
            ice.ShowDischarge(struck, victims);

            PlayerNet caster = owner != null ? owner.GetComponent<PlayerNet>() : null;
            if (caster != null && caster.IsSpawned)
                caster.BroadcastCharge(ice.transform.position, struck, victims.ToArray());
            return true;
        }

        static readonly Color WaterColour = new Color(0.2f, 0.45f, 0.95f);

        /// <summary>Whether the first solid thing on the way to <paramref name="target"/> is the target itself.</summary>
        bool InTheOpen(Vector3 from, Vector3 dir, float distance, Health target)
        {
            RaycastHit[] hits = Physics.RaycastAll(from, dir, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider first = null;
            foreach (RaycastHit hit in hits)
            {
                Transform t = hit.transform;
                if (t == transform || (owner != null && (t == owner || t.IsChildOf(owner)))) continue;
                if (MageCast.Teams.Spares(owner, hit.collider)) continue;
                if (hit.distance < nearest) { nearest = hit.distance; first = hit.collider; }
            }
            return first == null || first.GetComponentInParent<Health>() == target;
        }

        /// <summary>Bends the flight a little towards an enemy nearly in its path -- see AimAssist.</summary>
        void Assist(float dt)
        {
            if (!AimAssist || assisted >= AssistBudget || dt <= 0f) return;
            if (assistFor < 0)
            {
                PlayerNet by = owner != null ? owner.GetComponent<PlayerNet>() : null;
                assistFor = by != null && !by.IsBot ? 1 : 0;
            }
            if (assistFor == 0 || gravity != 0f) return;     // a lob lands where it was thrown; nothing to bend
            float speed = velocity.magnitude;
            if (speed < 1f) return;
            Vector3 dir = velocity / speed;
            Vector3 pos = transform.position;

            float bestAngle = AssistCone, bestDistance = AssistRange;
            Vector3 bestAim = Vector3.zero;
            foreach (Collider c in Physics.OverlapSphere(pos, AssistRange, ~0, QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h == null || h.IsDead) continue;
                if (owner != null && (h.transform == owner || h.transform.IsChildOf(owner))) continue;
                if (MageCast.Teams.Spares(owner, c)) continue;

                Hurtbox body = c.GetComponentInParent<Hurtbox>();
                Vector3 aim = body != null ? body.Centre : c.bounds.center;    // the middle of the body
                Vector3 to = aim - pos;
                float d = to.magnitude;
                if (d < 0.5f) continue;
                float angle = Vector3.Angle(dir, to);
                if (angle >= bestAngle) continue;

                // in the open: a wall, a barrier or somebody else in between and it flies on as aimed
                if (!InTheOpen(pos, to / d, d, h)) continue;

                bestAngle = angle;
                bestAim = aim;
                bestDistance = d;
            }
            if (bestAngle >= AssistCone) return;

            // An arc, not a kink: it eases in over the edge of the range (ArcStart) instead of starting at
            // full pull the moment somebody comes within reach, bends at a steady rate, and eases out as it
            // comes round -- a share of what is left each moment near the end, rather than full turn until
            // it points at them and then dead straight. (The pull growing over half the range took most
            // of the help away: +0.1-0.3 m from 10 m instead of +0.5-0.65.)
            float pull = Mathf.SmoothStep(0f, 1f, (AssistRange - bestDistance) / (AssistRange * ArcStart));
            float ease = bestAngle * (1f - Mathf.Exp(-ArcEase * dt));
            float turn = Mathf.Min(AssistPerMetre * pull * speed * dt, ease, AssistBudget - assisted);
            velocity = Vector3.RotateTowards(velocity, (bestAim - pos).normalized * speed, turn * Mathf.Deg2Rad, 0f);
            assisted += turn;
        }

        /// <summary>
        /// Through the flames: it is water from here on. It looks it -- the water bolt from the same pack the
        /// ice and fire come from, in place of the ice -- with a burst of spray where it changed.
        /// </summary>
        void BecomeWater()
        {
            watery = true;
            colour = WaterColour;
            SpellFx.Entry water = SpellFx.ByName("WATER");
            if (water != null && water.projectile != null)
            {
                SpellFx.Release(fx);
                fx = SpellFx.Play(water.projectile, transform.position, Quaternion.LookRotation(velocity.normalized),
                                  water.projectileScale * radius, transform);
                fxEntry = water;
                if (water.impact != null) SpellFx.Play(water.impact, transform.position, Quaternion.identity, water.impactScale * 0.6f);
            }
            else
            {
                Renderer r = GetComponent<Renderer>();
                if (r != null) { r.enabled = true; r.material = RuntimeMaterials.Emissive(WaterColour, 1.8f); }
                FlashAt(transform.position, WaterColour, 1f, 1.5f);
            }
        }

        /// <summary>
        /// A spell landing in a patch that already lies there. Combinations happen one after the other --
        /// something is on the ground, something else arrives -- never as two shots at once.
        ///
        ///   fire into ice, ice into fire   water: nothing happens standing in it, but it carries lightning
        ///   fire into water                put out, nothing left
        ///   ice into water                 freezes back to ice
        ///   air into fire                  fanned: the fire grows and moves the way the wind blew, once
        ///   air into ice                   whoever stands on the ice is blown across it
        ///   earth into fire                magma: burns harder and longer than the fire did
        ///   earth into water               mud: slower going than the rubble
        ///   earth into ice or a shock      broken up (grounded): a stone fence round where it was
        ///
        /// Server only: the server decides and tells everyone what the ground now looks like. Returns
        /// whether something combined, in which case the spell's own effect does not happen.
        /// </summary>
        bool Combine(Vector3 at)
        {
            if (!authoritative || spell == null || tier < 2) return false;   // tier I: the plain spell
            if (watery) return false;                                        // water lays its own patch (HitFloor)
            PlayerNet caster = owner != null ? owner.GetComponent<PlayerNet>() : null;
            Vector3 wind = new Vector3(velocity.x, 0f, velocity.z);
            wind = wind.sqrMagnitude > 0.01f ? wind.normalized : Vector3.forward;

            switch (spell.groundEffect)
            {
                case GroundEffect.Burn:
                {
                    SpellZone ice = SpellZone.At(at, GroundEffect.Ice, radius);
                    if (ice != null) { ReplaceZone(caster, ice, GroundEffect.Water, ice.transform.position, ice.Radius, 6f, 0f, WaterColour, attacker, false); return true; }

                    SpellZone water = SpellZone.At(at, GroundEffect.Water, radius);
                    if (water != null) { Flash(at, 1.2f, 1.2f); return true; }     // put out
                    return false;
                }

                case GroundEffect.Ice:
                {
                    SpellZone fire = SpellZone.At(at, GroundEffect.Burn, radius);
                    if (fire != null) { ReplaceZone(caster, fire, GroundEffect.Water, fire.transform.position, fire.Radius, 6f, 0f, WaterColour, attacker, false); return true; }

                    SpellZone water = SpellZone.At(at, GroundEffect.Water, radius);
                    if (water != null) { ReplaceZone(caster, water, GroundEffect.Ice, water.transform.position, water.Radius, spell.zoneLifetime, spell.zoneStrength, spell.colour, attacker, false); return true; }
                    return false;
                }

                case GroundEffect.Updraft:
                {
                    SpellZone fire = SpellZone.At(at, GroundEffect.Burn, radius);
                    if (fire != null)
                    {
                        // Blown away: the patch is gone and a wall of flame rolls on the way the wind
                        // blew, setting everyone it passes on fire (see FireWave). Starts at the
                        // patch's near edge, so it sweeps through where the fire was.
                        Vector3 centre = fire.transform.position;
                        Vector3 from = centre - wind * fire.Radius * 0.5f;
                        float burn = fire.Strength;
                        ulong burnsFor = fire.Attacker;
                        fire.Remove();
                        FireWave.Spawn(from, wind, burn, burnsFor, true);
                        if (caster != null && caster.IsSpawned) caster.BroadcastFireWave(centre, from, wind, burn, burnsFor);
                        Flash(at, 1.4f, 1.6f);
                        return true;
                    }

                    SpellZone ice = SpellZone.At(at, GroundEffect.Ice, radius);
                    if (ice != null)
                    {
                        // No grip to stop you: a shove that would carry somebody a metre on dry floor
                        // takes them the length of the patch.
                        foreach (PlayerMotor m in ice.MotorsTouching())
                            if (!MageCast.Teams.Spares(owner, m)) m.Slide(wind * IceBlast);
                        Flash(at, 1.4f, 1.6f);
                        return true;
                    }
                    return false;
                }

                case GroundEffect.Rubble:
                {
                    SpellZone fire = SpellZone.At(at, GroundEffect.Burn, radius);
                    if (fire != null)
                    {
                        ReplaceZone(caster, fire, GroundEffect.Magma, fire.transform.position, fire.Radius, SpellTiers.MagmaSeconds,
                                    fire.Strength * SpellTiers.MagmaBurn, MagmaColour, attacker, false);
                        return true;
                    }

                    SpellZone water = SpellZone.At(at, GroundEffect.Water, radius);
                    if (water != null)
                    {
                        ReplaceZone(caster, water, GroundEffect.Mud, water.transform.position, water.Radius, SpellTiers.MudSeconds,
                                    SpellTiers.MudSlow, MudColour, attacker, false);
                        return true;
                    }

                    SpellZone broken = SpellZone.At(at, GroundEffect.Ice, radius);
                    if (broken == null) broken = SpellZone.At(at, GroundEffect.Shock, radius);
                    if (broken != null)
                    {
                        ReplaceZone(caster, broken, GroundEffect.Rubble, broken.transform.position, broken.Radius, spell.zoneLifetime,
                                    0f, spell.colour, attacker, false);
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// Earth's patch: a ring of stone round where it landed, along the edge of the blast (StoneFence).
        /// Laid only where it can stand -- on ground people walk on (SpellZone.Lays), not on a rock, a wall
        /// top or a pillar, where it would mostly hang in the air. Server only; sent to all.
        /// </summary>
        void LayFence(Vector3 at)
        {
            float r = QuakeReach;
            if (!SpellZone.Lays(at, r)) return;
            SpellZone zone = SpellZone.Spawn(at, GroundEffect.Rubble, r, spell.zoneLifetime, 0f, colour, true, attacker, attacker);
            PlayerNet caster = owner != null ? owner.GetComponent<PlayerNet>() : null;
            if (caster != null && caster.IsSpawned)
                caster.BroadcastZone(zone.transform.position, GroundEffect.Rubble, r, spell.zoneLifetime, 0f, colour);
        }

        /// <summary>
        /// The fence round somebody struck directly: on the floor under their feet, if there is floor within
        /// reach of them (not in the middle of a jump off a ledge). Server only, like LayFence.
        /// </summary>
        void FenceIn(Health hp, PlayerMotor motor)
        {
            Vector3 feet;
            if (motor != null) feet = motor.transform.position;
            else if (hp != null)
            {
                Collider body = hp.GetComponentInChildren<Collider>();
                feet = body != null ? new Vector3(hp.transform.position.x, body.bounds.min.y, hp.transform.position.z)
                                    : hp.transform.position;
            }
            else return;

            // the floor under them, looking through bodies
            float nearest = float.MaxValue;
            Vector3 floor = feet;
            foreach (RaycastHit h in Physics.RaycastAll(feet + Vector3.up * 0.3f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<Health>() != null || h.collider.GetComponentInParent<PlayerMotor>() != null) continue;
                if (h.normal.y <= FloorNormal || StoneFence.Is(h.collider) || h.distance >= nearest) continue;
                nearest = h.distance;
                floor = h.point;
            }
            if (nearest < float.MaxValue) LayFence(floor);
        }

        static readonly Color MagmaColour = new Color(1f, 0.32f, 0.08f);
        /// <summary>A warning, not the stone's own dusty brown, which is lost on a pale floor.</summary>
        static readonly Color LandingColour = new Color(1f, 0.5f, 0.12f);
        static readonly Color MudColour = new Color(0.42f, 0.3f, 0.18f);

        /// <summary>
        /// Earth coming down: a blast round where it landed (SpellTiers.Quake*). Everyone within reach but
        /// whoever it struck directly takes a share of the hit, less towards the edge -- the caster too,
        /// if they are that close; a wall or a barrier between them and the blast shields them. Everyone
        /// in it, the one struck as well, is thrown a little off the ground, out of their stride; at tier
        /// III knocked off their feet for a moment and out of whatever they were drawing. Teammates are
        /// spared. Shown on every machine, dealt by the deciding copy.
        /// </summary>
        /// <summary>How far earth's blast reaches -- and its spikes, and the ring warning of it.</summary>
        float QuakeReach { get { return (tier >= 3 ? SpellTiers.QuakeRadiusIII : SpellTiers.QuakeRadius) * sizeScale; } }

        void Quake(Vector3 at, Health direct)
        {
            if (landing != null) { Destroy(landing.gameObject); landing = null; }
            float reach = QuakeReach;
            if (fxEntry != null && fxEntry.special != null)
            {
                SpellFx.PlayBurst(fxEntry.special, at, fxEntry.specialScale * reach / SpellTiers.QuakeRadius, fxEntry.specialLeadIn);
                SpellFx.OneShot(fxEntry.specialSound, at);
            }
            else FlashAt(at, colour, reach * 2f, 1.4f);
            ThirdPersonCamera.Shake(at, 1f, reach + 4f);

            if (!authoritative) return;
            Vector3 origin = at + Vector3.up * 0.3f;
            var done = new System.Collections.Generic.HashSet<Transform>();
            foreach (Collider c in Physics.OverlapSphere(origin, reach + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                Health hp = c.GetComponentInParent<Health>();
                PlayerMotor motor = c.GetComponentInParent<PlayerMotor>();
                if (hp == null && motor == null) continue;
                Transform root = motor != null ? motor.transform : hp.transform;
                if (!done.Add(root)) continue;
                if (hp != null && hp.IsDead) continue;
                if (MageCast.Teams.Spares(owner, c)) continue;
                float d;
                if (!Exposed(origin, root, c, out d) || d >= reach) continue;

                float near = 1f - d / reach;
                if (hp != null && hp != direct && damage > 0f)
                    hp.TakeDamage(damage * SpellTiers.QuakeShare * (0.4f + 0.6f * near), attacker);
                if (motor == null) continue;

                Vector3 away = root.position - at;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero;
                motor.Toss(away * (SpellTiers.QuakePush * near), SpellTiers.QuakeLift * (0.6f + 0.4f * near));
                if (tier >= 3)
                {
                    motor.ApplySlow(0f, SpellTiers.QuakeStagger);
                    GestureCaster hands = motor.GetComponent<GestureCaster>();
                    if (hands != null) hands.Interrupt(true);
                }
            }
        }

        /// <summary>
        /// A lob's landing, marked on the floor for everyone (LandingMark): where it will come down if
        /// nothing is in the way, worked out by flying the same arc ahead of it. Called once it has set off.
        /// </summary>
        public void MarkLanding()
        {
            if (!IsEarth || ended || gravity == 0f) return;
            Vector3 p = transform.position, v = velocity;
            const float Dt = 1f / 30f;
            for (float t = 0f; t < remaining; t += Dt)
            {
                Vector3 next = p + v * Dt + Vector3.up * (0.5f * gravity * Dt * Dt);
                Vector3 d = next - p;
                float length = d.magnitude;
                RaycastHit hit;
                if (length > 1e-4f && LandingRay(p, d / length, length, out hit))
                {
                    landing = LandingMark.Spawn(hit.point, hit.normal, QuakeReach, LandingColour, t + hit.distance / length * Dt);
                    return;
                }
                p = next;
                v += Vector3.up * (gravity * Dt);
            }
        }

        /// <summary>The first piece of the map on the way -- people move, the map does not.</summary>
        bool LandingRay(Vector3 from, Vector3 dir, float length, out RaycastHit first)
        {
            first = new RaycastHit();
            float nearest = float.MaxValue;
            foreach (RaycastHit h in Physics.RaycastAll(from, dir, length, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform == transform || (owner != null && h.transform.IsChildOf(owner))) continue;
                Collider c = h.collider;
                if (c.GetComponentInParent<Health>() != null || c.GetComponentInParent<PlayerMotor>() != null) continue;
                if (h.distance < nearest) { nearest = h.distance; first = h; }
            }
            return nearest < float.MaxValue;
        }

        /// <summary>How hard air throws people along ice, m/s. With the ice's grip that is about ten metres.</summary>
        const float IceBlast = 12f;

        static void ReplaceZone(PlayerNet caster, SpellZone old, GroundEffect kind, Vector3 at, float radius,
                              float lifetime, float strength, Color colour, ulong zoneOwner, bool fanned,
                              ulong damageBy = Health.NoAttacker)
        {
            Vector3 oldCentre = old.transform.position;
            GroundEffect oldKind = old.Effect;
            old.Remove();

            ulong hurts = damageBy != Health.NoAttacker ? damageBy : zoneOwner;
            SpellZone z = SpellZone.Spawn(at, kind, radius, lifetime, strength, colour, true, hurts, zoneOwner);
            if (fanned) z.MarkFanned();

            if (caster != null && caster.IsSpawned)
                caster.BroadcastReplace(oldCentre, oldKind, z.transform.position, kind, radius, lifetime, strength,
                                        colour, zoneOwner, fanned);
        }

        bool HitFloor(Vector3 at)
        {
            // Lightning grounds out -- unless the ground is ice or water, which carries it to everyone on it.
            if (spell != null && spell.chargeDamage > 0f && DischargeIceAt(at, at))
            {
                End();
                return true;
            }

            // from tier II it leaves its patch where it struck the floor
            if (IsLightning && tier >= 2)
            {
                Flash(at, 1f, 2f);
                LayShock(at);
                End();
                return true;
            }

            // earth: the blast where it came down, whatever else follows
            if (IsEarth) Quake(at, null);

            // air III into a patch: the air bomb, instead of whatever air would do to it
            if (IsAir && tier >= 3)
            {
                SpellZone patch = SpellZone.AnyAt(at, radius);
                if (patch != null)
                {
                    AirBomb(patch);
                    End();
                    return true;
                }
            }

            if (Combine(at))
            {
                End();
                return true;
            }

            // earth: its fence from tier II, round the blast -- on the ground, not on top of a rock
            if (IsEarth)
            {
                ShowImpact(at);
                if (tier >= 2 && authoritative) LayFence(at);
                End();
                return true;
            }

            if (IsFire && tier >= 3) Explode(at, null);

            // water, from tier II: a water patch where it lands -- nothing standing in it, but it carries lightning
            if (watery && tier >= 2)
            {
                Flash(at, 1f, 2f);
                if (authoritative)
                {
                    float r = spell.zoneRadius * sizeScale;
                    SpellZone pool = SpellZone.Spawn(at, GroundEffect.Water, r, 6f, 0f, WaterColour, true, attacker, attacker);
                    PlayerNet caster = owner != null ? owner.GetComponent<PlayerNet>() : null;
                    if (caster != null && caster.IsSpawned)
                        caster.BroadcastZone(pool.transform.position, GroundEffect.Water, r, 6f, 0f, WaterColour);
                }
                End();
                return true;
            }

            // tier I leaves nothing on the floor -- only its splash
            if (spell != null && spell.groundEffect != GroundEffect.None && tier < 2)
            {
                Flash(at, 1f, 2f);
                Splash(at, Vector3.up);
            }
            else if (spell != null && spell.groundEffect != GroundEffect.None && !authoritative)
            {
                // A watcher's copy leaves nothing: the server's patch is sent to everyone, so there is
                // exactly one patch, in exactly one place, whatever the copies happened to hit.
                Flash(at, 1f, 2f);
            }
            else if (spell != null && spell.groundEffect != GroundEffect.None)
            {
                // Size scales with how big the glyph was drawn, the same bargain the projectile itself
                // strikes. Burn scales with how WELL it was drawn -- 6/s times the same power as the
                // direct hit, so the patch stays below a direct hit at every quality.
                float strength = spell.zoneStrength;
                if (spell.groundEffect == GroundEffect.Burn) strength *= power;

                float zoneRadius = spell.zoneRadius * sizeScale;
                SpellZone zone = SpellZone.Spawn(at, spell.groundEffect, zoneRadius, spell.zoneLifetime, strength,
                                                 colour, true, attacker, attacker);
                ShowImpact(at);

                PlayerNet caster = owner != null ? owner.GetComponent<PlayerNet>() : null;
                if (caster != null && caster.IsSpawned)
                    caster.BroadcastZone(zone.transform.position, spell.groundEffect, zoneRadius,
                                         spell.zoneLifetime, strength, colour);
            }
            else if (areaRadius > 0f && authoritative)
            {
                PoisonPuddle.Spawn(at, areaRadius, areaLifetime, colour);
            }
            else
            {
                Flash(at, 1.4f, 2.5f);
            }

            End();
            return true;
        }

        /// <summary>
        /// Blows everyone near the wall away from it. Aimed at whoever is leaning on cover: the spell
        /// does not have to find them, it has to find the cover.
        /// </summary>
        void WallBurst(Vector3 at, Vector3 normal, float reach)
        {
            Vector3 away = new Vector3(normal.x, 0f, normal.z);
            if (away.sqrMagnitude < 0.01f) away = -velocity;
            away.Normalize();

            var shoved = new System.Collections.Generic.HashSet<PlayerMotor>();
            foreach (Collider c in Physics.OverlapSphere(at, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                PlayerMotor m = c.GetComponentInParent<PlayerMotor>();
                if (m == null || !shoved.Add(m)) continue;

                if (!authoritative) continue;      // a watcher's copy only shows the burst
                if (MageCast.Teams.Spares(owner, m)) continue;

                // full force at the wall, fading to nothing at the edge
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(m.transform.position, at) / reach);
                m.AddImpulse((away + Vector3.up * 0.35f).normalized * (knockback * falloff), attacker);
            }

            // as wide as the push, so the player can see how far from the wall was unsafe
            Flash(at, reach, 1.6f);
        }

        /// <summary>A brief glowing burst, <paramref name="size"/> metres across at its widest.</summary>
        void Flash(Vector3 at, float size, float glow)
        {
            // a spell with a real impact effect shows that instead of the placeholder sphere
            if (ShowImpact(at)) return;
            FlashAt(at, colour, size, glow);
        }

        /// <summary>The spell's impact effect and its sound, once per projectile. False if it has none.</summary>
        bool ShowImpact(Vector3 at)
        {
            if (fxEntry == null || fxEntry.impact == null) return false;
            if (impactShown) return true;
            impactShown = true;
            SpellFx.Play(fxEntry.impact, at, Quaternion.LookRotation(-velocity.normalized + Vector3.up * 0.001f),
                         fxEntry.impactScale * sizeScale);
            return true;
        }

        void OnDestroy()
        {
            Live.Remove(this);
            if (landing != null) Destroy(landing.gameObject);
            // the flying effect fades where it is rather than blinking out with the projectile
            SpellFx.Release(fx);
        }

        /// <summary>Every spell in flight on this machine -- what a bot looks at to see a shot coming.</summary>
        public static readonly System.Collections.Generic.List<Projectile> Live = new System.Collections.Generic.List<Projectile>();

        public Vector3 Velocity { get { return velocity; } }
        public Transform Owner { get { return owner; } }
        public bool Finished { get { return ended; } }

        /// <summary>A brief glowing burst anywhere -- also how a barrier shows it has broken.</summary>
        public static void FlashAt(Vector3 at, Color colour, float size, float glow)
        {
            // a projectile that just blinks out reads as a bug, so leave a brief mark
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(flash.GetComponent<Collider>());
            flash.name = "Impact";
            flash.transform.position = at;
            flash.transform.localScale = Vector3.one * size;

            Material m = RuntimeMaterials.Emissive(colour, glow);
            flash.GetComponent<Renderer>().material = m;
            flash.AddComponent<ImpactFlash>();
        }
    }

    public class ImpactFlash : MonoBehaviour
    {
        float life = 0.18f;
        float age;

        // Grows relative to the size it was given. It used to set an absolute scale, which quietly
        // threw away whatever size the caller asked for -- every flash came out the same 1.4 m.
        Vector3 fullSize;

        void Start()
        {
            fullSize = transform.localScale;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age / life;
            transform.localScale = fullSize * Mathf.Lerp(0.15f, 1f, t);
            if (age >= life) Destroy(gameObject);
        }
    }
}
