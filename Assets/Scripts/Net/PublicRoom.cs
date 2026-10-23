using System;
using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// A hosted room, put up in the list of open games (RoomBrowser) so that anybody can join it with a
    /// click instead of being sent the code.
    ///
    /// The list is Unity Lobby: the host keeps one entry there with the Relay code and what the room is
    /// (map, mode, how many people, playing or not), and everyone else only reads it. Guests never join
    /// the entry itself -- they join the room by its code, as with a typed one -- so the host is the only
    /// one who writes, and the count is the room's own.
    ///
    /// An entry has to be kept alive: Lobby stops listing one that has not been heard from for 30 s. That
    /// is also what clears up after a host whose browser tab simply closed, with no chance to say so.
    /// </summary>
    public class PublicRoom : MonoBehaviour
    {
        /// <summary>Whether a hosted room goes into the list, or is reached only by its code.</summary>
        public static bool Listed
        {
            get { return PlayerPrefs.GetInt("ListRoom", 1) == 1; }
            set { PlayerPrefs.SetInt("ListRoom", value ? 1 : 0); }
        }

        // what an entry carries, by key
        public const string CodeField = "code", MapField = "map", ModeField = "mode", PeopleField = "people",
                            BotsField = "bots", PlayingField = "playing", BuildField = "build";

        /// <summary>Lobby forgets an entry after 30 s of silence; twice as often as that, with room to spare.</summary>
        const float BeatEvery = 12f;

        /// <summary>The service takes 5 updates in 5 s; a change is passed on at most this often.</summary>
        const float SyncEvery = 2f;

        public static PublicRoom Instance { get; private set; }

        /// <summary>Why the room is not in the list, when it should be and is not -- shown in the lobby.</summary>
        public static string Problem { get; private set; }

        /// <summary>True while this machine's room is in the list.</summary>
        public static bool Up { get { return Instance != null && Instance.entryId != null; } }

        string entryId;
        string code;
        string sent;
        float nextBeat, nextSync;
        bool busy;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Puts the room with this Relay code up in the list -- if the host left it open, or <paramref name="always"/>
        /// (a server). Signed in already: the room was just made.
        /// </summary>
        public async void Open(string relayCode, bool always = false)
        {
            Problem = null;
            if ((!Listed && !always) || entryId != null || busy) return;
            busy = true;
            code = relayCode;
            try
            {
                var options = new CreateLobbyOptions { IsPrivate = false, Data = Snapshot(out sent) };
                Unity.Services.Lobbies.Models.Lobby entry = await LobbyService.Instance.CreateLobbyAsync(RoomName(), Lobby.MaxPlayers, options);
                // left while it was being made: take it straight down again
                if (code == null) { await LobbyService.Instance.DeleteLobbyAsync(entry.Id); return; }
                entryId = entry.Id;
                nextBeat = Time.unscaledTime + BeatEvery;
                nextSync = Time.unscaledTime + SyncEvery;
                Debug.Log("[Rooms] listed as \"" + entry.Name + "\" (" + entryId + ")");
            }
            catch (Exception e)
            {
                Problem = Explain(e);
                Debug.LogWarning("[Rooms] could not list the room: " + e.Message);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>Takes the room out of the list -- on leaving, or when the host makes it private.</summary>
        public async void Close()
        {
            code = null;
            string id = entryId;
            entryId = null;
            if (id == null) return;
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(id);
                Debug.Log("[Rooms] taken out of the list");
            }
            catch (Exception e)
            {
                // it goes out of the list by itself once it stops being kept alive
                Debug.LogWarning("[Rooms] could not take the room out of the list: " + e.Message);
            }
        }

        void OnApplicationQuit()
        {
            Close();
        }

        void Update()
        {
            if (entryId == null || busy) return;
            float now = Time.unscaledTime;
            if (now >= nextSync)
            {
                nextSync = now + SyncEvery;
                string changed;
                Dictionary<string, DataObject> data = Snapshot(out changed);
                if (changed != sent) { Sync(data, changed); return; }
            }
            if (now >= nextBeat) Beat();
        }

        async void Sync(Dictionary<string, DataObject> data, string signature)
        {
            busy = true;
            try
            {
                await LobbyService.Instance.UpdateLobbyAsync(entryId, new UpdateLobbyOptions { Name = RoomName(), Data = data });
                sent = signature;
                // an update keeps the entry alive as well as a heartbeat does
                nextBeat = Time.unscaledTime + BeatEvery;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rooms] could not update the listing: " + e.Message);
            }
            finally
            {
                busy = false;
            }
        }

        async void Beat()
        {
            busy = true;
            nextBeat = Time.unscaledTime + BeatEvery;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(entryId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Rooms] heartbeat failed: " + e.Message);
            }
            finally
            {
                busy = false;
            }
        }

        /// <summary>The room as the list shows it, and the same as one string, to tell when it has changed.</summary>
        Dictionary<string, DataObject> Snapshot(out string signature)
        {
            Lobby room = Lobby.Instance;
            int map = room != null ? room.Map.Value : NetSession.SelectedArena;
            int mode = room != null ? room.ModeIndex.Value : 0;
            int bots = room != null ? room.BotCount : 0;
            int people = room != null ? room.Members.Count - bots : 1;
            bool playing = room != null && room.Playing.Value;

            var data = new Dictionary<string, DataObject>
            {
                { CodeField, Public(code) },
                { MapField, Public(map.ToString()) },
                { ModeField, Public(mode.ToString()) },
                { PeopleField, Public(people.ToString()) },
                { BotsField, Public(bots.ToString()) },
                { PlayingField, Public(playing ? "1" : "0") },
                { BuildField, Public(BuildInfo.Label) },
            };
            signature = RoomName() + "|" + map + "|" + mode + "|" + people + "|" + bots + "|" + playing;
            return data;
        }

        static DataObject Public(string value)
        {
            return new DataObject(DataObject.VisibilityOptions.Public, value ?? "");
        }

        static string RoomName()
        {
            // a server is listed by its own name (-room), not by the name of whoever last played on that PC
            if (!string.IsNullOrEmpty(NetSession.RoomTitle)) return NetSession.RoomTitle.Trim();
            string name = NetSession.SafeName(NetSession.PlayerName);
            return name.Length > 0 ? name : "Mage";
        }

        static string Explain(Exception e)
        {
            string m = e.Message ?? e.GetType().Name;
            string lower = m.ToLowerInvariant();
            if (lower.Contains("forbidden") || lower.Contains("not enabled") || lower.Contains("403") || lower.Contains("404"))
                return "not in the list of open games: Lobby is not switched on for this project (Unity Dashboard > Multiplayer > Lobby)";
            return "not in the list of open games: " + m;
        }
    }
}
