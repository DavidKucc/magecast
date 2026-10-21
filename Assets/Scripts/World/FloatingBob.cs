using UnityEngine;

namespace MageCast.World
{
    /// <summary>
    /// Drifts a decoration up and down and turns it slowly -- the crystal over the Sanctum's core, the
    /// rocks floating round it. Looks only, and from the clock, so it needs nothing from the network.
    /// </summary>
    public class FloatingBob : MonoBehaviour
    {
        public float height = 0.25f;
        public float period = 4f;
        /// <summary>Degrees a second about the vertical.</summary>
        public float spin = 15f;
        /// <summary>0..1 along the bob, so a field of rocks does not move in step.</summary>
        public float phase;

        Vector3 rest;

        void Start()
        {
            rest = transform.localPosition;
        }

        void Update()
        {
            float t = Time.time / Mathf.Max(0.1f, period) + phase;
            transform.localPosition = rest + Vector3.up * (Mathf.Sin(t * Mathf.PI * 2f) * height);
            if (spin != 0f) transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
        }
    }
}
