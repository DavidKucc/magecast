using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Drives the character's Animator from the motor and the cast state.
    ///
    /// Kept as its own component rather than wired into GestureCaster so the caster never has to hold
    /// a reference to a model that may not exist -- the whole game worked on a yellow capsule until
    /// recently, and it should keep working if the art is ever swapped or missing.
    ///
    /// Two jobs. Locomotion goes to the base layer as a direction and an effort SEPARATELY, because
    /// this character faces the camera at all times and therefore spends most of a fight strafing or
    /// backing away rather than running forwards -- and because feeding both to one 2D tree put a
    /// quarter of an idle pose into every diagonal. Casting goes to a masked upper-body layer whose
    /// weight is faded here, so the legs keep running underneath while the arms do the spell.
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        [SerializeField] Animator animator;

        /// <summary>How fast the blend tree catches up to the real velocity. Snapping instantly makes
        /// the legs stutter on every tap of a movement key.
        /// The motor now ramps its own speed, so this only has to hide the last of the step -- 12 keeps
        /// the legs within a few frames of the body instead of trailing it by a third of a second.</summary>
        [SerializeField] float blendSmoothing = 12f;

        [SerializeField] float castLayerFade = 12f;

        /// <summary>How long the upper body stays with the send animation after the shot leaves.</summary>
        [SerializeField] float sendHold = 0.55f;

        static readonly int DrawingHash = Animator.StringToHash("Drawing");
        static readonly int SendHash = Animator.StringToHash("Send");
        static readonly int LeftHandHash = Animator.StringToHash("LeftHand");
        static readonly int DefensiveHash = Animator.StringToHash("Defensive");
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int DirXHash = Animator.StringToHash("DirX");
        static readonly int DirZHash = Animator.StringToHash("DirZ");
        static readonly int AirborneHash = Animator.StringToHash("Airborne");
        static readonly int JumpBackHash = Animator.StringToHash("JumpBack");
        static readonly int HoldHash = Animator.StringToHash("Hold");
        static readonly int JumpRunHash = Animator.StringToHash("JumpRun");
        static readonly int ClimbHash = Animator.StringToHash("Climb");
        bool hasClimb;

        /// <summary>Effort above this at take-off is a sprint, and the running jump plays.</summary>
        const float SprintEffort = 1.2f;

        /// <summary>How fast the legs change between the jog and the spell-in-hand walk, per second.</summary>
        const float HoldBlendSpeed = 5f;

        Gestures.GestureCaster caster;
        float shownHold;

        static readonly int DeadHash = Animator.StringToHash("Dead");
        static readonly int DeathDirHash = Animator.StringToHash("DeathDir");
        Combat.Health health;
        bool shownDead;

        /// <summary>How hard she has to be backing away for the jump to read as a backward leap, in m/s.
        /// Above a drift, below a committed backpedal.</summary>
        [SerializeField] float backwardJumpThreshold = 1f;

        bool wasGrounded = true;

        PlayerMotor motor;
        PlayerNet net;
        int castLayer = -1;

        // Somebody else's player has no motor running, so how it moves is read back off how the
        // network moved it. Smoothed, because positions arrive in steps at the network's tick rate.
        Vector3 lastPosition;
        Vector3 remoteVelocity;
        bool hasLastPosition;
        bool drawing;
        float sendUntil = -99f;
        float shownSpeed;
        Vector2 shownDir = Vector2.up;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            motor = GetComponent<PlayerMotor>();
            net = GetComponent<PlayerNet>();
            caster = GetComponent<Gestures.GestureCaster>();
            health = GetComponent<Combat.Health>();
            if (animator != null) castLayer = animator.GetLayerIndex("Cast");
            if (animator != null)
                foreach (AnimatorControllerParameter p in animator.parameters) if (p.nameHash == ClimbHash) hasClimb = true;
        }

        void Update()
        {
            if (animator == null || motor == null) return;

            bool remote = net != null && net.IsRemote;

            // Dying: the whole body falls, the arms included, and stays down until the respawn.
            bool dead = health != null && health.IsDead;
            if (dead != shownDead)
            {
                shownDead = dead;
                if (dead) animator.SetInteger(DeathDirHash, FallDirection());
                animator.SetBool(DeadHash, dead);
            }
            if (dead)
            {
                if (castLayer >= 0) animator.SetLayerWeight(castLayer, 0f);
                return;
            }
            Vector2 velocity = remote ? RemoteLocalVelocity() : motor.LocalPlanarVelocity;
            float speed = velocity.magnitude;
            if (remote) SetDrawing(net.Drawing.Value);

            // Effort, not metres per second: 1 means "as fast as she goes in this direction", so a
            // flat-out backpedal reads as hard as a flat-out run even though it is 2.4 m/s against 4.
            float effort = speed / Mathf.Max(0.01f, motor.WalkSpeedTowards(velocity));

            // Below walking pace the direction is noise -- a hair of residual velocity would spin the
            // directional tree while she stands still -- so the last real heading is held instead.
            if (speed > 0.2f)
            {
                // Turned through an ANGLE, not lerped between two vectors. The lerp-and-renormalise this
                // replaces could not reverse at all: forward lerped towards backward is a shorter vector
                // still pointing forward, and renormalising stretched it straight back to "forward". W
                // then S kept the legs running forward while the body went backwards -- the moonwalk --
                // until a stray sideways component let it swing round.
                //
                // A reversal snaps. By then the body has braked almost to a stop, so there is no stride
                // to see jump; turning it through 180 degrees instead would sweep the legs through a
                // sideways strafe on the way, which is a lie about where she is going.
                Vector2 wanted = velocity / speed;
                float angle = Vector2.SignedAngle(shownDir, wanted);
                if (Mathf.Abs(angle) > 135f)
                    shownDir = wanted;
                else
                    shownDir = Rotate(shownDir, angle * (1f - Mathf.Exp(-blendSmoothing * Time.deltaTime)));
            }

            // Smoothed, because the input is binary: tapping a key snaps the velocity from 0 to full in
            // one frame and the blend tree would jump between clips mid-stride.
            shownSpeed = Mathf.Lerp(shownSpeed, effort, 1f - Mathf.Exp(-blendSmoothing * Time.deltaTime));

            animator.SetFloat(SpeedHash, shownSpeed);

            // a spell in hand: she walks
            bool holding = remote ? net.Holding.Value : caster != null && caster.HeldSpell != null;
            shownHold = Mathf.MoveTowards(shownHold, holding ? 1f : 0f, HoldBlendSpeed * Time.deltaTime);
            animator.SetFloat(HoldHash, shownHold);
            animator.SetFloat(DirXHash, shownDir.x);
            animator.SetFloat(DirZHash, shownDir.y);

            // Read from the motor's grounded flag, not from the jump key, so being knocked off your
            // feet or stepping off a ledge looks the same as jumping -- which it should.
            bool airborne = remote ? !net.Grounded.Value : !motor.IsGrounded;

            // the pull-up has its own clip once one is in the folder (the animator builder adds it)
            if (hasClimb) animator.SetBool(ClimbHash, remote ? net.Climbing.Value : motor.IsClimbing);

            // Latched at the moment the feet leave the ground and held for the whole flight. Deciding
            // it every frame instead would let a mid-air steer swap the animation halfway through a jump.
            if (airborne && wasGrounded)
            {
                animator.SetBool(JumpBackHash, velocity.y < -backwardJumpThreshold);
                animator.SetBool(JumpRunHash, effort > SprintEffort);
            }
            wasGrounded = !airborne;

            animator.SetBool(AirborneHash, airborne);

            if (castLayer < 0) return;

            bool wantCast = drawing || Time.time < sendUntil;
            float weight = Mathf.Lerp(animator.GetLayerWeight(castLayer), wantCast ? 1f : 0f,
                                      1f - Mathf.Exp(-castLayerFade * Time.deltaTime));
            animator.SetLayerWeight(castLayer, weight);
        }

        /// <summary>
        /// Which way to fall, from where the nearest opponent stands -- the shot most likely came from
        /// them: hit from the front she falls back, from behind forward, from a side away from it.
        /// 0 backward, 1 forward, 2 to the left, 3 to the right.
        /// </summary>
        int FallDirection()
        {
            PlayerNet nearest = null;
            float best = float.MaxValue;
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (p == null || p.gameObject == gameObject) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = p; }
            }
            if (nearest == null) return 0;
            Vector3 from = transform.InverseTransformPoint(nearest.transform.position);
            if (Mathf.Abs(from.z) >= Mathf.Abs(from.x)) return from.z >= 0f ? 0 : 1;
            return from.x >= 0f ? 2 : 3;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c).normalized;
        }

        Vector2 RemoteLocalVelocity()
        {
            Vector3 now = transform.position;
            if (!hasLastPosition || Time.deltaTime <= 0f)
            {
                lastPosition = now;
                hasLastPosition = true;
                return Vector2.zero;
            }

            Vector3 raw = (now - lastPosition) / Time.deltaTime;
            raw.y = 0f;
            lastPosition = now;

            // a respawn is a jump across the map, not a sprint
            if (raw.sqrMagnitude > 20f * 20f) raw = Vector3.zero;

            remoteVelocity = Vector3.Lerp(remoteVelocity, raw, 1f - Mathf.Exp(-10f * Time.deltaTime));
            Vector3 local = transform.InverseTransformDirection(remoteVelocity);
            return new Vector2(local.x, local.z);
        }

        public void SetDrawing(bool value)
        {
            if (animator == null || drawing == value) return;
            drawing = value;
            animator.SetBool(DrawingHash, value);
        }

        /// <summary>
        /// Plays the cast-release animation. Two of them: a barrier is put up, everything else is thrown.
        /// </summary>
        public void PlaySend(bool defensive, bool leftHand = false)
        {
            if (animator == null) return;
            animator.SetBool(LeftHandHash, leftHand);      // mirrored: the left hand throws
            animator.SetBool(DefensiveHash, defensive);   // set BEFORE the trigger, or the transition
            animator.SetTrigger(SendHash);                // is evaluated against last cast's value
            sendUntil = Time.time + sendHold;
        }
    }
}
