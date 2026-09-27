using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MageCast
{
    /// <summary>
    /// Turns the chest and head towards whatever the crosshair is on.
    ///
    /// The body already faces the camera's yaw -- the motor does that every frame -- but yaw is all it
    /// does, so aiming up at a platform or down at someone below leaves the character staring flatly
    /// ahead while the spell flies off at an angle. This adds the pitch, and a little of the lead-in a
    /// person gives with their shoulders before they throw something.
    ///
    /// Runs before the animator so the constraints evaluate against this frame's target. The camera
    /// moves in LateUpdate, so the point being aimed at is one frame old, which at these angles is not
    /// something an eye can catch.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class UpperBodyAim : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Rig rig;
        [SerializeField] ThirdPersonCamera cam;

        /// <summary>
        /// How much of the aim the upper body takes. Turn it down for a stiffer, more grounded stance;
        /// up for a character that visibly tracks you. The per-bone split is in the rig itself, where
        /// the chest takes a third of what the head does.
        /// </summary>
        [SerializeField, Range(0f, 1f)] float weight = 1f;

        /// <summary>
        /// Nothing closer than this counts as something to look at. Without it, brushing a wall puts
        /// the aim point inside the character's own chest and the neck folds up.
        /// </summary>
        [SerializeField] float minDistance = 2.5f;

        [SerializeField] float maxDistance = 200f;

        /// <summary>How fast the aim point catches up. The raw crosshair jumps the moment a raycast
        /// slides off an edge, and the head would snap with it.</summary>
        [SerializeField] float smoothing = 14f;

        Vector3 shown;
        bool started;
        PlayerNet net;

        void Awake()
        {
            if (cam == null) cam = FindObjectOfType<ThirdPersonCamera>();
            net = GetComponent<PlayerNet>();
        }

        void Update()
        {
            if (target == null) return;

            // Somebody else's player looks where THEY are aiming, not where this machine's camera is.
            if (net != null && net.IsRemote)
            {
                Vector3 theirs = net.AimPoint.Value;
                if (theirs == Vector3.zero) theirs = transform.position + Vector3.up * 1.5f + transform.forward * 10f;
                if (!started) { shown = theirs; started = true; }
                else shown = Vector3.Lerp(shown, theirs, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
                target.position = shown;
                if (rig != null) rig.weight = weight;
                return;
            }

            if (cam == null) return;

            Vector3 origin = cam.AimOrigin;
            Vector3 forward = cam.AimDirection;

            // Same cast the caster uses to decide where a spell goes, so the character looks at the
            // thing she is about to hit rather than merely along the camera.
            RaycastHit hit;
            float distance = Physics.Raycast(origin, forward, out hit, maxDistance, ~0,
                                             QueryTriggerInteraction.Ignore)
                             ? hit.distance : maxDistance;

            Vector3 wanted = origin + forward * Mathf.Max(distance, minDistance);

            if (!started) { shown = wanted; started = true; }
            else shown = Vector3.Lerp(shown, wanted, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            target.position = shown;
            if (rig != null) rig.weight = weight;

            // passed on only when it has moved enough to see -- a head does not need millimetres
            if (net != null && net.IsSpawned && net.IsOwner && (net.AimPoint.Value - shown).sqrMagnitude > 0.04f)
                net.AimPoint.Value = shown;
        }
    }
}
