using System;
using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// "Find a game" on the main menu: every room somebody is hosting right now and has left open (see
    /// PublicRoom), with who hosts it, the map, the mode, how many are in, and Join. Joining is the same
    /// as typing the room's code -- the list only saves sending it.
    ///
    /// Read from Unity Lobby every few seconds while it is open. Drawn in the menu's own units, in the
    /// column where the menu's buttons are.
    /// </summary>
    public static class RoomBrowser
    {
        public static bool Open { get; private set; }

        struct Room
        {
            public string Name, Code, Build;
            public int Map, Mode, People, Bots;
            public bool Playing;
        }

        static readonly List<Room> rooms = new List<Room>();
        static bool loading, loaded;
        static string problem;
        static float nextRefresh;

        /// <summary>The service answers a query a second; every few is plenty for a list of rooms.</summary>
        const float RefreshEvery = 5f;

        const float RowH = 60f, RowGap = 8f;
        const int RowsShown = 6;

        static GUIStyle caps, roomName, detail, count, button, link, empty;

        public static void Show()
        {
            Open = true;
            nextRefresh = 0f;
        }

        public static void Hide()
        {
            Open = false;
        }

        /// <summary>How tall the list stands in the menu column, header and footer included.</summary>
        public static float Height
        {
            get { return 34f + Mathf.Max(3, Mathf.Min(rooms.Count, RowsShown)) * (RowH + RowGap) + 64f; }
        }

        static async void Refresh()
        {
            if (loading) return;
            NetSession session = NetSession.Instance;
            if (session == null) return;
            loading = true;
            nextRefresh = Time.unscaledTime + RefreshEvery;
            try
            {
                await session.SignedIn();
                var options = new QueryLobbiesOptions
                {
                    Count = 25,
                    Order = new List<QueryOrder> { new QueryOrder(false, QueryOrder.FieldOptions.Created) },
                };
                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
                rooms.Clear();
                foreach (Unity.Services.Lobbies.Models.Lobby entry in response.Results)
                {
                    string code = Read(entry, PublicRoom.CodeField);
                    if (code.Length == 0) continue;
                    rooms.Add(new Room
                    {
                        Name = entry.Name,
                        Code = code,
                        Build = Read(entry, PublicRoom.BuildField),
                        Map = Number(entry, PublicRoom.MapField),
                        Mode = Number(entry, PublicRoom.ModeField),
                        People = Number(entry, PublicRoom.PeopleField),
                        Bots = Number(entry, PublicRoom.BotsField),
                        Playing = Read(entry, PublicRoom.PlayingField) == "1",
                    });
                }
                problem = null;
                loaded = true;
            }
            catch (Exception e)
            {
                string m = e.Message ?? e.GetType().Name;
                string lower = m.ToLowerInvariant();
                problem = lower.Contains("forbidden") || lower.Contains("not enabled") || lower.Contains("403") || lower.Contains("404")
                        ? "the list of games is not switched on for this project (Unity Dashboard > Multiplayer > Lobby)"
                        : lower.Contains("network") || lower.Contains("connect") || lower.Contains("timeout")
                        ? "no connection to Unity services - is the internet up?"
                        : m;
                Debug.LogWarning("[Rooms] could not read the list: " + m);
            }
            finally
            {
                loading = false;
            }
        }

        static string Read(Unity.Services.Lobbies.Models.Lobby entry, string key)
        {
            DataObject value;
            return entry.Data != null && entry.Data.TryGetValue(key, out value) && value.Value != null ? value.Value : "";
        }

        static int Number(Unity.Services.Lobbies.Models.Lobby entry, string key)
        {
            int n;
            return int.TryParse(Read(entry, key), out n) ? n : 0;
        }

        static void Styles()
        {
            if (caps != null) return;
            caps = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            caps.normal.textColor = new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.8f);
            roomName = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            detail = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false, richText = true };
            count = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            button = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            link = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            empty = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        /// <summary>
        /// Draws the list in the menu column from <paramref name="y"/> down. Returns the code of the room the
        /// player chose to join, or null; <paramref name="bottom"/> is where the list ends.
        /// </summary>
        public static string Draw(float x, float y, float w, out float bottom)
        {
            Styles();
            if (!loading && Time.unscaledTime >= nextRefresh) Refresh();
            string chosen = null;

            GUI.Label(new Rect(x + 2f, y, w * 0.6f, 24f), "OPEN GAMES", caps);
            string state = loading && !loaded ? "looking..." : "Refresh";
            if (MenuArt.Link(new Rect(x + w - 110f, y - 3f, 110f, 28f), state, link)) Refresh();
            y += 34f;

            float listTop = y;
            if (problem != null && rooms.Count == 0)
            {
                empty.normal.textColor = new Color(1f, 0.75f, 0.6f);
                GUI.Label(new Rect(x, y + 20f, w, 3f * RowH), problem, empty);
            }
            else if (rooms.Count == 0)
            {
                empty.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
                GUI.Label(new Rect(x, y + 20f, w, 3f * RowH),
                          loaded ? "Nobody is hosting an open game right now.\nHost one - others will see it here."
                                 : "Looking for games...", empty);
            }
            for (int i = 0; i < rooms.Count && i < RowsShown; i++)
            {
                if (Row(rooms[i], new Rect(x, y, w, RowH))) chosen = rooms[i].Code;
                y += RowH + RowGap;
            }

            y = listTop + Mathf.Max(3, Mathf.Min(rooms.Count, RowsShown)) * (RowH + RowGap) + 6f;
            if (MenuArt.Button(new Rect(x, y, w, 50f), "Back", button)) Hide();
            bottom = y + 50f + 8f;
            return chosen;
        }

        /// <summary>One room: its host and how full it is above, the map, mode and state below, Join on the right.</summary>
        static bool Row(Room room, Rect r)
        {
            bool full = room.People >= Lobby.MaxPlayers;
            if (Event.current.type == EventType.Repaint)
            {
                MenuArt.RoundRect(r, new Color(0.03f, 0.035f, 0.05f, 0.85f), 10f);
                MenuArt.RoundRect(r, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.22f), 10f, 1.2f);
            }

            float jw = 96f;
            float textW = r.width - jw - 30f;
            roomName.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x + 14f, r.y + 6f, textW - 70f, 26f), room.Name, roomName);

            count.normal.textColor = full ? new Color(1f, 0.6f, 0.5f) : MenuArt.Gold;
            GUI.Label(new Rect(r.x + 14f + textW - 70f, r.y + 6f, 70f, 26f), room.People + "/" + Lobby.MaxPlayers, count);

            string map = room.Map >= 0 && room.Map < NetSession.ArenaTitles.Length ? NetSession.ArenaTitles[room.Map] : "?";
            string mode = room.Mode >= 0 && room.Mode < Lobby.ModeTitles.Length ? Lobby.ModeTitles[room.Mode] : "?";
            // one line, shortest first; a room on another build may not let you in, so that goes in front
            string line = map + " · " + mode + " · " + (room.Playing ? "playing" : "waiting");
            if (room.Bots > 0) line += " · " + room.Bots + (room.Bots == 1 ? " bot" : " bots");
            if (room.Build != BuildInfo.Label) line = "<color=#FFB080>other version</color> · " + line;
            detail.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            GUI.Label(new Rect(r.x + 14f, r.y + 32f, textW, 22f), line, detail);

            bool was = GUI.enabled;
            GUI.enabled = was && !full;
            bool join = MenuArt.Button(new Rect(r.xMax - jw - 10f, r.y + 10f, jw, r.height - 20f), full ? "Full" : "Join", button, !full);
            GUI.enabled = was;
            return join && !full;
        }
    }
}
