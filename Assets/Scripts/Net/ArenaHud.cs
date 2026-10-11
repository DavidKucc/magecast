using System.Collections.Generic;
using MageCast.Combat;
using Unity.Netcode;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Everything on screen that is about the match rather than about your own casting: the room
    /// code, who is in, the score, names over heads, who killed whom, the respawn wait -- and the Esc
    /// menu, which is the only way out of a game.
    ///
    /// OnGUI, like the rest of the HUD: placeholder until there is an art direction, and nothing here
    /// is worth building a canvas for that would only be thrown away.
    /// </summary>
    public class ArenaHud : MonoBehaviour
    {
        struct Kill
        {
            public string Text;
            public float At;
        }

        static readonly List<Kill> feed = new List<Kill>();

        GUIStyle big, bigLeft, bigRight, mid, small, nameTag, button;
        bool copied;
        float copiedAt;

        static string winner, winnerScore;
        static int winnerTeam;
        static float winnerUntil = -1f;

        /// <summary>
        /// The end of a match, for every screen: who won (a player, or a side in a team game), the final
        /// score, the countdown to the next match or back to the lobby.
        /// </summary>
        public static void ShowWinner(string name, string score, int team, float seconds)
        {
            winner = name;
            winnerScore = score;
            winnerTeam = team;
            winnerUntil = Time.time + seconds;
        }

        void Winner()
        {
            if (Time.time >= winnerUntil) return;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.3f, Screen.width, 150f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            PlayerNet local = PlayerNet.Local;
            bool me = local != null && (winnerTeam != Teams.None ? local.Team.Value == winnerTeam : local.DisplayName == winner);
            string title = me ? (winnerTeam != Teams.None ? "YOUR TEAM WINS" : "YOU WIN") : winner + " WINS";
            Color colour = winnerTeam != Teams.None ? Teams.Colour(winnerTeam)
                         : me ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.5f, 0.45f);
            Shadowed(new Rect(0f, Screen.height * 0.3f + 14f, Screen.width, 44f), title, big, colour);
            Shadowed(new Rect(0f, Screen.height * 0.3f + 66f, Screen.width, 30f), winnerScore, mid, Color.white);
            Shadowed(new Rect(0f, Screen.height * 0.3f + 104f, Screen.width, 26f),
                     (Lobby.Active ? "back to the lobby in " : "next match in ") + Mathf.CeilToInt(winnerUntil - Time.time),
                     small, new Color(1f, 1f, 1f, 0.75f));
        }

        public static void ReportKill(string killer, string victim)
        {
            feed.Add(new Kill {
                Text = string.IsNullOrEmpty(killer) ? victim + " went down" : killer + "  >  " + victim,
                At = Time.time });
            if (feed.Count > 5) feed.RemoveAt(0);
        }

        void OnDisable()
        {
            GameInput.MenuOpen = false;
        }

        bool wasLocked;
        float openedAt = -99f;

        void Update()
        {
            bool locked = WebPointer.IsLocked;

            // Esc in the help goes back to the menu, not out of it
            if (HelpScreen.Open)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) HelpScreen.Open = false;
                if (!GameInput.MenuOpen) HelpScreen.Open = false;
                wasLocked = locked;
                return;
            }

            if (NetSession.IsWeb)
            {
                // In a browser, Esc belongs to the browser: it frees the mouse, and the game may or may
                // not hear the key as well. So the lock being LOST is what opens the menu -- measured
                // from the page itself, not from Unity's idea of it. If the key does arrive too, it must
                // not immediately close the menu it just opened, hence the short grace period.
                if (wasLocked && !locked && PlayerNet.Local != null && !GameInput.MenuOpen) Open();

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (GameInput.MenuOpen) { if (Time.unscaledTime - openedAt > 0.3f) GameInput.MenuOpen = false; }
                    else if (!locked) Open();
                }
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (GameInput.MenuOpen) GameInput.MenuOpen = false; else Open();
            }

            wasLocked = locked;
        }

        void Open()
        {
            GameInput.MenuOpen = true;
            openedAt = Time.unscaledTime;
        }

        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigLeft = new GUIStyle(big) { alignment = TextAnchor.MiddleLeft };
            bigRight = new GUIStyle(big) { alignment = TextAnchor.MiddleRight };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            nameTag = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            button = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            Styles();
            NetSession session = NetSession.Instance;
            NetworkManager nm = NetworkManager.Singleton;

            NameTags();
            TopBar(session, nm);
            KillFeed();
            DeathOverlay();
            Winner();
            if (GameInput.MenuOpen && HelpScreen.Draw()) return;
            if (GameInput.MenuOpen) { Menu(session, nm); BuildInfo.DrawCorner(); }
            else ClickToPlay();
        }

        void TopBar(NetSession session, NetworkManager nm)
        {
            if (session == null || nm == null) return;
            float cx = Screen.width * 0.5f;

            if (session.Current == NetSession.Mode.Training && MatchDirector.BotCount == 0)
            {
                Shadowed(new Rect(cx - 200f, 8f, 400f, 24f), "TRAINING", mid, new Color(1f, 1f, 1f, 0.6f));
                return;
            }

            // the code sits at the top for the host until somebody has used it (a room that has a lobby
            // shows it there)
            bool alone = PlayerNet.All.Count - MatchDirector.BotCount < 2;
            if (session.Current == NetSession.Mode.Host && alone && !Lobby.Active && !string.IsNullOrEmpty(session.JoinCode))
            {
                Box(new Rect(cx - 210f, 6f, 420f, 70f));
                Shadowed(new Rect(cx - 200f, 8f, 400f, 36f), "CODE:  " + session.JoinCode, big, new Color(1f, 0.9f, 0.5f));
                Shadowed(new Rect(cx - 200f, 44f, 400f, 26f), "waiting for an opponent   (Esc - copy the code)", small, Color.white);
                return;
            }

            string ping = "";
            if (!nm.IsServer && nm.IsConnectedClient)
                ping = "ping " + nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId) + " ms";
            // where the Relay server is: a far one is the first thing to suspect when the ping is high
            if (!string.IsNullOrEmpty(session.RelayRegion)) ping += (ping.Length > 0 ? "   " : "") + "(" + session.RelayRegion + ")";

            if (Lobby.TeamGame)
            {
                // the two sides' kills, each in its colour, and the target
                Lobby lobby = Lobby.Instance;
                Box(new Rect(cx - 210f, 6f, 420f, 34f));
                Shadowed(new Rect(cx - 200f, 8f, 170f, 30f), Teams.Names[1].ToUpperInvariant() + "  " + lobby.Score(1),
                         bigRight, Teams.Colour(1));
                Shadowed(new Rect(cx - 30f, 8f, 60f, 30f), ":", big, Color.white);
                Shadowed(new Rect(cx + 30f, 8f, 170f, 30f), lobby.Score(2) + "  " + Teams.Names[2].ToUpperInvariant(),
                         bigLeft, Teams.Colour(2));
                Shadowed(new Rect(cx - 300f, 42f, 600f, 20f), "first team to " + MatchDirector.Target + " kills"
                         + (ping.Length > 0 ? "      " + ping : ""), small, new Color(1f, 1f, 1f, 0.75f));
                return;
            }

            // score: every player, kills and deaths
            var line = new System.Text.StringBuilder();
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (line.Length > 0) line.Append("      ");
                line.Append(p.DisplayName).Append("  ").Append(p.Kills.Value).Append(" / ").Append(p.Deaths.Value);
            }

            Box(new Rect(cx - 300f, 6f, 600f, 30f));
            Shadowed(new Rect(cx - 300f, 8f, 600f, 26f), line + "      (to " + MatchDirector.Target + ")"
                     + (ping.Length > 0 ? "   " + ping : ""), mid, Color.white);
        }

        void NameTags()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (PlayerNet p in PlayerNet.All)
            {
                if (p == PlayerNet.Local) continue;
                Health h = p.GetComponent<Health>();
                if (h != null && h.IsDead) continue;

                Vector3 s = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.95f);
                if (s.z <= 0.1f || s.z > 60f) continue;
                // Not through walls: a name over the cover gives away who is hiding behind it. A
                // teammate's is shown anyway -- where your own side is, you know.
                bool ally = Teams.Allies(PlayerNet.Local, p);
                if (!ally && !Sight.Sees(p.transform)) continue;
                Shadowed(new Rect(s.x - 100f, Screen.height - s.y - 12f, 200f, 24f), p.DisplayName, nameTag,
                         Teams.Colour(p.Team.Value));
            }
        }

        void KillFeed()
        {
            // centred under the score: the top corners belong to your spell readout and the rune sheet
            float y = 82f;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                float age = Time.time - feed[i].At;
                if (age > 6f) continue;
                float a = age < 5f ? 1f : 1f - (age - 5f);
                Shadowed(new Rect(Screen.width * 0.5f - 200f, y, 400f, 22f), feed[i].Text, mid, new Color(1f, 0.85f, 0.8f, a));
                y += 22f;
            }
        }

        /// <summary>A browser only hands the mouse over on a click, so say so rather than sit there.</summary>
        void ClickToPlay()
        {
            if (!GameInput.PointerFree || PlayerNet.Local == null) return;

            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Shadowed(new Rect(0f, Screen.height * 0.45f, Screen.width, 40f), "CLICK TO PLAY", big, Color.white);
            Shadowed(new Rect(0f, Screen.height * 0.45f + 38f, Screen.width, 24f),
                     "Esc gives the mouse back and opens the menu", small, new Color(1f, 1f, 1f, 0.7f));
        }

        void DeathOverlay()
        {
            PlayerNet me = PlayerNet.Local;
            if (me == null || !GameInput.LocalDead) return;

            GUI.color = new Color(0.3f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float left = Mathf.Max(0f, me.RespawnSeconds - (Time.time - me.DiedAt));
            Shadowed(new Rect(0f, Screen.height * 0.4f, Screen.width, 40f), "DOWN", big, new Color(1f, 0.45f, 0.4f));
            Shadowed(new Rect(0f, Screen.height * 0.4f + 40f, Screen.width, 30f),
                     "back in " + Mathf.CeilToInt(left), mid, Color.white);
        }

        bool statsCopied;
        float statsCopiedAt;
        GUIStyle statsStyle;

        /// <summary>
        /// How often your spells landed this session, in the pause menu's top left: the number that says
        /// whether aiming is fun or a chore. Copyable, for passing on.
        /// </summary>
        void Accuracy()
        {
            if (statsStyle == null)
                statsStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false };
            statsStyle.normal.textColor = Color.white;

            string report = Gestures.HitStats.Report();
            int lines = report.Split('\n').Length;
            float pw = Mathf.Min(Screen.width - 24f, 760f), ph = 64f + lines * 18f;
            var panel = new Rect(12f, Screen.height - ph - 12f, pw, ph);   // bottom left, clear of the menu
            Box(panel);
            Shadowed(new Rect(panel.x + 10f, panel.y + 6f, pw, 22f), "ACCURACY, THIS SESSION  (your own shots)", small, new Color(1f, 0.85f, 0.45f));
            GUI.Label(new Rect(panel.x + 10f, panel.y + 28f, pw - 20f, lines * 18f + 4f), report, statsStyle);

            float by = panel.y + ph - 30f;
            if (GUI.Button(new Rect(panel.x + 10f, by, 120f, 24f), "Copy"))
            {
                WebClipboard.Copy(report);
                statsCopied = true;
                statsCopiedAt = Time.time;
            }
            if (GUI.Button(new Rect(panel.x + 140f, by, 120f, 24f), "Reset")) Gestures.HitStats.Reset();
            if (statsCopied && Time.time - statsCopiedAt < 2f)
                Shadowed(new Rect(panel.x + 270f, by, 120f, 24f), "copied", small, new Color(0.6f, 1f, 0.6f));
        }

        void Menu(NetSession session, NetworkManager nm)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = 320f, h = 48f, x = Screen.width * 0.5f - w * 0.5f;
            float y = Mathf.Max(90f, Screen.height * 0.5f - 210f);

            Shadowed(new Rect(0f, y - 60f, Screen.width, 40f), "PAUSED", big, Color.white);
            Shadowed(new Rect(0f, y - 26f, Screen.width, 24f),
                     "the game keeps running - you can still be hit", small, new Color(1f, 1f, 1f, 0.7f));

            if (GUI.Button(new Rect(x, y, w, h), "Resume", button)) GameInput.MenuOpen = false;
            y += h + 10f;

            if (GUI.Button(new Rect(x, y, w, h), "How to play", button)) HelpScreen.Open = true;
            y += h + 10f;

            // settings live in the pause menu too, so they can be tuned without leaving a game
            Box(new Rect(x - 10f, y - 4f, w + 20f, GameSettings.Height));
            y = GameSettings.Draw(x, y, w) + 8f;

            if (session != null && !string.IsNullOrEmpty(session.JoinCode) && session.Current == NetSession.Mode.Host)
            {
                if (GUI.Button(new Rect(x, y, w, h), "Copy code  " + session.JoinCode, button))
                {
                    WebClipboard.Copy(session.JoinCode);
                    copied = true;
                    copiedAt = Time.time;
                }
                if (copied && Time.time - copiedAt < 2f)
                    Shadowed(new Rect(x + w + 10f, y, 200f, h), "copied", mid, new Color(0.6f, 1f, 0.6f));
                y += h + 10f;

                // On the web the whole invitation fits in a link: open it and you are in the room.
                if (NetSession.IsWeb)
                {
                    if (GUI.Button(new Rect(x, y, w, h), "Copy invite link", button))
                    {
                        WebClipboard.Copy(NetSession.InviteLink(session.JoinCode));
                        copied = true;
                        copiedAt = Time.time;
                    }
                    y += h + 10f;
                }
            }

            // The leader can end the match early and take everyone back to the lobby -- to change the map
            // or the sides without closing the room.
            if (nm != null && nm.IsServer && Lobby.Active && Lobby.Instance.Playing.Value)
            {
                if (GUI.Button(new Rect(x, y, w, h), "Back to the lobby (ends the match)", button))
                {
                    GameInput.MenuOpen = false;
                    Lobby.Instance.BackToLobby();
                }
                y += h + 10f;
            }

            // Bots in training: only whoever runs the game can add them (they live on the server). A hosted
            // room adds its bots in the lobby, on a side.
            if (nm != null && nm.IsServer && session != null && session.Current == NetSession.Mode.Training)
            {
                float bw = (w - 10f) / 2f;
                if (GUI.Button(new Rect(x, y, bw, 34f), "Add bot", button)) MatchDirector.SpawnBot();
                int bots = MatchDirector.BotCount;
                GUI.enabled = bots > 0;
                if (GUI.Button(new Rect(x + bw + 10f, y, bw, 34f), "Remove bots" + (bots > 0 ? " (" + bots + ")" : ""), button))
                    MatchDirector.RemoveBots();
                GUI.enabled = true;
                y += 44f;
                BotBrain.IgnoreHumans = GUI.Toggle(new Rect(x, y, w, 24f), BotBrain.IgnoreHumans,
                                                   "  bots leave me alone (watch them fight each other)");
                y += 30f;
                GUI.Label(new Rect(x, y, 120f, 22f), "bot skill " + Mathf.RoundToInt(BotBrain.Skill * 100f) + "%");
                BotBrain.Skill = GUI.HorizontalSlider(new Rect(x + 120f, y + 6f, w - 120f, 16f), BotBrain.Skill, 0f, 1f);
                y += 32f;
            }

            string leave = session != null && session.Current == NetSession.Mode.Host
                         ? "End the game (everyone leaves)" : "Leave to menu";
            if (GUI.Button(new Rect(x, y, w, h), leave, button) && session != null) session.Leave();
            y += h + 10f;

            // a web page cannot close itself; the tab is the way out
            if (!NetSession.IsWeb)
            {
                if (GUI.Button(new Rect(x, y, w, h), "Quit game", button)) Application.Quit();
                y += h + 10f;
            }
            y += 10f;

            Accuracy();

            if (nm != null)
            {
                // who is in, with kills and deaths -- by side in a team game
                for (int side = Lobby.TeamGame ? 1 : 0; side <= (Lobby.TeamGame ? 2 : 0); side++)
                {
                    string who = "";
                    foreach (PlayerNet p in PlayerNet.All)
                    {
                        if (side != Teams.None && p.Team.Value != side) continue;
                        who += (who.Length > 0 ? ",   " : "") + p.DisplayName + " " + p.Kills.Value + "/" + p.Deaths.Value;
                    }
                    string label = side == Teams.None ? "in the game: " : Teams.Names[side] + ": ";
                    Shadowed(new Rect(0f, y, Screen.width, 22f), label + who, small,
                             side == Teams.None ? Color.white : Teams.Colour(side));
                    y += 22f;
                }
            }
        }

        static void Box(Rect r)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
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
