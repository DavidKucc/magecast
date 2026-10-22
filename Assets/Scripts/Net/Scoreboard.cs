using System.Collections.Generic;
using System.Globalization;
using MageCast.Combat;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Who is in the match and how they are doing: kills, deaths and the ratio between them.
    ///   strip   always, across the top: a chip per player, the leader first, you in gold, and what wins
    ///   table   the whole of it, while Tab is held, in the pause menu, and under the result at the end of a
    ///           match -- by side in a team game, each side under its score
    /// Drawn like the spell card (MenuArt): dark rounded plates, a coloured edge, scaled with the screen.
    /// </summary>
    public static class Scoreboard
    {
        struct Row
        {
            public string Name;
            public int Kills, Deaths, Team;
            public bool Me, Bot, Down;
        }

        static GUIStyle chipName, chipNumber, rank, cell, cellName, header, title, note, teamScore, tag;

        static float Scale { get { return Mathf.Clamp(Screen.height / 1000f, 0.85f, 1.3f); } }

        static void Styles()
        {
            if (chipName != null) return;
            chipName = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Overflow };
            chipNumber = new GUIStyle(chipName) { fontSize = 16 };
            rank = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cell = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            cellName = new GUIStyle(cell) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            note = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            teamScore = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tag = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        /// <summary>Everyone (or one side), the most kills first, then the fewest deaths.</summary>
        static List<Row> Rows(int team)
        {
            var rows = new List<Row>();
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (p == null) continue;
                if (team != Teams.None && p.Team.Value != team) continue;
                Health h = p.GetComponent<Health>();
                rows.Add(new Row { Name = p.DisplayName, Kills = p.Kills.Value, Deaths = p.Deaths.Value, Team = p.Team.Value,
                                   Me = p == PlayerNet.Local, Bot = p.IsBot, Down = h != null && h.IsDead });
            }
            rows.Sort((a, b) => a.Kills != b.Kills ? b.Kills.CompareTo(a.Kills)
                              : a.Deaths != b.Deaths ? a.Deaths.CompareTo(b.Deaths)
                              : string.CompareOrdinal(a.Name, b.Name));
            return rows;
        }

        /// <summary>Kills over deaths; with no deaths yet, the kills themselves.</summary>
        static string Ratio(int kills, int deaths)
        {
            return (deaths == 0 ? kills : kills / (float)deaths).ToString("0.00", CultureInfo.InvariantCulture);
        }

        static Color NameColour(Row r)
        {
            if (r.Me) return MenuArt.Gold;
            return r.Team != Teams.None ? Teams.Colour(r.Team) : new Color(0.92f, 0.92f, 0.95f);
        }

        static void Label(Rect r, string text, GUIStyle style, Color c)
        {
            style.normal.textColor = new Color(0f, 0f, 0f, 0.55f * c.a);
            GUI.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        static void Plate(Rect r, Color edge, float edgeWidth = 1f, float radius = 10f)
        {
            MenuArt.RoundRect(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.3f), radius);
            MenuArt.RoundRect(r, new Color(0.045f, 0.05f, 0.072f, 0.84f), radius);
            MenuArt.RoundRect(r, edge, radius, edgeWidth);
        }

        /// <summary>Training keeps score but plays to nothing: no match ends there.</summary>
        static bool Training { get { return NetSession.Instance != null && NetSession.Instance.Current == NetSession.Mode.Training; } }

        static string Goal { get { return Training ? "training" : "first to " + MatchDirector.Target + (Lobby.TeamGame ? " kills" : ""); } }

        static string Footer(string ping)
        {
            string s = Training ? "" : Goal + "      ";
            if (!string.IsNullOrEmpty(ping)) s += ping + "      ";
            return s + "hold TAB for the table";
        }

        // ---------------------------------------------------------------- the strip

        /// <summary>A free-for-all: a chip per player across the top, best first -- the top four and you, past five.</summary>
        public static void Strip(string ping)
        {
            if (Event.current.type != EventType.Repaint) return;
            Styles();
            float s = Scale;
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float vw = Screen.width / s;

            List<Row> rows = Rows(Teams.None);
            var shown = new List<KeyValuePair<int, Row>>();
            for (int i = 0; i < rows.Count; i++)
                if (rows.Count <= 5 || i < 4 || rows[i].Me) shown.Add(new KeyValuePair<int, Row>(i + 1, rows[i]));

            const float H = 32f, Gap = 6f;
            var widths = new List<float>();
            float total = -Gap;
            foreach (var kv in shown)
            {
                float w = 10f + 22f + 8f + chipName.CalcSize(new GUIContent(kv.Value.Name)).x + 12f
                        + chipNumber.CalcSize(new GUIContent(kv.Value.Kills + " / " + kv.Value.Deaths)).x + 12f;
                widths.Add(w);
                total += w + Gap;
            }
            float x = (vw - total) * 0.5f, y = 8f;
            for (int i = 0; i < shown.Count; i++)
            {
                Row r = shown[i].Value;
                int place = shown[i].Key;
                Rect chip = new Rect(x, y, widths[i], H);
                Color edge = r.Me ? MenuArt.Gold : place == 1 && r.Kills > 0 ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 1f, 1f, 0.12f);
                Plate(chip, edge, r.Me ? 1.6f : 1f, 9f);

                // the place, on a little round badge: gold for the leader, once there is one
                Rect badge = new Rect(chip.x + 8f, chip.y + 5f, 22f, 22f);
                Color bc = place == 1 && r.Kills > 0 ? MenuArt.Gold : new Color(0.7f, 0.72f, 0.78f);
                MenuArt.RoundRect(badge, new Color(bc.r * 0.22f, bc.g * 0.22f, bc.b * 0.22f, 0.95f), 11f);
                MenuArt.RoundRect(badge, new Color(bc.r, bc.g, bc.b, 0.8f), 11f, 1.2f);
                rank.normal.textColor = bc;
                GUI.Label(badge, place.ToString(), rank);

                float nx = badge.xMax + 8f;
                float nw = chipName.CalcSize(new GUIContent(r.Name)).x;
                Label(new Rect(nx, chip.y, nw + 4f, H), r.Name, chipName, r.Down ? new Color(0.6f, 0.6f, 0.65f) : NameColour(r));
                Label(new Rect(nx + nw + 12f, chip.y, 80f, H), r.Kills + " / " + r.Deaths, chipNumber, Color.white);
                x += widths[i] + Gap;
            }
            Label(new Rect(0f, y + H + 4f, vw, 18f), Footer(ping), note, new Color(1f, 1f, 1f, 0.6f));
            GUI.matrix = keep;
        }

        /// <summary>A team game: the two sides' kills on plates in their colours either side of the middle.</summary>
        public static void TeamStrip(string ping)
        {
            if (Event.current.type != EventType.Repaint || !Lobby.Active) return;
            Styles();
            float s = Scale;
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float vw = Screen.width / s, cx = vw * 0.5f, y = 6f;
            const float W = 170f, H = 40f;
            int mine = PlayerNet.Local != null ? PlayerNet.Local.Team.Value : Teams.None;
            for (int team = 1; team <= 2; team++)
            {
                Color c = Teams.Colour(team);
                Rect r = new Rect(team == 1 ? cx - 14f - W : cx + 14f, y, W, H);
                if (team == mine) MenuArt.Glow(r.center, W * 1.4f, new Color(c.r, c.g, c.b, 0.12f));
                Plate(r, new Color(c.r, c.g, c.b, team == mine ? 0.9f : 0.5f), team == mine ? 1.6f : 1f);
                string name = Teams.Names[team].ToUpperInvariant();
                title.alignment = team == 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                Label(new Rect(r.x + 14f, r.y, r.width - 28f, H), name, title, c);
                title.alignment = TextAnchor.MiddleLeft;
                teamScore.alignment = team == 1 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                MenuArt.GlowText(new Rect(r.x + 14f, r.y, r.width - 28f, H), Lobby.Instance.Score(team).ToString(), teamScore, c, 0.6f);
                teamScore.alignment = TextAnchor.MiddleCenter;
            }
            Label(new Rect(cx - 14f, y, 28f, H), ":", teamScore, new Color(1f, 1f, 1f, 0.7f));
            Label(new Rect(0f, y + H + 4f, vw, 18f), Footer(ping), note, new Color(1f, 1f, 1f, 0.6f));
            GUI.matrix = keep;
        }

        // ---------------------------------------------------------------- the table

        const float TableW = 460f, RowH = 30f, Head = 46f, Columns = 26f;

        /// <summary>How wide the table is on screen, in pixels: a team game's two sides next to each other, or one above the other.</summary>
        public static float Width(bool stacked = false)
        {
            return (Lobby.TeamGame && !stacked ? TableW * 2f + 16f : TableW) * Scale;
        }

        /// <summary>
        /// The whole table, centred on <paramref name="cx"/> from <paramref name="y"/> (screen pixels); a team game's
        /// sides next to each other, or <paramref name="stacked"/> one above the other. Returns its height in pixels.
        /// </summary>
        public static float Table(float cx, float y, bool stacked = false)
        {
            Styles();
            float s = Scale;
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float vx = cx / s, vy = y / s, h;
            if (Lobby.TeamGame && stacked)
            {
                float h1 = Side(vx - TableW * 0.5f, vy, 1);
                h = h1 + 12f + Side(vx - TableW * 0.5f, vy + h1 + 12f, 2);
            }
            else if (Lobby.TeamGame)
            {
                float h1 = Side(vx - 8f - TableW, vy, 1);
                float h2 = Side(vx + 8f, vy, 2);
                h = Mathf.Max(h1, h2);
            }
            else h = Side(vx - TableW * 0.5f, vy, Teams.None);
            GUI.matrix = keep;
            return h * s;
        }

        /// <summary>One table: a free-for-all's, or one side's under its name and score. Returns its height.</summary>
        static float Side(float x, float y, int team)
        {
            List<Row> rows = Rows(team);
            float h = Head + Columns + Mathf.Max(1, rows.Count) * RowH + 10f;
            Color accent = team != Teams.None ? Teams.Colour(team) : MenuArt.Gold;
            Rect panel = new Rect(x, y, TableW, h);
            bool mine = team != Teams.None && PlayerNet.Local != null && PlayerNet.Local.Team.Value == team;
            Plate(panel, new Color(accent.r, accent.g, accent.b, team == Teams.None || mine ? 0.55f : 0.3f), 1.2f, 14f);

            // the title: a side's name and score, or what a free-for-all is played to
            if (team != Teams.None)
            {
                Label(new Rect(x + 18f, y + 6f, 200f, 34f), Teams.Names[team].ToUpperInvariant() + (mine ? "   (you)" : ""), title, accent);
                teamScore.alignment = TextAnchor.MiddleRight;
                MenuArt.GlowText(new Rect(x, y + 4f, TableW - 18f, 38f), Lobby.Active ? Lobby.Instance.Score(team).ToString() : "0", teamScore, accent, 0.6f);
                teamScore.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                Label(new Rect(x + 18f, y + 6f, 260f, 34f), "SCORES", title, MenuArt.Gold);
                note.alignment = TextAnchor.MiddleRight;
                Label(new Rect(x, y + 6f, TableW - 18f, 34f), Goal, note, new Color(1f, 1f, 1f, 0.6f));
                note.alignment = TextAnchor.MiddleCenter;
            }
            MenuArt.RoundRect(new Rect(x + 14f, y + Head - 2f, TableW - 28f, 1f), new Color(accent.r, accent.g, accent.b, 0.35f), 0f);

            // the columns
            float cy = y + Head;
            header.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            header.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(x + 52f, cy, 200f, Columns), "PLAYER", header);
            header.alignment = TextAnchor.MiddleCenter;
            float kx = x + TableW - 190f;
            GUI.Label(new Rect(kx, cy, 56f, Columns), "KILLS", header);
            GUI.Label(new Rect(kx + 58f, cy, 56f, Columns), "DEATHS", header);
            GUI.Label(new Rect(kx + 116f, cy, 64f, Columns), "K/D", header);

            float ry = cy + Columns;
            if (rows.Count == 0)
                Label(new Rect(x, ry, TableW, RowH), "nobody yet", note, new Color(1f, 1f, 1f, 0.5f));
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                Rect line = new Rect(x + 10f, ry, TableW - 20f, RowH - 3f);
                if (r.Me) MenuArt.RoundRect(line, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.14f), 7f);
                else if (i % 2 == 0) MenuArt.RoundRect(line, new Color(1f, 1f, 1f, 0.035f), 7f);

                // the place: the leader in gold
                Color pc = i == 0 && rows.Count > 1 && r.Kills > 0 ? MenuArt.Gold : new Color(0.7f, 0.72f, 0.78f);
                Label(new Rect(x + 14f, ry, 30f, RowH - 3f), (i + 1).ToString(), cell, pc);

                float nw = Mathf.Min(cellName.CalcSize(new GUIContent(r.Name)).x, kx - x - 120f);
                Color nc = NameColour(r);
                if (r.Down) nc = Color.Lerp(nc, new Color(0.4f, 0.4f, 0.45f), 0.6f);
                Label(new Rect(x + 52f, ry, nw + 4f, RowH - 3f), r.Name, cellName, nc);
                float tx = x + 52f + nw + 10f;
                if (r.Bot) tx = Tag(tx, ry + 7f, "BOT", new Color(0.7f, 0.72f, 0.78f));
                if (r.Down) Tag(tx, ry + 7f, "DOWN", new Color(1f, 0.45f, 0.4f));

                Label(new Rect(kx, ry, 56f, RowH - 3f), r.Kills.ToString(), cell, Color.white);
                Label(new Rect(kx + 58f, ry, 56f, RowH - 3f), r.Deaths.ToString(), cell, new Color(1f, 1f, 1f, 0.75f));
                Label(new Rect(kx + 116f, ry, 64f, RowH - 3f), Ratio(r.Kills, r.Deaths), cell,
                      r.Kills >= r.Deaths && r.Kills > 0 ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 1f, 1f, 0.65f));
                ry += RowH;
            }
            return h;
        }

        /// <summary>A small pill with a word on it; returns where the next one goes.</summary>
        static float Tag(float x, float y, string text, Color c)
        {
            float w = tag.CalcSize(new GUIContent(text)).x + 10f;
            Rect r = new Rect(x, y, w, 15f);
            MenuArt.RoundRect(r, new Color(c.r * 0.2f, c.g * 0.2f, c.b * 0.2f, 0.9f), 7f);
            MenuArt.RoundRect(r, new Color(c.r, c.g, c.b, 0.7f), 7f, 1f);
            tag.normal.textColor = c;
            GUI.Label(r, text, tag);
            return x + w + 5f;
        }
    }
}
