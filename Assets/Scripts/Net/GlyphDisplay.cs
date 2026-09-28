using System.Collections.Generic;
using MageCast.Gestures;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Somebody else's glyph, drawn in the air over their head while they draw it.
    ///
    /// This is the other half of the core rule. Drawing makes you vulnerable, and it is only a
    /// decision -- rather than a tax -- if the opponent can SEE you drawing and see what: a half-drawn
    /// Kenaz is "fire is coming, get behind something", and reading that in time is the skill on the
    /// receiving end.
    ///
    /// It faces whoever is watching and is shown the way its author drew it, not mirrored, so a rune
    /// reads as the rune from the front, the back or the side. Neutral white while forming; once the
    /// glyph becomes a spell in the hand it takes the spell's colour, because the shape has already
    /// told anyone paying attention.
    /// </summary>
    public class GlyphDisplay : MonoBehaviour
    {
        const float Height = 3.75f;      // clear of the head, the health bar and the name
        const float Scale = 1.5f;        // metres per screen height of drawing
        const float FadeSeconds = 0.6f;

        static readonly Color Forming = new Color(0.92f, 0.94f, 1f, 0.95f);

        Transform holder;
        LineRenderer line;
        readonly List<Vector2> raw = new List<Vector2>();
        readonly List<Vector2> shown = new List<Vector2>();
        readonly List<Vector3> points = new List<Vector3>();
        bool finished;
        Color colour = Forming;
        float fadeFrom = -1f;

        void Awake()
        {
            GameObject go = new GameObject("Glyph");
            go.transform.SetParent(transform, false);
            holder = go.transform;

            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = 0.06f;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.material = RuntimeMaterials.Line();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            line.enabled = false;
        }

        public void Begin()
        {
            raw.Clear();
            points.Clear();
            finished = false;
            colour = Forming;
            fadeFrom = -1f;
            Apply(1f);
        }

        public void Add(Vector2[] batch)
        {
            if (fadeFrom >= 0f) return;      // a straggler from a draw that has already ended
            raw.AddRange(batch);
            Rebuild();
            Apply(1f);
        }

        public void End(StrokeOutcome outcome, Color spellColour)
        {
            switch (outcome)
            {
                case StrokeOutcome.WindUp:
                    return;                  // not about a stroke at all

                case StrokeOutcome.Held:
                    // stays up in the spell's colour until it is sent or lost -- as the clean rune
                    colour = spellColour;
                    finished = true;
                    Rebuild();
                    Apply(1f);
                    return;

                case StrokeOutcome.Sent:
                case StrokeOutcome.Banked:
                    colour = spellColour;
                    finished = true;
                    Rebuild();
                    break;

                default:
                    colour = GestureCaster.FailColour;
                    break;
            }
            fadeFrom = Time.time;
        }

        /// <summary>
        /// The glyph as its author sees it: finished lines straight (see RuneSegments), so the rune an
        /// opponent reads is the rune, not the wobble -- reading it in time is their skill.
        /// </summary>
        void Rebuild()
        {
            points.Clear();
            List<Vector2> from = raw;
            if (RuneSegments.Enabled)
            {
                RuneSegments.Straighten(raw, shown, finished);
                from = shown;
            }
            foreach (Vector2 p in from) points.Add(new Vector3(p.x * Scale, p.y * Scale, 0f));
        }

        void Apply(float alpha)
        {
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);

            Color c = new Color(colour.r, colour.g, colour.b, colour.a * alpha);
            line.startColor = new Color(c.r, c.g, c.b, c.a * 0.45f);   // the oldest end fainter, as the
            line.endColor = c;                                         // owner's own trail draws it
            line.enabled = points.Count >= 2 && alpha > 0.01f;
        }

        void LateUpdate()
        {
            if (fadeFrom >= 0f)
            {
                float t = (Time.time - fadeFrom) / FadeSeconds;
                if (t >= 1f) { points.Clear(); raw.Clear(); fadeFrom = -1f; Apply(0f); return; }
                Apply(1f - t);
            }

            if (!line.enabled) return;

            Camera cam = Camera.main;
            holder.position = transform.position + Vector3.up * Height;
            if (cam != null) holder.rotation = cam.transform.rotation;
        }
    }
}
