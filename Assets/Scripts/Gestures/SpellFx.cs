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

            /// <summary>
            /// Turn applied on top of "facing the way the spell goes". Packs author their pieces facing
            /// whichever way their own demo needed -- the fire pentacle faces sideways, and aimed as it
            /// comes it is seen edge-on from behind the caster.
            /// </summary>
            public Vector3 castTurn;

            /// <summary>
            /// Played once where a projectile leaves the hands, for a spell whose flying effect has no
            /// sound with an attack of its own. The circle in the hands is silent: it is only the aim.
            /// </summary>
            public AudioClip launch;

            [Header("Tier effects")]
            /// <summary>On whoever was hit, while the effect lasts -- burning, frozen.</summary>
            public GameObject status;
            public float statusScale = 1f;
            /// <summary>The tier III blast -- fire's explosion, air's bomb.</summary>
            public GameObject special;
            public float specialScale = 1f;
            /// <summary>Seconds of wind-up the blast plays before its bang; dropped, the bang is now.</summary>
            public float specialLeadIn;
            public AudioClip specialSound;
            public AudioClip statusSound;

            [Header("Patch on the floor")]
            public GameObject zone;          // an area effect; see ZoneFx for how it is fitted to a patch
            /// <summary>The radius the area effect was made for; it is scaled from this to the patch's.</summary>
            public float zoneAuthoredRadius = 3f;
            /// <summary>Seconds of warning the area effect plays before its impact; a patch has none.</summary>
            public float zoneLeadIn = 2f;
            public AudioClip zoneStart;
            public AudioClip zoneLoop;
            public AudioClip zoneEnd;

            [Header("Barrier")]
            public AudioClip raise;          // the wall going up
            public AudioClip hum;            // while it stands, quietly
            public AudioClip struck;         // a spell hitting it, on top of the spell's own impact
            public AudioClip shatter;        // worn through
        }

        /// <summary>The spell whose look a patch of this kind wears -- water has none of its own yet.</summary>
        public static Entry ForZone(GroundEffect effect)
        {
            switch (effect)
            {
                case GroundEffect.Burn: return ByName("FIRE");
                case GroundEffect.Ice: return ByName("ICE");
                case GroundEffect.Updraft: return ByName("AIR");
                case GroundEffect.Water: return ByName("WATER");
                case GroundEffect.Shock: return ByName("LIGHTNING");
            }
            return null;
        }

        public List<Entry> entries = new List<Entry>();

        static SpellFx loaded;
        static bool tried;

        public static Entry For(Spell spell)
        {
            return spell == null ? null : ByName(spell.displayName);
        }

        public static Entry ByName(string name)
        {
            if (!tried)
            {
                tried = true;
                loaded = Resources.Load<SpellFx>("SpellFx");
            }
            if (loaded == null) return null;
            foreach (Entry e in loaded.entries)
                if (e != null && e.spell == name) return e;
            return null;
        }

        /// <summary>
        /// Puts an effect in the world, sized, with its sound made 3D -- the packs ship their sounds flat,
        /// which would make a fireball on the far side of the arena as loud as your own. Removed by
        /// itself once its particles are done.
        /// </summary>
        public static GameObject Play(GameObject prefab, Vector3 at, Quaternion rotation, float scale, Transform parent = null,
                                      bool sound = true)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, at, rotation, parent);
            // scale is meant in the world; under a scaled parent it has to be divided back out
            float inherited = parent != null ? Mathf.Max(0.0001f, parent.lossyScale.x) : 1f;
            go.transform.localScale = prefab.transform.localScale * (scale / inherited);
            Tame(go);
            if (!sound)
                foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) Destroy(a);

            if (parent == null) Destroy(go, Lifetime(go));
            return go;
        }

        /// <summary>
        /// A burst from the AoE pack, starting at its bang: the pack winds each one up for a moment
        /// first (a telegraph for a spell that lands later), which here would put the bang after the
        /// thing that caused it. Pieces that only belong to the wind-up are left out.
        /// </summary>
        public static GameObject PlayBurst(GameObject prefab, Vector3 at, float scale, float leadIn)
        {
            if (prefab == null) return null;
            GameObject go = Instantiate(prefab, at, Quaternion.identity);
            go.transform.localScale = prefab.transform.localScale * scale;
            Tame(go);

            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (ParticleSystem ps in systems)
            {
                var main = ps.main;
                float delay = main.startDelay.constantMax;
                if (delay + main.startLifetime.constantMax <= leadIn + 0.05f && delay < leadIn)
                {
                    var emission = ps.emission;
                    emission.enabled = false;
                    continue;
                }
                main.startDelay = Mathf.Max(0f, delay - leadIn);
            }
            foreach (ParticleSystem ps in systems) ps.Play(false);
            Destroy(go, Lifetime(go));
            return go;
        }

        /// <summary>
        /// Makes a freshly placed effect the game's to run: no demo scripts or colliders of its own,
        /// particles that follow its scale, and sounds that sit in the world.
        /// </summary>
        public static void Tame(GameObject go)
        {
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

            foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true))
            {
                MakeSpatial(a);
                a.volume *= Volume;
            }
        }

        /// <summary>How loud spells are, from the settings (see GameSettings.SpellVolume).</summary>
        public static float Volume { get { return GameSettings.SpellVolume; } }

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
            // eased out rather than cut: the impact's own sound takes over in that moment
            foreach (AudioSource a in effect.GetComponentsInChildren<AudioSource>(true)) SoundFade.Out(a, 0f, 0.12f);
            Destroy(effect, 2f);
        }

        /// <summary>
        /// A sound once, in the world. <paramref name="longest"/> cuts a long tail short -- with a fade, so
        /// a clip made for a slower moment does not ring on after what it belongs to is over.
        /// </summary>
        public static AudioSource OneShot(AudioClip clip, Vector3 at, float volume = 1f, float longest = 0f)
        {
            if (clip == null) return null;
            var go = new GameObject("Sound_" + clip.name);
            go.transform.position = at;
            AudioSource a = go.AddComponent<AudioSource>();
            MakeSpatial(a);
            a.clip = clip;
            a.volume = volume * Volume;
            a.Play();
            float length = clip.length;
            if (longest > 0f && longest < length)
            {
                SoundFade.Out(a, longest - 0.4f, 0.4f);
                length = longest;
            }
            Destroy(go, length + 0.1f);
            return a;
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
