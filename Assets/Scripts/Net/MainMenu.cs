using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// The first screen: a name, training, host a game, find one in the list of open games (RoomBrowser),
    /// join one by code.
    ///
    /// Drawn over a live backdrop (MenuBackdrop): the map you would train on drifting behind it, embers
    /// that shy away from the cursor, and a canvas to draw runes on with the mouse. The controls answer
    /// the mouse too (MenuArt): buttons lift and glow under it and ring where they are clicked, map cards
    /// grow and show the map behind the whole menu, and the five runes under the title light up -- and
    /// draw themselves on the backdrop when clicked.
    ///
    /// Laid out in its own units and scaled to the window, so it fits a short browser tab and does not
    /// float tiny in the middle of a big screen.
    ///
    /// The LAN row is tucked away on purpose. It is the fallback for when Relay is not available, and
    /// a tester who sees an "IP address" box first will type something into it.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        string code = "";
        string address = "127.0.0.1";
        bool showLan;
        bool showSettings;
        GUIStyle title, subtitle, caps, field, button, bigButton, smallButton, link, status, cardTitle, blurb, runeName;

        MenuBackdrop backdrop;
        int hoverMap = -1;

        const float Width = 420f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameInput.MenuOpen = false;
            GameInput.LocalDead = false;
            backdrop = new MenuBackdrop();

            NetSession session = NetSession.Ensure();
            if (session == null) return;
            session.HandleCommandLine();

            // Opened from an invite link: straight into the room, once. Only on the first visit to the
            // menu -- coming back here after leaving must not throw you back into the same game. With no
            // name yet, the code is filled in and the name asked for first: joining nameless made you
            // "Player 2" for everyone.
            string invited = NetSession.CodeFromPageAddress();
            if (invited != null && !inviteUsed)
            {
                inviteUsed = true;
                code = invited;
                if (NetSession.SafeName(NetSession.PlayerName).Length > 0) session.JoinOnline(invited);
                else askName = true;
            }
        }

        static bool inviteUsed;

        /// <summary>Came from an invite link with no name: the name box is pointed at until it is filled.</summary>
        bool askName;

        /// <summary>
        /// A room picked from Find a game. Joining without a name made you "Player 2" for everyone, so with
        /// none yet the list closes, the code goes in its box and the name box is pointed at.
        /// </summary>
        void JoinListed(NetSession session, string roomCode)
        {
            code = roomCode;
            if (NetSession.SafeName(NetSession.PlayerName).Length == 0)
            {
                RoomBrowser.Hide();
                askName = true;
                return;
            }
            RoomBrowser.Hide();
            session.JoinOnline(roomCode);
        }

        /// <summary>For the web page (unityInstance.SendMessage("Menu", "JoinFromPage", code)).</summary>
        public void JoinFromPage(string roomCode)
        {
            code = roomCode ?? "";
            if (NetSession.Instance != null) NetSession.Instance.JoinOnline(code);
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 78, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            subtitle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            caps = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            caps.normal.textColor = new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.8f);
            // a label's style, editable: no box of its own -- Plate draws the box
            field = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            field.normal.textColor = field.hover.textColor = field.focused.textColor = field.active.textColor = Color.white;
            button = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bigButton = new GUIStyle(button) { fontSize = 24 };
            smallButton = new GUIStyle(button) { fontSize = 14 };
            link = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            status = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
            cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            blurb = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperCenter, wordWrap = true };
            blurb.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            runeName = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            GUI.skin.settings.cursorColor = MenuArt.Gold;
            GUI.skin.settings.selectionColor = new Color(1f, 0.82f, 0.45f, 0.35f);
        }

        /// <summary>The column's height, in the menu's own units, for what is open right now.</summary>
        float ContentHeight()
        {
            if (RoomBrowser.Open) return 222f + RoomBrowser.Height + 50f;
            float h = 222f + 88f + 158f + 152f + 26f + 70f + 98f + 42f + 50f;
            if (showSettings) h += GameSettings.Height + 24f;
            if (showLan) h += 46f;
            return h;
        }

        void OnGUI()
        {
            Styles();
            if (backdrop == null) backdrop = new MenuBackdrop();
            NetSession session = NetSession.Instance;
            bool busy = session == null || session.Busy;
            Event e = Event.current;
            bool repaint = e.type == EventType.Repaint;

            // laid out in its own units, scaled to fit the window
            float contentH = ContentHeight();
            float s = Mathf.Clamp(Mathf.Min(Screen.height / (contentH + 60f), Screen.width / 560f), 0.5f, 1.25f);
            float VW = Screen.width / s, VH = Screen.height / s;
            float w = Width;
            float x = (VW - w) * 0.5f;
            float y = Mathf.Max(20f, (VH - contentH) * 0.5f);
            Rect column = new Rect(x - 36f, y - 10f, w + 72f, contentH + 20f);
            Rect blocked = new Rect(column.x * s, column.y * s, column.width * s, column.height * s);

            Rect everything = new Rect(0f, 0f, Screen.width, Screen.height);
            backdrop.Draw(hoverMap >= 0 ? hoverMap : NetSession.SelectedArena, HelpScreen.Open ? everything : blocked);
            if (repaint) hoverMap = -1;

            // the help covers the menu and takes its clicks, so nothing underneath is drawn meanwhile
            if (HelpScreen.Draw()) return;

            // Connecting, then loading the map: the loading screen, over everything -- until this menu is
            // gone with its scene, not only while the session says it is busy, or the menu would flash
            // back for the moment between the connection and the next scene.
            Unity.Netcode.NetworkManager nm = Unity.Netcode.NetworkManager.Singleton;
            if (busy || (nm != null && nm.IsListening))
            {
                string said = session != null ? session.Status ?? "" : "";
                MenuArt.Loading(said.StartsWith("join") ? "JOINING" : "LOADING",
                                said.Length > 0 ? said.TrimEnd('.') : "loading " + NetSession.ArenaTitles[NetSession.SelectedArena],
                                MapPicture(NetSession.SelectedArena));
                MenuArt.EndFrame();
                return;
            }
            BuildInfo.DrawCorner();

            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            // a pane of dark glass behind the column, so it reads over any map
            if (repaint)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(MenuArt.Grow(column, 90f), MenuArt.Soft);
                GUI.color = Color.white;
                MenuArt.RoundRect(column, new Color(0.035f, 0.04f, 0.06f, 0.7f), 18f);
                MenuArt.RoundRect(column, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.16f), 18f, 1.2f);
            }

            y = Title(x, y, w, s);

            GUI.enabled = !busy;
            if (RoomBrowser.Open)
            {
                string chosen = RoomBrowser.Draw(x, y, w, out y);
                if (chosen != null) JoinListed(session, chosen);
            }
            else
            {
                y = NameField(x, y, w);
                y = Maps(x, y, w);

                if (MenuArt.Button(new Rect(x, y, w, 58f), "Training", bigButton, true)) session.StartTraining(true);
                y += 58f + 12f;
                if (MenuArt.Button(new Rect(x, y, w, 58f), "Host a game", bigButton)) session.HostOnline();
                y += 58f;
                // whether a hosted game goes into everyone's Find a game, or is reached only by its code
                if (MenuArt.Link(new Rect(x, y, w, 24f), PublicRoom.Listed ? "open: anyone can join from Find a game   (click: private)"
                                                                           : "private: only with the code   (click: open to anyone)", link))
                    PublicRoom.Listed = !PublicRoom.Listed;
                y += 26f + 12f;
                if (MenuArt.Button(new Rect(x, y, w, 58f), "Find a game", bigButton)) RoomBrowser.Show();
                y += 58f + 22f;

                y = JoinRow(session, x, y, w);
                y = Links(session, x, y, w);
            }

            GUI.enabled = true;

            if (session != null && !string.IsNullOrEmpty(session.Status))
            {
                float pulse = busy ? 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
                Color c = busy ? new Color(1f, 1f, 1f, 0.9f * pulse) : new Color(1f, 0.75f, 0.6f);
                MenuArt.GlowText(new Rect(VW * 0.5f - 350f, y + 4f, 700f, 60f), session.Status, status, c, 0f);
            }

            GUI.matrix = Matrix4x4.identity;
            MenuArt.EndFrame();

            // Enter in the code box joins, since that is what everyone will try first
            if (!busy && !RoomBrowser.Open && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                && code.Trim().Length > 0)
            {
                session.JoinOnline(code);
                e.Use();
            }
        }

        // ---------------------------------------------------------------- the parts

        float Title(float x, float y, float w, float s)
        {
            float t = Time.unscaledTime;
            float glow = 1.1f + 0.35f * Mathf.Sin(t * 1.6f);
            MenuArt.GlowText(new Rect(x - 100f, y, w + 200f, 88f), "MAGE CAST", title, MenuArt.Gold, glow);
            y += 84f;

            if (Event.current.type == EventType.Repaint)
            {
                // a gold rule under it, brightest in the middle
                float cx = x + w * 0.5f;
                MenuArt.Line(new Vector2(cx - 170f, y + 2f), new Vector2(cx - 14f, y + 2f), 2f, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.55f));
                MenuArt.Line(new Vector2(cx + 14f, y + 2f), new Vector2(cx + 170f, y + 2f), 2f, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.55f));
                MenuArt.Glow(new Vector2(cx, y + 2f), 22f, MenuArt.Gold);
            }
            y += 8f;
            GUI.Label(new Rect(x - 100f, y, w + 200f, 24f), "draw the rune, aim, send", subtitle);
            y += 34f;

            y = Runes(x, y, w, s);
            return y + 14f;
        }

        /// <summary>The six runes, in their colours: under the mouse they light up, clicked they draw themselves.</summary>
        float Runes(float x, float y, float w, float s)
        {
            int n = MenuArt.RuneNames.Length;
            float size = 46f, gap = 16f;
            float rx = x + (w - (n * size + (n - 1) * gap)) * 0.5f;
            int over = -1;
            for (int i = 0; i < n; i++)
            {
                Rect r = new Rect(rx + i * (size + gap), y, size, size);
                bool on = GUI.enabled && r.Contains(Event.current.mousePosition);
                float h = MenuArt.Hover(MenuArt.Key(MenuArt.RuneNames[i], r), on);
                Color c = MenuArt.ElementColours[i];
                if (on) over = i;
                if (Event.current.type == EventType.Repaint)
                {
                    Rect plate = MenuArt.Grow(r, h * 3f);
                    plate.y -= h * 3f;
                    if (h > 0.01f) MenuArt.Glow(plate.center, size * 2.6f, new Color(c.r, c.g, c.b, 0.3f * h));
                    MenuArt.RoundRect(plate, new Color(0.06f, 0.065f, 0.09f, 0.8f), 9f);
                    MenuArt.RoundRect(plate, new Color(c.r, c.g, c.b, 0.25f + 0.65f * h), 9f, 1.2f + h);
                    MenuArt.Rune(MenuArt.RuneNames[i], MenuArt.Grow(plate, -10f), new Color(c.r, c.g, c.b, 0.7f + 0.3f * h), 3f + h, h);
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    MenuArt.Ripple(GUI.matrix.MultiplyPoint3x4(Event.current.mousePosition), c);
                    backdrop.Demonstrate(i, DemoBox(x, w, s));
                }
            }
            y += size + 6f;

            string hint = over >= 0 ? MenuArt.Elements[over] + "  -  click to see it drawn"
                                    : "draw a rune anywhere on the background";
            runeName.normal.textColor = over >= 0 ? MenuArt.ElementColours[over] : new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(x - 60f, y, w + 120f, 20f), hint, runeName);
            return y + 22f;
        }

        /// <summary>
        /// Where a rune is drawn for you: beside the menu, on whichever side has more room, as big as that
        /// room allows -- behind the menu it would not be seen. Only on a screen with no room either side
        /// does it go over the top of it.
        /// </summary>
        Rect DemoBox(float x, float w, float s)
        {
            float left = (x - 36f) * s, right = (x + w + 36f) * s;
            float roomRight = Screen.width - right, room = Mathf.Max(left, roomRight);
            float size = Mathf.Min(260f, Screen.height * 0.4f, room * 0.8f);
            Vector2 c;
            if (size >= 110f) c = new Vector2(roomRight >= left ? right + roomRight * 0.5f : left * 0.5f, Screen.height * 0.45f);
            else
            {
                size = Mathf.Min(260f, Screen.height * 0.4f);
                c = new Vector2(Screen.width * 0.5f, Screen.height * 0.3f);
            }
            return new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);
        }

        float NameField(float x, float y, float w)
        {
            bool nameless = NetSession.SafeName(NetSession.PlayerName).Length == 0;
            bool point = askName && nameless;
            GUI.Label(new Rect(x + 2f, y, w, 20f), point ? "YOUR NAME  -  type it, then Join below" : "YOUR NAME", caps);
            y += 22f;
            Rect r = new Rect(x, y, w, 46f);
            Plate(r, "name", point ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f) : 0f);
            GUI.SetNextControlName("name");
            string name = GUI.TextField(r, NetSession.PlayerName, 12, field);
            if (name != NetSession.PlayerName) NetSession.PlayerName = name;
            return y + 46f + 20f;
        }

        /// <summary>The box behind a text field: gold at the edge under the mouse, brighter while typing in it.</summary>
        void Plate(Rect r, string control, float insist)
        {
            bool over = GUI.enabled && r.Contains(Event.current.mousePosition);
            float h = MenuArt.Hover(MenuArt.Key(control, r), over);
            if (Event.current.type != EventType.Repaint) return;
            bool focused = GUI.GetNameOfFocusedControl() == control;
            if (insist > 0f) MenuArt.Glow(r.center, r.width * 1.3f, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.25f * insist));
            MenuArt.RoundRect(r, new Color(0.03f, 0.035f, 0.05f, 0.85f), 9f);
            float edge = Mathf.Max(focused ? 0.95f : 0.25f + 0.45f * h, insist);
            MenuArt.RoundRect(r, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, edge * (GUI.enabled ? 1f : 0.4f)), 9f, focused ? 2f : 1.3f);
        }

        /// <summary>The training maps as cards with their pictures; pointing at one shows it behind the menu.</summary>
        float Maps(float x, float y, float w)
        {
            GUI.Label(new Rect(x + 2f, y, w, 20f), "TRAINING MAP   (online: chosen in the lobby)", caps);
            y += 24f;
            int maps = NetSession.ArenaScenes.Length;
            float gap = 10f;
            float cw = (w - gap * (maps - 1)) / maps, ch = 92f;
            int shownBlurb = NetSession.SelectedArena;
            for (int i = 0; i < maps; i++)
            {
                Rect r = new Rect(x + i * (cw + gap), y, cw, ch);
                bool chosen = NetSession.SelectedArena == i;
                bool over = GUI.enabled && r.Contains(Event.current.mousePosition);
                float h = MenuArt.Hover(MenuArt.Key("map" + i, r), over);
                if (over) { hoverMap = i; shownBlurb = i; }

                if (Event.current.type == EventType.Repaint)
                {
                    Rect card = MenuArt.Grow(r, h * 3f);
                    card.y -= h * 4f;
                    if (h > 0.01f || chosen) MenuArt.Glow(card.center, card.width * 1.9f, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, chosen ? 0.18f + 0.1f * h : 0.15f * h));
                    MenuArt.RoundRect(new Rect(card.x, card.y + 4f, card.width, card.height), new Color(0f, 0f, 0f, 0.35f), 8f);
                    MenuArt.RoundRect(card, new Color(0.06f, 0.065f, 0.09f, 0.92f), 8f);

                    // the picture, zooming in a little under the mouse
                    Rect pic = new Rect(card.x + 4f, card.y + 4f, card.width - 8f, card.height - 30f);
                    Texture2D tex = MapPicture(i);
                    if (tex != null)
                    {
                        float zoom = 1f + 0.14f * h;
                        GUI.BeginClip(pic);
                        Rect big = new Rect(-pic.width * (zoom - 1f) * 0.5f, -pic.height * (zoom - 1f) * 0.5f, pic.width * zoom, pic.height * zoom);
                        GUI.color = new Color(1f, 1f, 1f, chosen || h > 0.01f ? 1f : 0.7f);
                        GUI.DrawTexture(big, tex, ScaleMode.ScaleAndCrop);
                        GUI.color = Color.white;
                        GUI.EndClip();
                    }

                    Color edge = chosen ? MenuArt.Gold : new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.2f + 0.7f * h);
                    MenuArt.RoundRect(card, edge, 8f, chosen ? 2f : 1.2f + h * 0.6f);
                    cardTitle.normal.textColor = chosen ? MenuArt.Gold : Color.Lerp(new Color(1f, 1f, 1f, 0.75f), Color.white, h);
                    GUI.Label(new Rect(card.x, card.yMax - 26f, card.width, 24f), NetSession.ArenaTitles[i], cardTitle);
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !chosen)
                {
                    NetSession.SelectedArena = i;
                    MenuArt.Ripple(GUI.matrix.MultiplyPoint3x4(Event.current.mousePosition), MenuArt.Gold);
                }
            }
            y += ch + 8f;
            if (shownBlurb >= 0 && shownBlurb < LobbyScreen.MapBlurbs.Length)
                GUI.Label(new Rect(x - 20f, y, w + 40f, 34f), LobbyScreen.MapBlurbs[shownBlurb], blurb);
            return y + 34f;
        }

        Texture2D[] pictures;
        Texture2D MapPicture(int i)
        {
            if (pictures == null)
            {
                pictures = new Texture2D[NetSession.ArenaScenes.Length];
                for (int k = 0; k < pictures.Length; k++)
                    pictures[k] = Resources.Load<Texture2D>("MapPreviews/" + NetSession.ArenaScenes[k]);
            }
            return i >= 0 && i < pictures.Length ? pictures[i] : null;
        }

        float JoinRow(NetSession session, float x, float y, float w)
        {
            GUI.Label(new Rect(x + 2f, y, w, 20f), "CODE FROM THE HOST", caps);
            y += 22f;
            Rect box = new Rect(x, y, w * 0.6f, 56f);
            Plate(box, "code", 0f);
            GUI.SetNextControlName("code");
            code = GUI.TextField(box, code.ToUpperInvariant(), 8, field);
            if (MenuArt.Button(new Rect(x + w * 0.63f, y, w * 0.37f, 56f), "Join", bigButton)) session.JoinOnline(code);
            return y + 56f + 20f;
        }

        float Links(NetSession session, float x, float y, float w)
        {
            // neither LAN nor Quit exists in a browser: a web page cannot listen for a connection or close itself
            int n = NetSession.IsWeb ? 2 : 4;
            float lw = w / n;
            if (MenuArt.Link(new Rect(x, y, lw, 30f), showSettings ? "Hide settings" : "Settings", link)) showSettings = !showSettings;
            if (MenuArt.Link(new Rect(x + lw, y, lw, 30f), "How to play", link)) HelpScreen.Open = true;
            if (!NetSession.IsWeb)
            {
                if (MenuArt.Link(new Rect(x + lw * 2f, y, lw, 30f), showLan ? "Hide LAN" : "LAN / IP", link)) showLan = !showLan;
                if (MenuArt.Link(new Rect(x + lw * 3f, y, lw, 30f), "Quit", link)) Application.Quit();
            }
            y += 40f;

            if (showSettings)
            {
                Rect panel = new Rect(x - 14f, y - 6f, w + 28f, GameSettings.Height + 14f);
                if (Event.current.type == EventType.Repaint)
                {
                    MenuArt.RoundRect(panel, MenuArt.Panel, 10f);
                    MenuArt.RoundRect(panel, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.25f), 10f, 1.2f);
                }
                y = GameSettings.Draw(x, y, w) + 18f;
            }

            if (showLan)
            {
                if (MenuArt.Button(new Rect(x, y, w * 0.46f, 36f), "Host on port 7777", smallButton)) session.HostDirect(7777);
                Rect ip = new Rect(x + w * 0.49f, y, w * 0.3f, 36f);
                Plate(ip, "ip", 0f);
                GUI.SetNextControlName("ip");
                address = GUI.TextField(ip, address, 40, smallButton);
                if (MenuArt.Button(new Rect(x + w * 0.82f, y, w * 0.18f, 36f), "Join", smallButton)) session.JoinDirect(address, 7777);
                y += 46f;
            }
            return y;
        }
    }
}
