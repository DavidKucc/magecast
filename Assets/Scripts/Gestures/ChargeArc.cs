using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A jagged bolt from one point to another that flickers and is gone -- how a charge running through
    /// an ice patch shows who it caught. Re-jagged every few frames, because a still zigzag reads as a
    /// drawn line rather than as electricity.
    /// </summary>
    public class ChargeArc : MonoBehaviour
    {
        const int Segments = 9;
        const float Life = 0.4f;

        LineRenderer line;
        Vector3 from, to;
        float age;
        float nextJag;

        public static void Spawn(Vector3 from, Vector3 to)
        {
            GameObject go = new GameObject("ChargeArc");
            ChargeArc arc = go.AddComponent<ChargeArc>();
            arc.from = from;
            arc.to = to;

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = Segments + 1;
            lr.widthMultiplier = 0.07f;
            lr.material = RuntimeMaterials.Line();
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            arc.line = lr;
            arc.Jag();
        }

        void Jag()
        {
            // The kinks are seeded from where the arc runs rather than drawn at random, so every machine
            // shows the same bolt -- nobody is going to compare, but there is no reason for them to differ.
            Vector3 dir = to - from;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            side.Normalize();
            float seed = from.x * 12.9898f + to.z * 78.233f + Mathf.Floor(age * 30f) * 3.7f;

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector3 p = from + dir * t;
                if (i > 0 && i < Segments)
                {
                    float wobble = Mathf.Sin(seed + i * 91.7f) * 0.35f;
                    float lift = Mathf.Cos(seed * 0.5f + i * 47.3f) * 0.25f;
                    p += side * wobble + Vector3.up * lift;
                }
                line.SetPosition(i, p);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Life) { Destroy(gameObject); return; }

            if (Time.time >= nextJag) { Jag(); nextJag = Time.time + 0.04f; }

            float a = 1f - age / Life;
            Color c = new Color(0.8f, 0.85f, 1f, a);
            line.startColor = c;
            line.endColor = new Color(1f, 1f, 1f, a);
        }
    }
}
