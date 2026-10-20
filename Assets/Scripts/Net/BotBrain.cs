using System.Collections.Generic;
using MageCast.Combat;
using MageCast.Gestures;
using UnityEngine;
using UnityEngine.AI;

namespace MageCast
{
    /// <summary>
    /// A computer player, on the server only. It plays by the rules a person does, through the same
    /// body and the same hands: it walks with the motor (accelerating, braking, out of stamina like
    /// anybody), drawing a rune takes it time and slows it, the rune forms over its head for everyone to
    /// read, an air hit knocks it away, a rune can fizzle or come out at a lower tier, and every
    /// cooldown, the global cooldown, a lock and NOT READY apply to it exactly as to you. What it does
    /// NOT have is a mouse: the drawing itself is skipped, the quality rolled by <see cref="Skill"/>.
    ///
    /// And it knows only what a person in its place would know (since 13 Oct; before, it saw through
    /// walls and behind its back, and reacted in the same frame):
    ///
    ///   eyes       a view of about 110 degrees ahead, plus anybody within a few metres; no walls
    ///   memory     where it last saw somebody, for a few seconds -- it goes there, looks, and gives up
    ///   being hit  tells it who hit it, and roughly from where, as the shot's direction would tell you
    ///   reaction   everything it sees, it sees a quarter of a second late (shorter with skill): its
    ///              lead is on where you were going, so a change of direction makes it miss
    ///   turning    no faster than a mouse flick, and it sends only once it faces its target
    ///
    /// Like a person it answers whoever is shooting at it before picking a new fight, waits a moment
    /// after spotting somebody before it starts a rune, sidesteps a shot it sees coming (when it reacts
    /// in time), and, badly hurt, breaks line of sight for a few seconds before coming back.
    /// </summary>
    public class BotBrain : MonoBehaviour
    {
        /// <summary>All bots leave people alone -- for watching them fight each other.</summary>
        public static bool IgnoreHumans;

        /// <summary>0 = clumsy, 1 = sharp: rune quality, aim, reaction time, turning, dodging.</summary>
        public static float Skill = 0.6f;

        /// <summary>For measuring: shots sidestepped and retreats out of sight, by all bots since the start.</summary>
        public static int Dodges, Retreats;

        const float FightRange = 12f;
        const float ThinkInterval = 0.15f;

        /// <summary>Half the width of what it sees, degrees -- a third-person camera's view, give or take.</summary>
        const float ViewHalfAngle = 55f;
        /// <summary>Anybody this close is noticed whichever way it faces: footsteps, a shadow, a shout.</summary>
        const float NearbyNotice = 3.5f;
        /// <summary>How long it remembers where it last saw somebody.</summary>
        const float MemorySeconds = 7f;

        PlayerNet self;
        PlayerMotor motor;
        GestureCaster hands;
        Health health;
        System.Random rnd;

        /// <summary>What it knows about somebody: where it last saw them, and whether it sees them now.</summary>
        class Known
        {
            public Vector3 Position;
            public float SeenAt;
            public bool Visible;
            public float HurtMeAt = -99f;
        }

        readonly Dictionary<PlayerNet, Known> known = new Dictionary<PlayerNet, Known>();
        PlayerNet target;
        bool targetSeen;
        float nextThink;
        float lastHealth = -1f;

        /// <summary>The target as it has been seen, oldest first -- read a reaction time late.</summary>
        readonly List<KeyValuePair<float, Vector3>> seen = new List<KeyValuePair<float, Vector3>>();

        NavMeshPath path;
        int corner;
        Vector3 pathGoal;
        float repathAt;

        float strafeSign = 1f, strafeSwitchAt;
        Vector3 stuckFrom;
        float stuckCheckAt;
        Vector3 wanderGoal;
        float wanderUntil;
        float facing;

        // dodging a shot it sees coming
        readonly HashSet<Projectile> judged = new HashSet<Projectile>();
        float dodgeAt = -1f, dodgeUntil = -1f;
        Vector3 dodgeDir;

        // breaking line of sight when badly hurt
        float hideUntil = -1f, hideAgainAt;
        Vector3 hidePoint;

        /// <summary>The ice or fire being drawn goes on the floor in front -- a wall, a line -- not at anybody.</summary>
        bool wallMode;

