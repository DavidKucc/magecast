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

        GUIStyle big, mid, small, nameTag, button;
        bool copied;
        float copiedAt;

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

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) GameInput.MenuOpen = !GameInput.MenuOpen;

            // In a browser, Esc is taken by the browser itself to release the mouse, and the game may
            // or may not hear the key. Losing the lock while playing is therefore treated as Esc.
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (NetSession.IsWeb && wasLocked && !locked && PlayerNet.Local != null) GameInput.MenuOpen = true;
            wasLocked = locked;
        }

        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
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
            if (GameInput.MenuOpen) Menu(session, nm);
            else ClickToPlay();
        }

        void TopBar(NetSession session, NetworkManager nm)
        {
            if (session == null || nm == null) return;
            float cx = Screen.width * 0.5f;

            if (session.Current == NetSession.Mode.Training)
            {
                Shadowed(new Rect(cx - 200f, 8f, 400f, 24f), "TRAINING", mid, new Color(1f, 1f, 1f, 0.6f));
                return;
            }

            // the code sits at the top for the host until somebody has used it
            bool alone = PlayerNet.All.Count < 2;
            if (session.Current == NetSession.Mode.Host && alone && !string.IsNullOrEmpty(session.JoinCode))
            {
                Box(new Rect(cx - 210f, 6f, 420f, 70f));
                Shadowed(new Rect(cx - 200f, 8f, 400f, 36f), "CODE:  " + session.JoinCode, big, new Color(1f, 0.9f, 0.5f));
                Shadowed(new Rect(cx - 200f, 44f, 400f, 26f), "waiting for an opponent   (Esc - copy the code)", small, Color.white);
                return;
            }

            // score: every player, kills and deaths
            var line = new System.Text.StringBuilder();
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (line.Length > 0) line.Append("      ");
                line.Append(p.DisplayName).Append("  ").Append(p.Kills.Value).Append(" / ").Append(p.Deaths.Value);
            }
            string ping = "";
            if (!nm.IsServer && nm.IsConnectedClient)
                ping = "   ping " + nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId) + " ms";

            Box(new Rect(cx - 260f, 6f, 520f, 30f));
            Shadowed(new Rect(cx - 260f, 8f, 520f, 26f), line + ping, mid, Color.white);
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
                Shadowed(new Rect(s.x - 100f, Screen.height - s.y - 12f, 200f, 24f), p.DisplayName, nameTag,
                         new Color(1f, 0.55f, 0.5f));
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

        void Menu(NetSession session, NetworkManager nm)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = 320f, h = 48f, x = Screen.width * 0.5f - w * 0.5f;
            float y = Screen.height * 0.5f - 150f;

            Shadowed(new Rect(0f, y - 60f, Screen.width, 40f), "PAUSED", big, Color.white);
            Shadowed(new Rect(0f, y - 26f, Screen.width, 24f),
                     "the game keeps running - you can still be hit", small, new Color(1f, 1f, 1f, 0.7f));

            if (GUI.Button(new Rect(x, y, w, h), "Resume", button)) GameInput.MenuOpen = false;
            y += h + 10f;

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

            if (nm != null)
            {
                string who = "";
                foreach (PlayerNet p in PlayerNet.All) who += (who.Length > 0 ? ", " : "") + p.DisplayName;
                Shadowed(new Rect(0f, y, Screen.width, 22f), "in the game: " + who, small, Color.white);
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
