using System.Collections.Generic;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// What the local player's camera can see and what its crosshair is on -- the one place the HUD
    /// asks, so a name tag, a spell readout over somebody's head and a health bar all agree.
    ///
    /// Anything over another player's head is information: who they are, what they hold, how hurt they
    /// are. None of it may come through a wall, or hiding stops meaning anything.
    /// </summary>
    public static class Sight
    {
        static readonly Dictionary<Transform, bool> seen = new Dictionary<Transform, bool>();
        static int seenFrame = -1;

        static Combat.Health aimed;
        static int aimedFrame = -1;

        /// <summary>
        /// Whether nothing solid stands between the camera and <paramref name="who"/>'s head or chest.
        /// Their own body does not count, nor yours, nor a barrier (it is see-through energy). Answered
        /// once per object per frame.
        /// </summary>
        public static bool Sees(Transform who)
        {
            if (who == null) return false;
            Camera cam = Camera.main;
            if (cam == null) return true;
            if (seenFrame != Time.frameCount) { seen.Clear(); seenFrame = Time.frameCount; }
            bool result;
            if (seen.TryGetValue(who, out result)) return result;

            result = false;
            Vector3 eye = cam.transform.position;
            foreach (float height in new[] { 1.6f, 1.0f })
            {
                Vector3 target = who.position + Vector3.up * height;
                Vector3 toward = target - eye;
                bool blocked = false;
                foreach (RaycastHit h in Physics.RaycastAll(eye, toward.normalized, toward.magnitude, ~0,
                                                            QueryTriggerInteraction.Ignore))
                {
                    Transform t = h.collider.transform;
                    if (t.IsChildOf(who)) continue;
                    if (PlayerNet.Local != null && t.IsChildOf(PlayerNet.Local.transform)) continue;
                    if (h.collider.GetComponentInParent<Gestures.CastShield>() != null) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) { result = true; break; }
            }
            seen[who] = result;
            return result;
        }

        /// <summary>
        /// Whatever with health the crosshair is on this frame, or null: the first thing along the
        /// camera's centre line, with a little slack (a 0.3 m wide ray) so a moving target stays aimed at.
        /// Barriers are looked through, like everything else here.
        /// </summary>
        public static Combat.Health Aimed()
        {
            if (aimedFrame == Time.frameCount) return aimed;
            aimedFrame = Time.frameCount;
            aimed = null;

            Camera cam = Camera.main;
            if (cam == null) return null;
            RaycastHit[] hits = Physics.SphereCastAll(cam.transform.position, 0.3f, cam.transform.forward, 80f, ~0,
                                                      QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (PlayerNet.Local != null && h.collider.transform.IsChildOf(PlayerNet.Local.transform)) continue;
                if (h.collider.GetComponentInParent<Gestures.CastShield>() != null) continue;
                // the cast starting inside something: the spring arm has the camera brushing a wall
                if (h.distance <= 0f) continue;
                aimed = h.collider.GetComponentInParent<Combat.Health>();
                break;      // the first solid thing: a wall in front hides whoever is behind it
            }
            return aimed;
        }

        /// <summary>Whether <paramref name="who"/> is somebody else's player (not you, not a dummy).</summary>
        public static bool IsOtherPlayer(Transform who)
        {
            if (who == null) return false;
            PlayerNet p = who.GetComponentInParent<PlayerNet>();
            return p != null && p != PlayerNet.Local;
        }
    }
}
