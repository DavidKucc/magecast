using Unity.Netcode;
using UnityEngine;

namespace MageCast.Combat
{
    /// <summary>
    /// Hit points. Deliberately plain: no armour, no resistances, no damage types.
    ///
    /// Damage numbers in this game are going to move a great deal before they settle -- the casting
    /// mechanic decides how often a spell lands at all, and that is still being measured -- so anything
    /// clever built on top of them now would be built on sand. What matters today is that a spell can
    /// be seen to do something and that the amount is one number you can change in the inspector.
    ///
    /// Networked: the server owns the number and everyone else is told. Only the server's copy of a
    /// spell ever calls TakeDamage, so a hit counts once no matter how many machines watched it land.
    /// When nothing is networked (an editor test, say) it behaves exactly as it always did.
    /// </summary>
    public class Health : NetworkBehaviour
    {
        /// <summary>No attacker -- the floor, your own misfire, or a dummy's reset.</summary>
        public const ulong NoAttacker = ulong.MaxValue;

        [SerializeField] float maxHealth = 100f;

        readonly NetworkVariable<float> networked = new NetworkVariable<float>(-1f);
        float local;

        public float Max { get { return maxHealth; } }
        // -1 is "the server has not said yet". Read as dead, it would hide a player for the frame
        // between spawning and hearing their health, depending only on which component spawned first.
        public float Current { get { return Networked ? (networked.Value < 0f ? maxHealth : networked.Value) : local; } }
        public bool IsDead { get { return Current <= 0f; } }
        public float Fraction { get { return maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f; } }

        /// <summary>Fires on every change, including heals and resets, on every machine. (current, max)</summary>
        public event System.Action<float, float> Changed;

        /// <summary>Fires once when it drops to zero, on every machine.</summary>
        public event System.Action Died;

        /// <summary>Last time damage landed -- the training dummy uses it to decide when to reset.</summary>
        public float LastDamagedAt { get; private set; }

        /// <summary>Who dealt the last damage, as a network client id. Server only.</summary>
        public ulong LastAttacker { get; private set; }

        /// <summary>True where this machine decides what happens to the number: the server, or offline.</summary>
        public bool HasAuthority { get { return !Networked || IsServer; } }

        bool Networked { get { return IsSpawned; } }

        void Awake()
        {
            local = maxHealth;
            LastAttacker = NoAttacker;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) networked.Value = maxHealth;
            networked.OnValueChanged += OnNetworkedChanged;
        }

        public override void OnNetworkDespawn()
        {
            networked.OnValueChanged -= OnNetworkedChanged;
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, NoAttacker);
        }

        public void TakeDamage(float amount, ulong attacker)
        {
            if (!HasAuthority || amount <= 0f || IsDead) return;

            LastAttacker = attacker;
            Set(Mathf.Max(0f, Current - amount));
        }

        public void Heal(float amount)
        {
            if (!HasAuthority || amount <= 0f) return;
            Set(Mathf.Min(maxHealth, Current + amount));
        }

        public void ResetToFull()
        {
            if (!HasAuthority) return;
            LastAttacker = NoAttacker;
            Set(maxHealth);
        }

        void Set(float value)
        {
            if (Networked)
            {
                // the change callback reports it, on this machine and every other
                networked.Value = value;
                return;
            }

            float old = local;
            local = value;
            Report(old, value);
        }

        void OnNetworkedChanged(float old, float value)
        {
            // -1 is "not yet initialised by the server"; the first real value is not a heal
            if (old < 0f) old = maxHealth;
            Report(old, value);
        }

        void Report(float old, float value)
        {
            if (value < old)
            {
                LastDamagedAt = Time.time;
                WorldPopups.Damage(transform, old - value);
            }

            if (Changed != null) Changed(value, maxHealth);
            if (value <= 0f && old > 0f && Died != null) Died();
        }
    }
}
