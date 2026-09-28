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

        SpellFx.Entry fxEntry;     // this spell's real effects, or null for the placeholder look
        GameObject fx;             // the flying effect, riding on this object
        bool impactShown;

        /// <summary>
        /// How hard an updraft pushes a shot upwards, m/s^2. Across a 4 m draft lightning (44 m/s) gains
        /// about 6 m/s of climb, fire about 10, ice about 16 -- enough that none of them arrive where
        /// they were aimed, and the slower the shot, the more completely it is thrown off.
        /// </summary>
        const float DraftLift = 70f;

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
            // No post-processing in a blockout, so the only way a crit can read as a crit is raw
            // emission and size. Both scale with the quality of the stroke that produced it.
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

            // Over an updraft the rising air bends the shot upwards and it sails over whoever stood
            // behind it -- a screen you can put down, not a mirror. Every shot, whoever cast it and
            // whoever laid the draft; a fast one is only nudged, a slow one is lifted clean away.
            if (SpellZone.DraftAt(transform.position) != null)
                velocity += Vector3.up * (DraftLift * dt);

            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance > 1e-5f)
            {
                Vector3 heading = step / distance;
                RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, heading, distance,
                                                          ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.MaxValue;
                RaycastHit best = new RaycastHit();
                bool hitSomething = false;

                for (int i = 0; i < hits.Length; i++)
                {
                    Transform t = hits[i].transform;
                    // Destroy() on our own collider only takes effect at the end of the frame, so on the
                    // first Update it is still there and a sweep from inside it reports a zero-distance
                    // hit -- the projectile would blow up on itself the instant it spawned.
                    if (t == transform) continue;
                    if (owner != null && (t == owner || t.IsChildOf(owner))) continue;
                    if (ignoreOnce != null && hits[i].collider == ignoreOnce) continue;

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

            Vector3 closest = hit.collider.ClosestPoint(transform.position);
            Vector3 up = transform.position - closest;
            return up.sqrMagnitude > 1e-6f && up.normalized.y > FloorNormal;
        }

        /// <summary>Something a spell hits with its whole body: a person, a dummy, a barrier.</summary>
        static bool IsTarget(Collider c)
        {
            return c.GetComponentInParent<Health>() != null || c.GetComponentInParent<PlayerMotor>() != null
                   || c.GetComponentInParent<CastShield>() != null;
        }

        /// <summary>Resolves a hit. Returns true if the projectile is finished, false if it bounced on.</summary>
        bool Impact(Vector3 at, Vector3 normal, Vector3 centreAtContact, Transform struck,
                    Collider collider, Vector3 heading)
        {
            // GetComponentInParent, because the thing a sweep reports is usually a child renderer or
            // collider rather than the object that owns the health.
            Health hp = struck != null ? struck.GetComponentInParent<Health>() : null;
            PlayerMotor motor = struck != null ? struck.GetComponentInParent<PlayerMotor>() : null;

            // A barrier stops the shot where it stands and pays for it in durability. No bounce: a
            // barrier that threw fire back at whoever fired it would be a reward for being shot at.
            CastShield shield = struck != null ? struck.GetComponentInParent<CastShield>() : null;
            if (shield != null)
            {
                // the ripple first: a hit that breaks it should still be seen landing
                shield.Struck(at, BarrierWear() / 30f);
                if (authoritative) shield.Wear(BarrierWear());
                Flash(at, 1.1f, 2.2f);
                End();
                return true;
            }

            if (hp != null || motor != null)
            {
                if (authoritative) HitPerson(hp, motor, heading);
                Flash(at, 1.4f, 2.5f);
                End();
                return true;
            }

            bool floor = normal.y > FloorNormal;
            return floor ? HitFloor(at) : HitWall(at, normal, centreAtContact, collider);
        }

        /// <summary>
        /// What this shot takes off a barrier: its damage times how hard its element is on barriers,
        /// and never less than the element's floor -- air deals no damage but still nudges the count.
        /// </summary>
        float BarrierWear()
        {
            if (spell == null) return damage;
            return Mathf.Max(damage * spell.barrierWear, spell.barrierWearFlat);
        }

        void HitPerson(Health hp, PlayerMotor motor, Vector3 heading)
        {
            if (hp != null && damage > 0f) hp.TakeDamage(damage, attacker);

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
                if (knockback > 0f)
                    // Along the flight path with a bit of lift, so it reads as being blown off your feet
                    // rather than shunted sideways.
                    motor.AddImpulse((heading + Vector3.up * 0.45f).normalized * knockback);

                if (spell != null && spell.hitSlowDuration > 0f && spell.hitSlow < 1f)
                    motor.ApplySlow(spell.hitSlow, spell.hitSlowDuration);

                if (spell != null && spell.interruptsDrawing)
                {
                    // Being shoved takes the hand off the glyph -- there is nothing left to salvage. A
                    // spell already drawn and HELD is untouched: the risk lives in drawing, not holding.
                    GestureCaster caster = motor.GetComponent<GestureCaster>();
                    if (caster != null) caster.Interrupt();
                }
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

        /// <summary>
        /// A spell landing in a patch that already lies there. Combinations happen one after the other --
        /// something is on the ground, something else arrives -- never as two shots at once.
        ///
        ///   fire into ice, ice into fire   water: nothing happens standing in it, but it carries lightning
        ///   fire into water                put out, nothing left
        ///   ice into water                 freezes back to ice
        ///   air into fire                  fanned: the fire grows and moves the way the wind blew, once
        ///   air into ice                   whoever stands on the ice is blown across it
        ///
        /// Server only: the server decides and tells everyone what the ground now looks like. Returns
        /// whether something combined, in which case the spell's own effect does not happen.
        /// </summary>
        bool Combine(Vector3 at)
        {
            if (!authoritative || spell == null) return false;
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
                        if (!fire.Fanned)
                        {
                            // Half again as wide and pushed the way the wind blew, so it still covers
                            // most of where it was and now reaches further. It burns a little longer for
                            // the stirring. Still the owner's fire -- the air only moved it.
                            Vector3 moved = fire.transform.position + wind * fire.Radius * 0.6f;
                            ReplaceZone(caster, fire, GroundEffect.Burn, moved, fire.Radius * 1.5f,
                                      Mathf.Max(fire.Remaining, 3f), fire.Strength, fire.Colour, fire.Owner, true,
                                      fire.Attacker);
                        }
                        Flash(at, 1.4f, 1.6f);
                        return true;
                    }

                    SpellZone ice = SpellZone.At(at, GroundEffect.Ice, radius);
                    if (ice != null)
                    {
                        // No grip to stop you: a shove that would carry somebody a metre on dry floor
                        // takes them the length of the patch.
                        foreach (PlayerMotor m in ice.MotorsTouching()) m.Slide(wind * IceBlast);
                        Flash(at, 1.4f, 1.6f);
                        return true;
                    }
                    return false;
                }
            }
            return false;
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

            if (Combine(at))
            {
                End();
                return true;
            }

            if (spell != null && spell.groundEffect != GroundEffect.None && !authoritative)
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

                // full force at the wall, fading to nothing at the edge
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(m.transform.position, at) / reach);
                m.AddImpulse((away + Vector3.up * 0.35f).normalized * (knockback * falloff));
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
            // the flying effect fades where it is rather than blinking out with the projectile
            SpellFx.Release(fx);
        }

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
