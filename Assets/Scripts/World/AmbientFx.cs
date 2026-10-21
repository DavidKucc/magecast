using MageCast.Gestures;
using UnityEngine;

namespace MageCast.World
{
    /// <summary>
    /// One of the spell packs' effects standing in the map for good -- the flames in a brazier, the
    /// glow of the healing spring, the air rising off a launch pad. Placed by the map builder with the
    /// pack's prefab; started here, so the map in the scene stays plain objects (and a copy of the
    /// project without the packs just shows nothing there).
    ///
    /// The packs make their effects to play once: a wind-up, a burst, five seconds of loop, a fade.
    /// Kept going, the wind-up and the fade are dropped and the rest loops; the anime pack's fire is a
    /// single puff, which SpellFx.KeepBurning turns into a steady flame.
    /// </summary>
    public class AmbientFx : MonoBehaviour
    {
        public GameObject prefab;
        public float scale = 1f;
        /// <summary>The anime pack's fire: kept burning at this many flames a second.</summary>
        public float flames;
        /// <summary>Its own sound, looped quietly in the world -- or none.</summary>
        public float soundVolume;

        GameObject effect;

        void Start()
        {
            if (prefab == null) return;
            effect = Instantiate(prefab, transform.position, transform.rotation, transform);
            float inherited = Mathf.Max(0.0001f, transform.lossyScale.x);
            effect.transform.localScale = prefab.transform.localScale * (scale / inherited);
            SpellFx.Tame(effect);

            foreach (AudioSource a in effect.GetComponentsInChildren<AudioSource>(true))
            {
                if (soundVolume <= 0f) { Destroy(a); continue; }
                a.loop = true;
                a.volume = soundVolume * SpellFx.Volume;
                a.maxDistance = 18f;
                if (!a.isPlaying) a.Play();
            }

            if (flames > 0f) SpellFx.KeepBurning(effect, flames);
            else Loop(effect);
        }

        /// <summary>Keeps an effect going: its opening and closing parts dropped, everything else repeating.</summary>
        public static void Loop(GameObject go)
        {
            ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (ParticleSystem ps in systems)
            {
                string n = ps.name.ToUpperInvariant();
                var main = ps.main;
                var emission = ps.emission;
                if (n.Contains("OUT") || n.Contains("WINDUP") || n.Contains("WIND UP"))
                {
                    emission.enabled = false;
                    continue;
                }
                if (n.EndsWith(" IN") || n.EndsWith("_IN"))
                {
                    // the opening flourish: once, at the start
                    main.loop = false;
                    main.startDelay = 0f;
                    continue;
                }

                main.startDelay = Mathf.Min(main.startDelay.constantMax, 0.3f);
                bool single = emission.burstCount > 0 && emission.rateOverTime.constantMax <= 0.01f
                              && main.startLifetime.constantMax >= main.duration - 0.1f;
                if (single)
                {
                    // one long-lived piece (a decal, a ring on the floor): made once and kept
                    main.loop = false;
                    main.startLifetime = 100000f;
                    var colour = ps.colorOverLifetime;
                    colour.enabled = false;
                    var size = ps.sizeOverLifetime;
                    size.enabled = false;
                }
                else main.loop = true;
            }
            foreach (ParticleSystem ps in systems) ps.Play(false);
        }
    }
}
