using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Somebody on fire: damage over a few seconds, and flames on them for as long as it lasts.
    ///
    /// Put on the one hit by a tier II+ fire -- on every machine, since every machine's copy of the shot
    /// hits them, so everyone sees the flames at once. Only the copy that decides (the server's, or
    /// offline) deals the damage. A second burn does not stack: it restarts the clock, at the stronger
    /// of the two.
    /// </summary>
    public class Burning : MonoBehaviour
    {
        const float Tick = 0.25f;

        Health health;
        float until;
        float perSecond;
        float nextTick;
        ulong attacker;
        bool authoritative;
        GameObject flames;

        public static void Apply(Health target, float perSecond, float seconds, ulong attacker, bool authoritative,
                                 SpellFx.Entry look)
        {
            if (target == null || target.IsDead) return;
            Burning b = target.GetComponent<Burning>();
            bool fresh = b == null;
            if (fresh) b = target.gameObject.AddComponent<Burning>();

            b.health = target;
            b.perSecond = fresh ? perSecond : Mathf.Max(b.perSecond, perSecond);
            b.until = Time.time + seconds;
            b.attacker = attacker;
            b.authoritative |= authoritative;
            if (fresh) b.nextTick = Time.time + Tick;

            if (b.flames == null && look != null && look.status != null)
            {
                b.flames = SpellFx.Play(look.status, target.transform.position, Quaternion.identity,
                                        look.statusScale, target.transform);
                SpellFx.OneShot(look.statusSound, target.transform.position, 0.6f, seconds);
            }
        }

        void Update()
        {
            if (health == null || health.IsDead || Time.time >= until)
            {
                SpellFx.Release(flames);
                Destroy(this);
                return;
            }
            if (!authoritative || Time.time < nextTick) return;
            nextTick += Tick;
            health.TakeDamage(perSecond * Tick, attacker);
        }

        void OnDestroy()
        {
            if (flames != null) SpellFx.Release(flames);
        }
    }
}
