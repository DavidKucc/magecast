using System.Collections.Generic;
using MageCast.Combat;
using MageCast.Gestures;
using UnityEngine;
using UnityEngine.AI;

namespace MageCast
{
    /// <summary>
    /// A computer player, on the server only. It plays by exactly the rules a person does, through the
    /// same body: it walks with the motor (accelerating, braking, jumping, out of stamina like anybody),
    /// and it casts with the same hands -- drawing a rune takes it time, slows it, shows the rune over
    /// its head for everyone to read, and an air hit knocks it away; a rune can fizzle or come out at a
    /// lower tier; every spell obeys its element's cooldown; and it aims with a lead and a human-sized
    /// error. What it does NOT have is a mouse: the drawing itself is skipped, the quality rolled.
    ///
    /// Behaviour, kept simple on purpose: pick the nearest living opponent, get to a fighting range by the
    /// walkable surface (BotNav), side-step back and forth while there, choose a spell for the moment
    /// (lightning at somebody on ice, air at somebody close or drawing, a barrier when hurt, otherwise
    /// fire and ice), draw it, hold it a moment, lead the shot, send it.
    ///
    /// <see cref="Skill"/> 0..1 moves the fizzle and tier odds and the aim error together.
    /// </summary>
    public class BotBrain : MonoBehaviour
    {
        /// <summary>All bots leave people alone -- for watching them fight each other.</summary>
        public static bool IgnoreHumans;

        /// <summary>0 = clumsy, 1 = sharp. Moves rune quality and aim error together.</summary>
        public static float Skill = 0.6f;

        const float FightRange = 12f;
        const float ThinkInterval = 0.15f;

        PlayerNet self;
        PlayerMotor motor;
        GestureCaster hands;
        Health health;
        System.Random rnd;

        PlayerNet target;
        Vector3 targetVelocity, lastTargetPos;
        bool targetSeen;
        float nextThink;

        NavMeshPath path;
        int corner;
        Vector3 pathGoal;
        float repathAt;

        float strafeSign = 1f, strafeSwitchAt;
        Vector3 stuckFrom;
        float stuckCheckAt;
        Vector3 wanderGoal;
        float wanderUntil;

        enum Phase { Idle, Drawing, Holding }
        Phase phase;
        Spell chosen;
        CastQuality chosenQuality;
        float chosenPrecision;
        Vector2[] rune;
        float drawStarted, drawLength, holdUntil, nextCastAt;
        int pointsSent;

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
                return;
            }

