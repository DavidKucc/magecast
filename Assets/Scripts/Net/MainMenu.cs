using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// The first screen: a name, training, host a game, join one by code.
    ///
    /// The LAN row is tucked away on purpose. It is the fallback for when Relay is not available, and
    /// a tester who sees an "IP address" box first will type something into it.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        string code = "";
        string address = "127.0.0.1";
        bool showLan;
        GUIStyle title, subtitle, label, field, button, status, smallButton;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameInput.MenuOpen = false;
            GameInput.LocalDead = false;

            NetSession session = NetSession.Ensure();
            if (session == null) return;
            session.HandleCommandLine();

            // Opened from an invite link: straight into the room, once. Only on the first visit to the
            // menu -- coming back here after leaving must not throw you back into the same game.
            string invited = NetSession.CodeFromPageAddress();
            if (invited != null && !inviteUsed)
            {
                inviteUsed = true;
                code = invited;
                session.JoinOnline(invited);
            }
        }

        static bool inviteUsed;

        /// <summary>For the web page (unityInstance.SendMessage("Menu", "JoinFromPage", code)).</summary>
        public void JoinFromPage(string roomCode)
        {
            code = roomCode ?? "";
            if (NetSession.Instance != null) NetSession.Instance.JoinOnline(code);
        }

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(1f, 0.82f, 0.45f);
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            subtitle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            field = new GUIStyle(GUI.skin.textField) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            smallButton = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            status = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
        }

        void OnGUI()
        {
            Styles();
            NetSession session = NetSession.Instance;
            bool busy = session == null || session.Busy;

            float w = 380f, h = 54f;
            float x = Screen.width * 0.5f - w * 0.5f;
            float y = Mathf.Max(30f, Screen.height * 0.5f - 290f);

            GUI.Label(new Rect(0f, y, Screen.width, 80f), "MAGE CAST", title);
            y += 76f;
            GUI.Label(new Rect(0f, y, Screen.width, 24f), "draw the rune, aim, send  -  test build", subtitle);
            y += 50f;

            GUI.enabled = !busy;

            GUI.Label(new Rect(x, y, w, 24f), "Your name", label);
            y += 26f;
            string name = GUI.TextField(new Rect(x, y, w, 44f), NetSession.PlayerName, 12, field);
            if (name != NetSession.PlayerName) NetSession.PlayerName = name;
            y += 64f;

            if (GUI.Button(new Rect(x, y, w, h), "Training", button)) session.StartTraining(true);
            y += h + 10f;

            if (GUI.Button(new Rect(x, y, w, h), "Host a game", button)) session.HostOnline();
            y += h + 22f;

            GUI.Label(new Rect(x, y, w, 24f), "Code from the host", label);
            y += 26f;
            code = GUI.TextField(new Rect(x, y, w * 0.58f, h), code.ToUpperInvariant(), 8, field);
            if (GUI.Button(new Rect(x + w * 0.62f, y, w * 0.38f, h), "Join", button)) session.JoinOnline(code);
            y += h + 22f;

            // Neither exists in a browser: a web page cannot listen for a LAN connection or close itself.
            if (!NetSession.IsWeb)
            {
                if (GUI.Button(new Rect(x, y, w * 0.48f, 34f), showLan ? "hide LAN" : "LAN / IP...", smallButton))
                    showLan = !showLan;
                if (GUI.Button(new Rect(x + w * 0.52f, y, w * 0.48f, 34f), "Quit", smallButton)) Application.Quit();
                y += 44f;
            }

            if (showLan)
            {
                if (GUI.Button(new Rect(x, y, w * 0.48f, 34f), "Host on port 7777", smallButton)) session.HostDirect(7777);
                address = GUI.TextField(new Rect(x + w * 0.52f, y, w * 0.30f, 34f), address, 40);
                if (GUI.Button(new Rect(x + w * 0.84f, y, w * 0.16f, 34f), "Join", smallButton)) session.JoinDirect(address, 7777);
                y += 44f;
            }

            GUI.enabled = true;

            if (session != null && !string.IsNullOrEmpty(session.Status))
            {
                status.normal.textColor = busy ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 0.75f, 0.6f);
                GUI.Label(new Rect(Screen.width * 0.5f - 350f, y + 6f, 700f, 80f), session.Status, status);
            }

            // Enter in the code box joins, since that is what everyone will try first
            Event e = Event.current;
            if (!busy && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                && code.Trim().Length > 0)
            {
                session.JoinOnline(code);
                e.Use();
            }
        }
    }
}
