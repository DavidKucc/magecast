using UnityEngine;

namespace MageCast.Combat
{
    /// <summary>
    /// A target that stands still, takes hits, and puts itself back together.
    ///
    /// The reset is the point. Testing a spell means casting it thirty times in a row and watching what
    /// changes; a dummy that has to be manually respawned turns that into thirty trips to the
    /// hierarchy. It flashes on every hit so a glancing shot is distinguishable from a miss, which
    /// matters more than it sounds when the projectile is a sphere and the target is a capsule.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] float resetAfterSeconds = 3f;
        [SerializeField] float flashSeconds = 0.12f;
        [SerializeField] Color flashColour = Color.white;

        Health health;
        Renderer body;
        Color restColour;
        float flashUntil;
        float deadSince = -1f;

        void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponentInChildren<Renderer>();
            if (body != null) restColour = body.material.color;

            health.Changed += OnChanged;
            health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (health == null) return;
            health.Changed -= OnChanged;
            health.Died -= OnDied;
        }

        void OnChanged(float current, float max)
        {
            flashUntil = Time.time + flashSeconds;
        }

        void OnDied()
        {
            deadSince = Time.time;
        }

        void Update()
        {
            if (body != null)
            {
                bool flashing = Time.time < flashUntil;
                Color wanted = flashing ? flashColour
                             : health.IsDead ? restColour * 0.35f
                             : restColour;
                if (body.material.color != wanted) body.material.color = wanted;
            }

            // only the machine that owns the number puts it back; everyone else is told
            if (health.HasAuthority && deadSince >= 0f && Time.time - deadSince >= resetAfterSeconds)
            {
                deadSince = -1f;
                health.ResetToFull();
            }
        }
    }
}
