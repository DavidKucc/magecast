using MageCast.Combat;
using UnityEngine;

namespace MageCast.World
{
    /// <summary>
    /// The spring on top of the Sanctum's core: whoever stands in it gets life back, a little at a time.
    /// It is the reason to go up there -- and up there you are the easiest target in the arena, with no
    /// cover and everyone below able to see you. Healing stops for a moment after you are hit, so it
    /// tops you up between fights rather than out-healing a fight.
    ///
    /// The server's copy heals (life is the server's); every machine shows the spring.
    /// </summary>
    public class HealingSpring : MonoBehaviour
    {
        public float radius = 2.2f;
        public float perSecond = 5f;
        /// <summary>No healing this long after being hurt.</summary>
        public float afterHit = 2f;

        const float Tick = 0.25f;
        float next;

        public static readonly System.Collections.Generic.List<HealingSpring> All = new System.Collections.Generic.List<HealingSpring>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + Tick;
            Vector3 centre = transform.position;
            foreach (Hurtbox body in Hurtbox.All)
            {
                if (body == null) continue;
                Health h = body.Health;
                if (h == null || h.IsDead || !h.HasAuthority || h.Current >= h.Max) continue;
                if (Time.time - h.LastDamagedAt < afterHit) continue;
                Vector3 off = body.transform.position - centre;
                if (off.y < -0.5f || off.y > 1.2f) continue;
                off.y = 0f;
                if (off.magnitude > radius) continue;
                h.Heal(perSecond * Tick);
            }
        }
    }
}