        enum Phase { Idle, Drawing, Holding }
        Phase phase;
        Spell chosen;
        CastQuality chosenQuality;
        float chosenPrecision;
        Vector2[] rune;
        float drawStarted, drawLength, holdUntil, nextCastAt;
        int pointsSent;

        // What skill does. Novice = 1 - skill. The aim, the lead and the runes move with the slider all
        // the way along it (20 Oct): until then most of it was felt only near 0 % (novice squared), and
        // bots fighting each other hit 40 % of their shots at 20 % skill, 43-50 % at 60 % and 50 % at
        // 100 % -- the percentage changed almost nothing a player could feel, and a 60 % bot drew tier III
        // one rune in five. Beginner (novice squared) is still what makes 0 % a real beginner on top: slow
        // to react and to turn, slow and unsure drawing, pauses between spells, rarely a dodge, and no feel
        // for the clever spell. (Until 14 Oct a 0 % bot drew almost as well as a 60 % one.)
        static float Novice { get { return 1f - Mathf.Clamp01(Skill); } }
        static float Beginner { get { return Novice * Novice; } }

        float Reaction { get { return 0.15f + 0.15f * Novice + 0.3f * Beginner; } }
        float TurnRate { get { return 760f - 520f * Novice; } }

        void Start()
        {
            self = GetComponent<PlayerNet>();
            motor = GetComponent<PlayerMotor>();
            hands = GetComponent<GestureCaster>();
            health = GetComponent<Health>();
            rnd = new System.Random(GetInstanceID());
            path = new NavMeshPath();
            BotNav.EnsureBaked();
            nextCastAt = Time.time + 1f;
            facing = transform.eulerAngles.y;
        }

        float Rand(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        void Update()
        {
            if (self == null || !self.IsSpawned || !self.IsServer) return;
            if (health.IsDead)
            {
                motor.ScriptedMove = Vector2.zero;
                motor.ScriptedSprint = false;
                // Down mid-rune: the hands let go, the way a person's do (GameInput.Blocked). Left
                // drawing, the bot came back with its hands still full and never cast again -- two of
                // six were found standing like that, slowed, for minutes.
                if (hands.BotDrawing) hands.Interrupt();
                phase = Phase.Idle;
                target = null;
                known.Clear();
                seen.Clear();
                hideUntil = dodgeUntil = dodgeAt = -1f;
                lastHealth = -1f;
                facing = transform.eulerAngles.y;
                return;
            }

            if (Time.time >= nextThink) Think();
            WatchForShots();
            Remember();
            Move();
            Cast();
        }

        // ------------------------------------------------------------------ what it knows

        void Think()
        {
            nextThink = Time.time + ThinkInterval;
            Perceive();

            PlayerNet best = null;
            float bestScore = float.MaxValue;
            foreach (KeyValuePair<PlayerNet, Known> k in known)
            {
                PlayerNet p = k.Key;
                float score = Vector3.Distance(k.Value.Position, transform.position);
                if (!k.Value.Visible) score += 15f;                              // somebody in sight first
                if (Time.time - k.Value.HurtMeAt < 3f) score -= 14f;             // answer whoever shoots you
                if (p == target) score -= 5f;                                   // and do not flip between two
                if (score < bestScore) { bestScore = score; best = p; }
            }

            bool sawBefore = targetSeen;
            if (best != target)
            {
                target = best;
                seen.Clear();
            }
            targetSeen = target != null && known[target].Visible;

            // just spotted: a moment to take it in before the hands start a rune
            if (targetSeen && !sawBefore)
                nextCastAt = Mathf.Max(nextCastAt, Time.time + Reaction + Rand(0.1f, 0.4f) + Beginner * Rand(0.2f, 0.8f));

            ConsiderHiding();
        }

        /// <summary>Who it can see, who just hurt it, and what it has forgotten.</summary>
        void Perceive()
        {
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (!Enemy(p)) { known.Remove(p); continue; }
                Known k;
                bool visible = CanSee(p);
                if (!known.TryGetValue(p, out k))
                {
                    if (!visible) continue;
                    k = new Known();
                    known[p] = k;
                }
                k.Visible = visible;
                if (visible) { k.Position = p.transform.position; k.SeenAt = Time.time; }
            }

            // hit: whoever did it is known, and where they were -- the shot came from there
            if (lastHealth >= 0f && health.Current < lastHealth - 0.01f)
            {
                PlayerNet by = PlayerNet.Find(health.LastAttacker);
                if (Enemy(by))
                {
                    Known k;
                    if (!known.TryGetValue(by, out k)) { k = new Known(); known[by] = k; }
                    k.HurtMeAt = Time.time;
                    k.Position = by.transform.position;
                    k.SeenAt = Mathf.Max(k.SeenAt, Time.time - 0.5f);
                }
            }
            lastHealth = health.Current;

            var forget = new List<PlayerNet>();
            foreach (KeyValuePair<PlayerNet, Known> k in known)
                if (k.Key == null || !k.Key.IsSpawned || Time.time - k.Value.SeenAt > MemorySeconds) forget.Add(k.Key);
            foreach (PlayerNet p in forget) known.Remove(p);
        }

