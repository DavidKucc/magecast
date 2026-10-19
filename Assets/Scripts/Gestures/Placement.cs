using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// How much floor a wall of ice or a line of fire laid across the way can stand on.
    ///
    /// Both run across the caster's facing, as long as their tier makes them. On a narrow wall top or a
    /// bridge that used to mean ice hanging in the air over both edges, or fire burning on nothing
    /// (19 Oct). Now the length is felt out along the floor from the spot, both ways, before it is laid:
    /// it stops where the floor ends, where it steps up or down (a ledge, the lip of a wall top), and
    /// where something solid stands in the way -- so on a wall 0.6 m wide the wall is 0.6 m wide, and
    /// it is moved over to sit on what there is.
    ///
    /// The map is the same everywhere, so every machine fits the same wall from the same spot and
    /// facing; the aim marker fits it the same way, so the outline on the floor is the wall to be.
    /// </summary>
    public static class Placement
    {
        const float Step = 0.1f;

        /// <summary>A step of the floor more than this per Step ends it -- a slope of about 50 deg still carries on.</summary>
        const float MaxRise = 0.12f;

        /// <summary>Never shorter than this: on a beam narrower still it hangs over a little rather than vanish.</summary>
        public const float Shortest = 0.5f;

        /// <summary>
        /// Fits a wall or line of <paramref name="length"/> across <paramref name="facing"/>, centred on
        /// <paramref name="spot"/>, to the floor: where its middle ends up and how long it can be.
        /// </summary>
        public static void Fit(Vector3 spot, Vector3 facing, float length, out Vector3 centre, out float fitted)
        {
            Vector3 flat = new Vector3(facing.x, 0f, facing.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, flat.normalized).normalized;

            float half = length * 0.5f;
            float left = Reach(spot, -across, half), right = Reach(spot, across, half);
            if (left + right < Shortest)
            {
                float add = (Shortest - left - right) * 0.5f;
                left += add;
                right += add;
            }
            fitted = left + right;
            centre = spot + across * ((right - left) * 0.5f);
            RaycastHit h;
            if (FireLine.Ground(centre + Vector3.up * 0.6f, 1.2f, out h)) centre.y = h.point.y;
        }

        /// <summary>How far from <paramref name="spot"/> along <paramref name="dir"/> the floor carries on, up to <paramref name="max"/>.</summary>
        static float Reach(Vector3 spot, Vector3 dir, float max)
        {
            float lastY = spot.y, good = 0f;
            Vector3 previous = spot;
            for (float s = Step; ; s += Step)
            {
                float at = Mathf.Min(s, max);
                Vector3 p = spot + dir * at;
                RaycastHit h;
                if (!FireLine.Ground(p + Vector3.up * 0.6f, 1.2f, out h) || h.normal.y <= Projectile.FloorNormal) break;
                if (Mathf.Abs(h.point.y - lastY) > MaxRise * (at - good) / Step + 0.02f) break;    // a ledge, a lip
                if (Blocked(previous + Vector3.up * 0.4f, new Vector3(p.x, h.point.y, p.z) + Vector3.up * 0.4f)) break;    // a wall
                lastY = h.point.y;
                good = at;
                previous = new Vector3(p.x, h.point.y, p.z);
                if (at >= max) break;
            }
            return good;
        }

        /// <summary>Something solid between two points at knee height -- not people, barriers or ice walls.</summary>
        static bool Blocked(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 1e-4f) return false;
            foreach (RaycastHit h in Physics.RaycastAll(a, d / length, length, ~0, QueryTriggerInteraction.Ignore))
            {
                Collider c = h.collider;
                if (c.GetComponentInParent<Health>() != null || c.GetComponentInParent<PlayerMotor>() != null
                    || c.GetComponentInParent<CastShield>() != null || c.GetComponentInParent<IceWall>() != null) continue;
                return true;
            }
            return false;
        }
    }
}
