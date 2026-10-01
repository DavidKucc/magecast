using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageCast
{
    /// <summary>
    /// Starting, joining and leaving a game. Lives on the NetworkManager and survives scene changes.
    ///
    /// Online games go through Unity Relay: the host asks Relay for a room and gets a short code, the
    /// other player types the code in. Neither side opens a port or knows the other's address, which is
    /// what makes "send the exe to a friend and play" actually work from two different home networks.
    ///
    /// Training is the same game hosted on this machine alone -- not a separate offline mode. Every
    /// spell goes down exactly the path it takes in a real match, so what works in training works online.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class NetSession : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";

        /// <summary>The maps, by scene: what the menu offers, in the order it offers them.</summary>
        public static readonly string[] ArenaScenes = { "Arena_Blockout", "Arena_Temple" };
        public static readonly string[] ArenaTitles = { "Blockout", "Temple" };

        /// <summary>Which map training and hosting open, remembered between sessions.</summary>
        public static int SelectedArena
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt("Arena", 0), 0, ArenaScenes.Length - 1); }
            set { PlayerPrefs.SetInt("Arena", Mathf.Clamp(value, 0, ArenaScenes.Length - 1)); }
        }

        public static bool IsArena(string scene)
        {
            return Array.IndexOf(ArenaScenes, scene) >= 0;
        }

        /// <summary>Players besides the host. Relay reserves room for exactly this many.</summary>
        const int MaxGuests = 3;

        /// <summary>
        /// How everyone talks to Relay: secure WebSockets, on every platform.
        ///
        /// A browser can only speak WebSockets, and a player on WebSockets and a player on plain UDP
        /// cannot meet in the same room. So the Windows build uses them too -- a hair more overhead
        /// per packet, in exchange for the exe and the web version being able to play each other.
        /// </summary>
        const string RelayConnection = "wss";

        /// <summary>True in a browser build. The browser cannot listen for connections, open a port,
        /// write files or quit, and several things below go a different way because of it.</summary>
        public static bool IsWeb
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public enum Mode { None, Training, Host, Client }

        public static NetSession Instance { get; private set; }

        [SerializeField] GameObject playerPrefab;

        public GameObject PlayerPrefab { get { return playerPrefab; } }
        public Mode Current { get; private set; }
        public string JoinCode { get; private set; }
        public bool Busy { get; private set; }

        /// <summary>The last thing worth telling the player -- shown on the menu.</summary>
        public string Status { get; private set; }

        NetworkManager manager;
        UnityTransport transport;
        bool servicesReady;
        bool commandLineHandled;

        public static string PlayerName
        {
            get { return PlayerPrefs.GetString("PlayerName", ""); }
            set { PlayerPrefs.SetString("PlayerName", value ?? ""); }
        }

        /// <summary>A name that fits the 29 bytes a network name gets, whatever alphabet it is in.</summary>
        public static string SafeName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 12) name = name.Substring(0, 12);
            while (System.Text.Encoding.UTF8.GetByteCount(name) > 29) name = name.Substring(0, name.Length - 1);
            return name;
        }

        /// <summary>
        /// The code in the page's address, from an invite link (...?join=ABC123), or null. Web only:
        /// a link that opens the game already joining is what makes "send a friend a link" one click.
        /// </summary>
        public static string CodeFromPageAddress()
        {
            if (!IsWeb) return null;
            string url = Application.absoluteURL ?? "";
            int q = url.IndexOf('?');
            if (q < 0) return null;
            foreach (string part in url.Substring(q + 1).Split('&', '#'))
            {
                if (!part.StartsWith("join=", StringComparison.OrdinalIgnoreCase)) continue;
                string code = Uri.UnescapeDataString(part.Substring(5)).Trim().ToUpperInvariant();
                return code.Length > 0 ? code : null;
            }
            return null;
        }

        /// <summary>A link that opens this game already joining the given room. Web only.</summary>
        public static string InviteLink(string code)
        {
            string url = Application.absoluteURL ?? "";
            int cut = url.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) url = url.Substring(0, cut);
            return url + "?join=" + Uri.EscapeDataString(code);
        }

        /// <summary>The session, created from Resources the first time anything needs one.</summary>
        public static NetSession Ensure()
        {
            if (Instance != null) return Instance;
            GameObject prefab = Resources.Load<GameObject>("NetworkManager");
            if (prefab == null)
            {
                Debug.LogError("[Net] Resources/NetworkManager is missing - run Tools > Arena > Build Network Setup");
                return null;
            }
            GameObject go = Instantiate(prefab);
            go.name = "NetworkManager";
            return go.GetComponent<NetSession>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Two copies of the game on one machine is how this gets tested, and a game that pauses
            // itself when it loses focus stops answering the other one.
            Application.runInBackground = true;

            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;

            if (playerPrefab != null && !manager.NetworkConfig.Prefabs.Contains(playerPrefab))
                manager.AddNetworkPrefab(playerPrefab);

            manager.OnClientDisconnectCallback += OnClientDisconnect;
            manager.OnTransportFailure += OnTransportFailure;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (manager == null) return;
            manager.OnClientDisconnectCallback -= OnClientDisconnect;
            manager.OnTransportFailure -= OnTransportFailure;
        }

        // ---------------------------------------------------------------- starting

        /// <summary>
        /// This machine alone. Listens on the loopback only, so Windows never asks about the firewall
        /// for what is a single-player practice run.
        /// </summary>
        public void StartTraining(bool loadArena)
        {
            // A browser cannot host on its own machine -- it has no way to listen for a connection, not
            // even from itself. So on the web, training is a Relay room that nobody else is told about.
            // It needs the internet where the exe does not, and that is the whole difference.
            if (IsWeb) { HostRelay(Mode.Training); return; }

            if (!Ready()) return;
            transport.UseWebSockets = false;
            transport.SetConnectionData("127.0.0.1", 7790, "127.0.0.1");
            if (!manager.StartHost()) { Fail("could not start training"); return; }
            Current = Mode.Training;
            JoinCode = null;
            if (loadArena) LoadArena();
        }

        /// <summary>Opens a room on Unity Relay and starts hosting it. The code appears in the arena.</summary>
        public void HostOnline()
        {
            HostRelay(Mode.Host);
        }

        /// <summary>Where this room's Relay server is, e.g. "europe-central2" -- shown next to the ping.</summary>
        public string RelayRegion { get; private set; }

        /// <summary>
        /// The Relay regions to open a room in, nearest first. Chosen here rather than left to the
        /// service: it would measure which region is closest by pinging them, which a browser cannot do,
        /// so from the web every room opened in the service's default region across the Atlantic --
        /// every message went there and back, and the ping with it. The players are in Central Europe.
        /// </summary>
        static readonly string[] PreferredRegions = { "europe-central2", "europe-west4", "europe-west1", "europe-north1" };

        /// <summary>The first preferred region the service offers, or null for its own choice.</summary>
        static async System.Threading.Tasks.Task<string> PickRegion()
        {
            try
            {
                var offered = await RelayService.Instance.ListRegionsAsync();
                var ids = new System.Collections.Generic.List<string>();
                foreach (var r in offered) ids.Add(r.Id);
                Debug.Log("[Net] relay regions offered: " + string.Join(", ", ids));
                foreach (string want in PreferredRegions) if (ids.Contains(want)) return want;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] could not list relay regions, leaving it to the service: " + e.Message);
            }
            return null;
        }

        async void HostRelay(Mode mode)
        {
            if (!Ready()) return;
            Busy = true;
            try
            {
                Say("connecting to Unity services...");
                await SignIn();

                Say(mode == Mode.Training ? "setting up training..." : "opening a room...");
                string region = await PickRegion();
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxGuests, region);
                RelayRegion = allocation.Region;
                Debug.Log("[Net] room opened in relay region " + allocation.Region + " (asked for " + (region ?? "the default") + ")");
                string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                transport.UseWebSockets = true;
                transport.SetRelayServerData(new RelayServerData(allocation, RelayConnection));
                if (!manager.StartHost()) { Fail("could not start hosting"); return; }

                Current = mode;
                JoinCode = mode == Mode.Host ? code : null;
                Say(mode == Mode.Host ? "room " + code + " is open" : "training");
                LoadArena();
            }
            catch (Exception e)
            {
                Fail(Explain(e));
            }
            finally
            {
                Busy = false;
            }
        }

        public async void JoinOnline(string code)
        {
            if (!Ready()) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) { Say("type the code from the host first"); return; }

            Busy = true;
            try
            {
                Say("connecting to Unity services...");
                await SignIn();

                Say("joining " + code + "...");
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
                RelayRegion = allocation.Region;

                transport.UseWebSockets = true;
                transport.SetRelayServerData(new RelayServerData(allocation, RelayConnection));
                if (!manager.StartClient()) { Fail("could not start the connection"); return; }

                // The host decides which scene everyone is in, and Netcode loads it for us.
                Current = Mode.Client;
                JoinCode = code;
                Say("joined " + code + ", loading the arena...");
            }
            catch (Exception e)
            {
                Fail(Explain(e));
            }
            finally
            {
                Busy = false;
            }
        }

        /// <summary>
        /// Straight to an address, no Relay. For a LAN, or for when Relay is not set up on the project --
        /// the host then has to be reachable, which across the internet means a forwarded port.
        /// </summary>
        public void HostDirect(ushort port)
        {
            if (!Ready()) return;
            transport.UseWebSockets = false;
            transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            if (!manager.StartHost()) { Fail("could not listen on port " + port); return; }
            Current = Mode.Host;
            JoinCode = "LAN:" + port;
            LoadArena();
        }

        public void JoinDirect(string address, ushort port)
        {
            if (!Ready()) return;
            transport.UseWebSockets = false;
            transport.SetConnectionData(string.IsNullOrEmpty(address) ? "127.0.0.1" : address.Trim(), port);
            if (!manager.StartClient()) { Fail("could not connect to " + address); return; }
            Current = Mode.Client;
            JoinCode = address + ":" + port;
            Say("connecting to " + address + ":" + port + "...");
        }

        bool Ready()
        {
            if (Busy) return false;
            if (manager.IsListening || manager.ShutdownInProgress)
            {
                Say("still shutting down the last game, try again in a moment");
                return false;
            }
            Status = "";
            return true;
        }

        void LoadArena()
        {
            // Joiners do not choose: Netcode takes them to whichever map the host is on.
            manager.SceneManager.LoadScene(ArenaScenes[SelectedArena], LoadSceneMode.Single);
        }

        async Task SignIn()
        {
            if (!servicesReady)
            {
                // A profile per running copy. Anonymous sign-in remembers who you were on this PC, so
                // two copies started side by side for a test would otherwise both be the same player,
                // and Relay would treat the second join as the first one reconnecting.
                string profile = "p" + Guid.NewGuid().ToString("N").Substring(0, 12);
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
                servicesReady = true;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string Explain(Exception e)
        {
            string m = e.Message ?? e.GetType().Name;
            string lower = m.ToLowerInvariant();

            if (e is RelayServiceException && lower.Contains("not found"))
                return "no room with that code - check it, or ask the host for a fresh one";
            if (lower.Contains("forbidden") || lower.Contains("not enabled") || lower.Contains("403"))
                return "Relay is not switched on for this project (Unity Dashboard > Multiplayer > Relay). " + m;
            if (lower.Contains("project") && lower.Contains("id"))
                return "the build is not linked to a Unity project: " + m;
            if (lower.Contains("network") || lower.Contains("connect") || lower.Contains("timeout"))
                return "no connection to Unity services - is the internet up? " + m;
            return m;
        }

        // ---------------------------------------------------------------- leaving

        public void Leave()
        {
            Leave("");
        }

        void Leave(string reason)
        {
            if (manager.IsListening) manager.Shutdown();
            Current = Mode.None;
            JoinCode = null;
            RelayRegion = null;
            GameInput.MenuOpen = false;
            GameInput.LocalDead = false;
            GameInput.MatchOverUntil = -1f;
            if (!string.IsNullOrEmpty(reason)) Say(reason);
            if (SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
        }

        void OnClientDisconnect(ulong clientId)
        {
            // The host hears about every guest that leaves; that is not our problem. Only a client
            // losing ITS connection -- the host quit, or the join never got through -- ends the game here.
            if (manager.IsServer) return;
            if (clientId != manager.LocalClientId && clientId != NetworkManager.ServerClientId) return;

            string why = string.IsNullOrEmpty(manager.DisconnectReason) ? "" : ": " + manager.DisconnectReason;
            Leave(Current == Mode.Client && IsArena(SceneManager.GetActiveScene().name)
                  ? "the host left the game" + why
                  : "could not connect" + why);
        }

        void OnTransportFailure()
        {
            Leave("the connection failed");
        }

        void Say(string text)
        {
            Status = text;
            Debug.Log("[Net] " + text);
        }

        void Fail(string text)
        {
            Status = text;
            Debug.LogWarning("[Net] " + text);
            if (manager.IsListening) manager.Shutdown();
            Current = Mode.None;
        }

        // ---------------------------------------------------------------- command line

        /// <summary>
        /// For starting a copy straight into a game without clicking: -training, -host, -join CODE,
        /// -lanhost [port], -lanjoin ADDRESS [port], -name NAME. Handled once, from the menu.
        /// </summary>
        public void HandleCommandLine()
        {
            if (commandLineHandled) return;
            commandLineHandled = true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i].ToLowerInvariant() == "-name") PlayerName = args[i + 1];

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                string next = i + 1 < args.Length ? args[i + 1] : null;
                ushort port;

                if (a == "-training") { StartTraining(true); return; }
                else if (a == "-host") { HostOnline(); return; }
                else if (a == "-join" && next != null) { JoinOnline(next); return; }
                else if (a == "-lanhost") { HostDirect(next != null && ushort.TryParse(next, out port) ? port : (ushort)7777); return; }
                else if (a == "-lanjoin" && next != null)
                {
                    string portArg = i + 2 < args.Length ? args[i + 2] : null;
                    JoinDirect(next, portArg != null && ushort.TryParse(portArg, out port) ? port : (ushort)7777);
                    return;
                }
            }
        }
    }
}
