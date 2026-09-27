using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A temporary crib sheet in the top-right: every gesture you can cast, and what it does.
    ///
    /// The glyphs are drawn from the RECOGNISER'S OWN TEMPLATES rather than from hand-made icons. That
    /// is the point of it -- a hand-drawn icon would slowly stop matching the shape the game actually
    /// wants, and the player would be practising the wrong thing without either of us knowing.
    ///
    /// Meant to come out once the runes are learned, so it is deliberately one component with no
    /// dependencies: delete the file and nothing else notices.
    /// </summary>
    public class GestureLegend : MonoBehaviour
    {
        [SerializeField] bool show = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F6;

        [SerializeField] int glyphSize = 46;
        [SerializeField] int rowHeight = 54;
        [SerializeField] int panelWidth = 190;
        [SerializeField] int margin = 12;

        GestureCaster caster;
        readonly Dictionary<string, Texture2D> glyphs = new Dictionary<string, Texture2D>();
        Texture2D panelBackground;
        GUIStyle nameStyle, runeStyle, titleStyle;

        void Awake()
        {
            caster = GetComponent<GestureCaster>();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) show = !show;
        }

        void OnGUI()
        {
            if (!show || caster == null) return;
            EnsureStyles();

            List<GestureCaster.VocabularyEntry> vocabulary = caster.Vocabulary();
            int height = rowHeight * vocabulary.Count + 30;
            var panel = new Rect(Screen.width - panelWidth - margin, margin, panelWidth, height);

            GUI.DrawTexture(panel, panelBackground);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, panelWidth, 20f), "GESTA   (F6)", titleStyle);

            float y = panel.y + 28f;
            foreach (GestureCaster.VocabularyEntry entry in vocabulary)
            {
                Texture2D glyph = GlyphFor(entry.Id, entry.Colour);
                if (glyph != null)
                    GUI.DrawTexture(new Rect(panel.x + 10f, y + (rowHeight - glyphSize) * 0.5f,
                                             glyphSize, glyphSize), glyph);

                nameStyle.normal.textColor = entry.Colour;
                GUI.Label(new Rect(panel.x + 10f + glyphSize + 10f, y + 8f, panelWidth, 20f),
                          entry.SpellName, nameStyle);
                GUI.Label(new Rect(panel.x + 10f + glyphSize + 10f, y + 26f, panelWidth, 20f),
                          entry.Id, runeStyle);
                y += rowHeight;
            }
        }

        Texture2D GlyphFor(string id, Color colour)
        {
            Texture2D cached;
            if (glyphs.TryGetValue(id, out cached) && cached != null) return cached;

            List<Vector2> points = GestureTemplates.BasePointsFor(id);
            if (points == null || points.Count < 2) return null;

            var tex = new Texture2D(glyphSize, glyphSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            var clear = new Color[glyphSize * glyphSize];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0f, 0f, 0f, 0f);
            tex.SetPixels(clear);

            // Fit the template into the icon rather than assuming its box: the runes and the circle do
            // not share extents, and a fixed scale would clip one of them.
            Vector2 min = points[0], max = points[0];
            foreach (Vector2 p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Vector2 span = max - min;
            float scale = (glyphSize - 9f) / Mathf.Max(0.001f, Mathf.Max(span.x, span.y));

            for (int i = 1; i < points.Count; i++)
                DrawSegment(tex, ToPixels(points[i - 1], min, span, scale),
                                 ToPixels(points[i], min, span, scale), colour);

            tex.Apply();
            glyphs[id] = tex;
            return tex;
        }

        Vector2 ToPixels(Vector2 p, Vector2 min, Vector2 span, float scale)
        {
            Vector2 centred = p - min - span * 0.5f;
            // y is flipped because a texture counts upwards and the stroke was authored screen-style
            return new Vector2(glyphSize * 0.5f + centred.x * scale,
                               glyphSize * 0.5f + centred.y * scale);
        }

        static void DrawSegment(Texture2D tex, Vector2 a, Vector2 b, Color colour)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                // two pixels wide, or a thin diagonal disappears into the background
                for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        int x = Mathf.RoundToInt(p.x) + dx;
                        int y = Mathf.RoundToInt(p.y) + dy;
                        if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                            tex.SetPixel(x, y, colour);
                    }
            }
        }

        void EnsureStyles()
        {
            if (panelBackground == null)
            {
                panelBackground = new Texture2D(1, 1);
                panelBackground.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                panelBackground.Apply();
            }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            }
            if (nameStyle == null)
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            if (runeStyle == null)
            {
                runeStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                runeStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
            }
        }
    }
}
