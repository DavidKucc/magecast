using System.Collections.Generic;
using MageCast.Combat;
using MageCast.Gestures;
using UnityEngine;

namespace MageCast.World
{
    /// <summary>
    /// A rune of air set in the floor at the foot of the Sanctum's core: step on it and it throws you in
    /// a high arc up onto the top, where the healing spring is -- the only way up. Up there you can see
    /// and be seen from everywhere, with nothing to hide behind; coming down is a step off the edge.
    ///
    /// Each machine throws its own player (and the server its bots), the way an updraft does, so the
    /// throw answers your step at once; every machine sees the pad fire for whoever steps on it -- the
    /// rune flares and the air rushes -- since everyone's body is seen standing on it.
    /// </summary>
    public class LaunchPad : MonoBehaviour
    {
        /// <summary>Where the arc comes down (world).</summary>
        public Vector3 landing;
        /// <summary>How far over the higher end the arc tops out.</summary>
        public float apex = 2f;
        public float radius = 1.1f;
        public RuneGlow rune;
        public AudioClip sound;

        static readonly Color AirColour = new Color(0.75f, 1f, 0.9f);
        readonly Dictionary<Transform, float> fired = new Dictionary<Transform, float>();
        const float Again = 1.2f;

        /// <summary>Every pad in the map (the bots look for one when they want the spring).</summary>
        public static readonly List<LaunchPad> All = new List<LaunchPad>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            Vector3 centre = transform.position;
            foreach (Hurtbox body in Hurtbox.All)
            {
                if (body == null) continue;
                PlayerMotor motor = body.GetComponent<PlayerMotor>();
                if (motor == null) continue;                       // a dummy stays where it is
                Vector3 feet = motor.transform.position;
                Vector3 off = feet - centre;
                if (off.y < -0.3f || off.y > 0.5f) continue;
                off.y = 0f;
                if (off.magnitude > radius) continue;
                if (!Grounded(motor)) continue;
                float last;
                if (fired.TryGetValue(motor.transform, out last) && Time.time - last < Again) continue;
                fired[motor.transform] = Time.time;

                if (motor.IsLocallyControlled) Throw(motor);
                if (rune != null) rune.Flare(AirColour);
                if (sound != null) SpellFx.OneShot(sound, centre + Vector3.up * 0.5f, 0.55f);
            }
        }

        static bool Grounded(PlayerMotor motor)
        {
            PlayerNet net = motor.GetComponent<PlayerNet>();
            return net != null && net.IsRemote ? net.Grounded.Value : motor.IsGrounded;
        }

        /// <summary>Up and over: straight up to the top of the arc, then down onto the landing.</summary>
        void Throw(PlayerMotor motor)
        {
            Vector3 from = motor.transform.position;
            float g = PlayerMotor.Gravity;
            float top = Mathf.Max(from.y, landing.y) + apex;
            float up = Mathf.Sqrt(2f * g * (top - from.y));
            float time = up / g + Mathf.Sqrt(2f * (top - landing.y) / g);
            Vector3 across = landing - from;
            across.y = 0f;
            motor.Toss(across / Mathf.Max(0.1f, time), up);
        }
    }
}
