using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Frozen to an ice patch: somebody wet stepped onto ice, or had it laid under them. They cannot move
    /// or jump for SpellTiers.WetFreezeSeconds -- they can still draw -- and the water that froze is
    /// gone, so they are dry again after.
    ///
    /// The second half of a combo: ice through a line of fire soaks (Wet), ice on the floor then holds
    /// them still for whatever comes next. Like Wet, put on the body by every machine, since every
    /// machine sees the same wet body touch the same ice; the body is only held still where it is moved
    /// from (its owner's machine, or the server for a bot). Seen as a pale, icy body and the ice burst
    /// at its feet.
    /// </summary>
    public class Frozen : MonoBehaviour
    {
        static readonly Color Ice = new Color(0.7f, 0.92f, 1f);
        static readonly Color Tint = new Color(0.8f, 0.97f, 1.35f);

        float until;
        Renderer[] tinted;
        MaterialPropertyBlock block;

        /// <summary>
        /// Freezes <paramref name="target"/> (or, for something without health, <paramref name="root"/>)
        /// for <paramref name="seconds"/>; <paramref name="motor"/> is held still if it is moved from here.
        /// </summary>
        public static void Apply(Health target, PlayerMotor motor, Transform root, float seconds)
        {
            Component body = target != null ? (Component)target : root;
            if (body == null || (target != null && target.IsDead)) return;

            Wet.Remove(body);                                       // the water is the ice now
            if (motor != null && motor.IsLocallyControlled) motor.ApplySlow(0f, seconds);

            Frozen f = body.GetComponent<Frozen>();
            if (f == null) f = body.gameObject.AddComponent<Frozen>();
            f.until = Mathf.Max(f.until, Time.time + seconds);

            WorldPopups.Word(body.transform, "FROZEN", Ice);
            SpellFx.Entry ice = SpellFx.ByName("ICE");
            if (ice != null && ice.status != null)
                SpellFx.Play(ice.status, body.transform.position, Quaternion.identity, ice.statusScale);
            if (ice != null) SpellFx.OneShot(ice.statusSound, body.transform.position, 0.8f);
        }

        void LateUpdate()
        {
            if (Time.time >= until) { Destroy(this); return; }

            // Laid again every frame: the wet tint it takes over from lets go of the body at the end of
            // the frame it froze in, and would take this one with it.
            if (tinted == null)
            {
                tinted = GetComponentsInChildren<Renderer>();
                block = new MaterialPropertyBlock();
            }
            foreach (Renderer r in tinted)
            {
                if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r.sharedMaterial == null
                    || !r.sharedMaterial.HasProperty("_Color")) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_Color", r.sharedMaterial.color * Tint);
                r.SetPropertyBlock(block);
            }
        }

        void OnDestroy()
        {
            if (tinted == null) return;
            foreach (Renderer r in tinted) if (r != null) r.SetPropertyBlock(null);
        }
    }
}
