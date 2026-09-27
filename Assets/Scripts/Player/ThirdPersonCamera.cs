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

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public Vector3 AimOrigin { get { return transform.position; } }
        public Vector3 AimDirection { get { return transform.forward; } }

        float currentDistance;

        void Start()
        {
            currentDistance = distance;
            if (target != null) Yaw = target.eulerAngles.y;
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
            if ((Cursor.lockState == CursorLockMode.Locked) != wantLocked) LockCursor(wantLocked);

            if (target == null) return;

            if (LookEnabled && !GameInput.MenuOpen && Cursor.lockState == CursorLockMode.Locked)
            {
                Yaw += Input.GetAxisRaw("Mouse X") * aimSensitivity;
                Pitch = Mathf.Clamp(Pitch - Input.GetAxisRaw("Mouse Y") * aimSensitivity, minPitch, maxPitch);
            }

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight + rot * Vector3.right * shoulderOffset;
            Vector3 back = rot * Vector3.back;

            // Spring arm. The blockout is dense by design, so without this the camera would spend
            // half the match inside a fin. Ignore anything belonging to the player itself.
            float wanted = distance;
            RaycastHit[] hits = Physics.SphereCastAll(pivot, collisionRadius, back, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform t = hits[i].transform;
                if (t == target || t.IsChildOf(target)) continue;
                if (hits[i].distance > 0f && hits[i].distance < wanted) wanted = hits[i].distance;
            }

            float speed = wanted < currentDistance ? pullInSpeed : pushOutSpeed;
            currentDistance = Mathf.MoveTowards(currentDistance, Mathf.Max(wanted, minDistance), speed * Time.deltaTime);

            transform.position = pivot + back * currentDistance;
            transform.rotation = rot;
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
