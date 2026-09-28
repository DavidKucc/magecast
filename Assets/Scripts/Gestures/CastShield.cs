using System.Collections.Generic;
using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// The barrier: a slab of cover planted in the aimed direction. Unlike the other spells it is not
    /// aimed AT anything -- it is placed, so where it goes is a positioning decision rather than a shot.
    ///
    /// It is a real collider, so it blocks projectiles from both sides, including the caster's own.
    /// That is deliberate: a barrier you can shoot through but your opponent cannot is a much bigger
    /// design commitment (teams, ownership, filtering) and it is worth finding out first whether the
    /// honest version is more interesting. Planting a wall you then have to move around is a real
    /// decision; a one-way window is just free value.
    ///
    /// It wears down. Every spell that hits it takes durability by how hard it hit and what it is --
    /// fire chews through it, lightning barely scratches it, air hardly at all -- so a barrier is a
    /// moment of safety, not a place to live. Shots stop against it; they do not bounce back at the
    /// shooter, which would make a barrier a reward for being shot at rather than a cost.
    ///
    /// One per player: putting up a new one takes the old one down.
    /// </summary>
    public class CastShield : MonoBehaviour
    {
        /// <summary>Durability of a cleanly drawn barrier; the draw scales it 0.6-1.4x.</summary>
        public const float BaseDurability = 60f;

        static readonly Dictionary<ulong, CastShield> byOwner = new Dictionary<ulong, CastShield>();

        float remaining;
        float total;
        float durability;
        float maxDurability;
        Material material;
        Color colour;
        ulong owner = Health.NoAttacker;
        PlayerNet ownerNet;           // the caster's network side, for telling everyone about wear
        bool authoritative = true;

        public float DurabilityFraction { get { return maxDurability > 0f ? durability / maxDurability : 0f; } }

        public static CastShield Spawn(Vector3 at, Vector3 facing, float width, float height,
                                       float lifetime, Color colour, float durability = BaseDurability,
                                       ulong owner = Health.NoAttacker, PlayerNet ownerNet = null,
                                       bool authoritative = true)
        {
            // one per player, on every machine alike
            CastShield previous;
            if (byOwner.TryGetValue(owner, out previous) && previous != null) Destroy(previous.gameObject);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CastShield";
            go.transform.position = at + Vector3.up * (height * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z).normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, height, 0.15f);

            Material m = RuntimeMaterials.Fade(colour, 0.42f, 0.8f);
            go.GetComponent<Renderer>().material = m;

            CastShield s = go.AddComponent<CastShield>();
            s.remaining = lifetime;
            s.total = lifetime;
            s.durability = durability;
            s.maxDurability = durability;
            s.material = m;
            s.colour = colour;
            s.owner = owner;
            s.ownerNet = ownerNet;
            s.authoritative = authoritative;
            byOwner[owner] = s;
            return s;
        }

        /// <summary>The current barrier of a player on this machine, or null.</summary>
        public static CastShield Of(ulong owner)
        {
            CastShield s;
            return byOwner.TryGetValue(owner, out s) ? s : null;
        }

        /// <summary>
        /// A hit takes this much durability. Only the copy that decides -- the server's, or offline -- does
        /// the arithmetic; everybody else is told the result, so a barrier breaks at the same moment for
        /// everyone rather than whenever each machine's copy of the shots happened to wear it through.
        /// </summary>
        public void Wear(float amount)
        {
            if (!authoritative || amount <= 0f) return;
            durability = Mathf.Max(0f, durability - amount);

            if (ownerNet != null && ownerNet.IsSpawned) ownerNet.BroadcastBarrierWear(DurabilityFraction);
            if (durability <= 0f) Break();
        }

        /// <summary>Everybody else's copy, set to what the server said is left.</summary>
        public void SetWear(float fraction)
        {
            durability = maxDurability * Mathf.Clamp01(fraction);
            if (durability <= 0f) Break();
        }

        void Break()
        {
            Projectile.FlashAt(transform.position, colour, 2.4f, 2.2f);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            CastShield current;
            if (byOwner.TryGetValue(owner, out current) && current == this) byOwner.Remove(owner);
        }

        /// <summary>
        /// The Standard shader needs all of this set by hand to blend -- setting only the alpha on the
        /// colour leaves it fully opaque, and an opaque shield you cannot see past is worse than none.
        /// </summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Destroy(gameObject); return; }

            // Visibly weakening -- by time AND by damage, whichever is further gone -- so the opponent
            // can see one more fireball will do it, and the caster knows when to move.
            float t = Mathf.Min(remaining / Mathf.Max(0.01f, total), DurabilityFraction);
            Color c = material.color;
            c.a = Mathf.Lerp(0.1f, 0.42f, t);
            material.color = c;
            material.SetColor("_EmissionColor", colour * Mathf.Lerp(0.15f, 0.8f, t));
        }
    }
}
