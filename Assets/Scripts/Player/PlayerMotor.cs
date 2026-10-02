using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// WASD + jump on a CharacterController. Strafe-style: the body always faces where the camera
    /// looks, so the direction a spell would lock to is the direction you are already facing.
    ///
    /// SpeedMultiplier / SprintLocked / JumpLocked exist for the cast mechanic to drive later
    /// (design calls for ~60% speed, no sprint and no jump while a glyph is being drawn). Nothing
    /// touches them yet.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Speed (m/s)")]
        // Lowered from 6/9, and the reason is worth keeping: at 6 m/s the run animation had to play 58%
        // fast to keep the feet from skating, which reads as a cartoon. Measured, the clips are authored
        // for 3.8 and 6.0 m/s, so these numbers let them play at their natural rate.
        //
        // The old 6 came from a map rule that no longer bites: cover used to be up to 8 m away, and the
        // thinned arena now averages 2.7 m -- 0.68 s at this speed. The worst corner is 7.3 m, 1.83 s.
        // Raise it if the game feels sluggish, but expect the legs to start looking hurried again above 5.
        //
        // Then moved down a gear: plain WASD is now a jog and Shift is the old 4 m/s run. The jog speed
        // is not a guess -- the downloaded "Run" clip measures 2.4 m/s, so 2.6 plays it at 1.08x, and
        // the run on Shift plays "Fast Run" (3.9 m/s) at 1.03x. Both near their natural rate.
        // The animator builder mirrors these two numbers; change them together.
        //
        // The clip then changed to "Jog Forward": "Run" held the pelvis 10 degrees to the right for its
        // whole cycle, which is what "he walks as if sideways" was, and "Jog Forward" swings evenly
        // about straight ahead. It was briefly slowed to 2.2 on a wrong reading of that clip's pace --
        // it travels 2.53 m/s by its own root motion -- and 2.2 was too slow to play. Back to 2.6, which
        // plays the new clip at 1.03x: its natural rate.
        //
        // And then up, for the game rather than the legs: at 2.6 nothing could be dodged. Fire crosses a
        // 12 m fight in 0.46 s, and after a quarter second of reaction a jogger had moved a hand's width.
        // W is now the old run on the straight "Fast Run" clip (3.6 m/s, played at 1.06x) and Shift is a
        // sprint on the same clip, until a real sprint clip is in the folder.
        // Then 4.3 / 6.2 after playing it: still too slow to get out of the way. Backing off and strafing
        // keep their multipliers, so they rise in step.
        [SerializeField] float jogSpeed = 4.3f;
        [SerializeField] float runSpeed = 6.2f;

        /// <summary>
        /// The body moves this much faster than the legs are animated for. On purpose, as a test: the
        /// two speeds above are what the animations are matched to (the animator builder reads them),
        /// and this multiplies only the actual movement -- so the game gets faster without the run
        /// cycles being wound up into a cartoon. The price is visible: at 1.5 the feet slide, since
        /// they cover two thirds of the ground the body does. Set to 1 to match them again.
        /// Backing off, strafing, acceleration and braking all scale with it.
        /// </summary>
        [SerializeField] float paceOverAnimation = 1.5f;

        /// <summary>
        /// How fast ground speed builds up and dies away, in m/s per second.
        ///
        /// Velocity used to be SET to the input every frame -- 0 to full in one frame and back. The
        /// animation cannot do that: the legs blend up over a few tenths of a second, so every start and
        /// every stop showed the body moving while the feet had not started yet, or the feet still running
        /// after the body had stopped. That is the "sliding on ice" look. With a ramp the body and the
        /// legs speed up together. 20 reaches a jog in 0.13 s, which still reads as instant on a key.
        /// </summary>
        // Raised with the speeds, so a dodge still takes the same eighth of a second to reach full pace.
        [SerializeField] float groundAcceleration = 30f;
        [SerializeField] float groundDeceleration = 38f;

        /// <summary>
        /// Backing away and strafing are slower than running forward. Standard in shooters, and here it
        /// also fixes something visible: measured, the backward run animation is authored for 3 m/s, so
        /// moving at the full 6 forced it to play at double rate and looked like fast-forward. Scaling
        /// the movement is the honest fix; speeding the clip up further was papering over it.
        /// </summary>
        [SerializeField, Range(0.3f, 1f)] float backwardMultiplier = 0.6f;
        [SerializeField, Range(0.3f, 1f)] float strafeMultiplier = 0.85f;

        [Header("Jump")]
        // 1.35 rather than the 1.2 in the scale contract, on purpose. An apex of exactly 1.2 m only
        // just grazes a 1.2 m platform, and CharacterController integrates in discrete steps, so the
        // landing becomes frame-perfect and feels broken. 15 cm of headroom makes it land every time.
        [SerializeField] float jumpHeight = 1.35f;
        [SerializeField] float gravity = -22f;           // heavier than real gravity; 9.81 feels floaty

        /// <summary>The fall rate as a positive number, for working out throws (see Toss).</summary>
        public const float Gravity = 22f;
        [SerializeField] float coyoteTime = 0.12f;       // still jumpable just after walking off an edge
        [SerializeField] float jumpBuffer = 0.12f;       // pressing just before landing still counts
        /// <summary>
        /// Steering in the air. Was 12, which let a standing jump pick up a full sideways run in a
        /// quarter of a second -- the body glided sideways in a held jump pose with nothing moving it.
        /// At 4 a jump keeps most of the momentum it left the ground with, and steering is a nudge.
        /// </summary>
        [SerializeField] float airAcceleration = 4f;

        /// <summary>
        /// How fast the body turns to face where the camera looks, in degrees per second.
        ///
        /// 720 means a 90-degree flick lands in an eighth of a second: fast enough that aiming never
        /// feels like it is fighting you, slow enough that the turn is something you watch happen.
        /// Raise it towards a snap if it ever feels loose; lower it for a heavier character.
        /// </summary>
        [SerializeField] float bodyTurnSpeed = 720f;

        [Header("Climb")]
        /// <summary>
        /// How far above the feet the hands reach to pull up onto a ledge. With the 1.35 m jump that is
        /// any ledge up to about 2.65 m: the 2.4 m ledges the maps are built with, but not a 2.8 m wall
        /// that is there to block sight, and not a 4 m storey.
        /// </summary>
        [SerializeField] float climbReach = 1.3f;
        /// <summary>Pulling up, start to crouching on top. Long enough to be shot during, on purpose.</summary>
        [SerializeField] float climbSeconds = 0.6f;

        /// <summary>Pulling up onto a ledge: no casting, and a direct hit drops you (CancelClimb).</summary>
        public bool IsClimbing { get; private set; }
        public float ClimbSeconds { get { return climbSeconds; } }

        /// <summary>The climb being made: the edge (on the wall face, at the height of the top), the
        /// direction into the wall, and the floor it started over. The animation hangs the hands on it.</summary>
        public Vector3 ClimbEdge { get; private set; }
        public Vector3 ClimbInto { get; private set; }
        public float ClimbFloor { get; private set; }

        Vector3 climbFrom, climbUp, climbTo;
        float climbStarted;

        [Header("Refs")]
        [SerializeField] ThirdPersonCamera cam;

        /// <summary>Scales ground speed. The cast mechanic will hold this at ~0.6 while drawing.</summary>
        public float SpeedMultiplier { get; set; }
        public bool SprintLocked { get; set; }
        public bool JumpLocked { get; set; }
        public bool IsGrounded { get; private set; }

        /// <summary>Up is positive, in m/s -- for the animator to tell a hop from a drop off a roof.</summary>
        public float VerticalSpeed { get { return velocity.y; } }

        /// <summary>Ground speed in metres per second, for the animator's blend tree to read.</summary>
        public float PlanarSpeed
        {
            get { return new Vector2(velocity.x + external.x, velocity.z + external.z).magnitude; }
        }

        /// <summary>
        /// The same movement expressed in the character's OWN space: +Z is forward, +X is right, in
        /// metres per second. The animator needs direction rather than speed, because this character
        /// faces the camera at all times and therefore spends most of a fight strafing or backing off
        /// rather than running forwards.
        /// </summary>
        public Vector2 LocalPlanarVelocity
        {
            get
            {
                Vector3 world = new Vector3(velocity.x + external.x, 0f, velocity.z + external.z);
                Vector3 local = transform.InverseTransformDirection(world);
                return new Vector2(local.x, local.z);
            }
        }

        /// <summary>
        /// Top ground speed in a given direction of the character's own space, walking.
        ///
        /// The animator needs this to say how hard she is moving as a FRACTION rather than in m/s:
        /// backing away flat out is 2.4 m/s and strafing is 3.4, and if the animator compared either
        /// against the 4 m/s of a forward run it would conclude she was half standing still.
        /// </summary>
        public float WalkSpeedTowards(Vector2 localDirection)
        {
            Vector2 d = localDirection.sqrMagnitude < 0.000001f ? new Vector2(0f, 1f) : localDirection.normalized;
            float x = d.x * strafeMultiplier;
            float z = d.y < 0f ? d.y * backwardMultiplier : d.y;
            return new Vector2(x, z).magnitude * jogSpeed * paceOverAnimation;
        }

        CharacterController cc;
        Vector3 velocity;
        Vector3 external;                  // knockback and other outside shoves
        float lastGroundedTime = -999f;
        float lastJumpPressedTime = -999f;

        /// <summary>
        /// How fast an outside shove bleeds off. Higher is snappier; around 3.5 a hit carries you for
        /// roughly a second, which is long enough to be dragged out of cover and short enough that you
        /// never feel like you have lost the controls.
        /// </summary>
        [SerializeField] float externalDamping = 3.5f;

        /// <summary>
        /// Push from something other than the player's own input -- the air spell, for now.
        ///
        /// Kept in its own vector rather than added to velocity, because the grounded branch below
        /// overwrites velocity.x and velocity.z from the movement input every single frame. An impulse
        /// added to velocity would be erased before it ever moved anybody.
        /// </summary>
        public void AddImpulse(Vector3 impulse)
        {
            // Somebody else's character is moved by its owner, so a shove is passed on to them.
            if (!IsLocallyControlled) { if (net != null) net.SendImpulse(impulse); return; }
            external += impulse;
        }

        /// <summary>
        /// A push along the ground that friction has to stop -- unlike AddImpulse, which fades by itself
        /// whatever you stand on. On dry floor it is gone in a fifth of a second; on ice, with the grip
        /// gone, it carries you the length of the patch. That is air blowing across ice.
        /// </summary>
        public void Slide(Vector3 push)
        {
            if (!IsLocallyControlled) { if (net != null) net.SendSlide(push); return; }
            velocity.x += push.x;
            velocity.z += push.z;
        }

        PlayerNet net;

        /// <summary>
        /// True where this machine moves this body: always offline, and only for your own player in a
        /// networked game. Everyone else's body is a puppet driven by their machine.
        /// </summary>
        public bool IsLocallyControlled { get { return net == null || !net.IsSpawned || net.IsOwner; } }

        float slowMultiplier = 1f;
        float slowUntil = -99f;

        /// <summary>
        /// Slows the character from outside -- an ice patch, an ice bolt.
        ///
        /// Kept apart from SpeedMultiplier on purpose. That one belongs to the caster, which sets it to
        /// 0.6 while drawing and back to 1 the moment the draw ends; sharing it would mean finishing a
        /// glyph cancelled the ice you were standing in. The two multiply instead, which is also the
        /// design: drawing inside an ice patch leaves you at 30%.
        ///
        /// Slows do not stack. The strongest one active wins, and an equal one only extends it.
        /// </summary>
        public void ApplySlow(float multiplier, float duration)
        {
            if (!IsLocallyControlled) { if (net != null) net.SendSlow(multiplier, duration); return; }

            float until = Time.time + duration;
            bool active = Time.time < slowUntil;

            if (!active || multiplier < slowMultiplier)
            {
                slowMultiplier = multiplier;
                slowUntil = until;
            }
            else if (Mathf.Approximately(multiplier, slowMultiplier))
            {
                slowUntil = Mathf.Max(slowUntil, until);
            }
        }

        public float CurrentSlow { get { return Time.time < slowUntil ? slowMultiplier : 1f; } }

        float grip = 1f;
        float gripUntil = -99f;

        /// <summary>
        /// Takes the grip away -- an ice patch. Top speed is untouched; what goes is the ability to CHANGE
        /// it: speeding up, braking and turning all run at this share of normal. You keep sliding the
        /// way you were going, which is exactly what a player needs in order to be hit.
        /// The least grip active wins.
        /// </summary>
        public void ApplySlippery(float gripMultiplier, float duration)
        {
            if (!IsLocallyControlled) return;     // a patch applies itself on the body's own machine
            bool active = Time.time < gripUntil;
            if (!active || gripMultiplier < grip) grip = gripMultiplier;
            gripUntil = Mathf.Max(active ? gripUntil : 0f, Time.time + duration);
        }

        public float CurrentGrip { get { return Time.time < gripUntil ? grip : 1f; } }

        /// <summary>
        /// Throws the character straight up, as a proper jump would -- not through the external impulse,
        /// which bleeds off exponentially and would make a launch float instead of arc.
        /// </summary>
        public void Launch(float upSpeed)
        {
            if (!IsLocallyControlled) { if (net != null) net.SendLaunch(upSpeed); return; }

            if (velocity.y < upSpeed) velocity.y = upSpeed;
            lastGroundedTime = -999f;     // no coyote-time jump stacked on top of the launch
        }

        /// <summary>
        /// Throws the body in an arc: up at <paramref name="upSpeed"/> and along at
        /// <paramref name="along"/>, replacing its own run -- the air bomb pulling people into a patch.
        /// In the air there is little control, so it lands where it was thrown.
        /// </summary>
        public void Toss(Vector3 along, float upSpeed)
        {
            if (!IsLocallyControlled) { if (net != null) net.SendToss(along, upSpeed); return; }
            velocity.x = along.x;
            velocity.z = along.z;
            if (velocity.y < upSpeed) velocity.y = upSpeed;
            external = Vector3.zero;
            lastGroundedTime = -999f;
            tossed = true;
        }

        /// <summary>In a throw from Toss: no steering until the feet are back on the ground.</summary>
        bool tossed;

        /// <summary>
        /// Puts the body somewhere else outright -- a respawn. The controller has to be off while the
        /// transform moves, or it snaps straight back to where it was on its next Move.
        /// </summary>
        public void TeleportTo(Vector3 position, float yaw)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(position + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = was;

            velocity = Vector3.zero;
            external = Vector3.zero;
            tossed = false;
            IsClimbing = false;
            slowUntil = -99f;
            gripUntil = -99f;
        }

        void Awake()
        {
            net = GetComponent<PlayerNet>();
            cc = GetComponent<CharacterController>();
            SpeedMultiplier = 1f;
            if (cam == null) cam = FindObjectOfType<ThirdPersonCamera>();
        }

        void Update()
        {
            // switched off while dead, and a disabled controller must not be asked to move
            if (!cc.enabled) return;

            IsGrounded = cc.isGrounded;
            if (IsGrounded) lastGroundedTime = Time.time;
            bool hands = !GameInput.Blocked;
            if (hands && Input.GetButtonDown("Jump")) lastJumpPressedTime = Time.time;

            float yaw = cam != null ? cam.Yaw : transform.eulerAngles.y;
            Quaternion basis = Quaternion.Euler(0f, yaw, 0f);

            // The body FOLLOWS the camera instead of being set to it.
            //
            // This line used to read `transform.rotation = basis`, which snapped the whole character to
            // the mouse every frame. Nothing animates a snap, so any flick of the mouse spun her on the
            // spot like a model on a turntable -- a large part of "the movement is weird" that no
            // animation clip could have fixed, because no clip was playing during it.
            //
            // Aim is untouched: spells still go where the camera points, so the lag never costs a shot.
            // What it buys is that turning now shows up as movement -- the blend tree sees the velocity
            // swing sideways relative to the body and leans into the turn.
            transform.rotation = Quaternion.RotateTowards(transform.rotation, basis,
                                                          bodyTurnSpeed * Time.deltaTime);

            if (IsClimbing) { ClimbStep(); return; }

            // With a menu open the keys belong to the menu; the body still falls and still slides to a stop.
            Vector3 input = hands ? new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"))
                                  : Vector3.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Shift runs in every direction, not only forward: it is the old default speed, and this
            // character spends most of a fight strafing, so a forward-only run would rarely apply.
            bool sprinting = hands && !SprintLocked && Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > 0.01f;

            // shaped before the basis rotation, so the limits apply to the character's own axes
            Vector3 shaped = input;
            if (shaped.z < 0f) shaped.z *= backwardMultiplier;
            shaped.x *= strafeMultiplier;

            Vector3 wanted = basis * shaped * ((sprinting ? runSpeed : jogSpeed) * paceOverAnimation
                                               * SpeedMultiplier * CurrentSlow);

            // a throw ends when it comes down, not in the frame it leaves the ground
            if (tossed && IsGrounded && velocity.y <= 0f) tossed = false;

            if (tossed)
            {
                // thrown: the arc is the throw's, not the player's
            }
            else if (IsGrounded)
            {
                Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
                // speeding up (or turning) uses the acceleration, only letting go uses the braking
                float rate = wanted.sqrMagnitude > flat.sqrMagnitude * 0.99f ? groundAcceleration
                                                                              : groundDeceleration;
                flat = Vector3.MoveTowards(flat, wanted, rate * paceOverAnimation * CurrentGrip * Time.deltaTime);
                velocity.x = flat.x;
                velocity.z = flat.z;
                if (velocity.y < 0f) velocity.y = -2f;   // keep it pinned down so isGrounded stays honest
            }
            else
            {
                // partial air control: you can adjust a jump, you cannot turn it into a new one
                Vector3 air = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), wanted,
                                                  airAcceleration * Time.deltaTime);
                velocity.x = air.x;
                velocity.z = air.z;
            }

            // Jumped at a ledge too high to land on: grab it and pull up. Only while pushing towards it,
            // and only from the top of the jump down -- on the way up a jump may still clear it by itself.
            if (!IsGrounded && !tossed && !JumpLocked && CurrentSlow > 0f && velocity.y < 2.5f
                && wanted.sqrMagnitude > 0.01f && TryClimb(wanted))
                return;

            // frozen solid (an ice III): no running and no jumping out of it either
            bool jumpReady = !JumpLocked && CurrentSlow > 0f
                             && Time.time - lastGroundedTime <= coyoteTime
                             && Time.time - lastJumpPressedTime <= jumpBuffer;
            if (jumpReady)
            {
                velocity.y = Mathf.Sqrt(2f * -gravity * jumpHeight);
                lastGroundedTime = -999f;
                lastJumpPressedTime = -999f;
            }

            velocity.y += gravity * Time.deltaTime;
            cc.Move((velocity + external) * Time.deltaTime);

            // exponential bleed-off, framerate independent
            external *= Mathf.Exp(-externalDamping * Time.deltaTime);
            if (external.sqrMagnitude < 0.01f) external = Vector3.zero;
        }

        // ---------------------------------------------------------------- climbing

        /// <summary>
        /// Looks for a ledge in the direction of <paramref name="wish"/>: a wall right in front, a flat top
        /// within reach of the hands, room to stand up there and room to rise to it. Starts the pull-up
        /// when all four are there.
        /// </summary>
        bool TryClimb(Vector3 wish)
        {
            Vector3 dir = new Vector3(wish.x, 0f, wish.z).normalized;
            Vector3 feet = transform.position;
            float r = cc.radius;

            RaycastHit wall = default;
            bool found = false;
            foreach (float h in new[] { 0.5f, 1.1f })
            {
                if (!Cast(feet + Vector3.up * h, dir, r + 0.4f, out wall)) continue;
                if (Mathf.Abs(wall.normal.y) > 0.3f) continue;
                found = true;
                break;
            }
            if (!found) return false;

            Vector3 into = new Vector3(-wall.normal.x, 0f, -wall.normal.z).normalized;
            if (Vector3.Dot(into, dir) < 0.5f) return false;       // brushing along a wall is not climbing it

            Vector3 probe = wall.point + into * 0.3f;
            probe.y = feet.y + climbReach + 0.3f;
            RaycastHit top;
            if (!Cast(probe, Vector3.down, climbReach + 0.3f, out top) || top.normal.y < 0.7f) return false;
            float rise = top.point.y - feet.y;
            if (rise < 0.15f || rise > climbReach) return false;

            Vector3 up = new Vector3(feet.x, top.point.y + 0.05f, feet.z);
            Vector3 land = new Vector3(wall.point.x, top.point.y + 0.05f, wall.point.z) + into * (r + 0.15f);
            if (Blocked(up) || Blocked(land)) return false;

            RaycastHit floor;
            ClimbFloor = Cast(feet + Vector3.up * 0.1f, Vector3.down, 4f, out floor) ? floor.point.y : feet.y - 1f;
            ClimbEdge = new Vector3(wall.point.x, top.point.y, wall.point.z);
            ClimbInto = into;
            IsClimbing = true;
            climbStarted = Time.time;
            climbFrom = feet;
            climbUp = up;
            climbTo = land;
            velocity = Vector3.zero;
            external = Vector3.zero;
            return true;
        }

        /// <summary>Straight up the face first, then over the edge -- the capsule never cuts the corner.</summary>
        void ClimbStep()
        {
            float k = Mathf.Clamp01((Time.time - climbStarted) / climbSeconds);
            Vector3 target = k < 0.6f
                ? Vector3.Lerp(climbFrom, climbUp, Mathf.SmoothStep(0f, 1f, k / 0.6f))
                : Vector3.Lerp(climbUp, climbTo, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
            cc.Move(target - transform.position);
            if (k < 1f) return;
            IsClimbing = false;
            velocity = new Vector3(0f, -2f, 0f);
            cc.Move(Vector3.down * 0.1f);       // feet onto the top now, so the animator sees a landing, not a fall
            IsGrounded = cc.isGrounded;
            lastGroundedTime = Time.time;
        }

        /// <summary>Knocked off the ledge: let go where you are and fall.</summary>
        public void CancelClimb()
        {
            if (!IsClimbing) return;
            IsClimbing = false;
            velocity = Vector3.zero;
            lastGroundedTime = -999f;
        }

        /// <summary>Level geometry only: not yourself, not other people, not a barrier, not a dummy.</summary>
        bool Solid(Collider c)
        {
            return c != null && !c.isTrigger && !c.transform.IsChildOf(transform)
                   && c.GetComponentInParent<PlayerMotor>() == null
                   && c.GetComponentInParent<Gestures.CastShield>() == null
                   && c.GetComponentInParent<Combat.TrainingDummy>() == null;
        }

        bool Cast(Vector3 from, Vector3 dir, float distance, out RaycastHit nearest)
        {
            nearest = default;
            bool any = false;
            foreach (RaycastHit h in Physics.RaycastAll(from, dir, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!Solid(h.collider) || (any && h.distance >= nearest.distance)) continue;
                nearest = h;
                any = true;
            }
            return any;
        }

        /// <summary>Whether a standing body with its feet at <paramref name="feet"/> would be inside something.</summary>
        bool Blocked(Vector3 feet)
        {
            float r = cc.radius;
            foreach (Collider c in Physics.OverlapCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * (cc.height - r),
                                                          r * 0.95f, ~0, QueryTriggerInteraction.Ignore))
                if (Solid(c)) return true;
            return false;
        }
    }
}
