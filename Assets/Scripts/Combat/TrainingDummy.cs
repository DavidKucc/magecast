using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Combat
{
    /// <summary>
    /// A target that stands still, takes hits, and puts itself back together.
    ///
    /// The reset is the point. Testing a spell means casting it thirty times in a row and watching what
    /// changes; a dummy that has to be manually respawned turns that into thirty trips to the
    /// hierarchy. It flashes on every hit so a glancing shot is distinguishable from a miss.
    ///
    /// Since 16 Oct it wears the players' own body -- the same character, standing idle and facing you
    /// -- and is hit where that body is (Hurtbox), so what lands on it in training is what would land on
    /// a person. Killed, it falls the way a player does and gets up again. It is not a player: bots
    /// never take it for one (they look only at PlayerNet), and outside training it is put away
    /// (MatchDirector). Without the character (a clone of the repository has no Mixamo files) it stays
    /// the red capsule it was built as.
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

        // wearing a player's body
        Transform figure;
        Animator animator;
        Renderer[] skin;
        MaterialPropertyBlock block;
        bool tinted, faced;
        static readonly int DeadParam = Animator.StringToHash("Dead");

        void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponentInChildren<Renderer>();
            if (body != null) restColour = body.material.color;

            health.Changed += OnChanged;
            health.Died += OnDied;
        }

        void Start()
        {
            WearBody();
        }

        void OnDestroy()
        {
            if (health == null) return;
            health.Changed -= OnChanged;
            health.Died -= OnDied;
        }

        /// <summary>
        /// Puts on a copy of the players' body (the Player prefab's "Body": the character and its animator)
        /// in place of the capsule's look. The aim rig is left off -- it follows a target only a player
        /// has -- and so is every layer over the legs, so it simply stands.
        /// </summary>
        void WearBody()
        {
            GameObject prefab = NetSession.Instance != null ? NetSession.Instance.PlayerPrefab : null;
            Transform source = prefab != null ? prefab.transform.Find("Body") : null;
            if (source == null) return;

            // made inside an inactive holder, so nothing on it wakes up before the rig is taken off
            var holder = new GameObject("DummyBody");
            holder.SetActive(false);
            GameObject copy = Instantiate(source.gameObject, holder.transform);
            var rigging = new List<MonoBehaviour>();
            foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Namespace == "UnityEngine.Animations.Rigging") rigging.Add(mb);
            // constraints first, then the rig, then its builder
            rigging.Sort((a, b) => Order(a).CompareTo(Order(b)));
            foreach (MonoBehaviour mb in rigging) DestroyImmediate(mb);

            figure = copy.transform;
            figure.SetParent(transform, false);
            // the capsule stands with its middle a metre up; the body's origin is at its feet
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            float half = capsule != null ? capsule.height * 0.5f : 1f;
            figure.localPosition = Vector3.down * half;
            figure.localRotation = Quaternion.identity;
            Destroy(holder);

            if (body != null) body.enabled = false;
            body = null;
            skin = figure.GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            animator = figure.GetComponent<Animator>();
            if (animator != null)
                for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);

            // hit where the body is, as a person is
            if (GetComponent<Hurtbox>() == null) gameObject.AddComponent<Hurtbox>();
        }

        static int Order(MonoBehaviour mb)
        {
            string n = mb.GetType().Name;
            return n.EndsWith("Constraint") ? 0 : n == "Rig" ? 1 : 2;
        }

        void OnChanged(float current, float max)
        {
            flashUntil = Time.time + flashSeconds;
        }

        void OnDied()
        {
            deadSince = Time.time;
            // in the kill feed like anyone else: training is where you first look for it
            if (!health.HasAuthority) return;
            PlayerNet killer = PlayerNet.Find(health.LastAttacker);
            KillFeed.Report(killer != null ? killer.DisplayName : "", killer != null ? killer.Team.Value : Teams.None,
                            "Dummy", Teams.None, health.LastCause, killer != null && killer == PlayerNet.Local, false);
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
            if (figure != null) Figure();

            // only the machine that owns the number puts it back; everyone else is told
            if (health.HasAuthority && deadSince >= 0f && Time.time - deadSince >= resetAfterSeconds)
            {
                deadSince = -1f;
                health.ResetToFull();
            }
        }

        /// <summary>The worn body: turned to face you once you are there, falls when killed, flashes when hit.</summary>
        void Figure()
        {
            if (!faced && PlayerNet.Local != null)
            {
                Vector3 to = PlayerNet.Local.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) figure.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                faced = true;
            }
            if (animator != null) animator.SetBool(DeadParam, health.IsDead);

            bool flashing = Time.time < flashUntil;
            if (flashing)
            {
                foreach (Renderer r in skin)
                {
                    if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_Color")) continue;
                    r.GetPropertyBlock(block);
                    block.SetColor("_Color", r.sharedMaterial.color * new Color(1.9f, 1.6f, 1.6f));
                    r.SetPropertyBlock(block);
                }
                tinted = true;
            }
            else if (tinted)
            {
                foreach (Renderer r in skin) if (r != null) r.SetPropertyBlock(null);
                tinted = false;
            }
        }
    }
}
