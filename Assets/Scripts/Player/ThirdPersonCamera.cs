using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Orbit camera with a spring arm. Owns yaw/pitch for the whole rig -- the motor reads Yaw from
    /// here rather than the other way round, so the body can never lag a frame behind the aim.
    ///
    /// Built with the gesture mechanic in mind but deliberately not implementing it: holding RMB will
    /// later set LookEnabled = false, which freezes the camera exactly where it stood while the mouse
    /// goes on to draw a glyph. AimOrigin/AimDirection are what the cast will be snapshotted from.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Framing")]
        [SerializeField] float pivotHeight = 1.55f;      // roughly the shoulder of a 2 m player
        [SerializeField] float shoulderOffset = 0.5f;
        [SerializeField] float distance = 4.5f;
        [SerializeField] float minPitch = -30f;
        [SerializeField] float maxPitch = 65f;

        // Aiming and drawing want very different numbers: a sensitivity that feels right for flicking
        // the camera around is far too twitchy for tracing a shape. Separate fields from day one, so
        // the drawing sensitivity can be tuned without wrecking the aim.
        [Header("Sensitivity")]
        [SerializeField] float aimSensitivity = 2.4f;

        [Header("Collision")]
        [SerializeField] float collisionRadius = 0.28f;
        [SerializeField] float minDistance = 0.9f;
        [SerializeField] float pullInSpeed = 40f;        // snap in hard...
        [SerializeField] float pushOutSpeed = 6f;        // ...but ease back out, or it strobes

        /// <summary>False freezes the camera without freezing the player. The cast mechanic drives this.</summary>
        public bool LookEnabled = true;

        /// <summary>
        /// Over the left shoulder instead of the right -- a click of the mouse wheel swaps. Spells then
        /// leave from the left hand, on the side the crosshair looks past.
        /// </summary>
        public bool LeftShoulder { get; private set; }

        /// <summary>-1 left .. +1 right, easing towards the chosen side so the swap is a move, not a cut.</summary>
        float side = 1f;
        const float SideSwapSpeed = 7f;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public Vector3 AimOrigin { get { return Time.time < shakeUntil ? aimFrom : transform.position; } }
        public Vector3 AimDirection { get { return transform.forward; } }

        float currentDistance;

        // ---- a jolt when the ground is hit close by (an earth landing)
        static ThirdPersonCamera active;
        float shakeUntil, shakeStrength;
        const float ShakeSeconds = 0.35f;
        const float ShakeMetres = 0.09f;

        /// <summary>
        /// Shakes the view a little, as hard as <paramref name="strength"/> (0..1) where it happened and
        /// fading to nothing <paramref name="reach"/> metres off. The view only -- the aim stays where it was.
        /// </summary>
        public static void Shake(Vector3 at, float strength, float reach)
        {
            if (active == null || active.target == null || reach <= 0f) return;
            float s = strength * (1f - Vector3.Distance(active.target.position, at) / reach);
            if (s <= 0f) return;
            if (Time.time >= active.shakeUntil || s > active.shakeStrength) active.shakeStrength = s;
            active.shakeUntil = Time.time + ShakeSeconds;
        }

        void OnEnable() { active = this; }

        void Start()
        {
            currentDistance = distance;
            if (target != null) Yaw = target.eulerAngles.y;

            // The spell effects fade their smoke and floor glows into the ground by the scene's depth;
            // without it those parts are simply not drawn.
            Camera cam = GetComponent<Camera>();
            if (cam != null) cam.depthTextureMode |= DepthTextureMode.Depth;
        }

        /// <summary>
        /// Follow a player -- or nobody, which leaves the camera where it is. Players are spawned by the
        /// network now rather than sitting in the scene, so the camera finds out who is "you" from
        /// whoever turns out to be the local player, not from a reference wired in the editor.
        /// </summary>
        public void SetTarget(Transform newTarget, float yaw)
        {
            target = newTarget;
            Yaw = yaw;
            Pitch = 0f;
            currentDistance = distance;
        }

        void LateUpdate()
        {
            // The mouse is locked to the game only while there is a player to steer and no menu open.
            // Esc used to toggle the lock directly; it opens the menu now, and the lock follows it.
            bool wantLocked = target != null && !GameInput.MenuOpen;
            if (NetSession.IsWeb)
            {
                // Asked for again whenever the browser does not actually have it: Unity turns a Locked
                // request into the browser's pointer lock on the next click, and the browser drops the
                // lock on Esc without Unity always noticing.
                if (wantLocked) { if (Cursor.lockState != CursorLockMode.Locked) LockCursor(true); }
                else if (Cursor.lockState != CursorLockMode.None || WebPointer.IsLocked) LockCursor(false);
            }
            else if ((Cursor.lockState == CursorLockMode.Locked) != wantLocked)
            {
                LockCursor(wantLocked);
            }

            if (target == null) return;

            // Only while the mouse is REALLY locked. In a browser, Cursor.lockState can still say Locked
            // after Esc has freed the pointer, and the camera then swung round under a visible cursor.
            if (LookEnabled && !GameInput.MenuOpen && WebPointer.IsLocked)
            {
                float sensitivity = aimSensitivity * GameSettings.MouseSensitivity;
                Yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
                Pitch = Mathf.Clamp(Pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, minPitch, maxPitch);
            }

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            if (!GameInput.Blocked && Input.GetMouseButtonDown(2)) LeftShoulder = !LeftShoulder;
            side = Mathf.MoveTowards(side, LeftShoulder ? -1f : 1f, SideSwapSpeed * Time.deltaTime);
            Vector3 pivot = target.position + Vector3.up * pivotHeight + rot * Vector3.right * (shoulderOffset * side);
            Vector3 back = rot * Vector3.back;

            // Spring arm. The blockout is dense by design, so without this the camera would spend
            // half the match inside a fin. Ignore anything belonging to the player itself.
            float wanted = distance;
            RaycastHit[] hits = Physics.SphereCastAll(pivot, collisionRadius, back, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform t = hits[i].transform;
                if (t == target || t.IsChildOf(target)) continue;
                // barriers are see-through energy: the camera does not duck in front of them
                if (hits[i].collider.GetComponentInParent<Gestures.CastShield>() != null) continue;
                if (hits[i].distance > 0f && hits[i].distance < wanted) wanted = hits[i].distance;
            }

            float speed = wanted < currentDistance ? pullInSpeed : pushOutSpeed;
            currentDistance = Mathf.MoveTowards(currentDistance, Mathf.Max(wanted, minDistance), speed * Time.deltaTime);

            transform.position = pivot + back * currentDistance;
            transform.rotation = rot;
            aimFrom = transform.position;
            float left = shakeUntil - Time.time;
            if (left > 0f)
            {
                float t = Time.time * 43f, k = shakeStrength * ShakeMetres * (left / ShakeSeconds);
                transform.position += (transform.right * Mathf.Sin(t) + transform.up * Mathf.Cos(t * 1.3f)) * k;
            }
        }

        /// <summary>Where the camera is, unshaken -- what the aim is taken from.</summary>
        Vector3 aimFrom;

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