        bool Enemy(PlayerNet p)
        {
            if (p == null || p == self || !p.IsSpawned) return false;
            if (IgnoreHumans && !p.IsBot) return false;
            if (Teams.Allies(self, p)) return false;
            Health h = p.GetComponent<Health>();
            return h != null && !h.IsDead;
        }

        /// <summary>In front of it (or right next to it) and not behind a wall.</summary>
        bool CanSee(PlayerNet p)
        {
            Vector3 to = p.transform.position - transform.position;
            float flat = new Vector2(to.x, to.z).magnitude;
            if (flat > NearbyNotice)
            {
                Vector3 ahead = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
                if (Vector3.Angle(ahead, new Vector3(to.x, 0f, to.z)) > ViewHalfAngle) return false;
            }
            return LineOfSight(p);
        }

        bool LineOfSight(PlayerNet p)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            Vector3 d = chest - eye;
            foreach (RaycastHit h in Physics.RaycastAll(eye, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform) || h.transform.IsChildOf(p.transform)) continue;
                if (h.collider.GetComponentInParent<PlayerNet>() != null) continue;     // people do not hide people
                return false;
            }
            return true;
        }

        /// <summary>The target's track while in sight, for reading it a reaction time late.</summary>
        void Remember()
        {
            if (target == null || !targetSeen) return;
            seen.Add(new KeyValuePair<float, Vector3>(Time.time, target.transform.position));
            while (seen.Count > 2 && seen[0].Key < Time.time - 1.5f) seen.RemoveAt(0);
        }

        /// <summary>Where the target was, as seen <see cref="Reaction"/> ago (the last sighting if longer).</summary>
        Vector3 SeenAt(float ago)
        {
            if (seen.Count == 0) return target != null && known.ContainsKey(target) ? known[target].Position : transform.position;
            float t = Time.time - ago;
            for (int i = seen.Count - 1; i > 0; i--)
                if (seen[i - 1].Key <= t)
                {
                    float span = seen[i].Key - seen[i - 1].Key;
                    float f = span > 1e-4f ? Mathf.Clamp01((t - seen[i - 1].Key) / span) : 1f;
                    return Vector3.Lerp(seen[i - 1].Value, seen[i].Value, f);
                }
            return seen[0].Value;
        }

        Vector3 Perceived { get { return SeenAt(Reaction); } }

        /// <summary>The target's velocity as it looked a reaction time ago -- a change of course is seen late.</summary>
        Vector3 PerceivedVelocity
        {
            get
            {
                Vector3 v = (SeenAt(Reaction) - SeenAt(Reaction + 0.2f)) / 0.2f;
                v.y = 0f;
                return v.magnitude < 15f ? v : Vector3.zero;
            }
        }

        // ------------------------------------------------------------------ dodging and hiding

        /// <summary>
        /// A shot it can see, on course to pass within a metre of it, is sidestepped -- a reaction time
        /// after it saw it, if that is still in time, and not every time (better with skill).
        /// </summary>
        void WatchForShots()
        {
            if (dodgeAt > 0f && Time.time >= dodgeAt)
            {
                dodgeAt = -1f;
                dodgeUntil = Time.time + Rand(0.35f, 0.55f);
                Dodges++;
            }
            if (dodgeAt > 0f || Time.time < dodgeUntil) return;

            Vector3 me = transform.position + Vector3.up * 1.1f;
            Vector3 ahead = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            for (int i = Projectile.Live.Count - 1; i >= 0; i--)
            {
                Projectile p = Projectile.Live[i];
                if (p == null || p.Finished || judged.Contains(p)) continue;
                Transform owner = p.Owner;
                if (owner == transform || (owner != null && Teams.Spares(owner, self))) { judged.Add(p); continue; }

                Vector3 rel = me - p.transform.position;
                Vector3 v = p.Velocity;
                float speed2 = v.sqrMagnitude;
                if (speed2 < 1f) continue;
                float tHit = Vector3.Dot(rel, v) / speed2;
                if (tHit <= 0f || tHit > 1.2f) continue;
                if ((rel - v * tHit).magnitude > 1.1f) continue;

                // seen at all: ahead of it, or close enough to hear
                Vector3 toShot = p.transform.position - transform.position;
                if (toShot.magnitude > 6f && Vector3.Angle(ahead, new Vector3(toShot.x, 0f, toShot.z)) > ViewHalfAngle) continue;

                judged.Add(p);
                if (tHit < Reaction + 0.1f) continue;                           // too late to do anything
                if (rnd.NextDouble() > 0.7f - 0.6f * Novice) continue;          // did not react

                Vector3 side = Vector3.Cross(Vector3.up, new Vector3(v.x, 0f, v.z).normalized);
                if (rnd.NextDouble() < 0.5) side = -side;
                if (!BotNav.Open(transform.position, side, 1.5f)) side = -side;
                dodgeDir = side;
                dodgeAt = Time.time + Reaction;
                break;
            }
            if (judged.Count > 64) judged.RemoveWhere(p => p == null);
        }

        /// <summary>Badly hurt, with no barrier up: out of its sight for a few seconds, then back.</summary>
        void ConsiderHiding()
        {
            if (target == null || Time.time < hideAgainAt || Time.time < hideUntil) return;
            if (health.Fraction > 0.35f || CastShield.Of(self.AttackerId) != null || !targetSeen) return;
            if (rnd.NextDouble() > 0.35 + 0.4 * Skill) { hideAgainAt = Time.time + 2f; return; }

            Vector3 threat = known[target].Position + Vector3.up * 1.6f;
            Vector3 best = Vector3.zero;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = BotNav.RandomPoint(transform.position, 9f, rnd);
                float d = Vector3.Distance(p, transform.position);
                if (d < 2f || d > bestDistance) continue;
                if (!Blocked(threat, p + Vector3.up * 1.3f)) continue;
                best = p;
                bestDistance = d;
            }
            hideAgainAt = Time.time + 9f;
            if (bestDistance == float.MaxValue) return;
            hidePoint = best;
            hideUntil = Time.time + Rand(2.5f, 4.5f);
            Retreats++;
        }

        /// <summary>Something of the map between two points.</summary>
        static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            foreach (RaycastHit h in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<PlayerNet>() != null) continue;
                if (h.collider.GetComponentInParent<CastShield>() != null) continue;
                return true;
            }
            return false;
        }

        bool Hiding { get { return Time.time < hideUntil; } }

        // ------------------------------------------------------------------ moving

        void Move()
        {
            Vector3 pos = transform.position;
            Vector3 want = Vector3.zero;
            bool sprint = false;
            float face = facing;

            if (target == null)
            {
                // nobody to fight: wander the map, looking where it goes
                if (Time.time > wanderUntil || (wanderGoal - pos).sqrMagnitude < 4f)
                {
                    wanderGoal = BotNav.RandomPoint(Vector3.zero, 30f, rnd);
                    wanderUntil = Time.time + 8f;
                }
                want = Follow(wanderGoal);
                // looking about as it goes, the way a person swings the camera -- it only sees ahead
                face = Yaw(want, facing) + Mathf.Sin(Time.time * 0.9f + GetInstanceID()) * 55f;
            }
            else
            {
                Known k = known[target];
                Vector3 last = targetSeen ? Perceived : k.Position;
                Vector3 to = last - pos;
                to.y = 0f;
                float d = to.magnitude;
                Vector3 toDir = d > 0.01f ? to / d : transform.forward;

                if (Hiding)
                {
                    // out of sight, facing back towards the danger
                    want = Follow(hidePoint);
                    if ((hidePoint - pos).sqrMagnitude < 1f) want = Vector3.zero;
                    face = Yaw(toDir, facing);
                }
                else if (!targetSeen)
                {
                    // to where it was last seen; nobody there -- forget it
                    want = Follow(k.Position);
                    sprint = d > 14f && !hands.BotBusy;
                    face = Yaw(want.sqrMagnitude > 0.01f ? want : toDir, facing);
                    if (d < 2f) { known.Remove(target); target = null; }
                }
                else if (d > FightRange + 10f)
                {
                    want = Follow(last);
                    sprint = d > 20f && !hands.BotBusy;
                    face = Yaw(toDir, facing);
                }
                else
                {
                    // in a fight: hold the range and side-step, so a shot has to be led
                    if (Time.time >= strafeSwitchAt)
                    {
                        // a beginner often just stands there while fighting
                        if (rnd.NextDouble() < 0.6 * Beginner) strafeSign = 0f;
                        else if (strafeSign == 0f) strafeSign = rnd.NextDouble() < 0.5 ? 1f : -1f;
                        else if (rnd.NextDouble() < 0.6) strafeSign = -strafeSign;
                        strafeSwitchAt = Time.time + Rand(0.6f, 1.8f);
                        if (!hands.BotBusy && rnd.NextDouble() < 0.1) motor.ScriptedJump = true;
                    }
                    Vector3 side = Vector3.Cross(Vector3.up, toDir) * strafeSign;
                    if (!BotNav.Open(pos, side, 1.5f)) { strafeSign = -strafeSign; side = -side; }
                    float range = d > FightRange + 3f ? 0.7f : d < FightRange - 5f ? -0.7f : 0f;
                    want = (side + toDir * range).normalized;
                    if (!BotNav.Open(pos, want, 1.5f)) want = Follow(last);
                    face = Yaw(toDir, facing);
                }

                if (target != null) self.AimPoint.Value = last + Vector3.up * 1.3f;
            }

            // a shot it saw coming: a step aside, whatever else it was doing
            if (Time.time < dodgeUntil) { want = dodgeDir; sprint = false; }

            // stuck against something: hop and change sides
            if (Time.time >= stuckCheckAt)
            {
                if (want.sqrMagnitude > 0.1f && (pos - stuckFrom).sqrMagnitude < 0.09f)
                {
                    motor.ScriptedJump = true;
                    strafeSign = -strafeSign;
                    repathAt = 0f;
                }
                stuckFrom = pos;
                stuckCheckAt = Time.time + 1f;
            }

            // turning at the speed of a hand on a mouse, not in one frame
            facing = Mathf.MoveTowardsAngle(facing, face, TurnRate * Time.deltaTime);
            motor.ScriptedYaw = facing;
            Quaternion toLocal = Quaternion.Euler(0f, -facing, 0f);
            Vector3 local = toLocal * want;
            motor.ScriptedMove = new Vector2(local.x, local.z);
            motor.ScriptedSprint = sprint;
        }

        static float Yaw(Vector3 dir, float fallback)
        {
            dir.y = 0f;
            return dir.sqrMagnitude < 0.0001f ? fallback : Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>The direction to walk now to get to <paramref name="goal"/> along the walkable surface.</summary>
        Vector3 Follow(Vector3 goal)
        {
            Vector3 pos = transform.position;
            if (Time.time >= repathAt || (goal - pathGoal).sqrMagnitude > 4f)
            {
                repathAt = Time.time + 0.5f;
                pathGoal = goal;
                corner = BotNav.Path(pos, goal, path) ? 1 : -1;
            }
            if (corner < 0 || path.corners.Length == 0) return Flat(goal - pos).normalized;
            while (corner < path.corners.Length && Flat(path.corners[corner] - pos).sqrMagnitude < 0.5f) corner++;
            if (corner >= path.corners.Length) return Vector3.zero;
            return Flat(path.corners[corner] - pos).normalized;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>How far off its target it is turned, degrees.</summary>
        float FacingError
        {
            get
            {
                if (target == null) return 180f;
                Vector3 to = Perceived - transform.position;
                return Mathf.Abs(Mathf.DeltaAngle(facing, Yaw(to, facing)));
            }
        }

        // ------------------------------------------------------------------ casting

        void Cast()
        {
            switch (phase)
            {
                case Phase.Idle:
                    if (target == null || !targetSeen || Hiding || Time.time < nextCastAt || !hands.BotCanStart) return;
                    float d = Vector3.Distance(Perceived, transform.position);
                    if (d > 32f) return;
                    chosen = ChooseSpell(d);
                    if (chosen == null) { nextCastAt = Time.time + 0.3f; return; }
                    // under fire and hurt, with its ice: put it up as a wall in front instead of throwing it
                    // -- the cover a person would make, more often the better the bot
                    wallMode = chosen == hands.SpellByIndex(GestureCaster.IceId) && d > 6f
                               && known.ContainsKey(target) && Time.time - known[target].HurtMeAt < 2.5f
                               && health.Fraction < 0.7f && CastShield.Of(self.AttackerId) == null
                               && rnd.NextDouble() < 0.2 + 0.4 * Skill;
                    // somebody coming in close, or it hurt and wanting room: a line of fire across the way
                    if (chosen == hands.SpellByIndex(GestureCaster.FireId) && d > 3f && d < 8f)
                    {
                        Vector3 away = transform.position - Perceived;
                        away.y = 0f;
                        bool closing = away.sqrMagnitude > 0.01f && Vector3.Dot(PerceivedVelocity, away.normalized) > 1.5f;
                        wallMode = (closing || health.Fraction < 0.5f) && rnd.NextDouble() < 0.15 + 0.35 * Skill;
                    }
                    RollQuality();
                    rune = GestureTemplates.Corners(chosen.gesture);
                    int segments = rune != null ? rune.Length - 1 : 3;
                    drawLength = 0.25f + 0.2f * segments + Rand(0f, 0.2f) + 0.2f * Novice + 0.45f * Beginner;
                    drawStarted = Time.time;
                    pointsSent = 0;
                    hands.BotBeginDraw();
                    phase = hands.BotDrawing ? Phase.Drawing : Phase.Idle;
                    break;

                case Phase.Drawing:
                    if (!hands.BotDrawing)
                    {
                        // knocked out of the hand (air, a shock patch)
                        phase = Phase.Idle;
                        nextCastAt = Time.time + Rand(0.4f, 0.9f);
                        return;
                    }
                    float t = Mathf.Clamp01((Time.time - drawStarted) / drawLength);
                    SendRune(t);
                    if (t < 1f) return;
                    hands.BotFinishDraw(chosen, chosenQuality, chosenPrecision);
                    if (hands.BotHeldTier > 0)
                    {
                        phase = Phase.Holding;
                        holdUntil = Time.time + Rand(0.2f, 0.6f) + Beginner * Rand(0.1f, 0.6f);
                    }
                    else
                    {
                        phase = Phase.Idle;
                        nextCastAt = Time.time + Rand(0.6f, 1.1f);
                    }
                    break;

                case Phase.Holding:
                    if (hands.BotHeldTier == 0) { phase = Phase.Idle; nextCastAt = Time.time + 0.4f; return; }
                    // like a person: sends when it has the target in front of it, not over its shoulder
                    if (target == null || !targetSeen || Hiding || Time.time < holdUntil || FacingError > 18f) return;
                    if (hands.BotSend(AimFor(chosen), wallMode))
                    {
                        phase = Phase.Idle;
                        nextCastAt = Time.time + Rand(0.35f, 1.1f) + Beginner * Rand(0.3f, 1.1f);
                    }
                    break;
            }
        }

        /// <summary>A spell for the moment, from those whose rune will make a spell once drawn.</summary>
        Spell ChooseSpell(float distance)
        {
            var options = new List<KeyValuePair<Spell, float>>();
            Spell fire = hands.SpellByIndex(GestureCaster.FireId), ice = hands.SpellByIndex(GestureCaster.IceId);
            Spell bolt = hands.SpellByIndex(GestureCaster.LightningId), air = hands.SpellByIndex(GestureCaster.AirId);
            Spell ward = hands.SpellByIndex(GestureCaster.BarrierId);
            Spell earth = hands.SpellByIndex(GestureCaster.EarthId);

            bool onConductor = SpellZone.ConductorAt(target.transform.position) != null;
            // the rune over their head reads only from fighting distance, for a bot as for you
            bool drawing = target.Drawing.Value && distance <= Sight.ReadRange;
            Add(options, fire, 4f);
            Add(options, ice, distance < 18f ? 3f : 1.5f);
            // the clever spell for the moment -- lightning into ice, air at somebody drawing -- is seen
            // less often the less skilled the bot
            Add(options, bolt, onConductor ? Mathf.Lerp(3f, 12f, Skill) : distance > 18f ? 3f : 2f);
            Add(options, air, distance < 6f ? 6f : drawing ? Mathf.Lerp(1.2f, 4f, Skill) : 0.8f);
            // a lob: for somebody keeping still or behind cover, from where it can be thrown
            bool still = PerceivedVelocity.sqrMagnitude < 1f;
            Add(options, earth, distance < 4f || distance > 22f ? 0.3f : still ? Mathf.Lerp(2f, 4f, Skill) : 1.5f);
            bool beingShot = known.ContainsKey(target) && Time.time - known[target].HurtMeAt < 2f;
            Add(options, ward, CastShield.Of(self.AttackerId) != null ? 0f
                             : health.Fraction < 0.4f ? 3f : beingShot ? 0.8f : 0.15f);

            // A barrier now and then for no reason is fine; one every time the attacks are not ready is
            // not -- that is filling the pause with whatever is free. Unhurt, wait for an attack instead.
            if (options.Count == 1 && options[0].Key == ward && options[0].Value < 1f) return null;

            float total = 0f;
            foreach (var o in options) total += o.Value;
            if (total <= 0f) return null;
            float pick = Rand(0f, total);
            foreach (var o in options) { pick -= o.Value; if (pick <= 0f) return o.Key; }
            return options[options.Count - 1].Key;
        }

        /// <summary>
        /// A spell is an option if its rune will make the spell by the time it is drawn: not locked, not
        /// still well on its element's own cooldown (a rune finished too soon makes nothing). The global
        /// cooldown does not count -- it is for drawing the next one, the way a person uses it.
        /// </summary>
        void Add(List<KeyValuePair<Spell, float>> options, Spell s, float weight)
        {
            if (s != null && hands.BlockLeft(s) <= ReadyWithin && weight > 0f) options.Add(new KeyValuePair<Spell, float>(s, weight));
        }

        /// <summary>
        /// How far off its element's own cooldown a rune may be begun: the quickest rune takes about half
        /// a second to draw, and one finished within the queue window of the end still makes its spell.
        /// </summary>
        const float ReadyWithin = 0.8f;

        /// <summary>
        /// How the rune came out: fizzle, tier I, II or III, by skill. Tier III is a sharp bot's -- a quarter
        /// of its runes at 100 %, one in eight at 80 %, one in twenty at 60 %, next to none below (skill
        /// cubed); until 20 Oct it was 30 % less a little per step down, so a 60 % bot drew III one rune in
        /// five. Tier I goes from one rune in six at 100 % to half of them at 0 %.
        /// </summary>
        void RollQuality()
        {
            float r = (float)rnd.NextDouble();
            float s = Mathf.Clamp01(Skill);
            float fizzle = 0.04f + 0.21f * Beginner, weak = 0.16f + 0.34f * Novice, perfect = 0.25f * s * s * s;
            if (r < fizzle) { chosenQuality = CastQuality.Fizzle; chosenPrecision = 0f; }
            else if (r < fizzle + weak) { chosenQuality = CastQuality.Weak; chosenPrecision = Rand(0.45f, 0.64f); }
            else if (r > 1f - perfect) { chosenQuality = CastQuality.Perfect; chosenPrecision = Rand(0.86f, 0.98f); }
            else { chosenQuality = CastQuality.Clean; chosenPrecision = Rand(0.66f, 0.84f); }
        }

        /// <summary>The rune over the bot's head, drawn out over the time it takes: corners joined in order.</summary>
        void SendRune(float t)
        {
            if (rune == null || rune.Length < 2) return;
            const int total = 24;
            int upTo = Mathf.FloorToInt(t * total);
            for (; pointsSent <= upTo && pointsSent <= total; pointsSent++)
            {
                float along = pointsSent / (float)total * (rune.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(along), rune.Length - 2);
                Vector2 p = Vector2.Lerp(rune[i], rune[i + 1], along - i) * 0.16f;
                hands.BotStrokePoint(p + new Vector2(Rand(-0.004f, 0.004f), Rand(-0.004f, 0.004f)));
            }
        }

        /// <summary>
        /// Where to send it: where the target looked to be going, a reaction time ago, when the spell
        /// gets there (two passes of the lead) -- at the chest, or at the feet for a patch now and then --
        /// plus an aim error that grows with distance and with how fast the target crosses its view, and
        /// shrinks with skill.
        /// </summary>
        Vector3 AimFor(Spell spell)
        {
            Vector3 feet = Perceived;
            if (spell == hands.SpellByIndex(GestureCaster.BarrierId)) return feet + Vector3.up * 1.2f;
            // an ice wall, a line of fire: the floor a couple of metres towards whoever it is about --
            // the line about halfway, so it lies between them
            if (wallMode)
            {
                Vector3 toward = feet - transform.position;
                toward.y = 0f;
                float along = spell == hands.SpellByIndex(GestureCaster.FireId)
                    ? Mathf.Clamp(toward.magnitude * 0.5f, 2f, 3.5f) : 2.6f;
                return transform.position + (toward.sqrMagnitude > 0.01f ? toward.normalized : transform.forward) * along;
            }

            int tier = hands.BotHeldTier;
            float speed = tier <= 1 && spell.speedTierI > 0f ? spell.speedTierI : spell.speed;
            Vector3 from = hands.MuzzlePoint;
            // leads less the less skilled: a beginner shoots where you are, not where you are going
            Vector3 velocity = PerceivedVelocity * (1f - 0.6f * Novice);

            // earth is thrown up and over to land on the feet, led by the time the throw is in the air
            if (spell == hands.SpellByIndex(GestureCaster.EarthId))
            {
                Vector3 spot = feet;
                float flight = 0f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 dir;
                    float v;
                    GestureCaster.Lob(from, spot, out dir, out v);
                    Vector3 across = spot - from;
                    across.y = 0f;
                    float level = new Vector2(dir.x, dir.z).magnitude * v;
                    flight = across.magnitude / Mathf.Max(0.5f, level) + Reaction;
                    spot = feet + velocity * flight;
                }
                float off = AimBase + AimPerMetre * Vector3.Distance(from, spot)
                          + velocity.magnitude * flight * (0.15f + 0.35f * Novice);
                return spot + new Vector3(Rand(-1f, 1f), 0f, Rand(-1f, 1f)) * off;
            }
            // the lead is judged from what it saw, over the time the shot will take -- reaction included
            Vector3 aim = feet + Vector3.up * 1.1f;
            for (int i = 0; i < 2; i++)
            {
                float flight = Vector3.Distance(from, aim) / Mathf.Max(1f, speed) + Reaction;
                aim = feet + Vector3.up * 1.1f + velocity * flight;
            }
            bool patch = tier >= 2 && spell.groundEffect != GroundEffect.None && spell.groundEffect != GroundEffect.Updraft;
            if (patch && rnd.NextDouble() < 0.3) aim.y = feet.y + 0.05f;

            Vector3 line = (aim - from).normalized;
            float crossing = Vector3.Cross(line, velocity).magnitude;           // m/s across the line of fire
            float distance = Vector3.Distance(from, aim);
            // The error is across the line of fire -- sideways and up and down, the ways a shot misses;
            // one along the line only moved the aim nearer or further and missed nothing. It grows with
            // distance and with how fast the target crosses the view, and with every step down the slider.
            // (The aim assist bends people's shots, not bots' -- see Projectile.AimAssist.)
            float error = AimBase + AimPerMetre * distance + crossing * (0.06f + 0.25f * Novice);
            Vector3 side = Vector3.Cross(Vector3.up, line);
            side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
            Vector3 up = Vector3.Cross(line, side).normalized;
            aim += (side * Rand(-1f, 1f) + up * Rand(-0.6f, 0.6f)) * error;
            return aim;
        }

        /// <summary>How far off a shot goes, at any range (a hand that is not steady) and per metre, by skill.</summary>
        static float AimBase { get { return 0.25f * Novice; } }
        static float AimPerMetre { get { return 0.02f + 0.2f * Novice; } }
    }
}
