using MageCast.Combat;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Soaked, for a few seconds: hit by water -- an ice bolt that went through a line of fire.
    ///
    ///   lightning into somebody wet   strikes SpellTiers.WetShockMultiplier times as hard, and dries them
    ///   fire on somebody wet          only steams: no burning, and it dries them
    ///
    /// A trade, not just a weakness: being wet is the one way not to burn. Anybody can be soaked, the
    /// caster too.
    ///
    /// Like Burning, put on the body by every machine's copy of the hit, so everybody sees it; the
    /// server's is the one its damage is decided by. Seen as a cold blue over the body and water
    /// dripping off it -- it has to be readable from across a fight, or the lightning that punishes it
    /// is a surprise rather than a decision.
    /// </summary>
    public class Wet : MonoBehaviour
    {
        static readonly Color Water = new Color(0.35f, 0.65f, 1f);
        static readonly Color Tint = new Color(0.62f, 0.8f, 1.15f);

        static Material dropMaterial;

        float until;
        GameObject drips;
        Renderer[] tinted;
        MaterialPropertyBlock block;

        public float Left { get { return Mathf.Max(0f, until - Time.time); } }

        /// <summary>Soaks <paramref name="target"/> for <paramref name="seconds"/> (from now, if longer).</summary>
        public static void Apply(Health target, float seconds)
        {
            if (target == null || target.IsDead) return;
            Wet w = target.GetComponent<Wet>();
            bool fresh = w == null;
            if (fresh) w = target.gameObject.AddComponent<Wet>();
            w.until = Mathf.Max(w.until, Time.time + seconds);
            if (fresh) w.Show();
            WorldPopups.Word(target.transform, "WET", Water);
        }

        /// <summary>Whether <paramref name="target"/> is wet right now.</summary>
        public static bool Is(Component target)
        {
            if (target == null) return false;
            Wet w = target.GetComponentInParent<Wet>();
            return w != null && w.Left > 0f;
        }

        /// <summary>Dried off -- by the lightning that used it, or the fire it put out.</summary>
        public static void Remove(Component target)
        {
            if (target == null) return;
            Wet w = target.GetComponentInParent<Wet>();
            if (w == null) return;
            w.until = 0f;       // dry at once: Destroy waits for the end of the frame, a second hit in it must not see it wet
            Destroy(w);
        }

        void Show()
        {
            // a cold blue over everything the body is drawn with
            tinted = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            foreach (Renderer r in tinted)
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_Color")) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_Color", r.sharedMaterial.color * Tint);
                r.SetPropertyBlock(block);
            }

            // water running off it
            drips = new GameObject("Drips");
            drips.transform.SetParent(transform, false);
            drips.transform.localPosition = Vector3.up * 1.2f;
            var ps = drips.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            main.startColor = new Color(0.6f, 0.82f, 1f, 0.9f);
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 34f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.38f;
            var r2 = drips.GetComponent<ParticleSystemRenderer>();
            if (dropMaterial == null) dropMaterial = RuntimeMaterials.Line();     // unlit, vertex colour: a bright drop
            r2.sharedMaterial = dropMaterial;
            r2.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        void Update()
        {
            if (Time.time >= until) Destroy(this);
        }

        void OnDestroy()
        {
            if (tinted != null)
                foreach (Renderer r in tinted) if (r != null) r.SetPropertyBlock(null);
            if (drips != null)
            {
                var ps = drips.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                drips.transform.SetParent(null, true);
                Destroy(drips, 0.7f);
            }
        }
    }
}
