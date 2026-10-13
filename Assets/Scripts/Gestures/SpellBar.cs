using System.Collections.Generic;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// The spell bar along the bottom of the screen, the way WoW has one: a tile per spell, attacks
    /// together and defences apart (special spells, when there are any, a third group), and over every
    /// tile that cannot be sent yet a dark clock hand winding round as the time runs out.
    ///
    ///   number in the middle   the element's own cooldown, or a lock -- seconds to go
    ///   sweep, no number       only the global cooldown after the last attack, as in WoW
    ///   red, LOCKED            an interrupt locked the element (SpellTiers.SchoolLock)
    ///   gold frame, tier       the spell in your hand; QUEUED when a send waits for the cooldown
    ///   white frame            the rune you are drawing reads as this one right now; red if,
    ///                          finished now, it would make nothing (cooldown or lock)
    ///   a flash                a cooldown has just ended
    ///
    /// The tiles show the runes from the recogniser's own corners, so the bar is also the crib sheet:
    /// what you see is the shape the game wants. F6 hides it.
    /// </summary>
    public class SpellBar : MonoBehaviour
    {
        [SerializeField] bool show = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F6;

        static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        static readonly Color LockRed = new Color(1f, 0.35f, 0.3f);

        GestureCaster caster;
        GUIStyle nameStyle, numberStyle, tagStyle, tierStyle;

        readonly Dictionary<string, bool> wasCooling = new Dictionary<string, bool>();
        readonly Dictionary<string, float> readyFlash = new Dictionary<string, float>();

        void Awake()
        {
            caster = GetComponent<GestureCaster>();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) show = !show;
        }

        void Styles(float size)
        {
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, clipping = TextClipping.Overflow };
                numberStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                tagStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, clipping = TextClipping.Overflow };
                tierStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight, clipping = TextClipping.Overflow };
            }
            nameStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 0.19f, 10f, 13f));
            numberStyle.fontSize = Mathf.RoundToInt(size * 0.36f);
            tagStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 0.17f, 9f, 12f));
            tierStyle.fontSize = Mathf.RoundToInt(size * 0.24f);
        }

        void OnGUI()
        {
            if (!show || caster == null || Event.current.type != EventType.Repaint) return;

            List<GestureCaster.VocabularyEntry> spells = caster.Vocabulary();
            if (spells.Count == 0) return;

            float size = Mathf.Round(Mathf.Clamp(Screen.height * 0.075f, 44f, 70f));
            float gap = Mathf.Round(size * 0.12f), groupGap = Mathf.Round(size * 0.45f);
            float nameHeight = nameStyle != null ? nameStyle.fontSize + 6f : 18f;
            Styles(size);

            float width = 0f;
            for (int i = 0; i < spells.Count; i++)
                width += size + (i == 0 ? 0f : spells[i].Role == spells[i - 1].Role ? gap : groupGap);

            float x = Mathf.Round((Screen.width - width) * 0.5f);
            float y = Screen.height - size - nameHeight - 12f;
            // clear of the health bar in the bottom left, on a narrow screen
            if (x < 300f) y -= 62f;

            // a plate behind the whole bar, so it reads against a bright floor as well as a dark one
            Fill(new Rect(x - 12f, y - 10f, width + 24f, size + nameHeight + 18f), new Color(0f, 0f, 0f, 0.32f));

            for (int i = 0; i < spells.Count; i++)
            {
                if (i > 0) x += spells[i].Role == spells[i - 1].Role ? gap : groupGap;
                Tile(spells[i], new Rect(x, y, size, size), nameHeight);
                x += size;
            }
        }

        void Tile(GestureCaster.VocabularyEntry s, Rect r, float nameHeight)
        {
            bool cooling = s.Cooldown > 0f;

            // a cooldown that has just run out (not the global one -- that would flash every second)
            bool was;
            wasCooling.TryGetValue(s.Id, out was);
            if (was && !cooling) readyFlash[s.Id] = Time.time;
            wasCooling[s.Id] = cooling && !s.GlobalOnly;

            // the spell in the hand glows behind its tile
            if (s.Held)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
                Fill(Grow(r, 7f), new Color(Gold.r, Gold.g, Gold.b, 0.22f * pulse));
                Fill(Grow(r, 4f), new Color(s.Colour.r, s.Colour.g, s.Colour.b, 0.35f * pulse));
            }

            Fill(Grow(r, 2f), new Color(0f, 0f, 0f, 0.85f));
            Texture2D icon = SpellIcons.Icon(s.Id, s.Colour);
            if (icon != null) GUI.DrawTexture(r, icon);

            if (cooling)
            {
                // the clock hand, red for a lock
                GUI.color = s.Locked ? new Color(0.5f, 0.03f, 0.02f, 0.75f) : new Color(0f, 0f, 0f, 0.68f);
                GUI.DrawTexture(r, SpellIcons.Sweep(s.Cooldown / Mathf.Max(0.01f, s.Duration)));
                GUI.color = Color.white;
                // greyed a little all over, as an unusable button is
                Fill(r, new Color(0.1f, 0.1f, 0.12f, 0.25f));

                if (!s.GlobalOnly)
                    Outlined(r, s.Cooldown.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), numberStyle,
                             s.Locked ? LockRed : Color.white);
                if (s.Locked) Outlined(new Rect(r.x, r.y + 2f, r.width, 16f), "LOCKED", tagStyle, LockRed);
            }

            float flash;
            if (readyFlash.TryGetValue(s.Id, out flash) && Time.time - flash < 0.35f)
                Fill(r, new Color(1f, 1f, 1f, 0.55f * (1f - (Time.time - flash) / 0.35f)));

            // the frame: gold in the hand, white while being drawn -- red if, finished now, the rune
            // would make nothing -- the element's colour otherwise
            Color frame = s.Held ? Gold
                        : s.Drawing ? (s.WouldFail ? LockRed : Color.white)
                        : cooling ? new Color(0.45f, 0.45f, 0.5f, 0.6f)
                        : new Color(s.Colour.r, s.Colour.g, s.Colour.b, 0.75f);
            Frame(r, s.Held || s.Drawing ? 3f : 2f, frame);

            if (s.Held)
            {
                Outlined(new Rect(r.x, r.y, r.width - 4f, r.height - 1f), SpellTiers.Roman(s.HeldTier), tierStyle,
                         Color.Lerp(s.Colour, Color.white, 0.5f));
                if (s.Queued) Outlined(new Rect(r.x, r.y - 16f, r.width, 16f), "QUEUED", tagStyle, Gold);
            }

            Color nameColour = cooling ? new Color(0.65f, 0.65f, 0.7f) : Color.Lerp(s.Colour, Color.white, 0.25f);
            Outlined(new Rect(r.x - 10f, r.yMax + 4f, r.width + 20f, nameHeight), s.SpellName, nameStyle, nameColour);
        }

        // ---------------------------------------------------------------- drawing

        static Rect Grow(Rect r, float by)
        {
            return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Frame(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y + t, t, r.height - 2f * t), c);
            Fill(new Rect(r.xMax - t, r.y + t, t, r.height - 2f * t), c);
        }

        /// <summary>Text with a dark outline, readable over any icon.</summary>
        static void Outlined(Rect r, string text, GUIStyle style, Color colour)
        {
            Color keep = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.9f * colour.a);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * 1.5f, r.y + dy * 1.5f, r.width, r.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(r, text, style);
            style.normal.textColor = keep;
        }
    }
}
