using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Real effects for a spell, where it has them: what appears in the hands when it is cast, what
    /// flies, what bursts where it lands -- each with its sound, which the effect prefabs carry. A spell
    /// with no entry keeps the placeholder glowing sphere, so art can arrive one spell at a time.
    ///
    /// Kept apart from the spell's numbers (GestureCaster.Spell) on purpose: those are gameplay and live
    /// on the player prefab; these are looks, and bought effect packs are not in the public repository.
    /// Resources/SpellFx.asset holds the entries; Tools > Arena > Assign Spell Effects fills it.
    /// </summary>
    [CreateAssetMenu(menuName = "Mage Cast/Spell Effects")]
    public class SpellFx : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            /// <summary>Matches Spell.displayName.</summary>
            public string spell;

            public GameObject cast;         // in the hands, at the moment of sending
            public GameObject projectile;   // replaces the sphere while it flies
            public GameObject impact;       // where it stops

            /// <summary>
            /// Size of the flying effect for a projectile of radius 1 -- it is scaled with the real one,
            /// so a big, well-drawn fireball also LOOKS bigger.
            /// </summary>
            public float projectileScale = 1f;
            public float castScale = 1f;
            public float impactScale = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        static SpellFx loaded;
        static bool tried;

        public static Entry For(Spell spell)
        {
            if (spell == null) return null;
            if (!tried)
            {
                tried = true;
                loaded = Resources.Load<SpellFx>("SpellFx");
            }
            if (loaded == null) return null;
            foreach (Entry e in loaded.entries)
                if (e != null && e.spell == spell.displayName) return e;
            return null;
        }

        /// <summary>
        /// Puts an effect in the world, sized, with its sound made 3D -- the packs ship their sounds flat,
        /// which would make a fireball on the far side of the arena as loud as your own. Removed by
        /// itself once its particles are done.
        /// </summary>
        public static GameObject Play(GameObject prefab, Vector3 at, Quaternion rotation, float scale, Transform parent = null)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, at, rotation, parent);
            // scale is meant in the world; under a scaled parent it has to be divided back out
            float inherited = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
            go.transform.localScale = prefab.transform.localScale * (scale / inherited);

            // The packs' demo scripts move, collide and destroy their own copies; here the game does all
            // of that, so they go -- at once, not at the end of the frame: the fire projectile's script
            // fired its trigger in the frame it was created and threw, which pauses the editor. Their
            // colliders go too; a spell is hit-tested by the game's own sweep, never by its looks.
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Namespace == null) DestroyImmediate(mb);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);

            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                // particles follow the object's scale, not just its position
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) MakeSpatial(a);

            if (parent == null) Destroy(go, Lifetime(go));
            return go;
        }

        public static void MakeSpatial(AudioSource a)
        {
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 4f;
            a.maxDistance = 45f;
            a.dopplerLevel = 0f;
        }

        /// <summary>
        /// Lets a flying effect go when its projectile is gone: it stops emitting and fades out where it
        /// is, trail and all, instead of vanishing mid-air along with the object carrying it.
        /// </summary>
        public static void Release(GameObject effect)
        {
            if (effect == null) return;
            effect.transform.SetParent(null, true);
            foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (AudioSource a in effect.GetComponentsInChildren<AudioSource>(true)) a.Stop();
            Destroy(effect, 2f);
        }

        static float Lifetime(GameObject go)
        {
            float longest = 1f;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop) continue;
                longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
            }
            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true))
                if (a.clip != null && !a.loop) longest = Mathf.Max(longest, a.clip.length);
            return Mathf.Min(longest + 0.5f, 8f);
        }
    }
}
