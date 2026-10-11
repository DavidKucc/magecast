using Unity.Netcode;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// The lobby on screen: the room code, the map (with a picture of each), the mode and the kill target
    /// on the left; who is in, on which side, and who is ready on the right; Leave and Ready -- or Start,
    /// for the host -- along the bottom.
    ///
    /// Only the host changes the settings; everyone else sees them change. A change makes everybody say
    /// they are ready again, so nobody is started into a map they did not agree to.
    ///
    /// OnGUI like the rest of the menus, and no glyphs beyond plain letters: a browser build has no
    /// system fonts to fall back on, and a tick mark there is an empty box.
    /// </summary>
    public class LobbyScreen : MonoBehaviour
    {
        /// <summary>One line per map, under its picture, in the order of NetSession.ArenaScenes.</summary>
        public static readonly string[] MapBlurbs =
        {
            "the original test arena: compact, lots of cover, 1v1 lanes",
            "altar, towers and a sunken tomb: a fight on three levels",
            "two bridges over a riverbed: flank below or hold the decks",
            "a grassy valley ringed by mountains, ruined towers: four times the floor"
        };

        static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        static readonly Color ReadyGreen = new Color(0.55f, 1f, 0.55f);

        GUIStyle title, header, label, small, rowName, rowTag, button, chosen, bigButton, smallButton, status, codeStyle;
        Texture2D[] previews;
        bool showSettings, copied;
        float copiedAt;

        int seenMap = -1, seenMode = -1, seenKills = -1;
        string notice;
        float noticeUntil;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameInput.MenuOpen = false;
            GameInput.LocalDead = false;
            GameInput.MatchOverUntil = -1f;

            previews = new Texture2D[NetSession.ArenaScenes.Length];
            for (int i = 0; i < previews.Length; i++)
                previews[i] = Resources.Load<Texture2D>("MapPreviews/" + NetSession.ArenaScenes[i]);
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            rowName = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            rowTag = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            chosen = new GUIStyle(button);
            chosen.normal.textColor = chosen.hover.textColor = chosen.active.textColor = Gold;
            bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            smallButton = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            status = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            codeStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        }

        void OnGUI()
        {
            Styles();
            if (HelpScreen.Draw()) return;

            NetSession session = NetSession.Instance;
            NetworkManager nm = NetworkManager.Singleton;
            Lobby lobby = Lobby.Instance;
            bool leader = nm != null && nm.IsServer;

            Background(lobby);
            BuildInfo.DrawCorner();

            float W = Mathf.Min(Screen.width - 32f, 1180f);
            float X = (Screen.width - W) * 0.5f;

            Shadowed(new Rect(X, 14f, W * 0.5f, 54f), "LOBBY", title, Gold);
            if (session != null && !string.IsNullOrEmpty(session.JoinCode)) RoomCode(session, X + W, 14f);

            if (lobby == null || !lobby.IsSpawned)
            {
                Shadowed(new Rect(0f, Screen.height * 0.45f, Screen.width, 30f), "joining the room...", status, Color.white);
                Footer(session, null, false, X, W);
                return;
            }

            WatchSettings(lobby, leader);

            float top = 86f;
            float bottom = Screen.height - 84f;
            bool wide = Screen.width >= 900f;
            float leftW = wide ? W * 0.56f : W;
            float rightX = wide ? X + leftW + 24f : X;
            float rightW = wide ? W - leftW - 24f : W;

            float y = top;
            if (showSettings)
            {
                Box(new Rect(X - 10f, y - 6f, leftW + 20f, GameSettings.Height + 12f));
                y = GameSettings.Draw(X, y, leftW);
            }
            else y = Settings(lobby, leader, X, y, leftW);

            Players(lobby, leader, nm, rightX, wide ? top : y + 16f, rightW, bottom);
            Footer(session, lobby, leader, X, W);
        }

        // ---------------------------------------------------------------- the picture behind it all

        void Background(Lobby lobby)
        {
            int map = lobby != null && lobby.IsSpawned ? lobby.Map.Value : NetSession.SelectedArena;
            Texture2D tex = previews != null && map >= 0 && map < previews.Length ? previews[map] : null;
            if (tex != null) GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), tex, ScaleMode.ScaleAndCrop);
            GUI.color = new Color(0.03f, 0.035f, 0.05f, tex != null ? 0.72f : 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void RoomCode(NetSession session, float right, float y)
        {
            float w = 300f;
            Rect r = new Rect(right - w, y, w, 34f);
            Shadowed(new Rect(r.x, y - 4f, w, 18f), "room code - send it to friends", rowTag, new Color(1f, 1f, 1f, 0.6f));
            Shadowed(new Rect(r.x, y + 12f, w, 36f), session.JoinCode, codeStyle, Gold);

            float bw = NetSession.IsWeb ? 110f : 0f;
            float by = y + 50f;
            if (GUI.Button(new Rect(right - 110f - (bw > 0f ? bw + 8f : 0f), by, 110f, 24f), "Copy code", smallButton))
            {
                WebClipboard.Copy(session.JoinCode);
                copied = true;
                copiedAt = Time.time;
            }
            if (NetSession.IsWeb && GUI.Button(new Rect(right - bw, by, bw, 24f), "Copy invite link", smallButton))
            {
                WebClipboard.Copy(NetSession.InviteLink(session.JoinCode));
                copied = true;
                copiedAt = Time.time;
            }
            if (copied && Time.time - copiedAt < 2f)
                Shadowed(new Rect(right - 400f, by, 160f, 24f), "copied", rowTag, ReadyGreen);
        }

        /// <summary>Somebody else changed the settings: say so, since it also took your ready away.</summary>
        void WatchSettings(Lobby lobby, bool leader)
        {
            int map = lobby.Map.Value, mode = lobby.ModeIndex.Value, kills = lobby.KillsToWin.Value;
            if (seenMap >= 0 && !leader && (map != seenMap || mode != seenMode || kills != seenKills))
            {
                notice = "the host changed the match settings - press Ready again when you are happy with them";
                noticeUntil = Time.time + 5f;
            }
            seenMap = map;
            seenMode = mode;
            seenKills = kills;
        }

        // ---------------------------------------------------------------- map, mode, kills

        float Settings(Lobby lobby, bool leader, float x, float y, float w)
        {
            Shadowed(new Rect(x, y, w, 22f), "MAP" + (leader ? "" : "   (the host chooses)"), header, Gold);
            y += 26f;

            int maps = NetSession.ArenaScenes.Length;
            float gap = 10f;
            float cw = (w - gap * (maps - 1)) / maps;
            float ih = cw * 9f / 16f;
            for (int i = 0; i < maps; i++)
            {
                Rect card = new Rect(x + i * (cw + gap), y, cw, ih + 26f);
                bool on = lobby.Map.Value == i;
                if (on)
                {
                    GUI.color = Gold;
                    GUI.DrawTexture(new Rect(card.x - 3f, card.y - 3f, card.width + 6f, card.height + 6f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                Box(card, 0.75f);
                Rect image = new Rect(card.x, card.y, cw, ih);
                if (previews[i] != null) GUI.DrawTexture(image, previews[i], ScaleMode.ScaleAndCrop);
                if (!on)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.4f);
                    GUI.DrawTexture(image, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                Shadowed(new Rect(card.x + 8f, card.y + ih + 2f, cw - 16f, 22f), NetSession.ArenaTitles[i], header,
                         on ? Gold : Color.white);
                if (leader && !on && GUI.Button(card, GUIContent.none, GUIStyle.none)) lobby.SetMap(i);
            }
            y += ih + 34f;
            Shadowed(new Rect(x, y, w, 22f), MapBlurbs[Mathf.Clamp(lobby.Map.Value, 0, MapBlurbs.Length - 1)], label,
                     new Color(1f, 1f, 1f, 0.8f));
            y += 36f;

            Shadowed(new Rect(x, y, w, 22f), "MODE", header, Gold);
            y += 26f;
            float mw = (w - 10f) / 2f;
            GUI.enabled = leader;
            for (int i = 0; i < Lobby.ModeTitles.Length; i++)
            {
                bool on = lobby.ModeIndex.Value == i;
                if (Choice(new Rect(x + i * (mw + 10f), y, mw, 40f), on, Lobby.ModeTitles[i])) lobby.SetMode((Lobby.GameMode)i);
            }
            GUI.enabled = true;
            y += 46f;
            Shadowed(new Rect(x, y, w, 22f), Lobby.ModeBlurbs[lobby.ModeIndex.Value], label, new Color(1f, 1f, 1f, 0.8f));
            y += 34f;

            Shadowed(new Rect(x, y, w, 22f), lobby.Mode == Lobby.GameMode.Teams ? "KILLS FOR A TEAM TO WIN" : "KILLS TO WIN",
                     header, Gold);
            y += 26f;
            int n = Lobby.KillChoices.Length;
            float kw = (w - 10f * (n - 1)) / n;
            GUI.enabled = leader;
            for (int i = 0; i < n; i++)
            {
                int k = Lobby.KillChoices[i];
                bool on = lobby.KillsToWin.Value == k;
                if (Choice(new Rect(x + i * (kw + 10f), y, kw, 34f), on, k.ToString())) lobby.SetKillsToWin(k);
            }
            GUI.enabled = true;
            y += 40f;
            return y;
        }

        // ---------------------------------------------------------------- who is in

        void Players(Lobby lobby, bool leader, NetworkManager nm, float x, float y, float w, float bottom)
        {
            ulong me = nm != null ? nm.LocalClientId : 0;
            Shadowed(new Rect(x, y, w, 22f), "PLAYERS   " + lobby.Members.Count + " / " + Lobby.MaxPlayers, header, Gold);
            y += 28f;

            if (lobby.Mode == Lobby.GameMode.Teams)
            {
                for (int side = 1; side <= 2; side++)
                    y = Side(lobby, leader, me, side, x, y, w) + 12f;
            }
            else
            {
                float h = 12f + Mathf.Max(1, lobby.Members.Count) * 36f;
                Box(new Rect(x, y, w, h));
                float ry = y + 6f;
                for (int i = 0; i < lobby.Members.Count; i++) { Row(lobby, leader, me, lobby.Members[i], x + 8f, ry, w - 16f); ry += 36f; }
                y += h + 8f;
                if (leader)
                {
                    GUI.enabled = lobby.Members.Count < Lobby.MaxPlayers;
                    if (GUI.Button(new Rect(x, y, 140f, 30f), "Add a bot", button)) lobby.AddBot();
                    GUI.enabled = true;
                    y += 38f;
                }
            }

            if (lobby.BotCount > 0 || leader)
            {
                GUI.Label(new Rect(x, y, 150f, 22f), "bot skill " + Mathf.RoundToInt(lobby.BotSkill.Value * 100f) + "%", small);
                if (leader)
                {
                    float s = GUI.HorizontalSlider(new Rect(x + 150f, y + 6f, Mathf.Min(220f, w - 160f), 16f), lobby.BotSkill.Value, 0f, 1f);
                    if (Mathf.Abs(s - lobby.BotSkill.Value) > 0.005f) lobby.SetBotSkill(s);
                }
            }
        }

        float Side(Lobby lobby, bool leader, ulong me, int side, float x, float y, float w)
        {
            Color c = Teams.Colour(side);
            int count = lobby.Count(side);
            float h = 44f + Mathf.Max(1, count) * 36f;

            Box(new Rect(x, y, w, h));
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, 4f, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Shadowed(new Rect(x + 14f, y + 6f, 200f, 26f), Teams.Names[side].ToUpperInvariant() + "   " + count + " / " + Teams.PerTeam,
                     header, c);

            float bx = x + w - 8f;
            int mine = lobby.IndexOfClient(me);
            bool here = mine >= 0 && lobby.Members[mine].Team == side;
            if (!here && mine >= 0)
            {
                GUI.enabled = count < Teams.PerTeam;
                if (GUI.Button(new Rect(bx - 90f, y + 6f, 90f, 26f), "Join " + Teams.Names[side], smallButton)) lobby.ChooseTeam(side);
                GUI.enabled = true;
                bx -= 98f;
            }
            if (leader)
            {
                GUI.enabled = count < Teams.PerTeam && lobby.Members.Count < Lobby.MaxPlayers;
                if (GUI.Button(new Rect(bx - 80f, y + 6f, 80f, 26f), "+ bot", smallButton)) lobby.AddBot(side);
                GUI.enabled = true;
            }

            float ry = y + 40f;
            for (int i = 0; i < lobby.Members.Count; i++)
            {
                if (lobby.Members[i].Team != side) continue;
                Row(lobby, leader, me, lobby.Members[i], x + 14f, ry, w - 22f);
                ry += 36f;
            }
            if (count == 0) Shadowed(new Rect(x + 14f, ry, w - 28f, 30f), "nobody yet", small, new Color(1f, 1f, 1f, 0.5f));
            return y + h;
        }

        void Row(Lobby lobby, bool leader, ulong me, LobbyMember m, float x, float y, float w)
        {
            bool host = !m.IsBot && m.ClientId == NetworkManager.ServerClientId;
            bool isMe = !m.IsBot && m.ClientId == me;
            Color nameColour = isMe ? Gold : Color.white;
            float tagW = 150f;

            if (leader && m.IsBot)
            {
                // a bot's line: its name, and the host's buttons to move it or take it out
                float bx = x + w;
                if (GUI.Button(new Rect(bx - 70f, y + 3f, 70f, 26f), "remove", smallButton)) { lobby.RemoveBot(m.BotNumber); return; }
                bx -= 76f;
                if (lobby.Mode == Lobby.GameMode.Teams)
                {
                    int other = m.Team == 1 ? 2 : 1;
                    GUI.enabled = lobby.Count(other) < Teams.PerTeam;
                    if (GUI.Button(new Rect(bx - 70f, y + 3f, 70f, 26f), "to " + Teams.Names[other], smallButton)) lobby.SwapBot(m.BotNumber);
                    GUI.enabled = true;
                    bx -= 76f;
                }
                tagW = x + w - bx;
            }

            Shadowed(new Rect(x, y, w - tagW, 32f), Lobby.NameOf(m) + (isMe ? "  (you)" : ""), rowName, nameColour);
            if (leader && m.IsBot) return;

            string tag;
            Color tagColour;
            if (m.IsBot) { tag = "BOT"; tagColour = new Color(1f, 1f, 1f, 0.55f); }
            else if (host) { tag = "HOST"; tagColour = Gold; }
            else if (m.Ready) { tag = "READY"; tagColour = ReadyGreen; }
            else { tag = "not ready"; tagColour = new Color(1f, 1f, 1f, 0.45f); }
            Shadowed(new Rect(x + w - tagW, y, tagW, 32f), tag, rowTag, tagColour);
        }

        // ---------------------------------------------------------------- leave, ready, start

        void Footer(NetSession session, Lobby lobby, bool leader, float x, float w)
        {
            float y = Screen.height - 70f;
            Box(new Rect(0f, y - 10f, Screen.width, 80f), 0.5f);

            if (GUI.Button(new Rect(x, y, 150f, 50f), leader ? "Close room" : "Leave", button) && session != null)
                session.Leave();
            if (GUI.Button(new Rect(x + 160f, y, 130f, 24f), "How to play", smallButton)) HelpScreen.Open = true;
            if (GUI.Button(new Rect(x + 160f, y + 26f, 130f, 24f), showSettings ? "hide settings" : "Settings", smallButton))
                showSettings = !showSettings;

            if (lobby == null) return;

            float bw = 260f;
            Rect main = new Rect(x + w - bw, y, bw, 50f);
            string say;
            Color sayColour = new Color(1f, 1f, 1f, 0.8f);
            if (leader)
            {
                string why;
                bool can = lobby.CanStart(out why);
                GUI.enabled = can;
                if (GUI.Button(main, "START MATCH", bigButton)) lobby.StartMatch();
                GUI.enabled = true;
                say = can ? "everyone is ready - start when you are" : why;
                if (can) sayColour = ReadyGreen;
            }
            else
            {
                int mine = lobby.IndexOfClient(NetworkManager.Singleton.LocalClientId);
                bool ready = mine >= 0 && lobby.Members[mine].Ready;
                GUI.backgroundColor = ready ? ReadyGreen : Color.white;
                if (GUI.Button(main, ready ? "READY" : "I'M READY", bigButton)) lobby.SetReady(!ready);
                GUI.backgroundColor = Color.white;
                say = ready ? "waiting for the host to start  (click again to take it back)"
                            : lobby.Mode == Lobby.GameMode.Teams ? "pick a side, then say you are ready" : "say you are ready";
                if (ready) sayColour = ReadyGreen;
            }

            if (notice != null && Time.time < noticeUntil) { say = notice; sayColour = new Color(1f, 0.75f, 0.6f); }
            float sx = x + 300f, sw = w - 300f - bw - 10f;
            if (sw > 120f) Shadowed(new Rect(sx, y, sw, 50f), say, status, sayColour);
        }

        // ---------------------------------------------------------------- drawing helpers

        /// <summary>One of a row of choices: the chosen one in gold, so it reads from across the room. True on a click.</summary>
        bool Choice(Rect r, bool on, string text)
        {
            GUIStyle style = on ? chosen : button;
            Color keepBg = GUI.backgroundColor;
            if (on) GUI.backgroundColor = Gold;
            bool clicked = GUI.Button(r, text, style) && !on;
            GUI.backgroundColor = keepBg;
            return clicked;
        }

        static void Box(Rect r, float alpha = 0.55f)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static void Shadowed(Rect r, string text, GUIStyle style, Color colour)
        {
            Color keep = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * colour.a);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(r, text, style);
            style.normal.textColor = keep;
        }
    }
}
