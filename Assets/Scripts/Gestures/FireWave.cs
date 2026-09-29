using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A fire patch blown away by air (tier II): the patch is gone and a wall of flame rolls on the way
    /// the wind blew. Everyone it passes through catches fire and is shoved along with it, once each.
    ///
    /// It turns a patch that just lay there into something aimed. Stops at a wall -- fire does not go
    /// through cover. Runs on every machine for the picture; only the deciding copy (authoritative)
    /// shoves anybody, and the burn deals damage only there (see Burning).
    /// </summary>
    public class FireWave : MonoBehaviour
    {
        const float Speed = 12f;          // m/s
        const float Length = 8f;          // m
        const float Reach = 1.6f;         // how far to either side and up it catches people
        const float PuffEvery = 1.4f;     // m between the bursts of flame it is drawn with
        const float Push = 8f;            // m/s, along the wind with a little lift

        Vector3 heading;
        float travelled;
        float nextPuff;
        float burnPerSecond;
        ulong attacker;
        bool authoritative;
        SpellFx.Entry look;
        readonly HashSet<Health> burnt = new HashSet<Health>();
        readonly HashSet<PlayerMotor> pushed = new HashSet<PlayerMotor>();

        public static FireWave Spawn(Vector3 from, Vector3 wind, float burnPerSecond, ulong attacker, bool authoritative)
        {
            var go = new GameObject("FireWave");
            go.transform.position = from;
            FireWave w = go.AddComponent<FireWave>();
            w.heading = new Vector3(wind.x, 0f, wind.z).normalized;
            if (w.heading.sqrMagnitude < 0.01f) w.heading = Vector3.forward;
            w.burnPerSecond = burnPerSecond;
            w.attacker = attacker;
            w.authoritative = authoritative;
            w.look = SpellFx.ByName("FIRE");
            if (w.look != null) SpellFx.OneShot(w.look.specialSound, from, 0.9f);
            return w;
        }

        void Update()
        {
            float step = Speed * Time.deltaTime;

            // a wall in the way ends it there
            RaycastHit wall;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.8f, 0.4f, heading, out wall, step, ~0,
                                   QueryTriggerInteraction.Ignore)
                && wall.normal.y < 0.5f && wall.collider.GetComponentInParent<Health>() == null
                && wall.collider.GetComponentInParent<PlayerMotor>() == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += heading * step;
            travelled += step;

            if (travelled >= nextPuff)
            {
                nextPuff += PuffEvery;
                if (look != null && look.special != null)
                    SpellFx.PlayBurst(look.special, transform.position, 0.4f, look.specialLeadIn);
            }

            foreach (Collider c in Physics.OverlapSphere(transform.position + Vector3.up * 0.9f, Reach, ~0,
                                                          QueryTriggerInteraction.Ignore))
            {
                Health h = c.GetComponentInParent<Health>();
                if (h != null && burnt.Add(h)) Burning.Apply(h, burnPerSecond, SpellTiers.BurnSeconds, attacker, authoritative, look);

                PlayerMotor m = c.GetComponentInParent<PlayerMotor>();
                if (m != null && authoritative && pushed.Add(m)) m.AddImpulse((heading + Vector3.up * 0.4f).normalized * Push);
            }

            if (travelled >= Length) Destroy(gameObject);
        }
    }
}
