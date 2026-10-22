using System.Collections.Generic;
using MageCast.Combat;
using MageCast.Gestures;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Top right: who killed whom, and with what -- the killer, the spell's rune on its tile in the element's
    /// colour (with BURN, MAGMA or SLAM when it was not the spell itself), the victim. Your own kills edged in
    /// gold, your own death in red. The newest on top; each slides in and fades after a few seconds.
    /// </summary>
    public static class KillFeed
    {
        struct Entry
        {
            public string Killer, Victim;
            public int KillerTeam, VictimTeam;
            public DamageCause Cause;
            public bool Mine, MyDeath;
            public float At;
        }

        const int Most = 5;
        const float Shown = 6f, Fade = 0.8f, Slide = 0.2f;

        static readonly List<Entry> entries = new List<Entry>();
        static GUIStyle name, detail;

        public static void Report(string killer, int killerTeam, string victim, int victimTeam, DamageCause cause, bool mine, bool myDeath)
        {
            entries.Add(new Entry { Killer = killer, KillerTeam = killerTeam, Victim = victim, VictimTeam = victimTeam,
                                    Cause = cause, Mine = mine, MyDeath = myDeath, At = Time.time });
            if (entries.Count > Most) entries.RemoveAt(0);
        }

        public static void Clear() { entries.Clear(); }

        /// <summary>The rune and colour a cause is shown with; false for a cause with no spell to show.</summary>
        static bool Icon(DamageCause cause, out string rune, out Color colour)
        {
            int element;
            switch (cause)
            {
                case DamageCause.Fire: case DamageCause.Burn: element = 0; break;
                case DamageCause.Ice: element = 1; break;
                case DamageCause.Lightning: element = 2; break;
                case DamageCause.Air: case DamageCause.Slam: element = 3; break;
                case DamageCause.Earth: case DamageCause.Magma: element = 4; break;
                default: rune = null; colour = Color.white; return false;
            }
            rune = MenuArt.RuneNames[element];
            // magma is earth's rune burning in fire's colour
            colour = MenuArt.ElementColours[cause == DamageCause.Magma ? 0 : element];
            return true;
        }

        static string Word(DamageCause cause)
        {
            switch (cause)
            {
                case DamageCause.Burn: return "BURN";
                case DamageCause.Magma: return "MAGMA";
                case DamageCause.Slam: return "SLAM";
                default: return null;
            }
        }

        static Color NameColour(int team, bool you, bool youDied)
        {
            if (you) return youDied ? new Color(1f, 0.5f, 0.45f) : MenuArt.Gold;
            return team != Teams.None ? Teams.Colour(team) : new Color(0.92f, 0.92f, 0.95f);
        }

        /// <summary>Draws the feed at the top right; call from OnGUI.</summary>
        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint || entries.Count == 0) return;
            if (name == null)
            {
                name = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
                detail = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            }

            float s = Mathf.Clamp(Screen.height / 1000f, 0.85f, 1.3f);
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float right = Screen.width / s - 14f, y = 14f;
            const float H = 34f, Tile = 26f, Gap = 8f;

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry e = entries[i];
                float age = Time.time - e.At;
                if (age > Shown) continue;
                float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((Shown - age) / Fade);
                float slide = (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Slide))) * 40f;

                string rune;
                Color colour;
                bool hasIcon = Icon(e.Cause, out rune, out colour);
                string word = Word(e.Cause);
                bool killed = !string.IsNullOrEmpty(e.Killer);

                // killer, what, victim; with nobody to blame and nothing to show, the victim "went down"
                if (!killed && !hasIcon) word = "WENT DOWN";
                float killerW = killed ? name.CalcSize(new GUIContent(e.Killer)).x : 0f;
                float victimW = name.CalcSize(new GUIContent(e.Victim)).x;
                float wordW = word != null ? detail.CalcSize(new GUIContent(word)).x + 6f : 0f;
                string arrow = !hasIcon && killed ? ">" : null;
                float arrowW = arrow != null ? name.CalcSize(new GUIContent(arrow)).x : 0f;
                float w = 16f + (killed ? killerW + Gap : 0f) + (hasIcon ? Tile + wordW + Gap : arrow != null ? arrowW + Gap : 0f)
                        + victimW + (!killed && !hasIcon ? wordW + Gap : 0f) + 16f;

                Rect r = new Rect(right - w + slide, y, w, H);
                Color edge = e.Mine ? MenuArt.Gold : e.MyDeath ? new Color(1f, 0.4f, 0.35f) : new Color(1f, 1f, 1f, 0.12f);
                MenuArt.RoundRect(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f * a), 10f);
                MenuArt.RoundRect(r, new Color(0.045f, 0.05f, 0.072f, 0.82f * a), 10f);
                if (e.MyDeath) MenuArt.RoundRect(r, new Color(0.35f, 0.04f, 0.04f, 0.35f * a), 10f);
                MenuArt.RoundRect(r, new Color(edge.r, edge.g, edge.b, (e.Mine || e.MyDeath ? 0.85f : edge.a) * a), 10f,
                                  e.Mine || e.MyDeath ? 1.6f : 1f);

                float x = r.x + 16f;
                if (killed)
                {
                    Text(new Rect(x, r.y, killerW + 4f, H), e.Killer, name, NameColour(e.KillerTeam, e.Mine, false), a);
                    x += killerW + Gap;
                }
                if (hasIcon)
                {
                    Texture2D tile = SpellIcons.Icon(rune, colour, 52);
                    Rect tr = new Rect(x, r.y + (H - Tile) * 0.5f, Tile, Tile);
                    if (tile != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, a);
                        GUI.DrawTexture(tr, tile);
                        GUI.color = Color.white;
                    }
                    MenuArt.RoundRect(tr, new Color(colour.r, colour.g, colour.b, 0.6f * a), 4f, 1f);
                    x += Tile;
                    if (word != null)
                    {
                        detail.normal.textColor = new Color(colour.r, colour.g, colour.b, 0.9f * a);
                        GUI.Label(new Rect(x + 6f, r.y, wordW, H), word, detail);
                        x += wordW;
                    }
                    x += Gap;
                }
                else if (arrow != null)
                {
                    Text(new Rect(x, r.y, arrowW + 4f, H), arrow, name, new Color(1f, 1f, 1f, 0.55f), a);
                    x += arrowW + Gap;
                }
                Text(new Rect(x, r.y, victimW + 4f, H), e.Victim, name, NameColour(e.VictimTeam, e.MyDeath, true), a);
                if (!killed && !hasIcon)
                {
                    detail.normal.textColor = new Color(1f, 1f, 1f, 0.6f * a);
                    GUI.Label(new Rect(x + victimW + Gap, r.y, wordW, H), word, detail);
                }

                y += H + 6f;
            }
            GUI.matrix = keep;
        }

        static void Text(Rect r, string text, GUIStyle style, Color c, float a)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.6f * a);
            GUI.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = new Color(c.r, c.g, c.b, c.a * a);
            GUI.Label(r, text, style);
        }
    }
}
