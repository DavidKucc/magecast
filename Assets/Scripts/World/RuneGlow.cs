using System.Collections.Generic;
using UnityEngine;

namespace MageCast.World
{
    /// <summary>
    /// Runes cut into the Sanctum's stone, glowing. They breathe gently at rest, and a spell striking
    /// the block they are cut into lights them up in that spell's colour, fading back over a couple of
    /// seconds -- fire leaves a pillar burning orange for a moment, ice washes it blue. Looks only, on
    /// every machine (each flies its own copy of every spell, so each sees the same strike).
    ///
    /// The lines are LineRenderers and halo quads using the SanctumGlow material; their colour is their
    /// vertex colour, set here.
    /// </summary>
    public class RuneGlow : MonoBehaviour
    {
        public Color rest = new Color(0.3f, 1f, 0.95f, 1f);
        /// <summary>How deep the breathing goes (0 none) and how long a breath is.</summary>
        public float breath = 0.25f;
        public float breathPeriod = 3.5f;
        public float phase;
        public LineRenderer[] lines;
        public MeshFilter[] halos;

        Color flare;
        float flareAt = -99f;
        const float FlareSeconds = 2.4f;

        /// <summary>A spell struck <paramref name="c"/>: the runes on it, if it has any, light up in <paramref name="colour"/>.</summary>
        public static void Struck(Collider c, Color colour)
        {
            if (c == null) return;
            RuneGlow g = c.GetComponent<RuneGlow>();
            if (g == null) g = c.GetComponentInParent<RuneGlow>();
            if (g != null) g.Flare(colour);
        }

        public void Flare(Color colour)
        {
            flare = colour;
            flareAt = Time.time;
        }

        void Update()
        {
            float b = 1f - breath * 0.5f + breath * 0.5f * Mathf.Sin((Time.time / breathPeriod + phase) * Mathf.PI * 2f);
            Color c = rest * b;
            float since = Time.time - flareAt;
            if (since < FlareSeconds)
            {
                float k = 1f - since / FlareSeconds;
                k = k * k;
                c = Color.Lerp(c, flare * 1.4f, k);       // brighter than at rest, but still its own colour
            }
            c.a = 1f;
            if (lines != null)
                foreach (LineRenderer l in lines)
                    if (l != null) { l.startColor = c; l.endColor = c; }
            if (halos != null && c != haloColour)
            {
                haloColour = c;
                foreach (MeshFilter h in halos)
                    if (h != null) Tint(h, c);
            }
        }

        Color haloColour = Color.clear;
        static readonly List<Color> buffer = new List<Color>();

        /// <summary>A halo's colour, through its vertex colours (its own copy of the quad).</summary>
        static void Tint(MeshFilter f, Color c)
        {
            Mesh m = f.mesh;
            buffer.Clear();
            for (int i = 0; i < m.vertexCount; i++) buffer.Add(c);
            m.SetColors(buffer);
        }
    }
}
