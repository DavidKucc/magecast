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
                // an element that cannot be sent yet is greyed out, with the time it still needs
                bool cooling = entry.Cooldown > 0f;
                Color colour = cooling ? Color.Lerp(entry.Colour, new Color(0.45f, 0.45f, 0.47f), 0.75f) : entry.Colour;
                Texture2D glyph = GlyphFor(entry.Id, entry.Colour);
                if (glyph != null)
                {
                    if (cooling) GUI.color = new Color(1f, 1f, 1f, 0.3f);
                    GUI.DrawTexture(new Rect(panel.x + 10f, y + (rowHeight - glyphSize) * 0.5f,
                                             glyphSize, glyphSize), glyph);
                    GUI.color = Color.white;
                }

                nameStyle.normal.textColor = colour;
                GUI.Label(new Rect(panel.x + 10f + glyphSize + 10f, y + 8f, panelWidth, 20f),
                          cooling ? entry.SpellName + "   " + entry.Cooldown.ToString("0.0") + " s" : entry.SpellName, nameStyle);
                GUI.Label(new Rect(panel.x + 10f + glyphSize + 10f, y + 26f, panelWidth, 20f),
                          entry.Id, runeStyle);
                y += rowHeight;
            }
        }

        Texture2D GlyphFor(string id, Color colour)
        {
            return RuneIcons.Get(id, colour, glyphSize);
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