            if (Time.time >= nextThink) Think();
            TrackTarget();
            Move();
            Cast();
        }

        // ------------------------------------------------------------------ choosing whom to fight

        void Think()
        {
            nextThink = Time.time + ThinkInterval;
            PlayerNet best = null;
            float bestScore = float.MaxValue;
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (p == null || p == self || !p.IsSpawned) continue;
                if (IgnoreHumans && !p.IsBot) continue;
                if (Teams.Allies(self, p)) continue;
                Health h = p.GetComponent<Health>();
                if (h == null || h.IsDead) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (!Sees(p)) d += 25f;              // somebody in sight first
                if (p == target) d -= 4f;            // and do not flip between two at the same distance
                if (d < bestScore) { bestScore = d; best = p; }
            }
            if (best != target)
            {
                target = best;
                if (target != null) { lastTargetPos = target.transform.position; targetVelocity = Vector3.zero; }
            }
            targetSeen = target != null && Sees(target);
        }

        bool Sees(PlayerNet p)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 chest = p.transform.position + Vector3.up * 1.2f;
            Vector3 d = chest - eye;
            foreach (RaycastHit h in Physics.RaycastAll(eye, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform) || h.transform.IsChildOf(p.transform)) continue;
                return false;
            }
            return true;
        }

        void TrackTarget()
        {
            if (target == null) return;
            Vector3 p = target.transform.position;
            Vector3 v = (p - lastTargetPos) / Mathf.Max(Time.deltaTime, 0.001f);
            v.y = 0f;
            if (v.magnitude < 20f) targetVelocity = Vector3.Lerp(targetVelocity, v, 1f - Mathf.Exp(-6f * Time.deltaTime));
            lastTargetPos = p;
        }

        // ------------------------------------------------------------------ moving

        void Move()
        {
            Vector3 pos = transform.position;
            Vector3 want = Vector3.zero;
            bool sprint = false;
            float face;

            if (target == null)
            {
                // nobody to fight: wander the map
                if (Time.time > wanderUntil || (wanderGoal - pos).sqrMagnitude < 4f)
                {
                    wanderGoal = BotNav.RandomPoint(Vector3.zero, 30f, rnd);
                    wanderUntil = Time.time + 8f;
                }
                want = Follow(wanderGoal);
                face = Yaw(want, transform.eulerAngles.y);
            }
            else
            {
                Vector3 to = target.transform.position - pos;
                to.y = 0f;
                float d = to.magnitude;
                Vector3 toDir = d > 0.01f ? to / d : transform.forward;

                if (!targetSeen || d > FightRange + 10f)
                {
                    // go and find them, by the paths
                    want = Follow(target.transform.position);
                    sprint = d > 20f && !hands.BotBusy;
                    face = targetSeen ? Yaw(toDir, 0f) : Yaw(want, transform.eulerAngles.y);
                }
                else
                {
                    // in a fight: hold the range and side-step, so a shot has to be led
                    if (Time.time >= strafeSwitchAt)
                    {
                        if (rnd.NextDouble() < 0.6) strafeSign = -strafeSign;
                        strafeSwitchAt = Time.time + Rand(0.6f, 1.8f);
                        if (!hands.BotBusy && rnd.NextDouble() < 0.12) motor.ScriptedJump = true;
                    }
                    Vector3 side = Vector3.Cross(Vector3.up, toDir) * strafeSign;
                    if (!BotNav.Open(pos, side, 1.5f)) { strafeSign = -strafeSign; side = -side; }
                    float range = d > FightRange + 3f ? 0.7f : d < FightRange - 5f ? -0.7f : 0f;
                    want = (side + toDir * range).normalized;
                    if (!BotNav.Open(pos, want, 1.5f)) want = Follow(target.transform.position);
                    face = Yaw(toDir, 0f);
                    self.AimPoint.Value = target.transform.position + Vector3.up * 1.3f;
                }
            }

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

            motor.ScriptedYaw = face;
            Quaternion toLocal = Quaternion.Euler(0f, -face, 0f);
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

        // ------------------------------------------------------------------ casting

        void Cast()
        {
            switch (phase)
            {
                case Phase.Idle:
                    if (target == null || !targetSeen || Time.time < nextCastAt || !hands.BotCanStart) return;
                    float d = Vector3.Distance(target.transform.position, transform.position);
                    if (d > 32f) return;
                    chosen = ChooseSpell(d);
                    if (chosen == null) { nextCastAt = Time.time + 0.3f; return; }
                    RollQuality();
                    rune = GestureTemplates.Corners(chosen.gesture);
                    int segments = rune != null ? rune.Length - 1 : 3;
                    drawLength = 0.25f + 0.2f * segments + Rand(0f, 0.2f) + (1f - Skill) * 0.2f;
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
                        nextCastAt = Time.time + Rand(0.3f, 0.7f);
                        return;
                    }
                    float t = Mathf.Clamp01((Time.time - drawStarted) / drawLength);
                    SendRune(t);
                    if (t < 1f) return;
                    hands.BotFinishDraw(chosen, chosenQuality, chosenPrecision);
                    if (hands.BotHeldTier > 0)
                    {
                        phase = Phase.Holding;
                        holdUntil = Time.time + Rand(0.15f, 0.5f);
                    }
                    else
                    {
                        phase = Phase.Idle;
                        nextCastAt = Time.time + Rand(0.6f, 1f);
                    }
                    break;

                case Phase.Holding:
                    if (hands.BotHeldTier == 0) { phase = Phase.Idle; nextCastAt = Time.time + 0.4f; return; }
                    if (target == null || !targetSeen || Time.time < holdUntil) return;
                    if (hands.BotSend(AimFor(chosen)))
                    {
                        phase = Phase.Idle;
                        nextCastAt = Time.time + Rand(0.2f, 0.9f);
                    }
                    break;
            }
        }

        /// <summary>A spell for the moment, from those whose element is ready.</summary>
        Spell ChooseSpell(float distance)
        {
            var options = new List<KeyValuePair<Spell, float>>();
            Spell fire = hands.SpellByIndex(GestureCaster.FireId), ice = hands.SpellByIndex(GestureCaster.IceId);
            Spell bolt = hands.SpellByIndex(GestureCaster.LightningId), air = hands.SpellByIndex(GestureCaster.AirId);
            Spell ward = hands.SpellByIndex(GestureCaster.BarrierId);

            bool onConductor = SpellZone.ConductorAt(target.transform.position) != null;
            bool drawing = target.Drawing.Value;
            Add(options, fire, 4f);
            Add(options, ice, distance < 18f ? 3f : 1.5f);
            Add(options, bolt, onConductor ? 12f : distance > 18f ? 3f : 2f);
            Add(options, air, distance < 6f ? 6f : drawing ? 4f : 0.8f);
            Add(options, ward, health.Fraction < 0.4f && CastShield.Of(self.AttackerId) == null ? 3f : 0.15f);

            // A barrier now and then for no reason is fine; one every time the attacks are on the global
            // cooldown is not -- that is filling the pause with whatever is free, the thing the pause is
            // there to stop. Unhurt, wait for an attack instead.
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

        /// <summary>How the rune came out: fizzle, tier I, II or III, by skill.</summary>
        void RollQuality()
        {
            float r = (float)rnd.NextDouble();
            float fizzle = 0.13f - 0.09f * Skill, weak = 0.42f - 0.25f * Skill, perfect = 0.05f + 0.25f * Skill;
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
        /// Where to send it: where the target will be when the spell gets there (two passes of the lead),
        /// at the chest -- or at the feet, for a patch, now and then -- plus an aim error that grows with
        /// distance and shrinks with skill.
        /// </summary>
        Vector3 AimFor(Spell spell)
        {
            Vector3 feet = target.transform.position;
            if (spell == hands.SpellByIndex(GestureCaster.BarrierId)) return feet + Vector3.up * 1.2f;

            int tier = hands.BotHeldTier;
            float speed = tier <= 1 && spell.speedTierI > 0f ? spell.speedTierI : spell.speed;
            Vector3 from = hands.MuzzlePoint;
            Vector3 aim = feet + Vector3.up * 1.1f;
            for (int i = 0; i < 2; i++)
            {
                float flight = Vector3.Distance(from, aim) / Mathf.Max(1f, speed);
                aim = feet + Vector3.up * 1.1f + targetVelocity * flight;
            }
            bool patch = tier >= 2 && spell.groundEffect != GroundEffect.None && spell.groundEffect != GroundEffect.Updraft;
            if (patch && rnd.NextDouble() < 0.3) aim.y = feet.y + 0.05f;

            float error = (0.015f + (1f - Skill) * 0.05f) * Vector3.Distance(from, aim);
            aim += new Vector3(Rand(-1f, 1f), Rand(-0.6f, 0.6f), Rand(-1f, 1f)) * error;
            return aim;
        }
    }
}
