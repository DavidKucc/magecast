using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Where a thrown earth will come down, on the floor from the moment it leaves the hand: the blast's
    /// reach as a ring, and a second ring closing in on it as the stone falls -- the two meet as it
    /// lands. Every machine flies its own copy of the stone and marks its own landing, so everyone sees
    /// it, the one it is meant for most of all: dodging a lob is reading this and stepping out in time.
    /// </summary>
    public class LandingMark : MonoBehaviour
    {
        const int Segments = 48;

        LineRenderer outer, inner;
        Vector3 centre, tangent, bitangent;
        float radius, total, left;
        Color colour;

        public static LandingMark Spawn(Vector3 at, Vector3 normal, float radius, Color colour, float seconds)
        {
            var go = new GameObject("LandingMark");
            LandingMark m = go.AddComponent<LandingMark>();
            Vector3 n = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            m.tangent = Vector3.Cross(n, Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            m.bitangent = Vector3.Cross(n, m.tangent);
            m.centre = at + n * 0.05f;
            m.radius = radius;
            m.colour = colour;
            m.total = m.left = Mathf.Max(0.05f, seconds);
            m.outer = m.Line("Reach", 0.11f);
            m.inner = m.Line("Closing", 0.07f);
            m.Draw(m.outer, radius);
            m.Update();
            return m;
        }

        LineRenderer Line(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.widthMultiplier = width;
            lr.numCornerVertices = 2;
            lr.material = RuntimeMaterials.Line();
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = Segments;
            return lr;
        }

        void Draw(LineRenderer lr, float r)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                lr.SetPosition(i, centre + (tangent * Mathf.Cos(a) + bitangent * Mathf.Sin(a)) * r);
            }
        }

        void Update()
        {
            left -= Time.deltaTime;
            float done = Mathf.Clamp01(1f - left / total);
            // closing in from twice the reach, so it is plainly moving from the first moment
            Draw(inner, radius * Mathf.Lerp(2f, 1f, done));
            // brighter as it comes, a pulse in the last third
            float pulse = done > 0.66f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 30f) : 0f;
            Color c = new Color(colour.r, colour.g, colour.b, Mathf.Lerp(0.7f, 1f, done) - 0.3f * pulse);
            outer.startColor = outer.endColor = c;
            inner.startColor = inner.endColor = new Color(colour.r, colour.g, colour.b, Mathf.Lerp(0.35f, 0.9f, done));
            // never left lying about if its stone is lost some other way
            if (left < -1.5f) Destroy(gameObject);
        }
    }
}
