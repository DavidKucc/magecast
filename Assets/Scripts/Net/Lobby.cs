using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageCast
{
    /// <summary>One line of the lobby: a person (by connection) or a bot (by number).</summary>
    public struct LobbyMember : INetworkSerializable, IEquatable<LobbyMember>
    {
        public ulong ClientId;
        /// <summary>0 for a person; a bot's own number otherwise.</summary>
        public int BotNumber;
        public FixedString32Bytes Name;
        /// <summary>1 or 2. Kept in a free-for-all as well, so switching to teams starts from a fair split.</summary>
        public int Team;
        public bool Ready;

        public bool IsBot { get { return BotNumber > 0; } }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId);
            s.SerializeValue(ref BotNumber);
            s.SerializeValue(ref Name);
            s.SerializeValue(ref Team);
            s.SerializeValue(ref Ready);
        }

        public bool Equals(LobbyMember o)
        {
            return ClientId == o.ClientId && BotNumber == o.BotNumber && Name.Equals(o.Name) && Team == o.Team
                   && Ready == o.Ready;
        }
    }

    /// <summary>
    /// The room before (and between) matches, and what the match is: which map, which mode, how many
    /// kills win it, who is on which side, who is ready. One person leads -- only they change the
    /// settings, add bots and start; everyone else picks a side and says they are ready. In a room a player
    /// hosts, that is the host. On a server with nobody playing on it (NetSession.Mode.Server) it is the
    /// first person in, and when they leave, the next one.
    ///
    /// One networked object for the whole online session. The host spawns it as the room opens and it is
    /// never tied to a scene, so it rides from the lobby into the arena and back: the arena reads the
    /// sides and the kill target off it, and a player who joins halfway through a match is put on a side
    /// by it like anybody else. Training has none -- no lobby, no teams, nothing to win.
    /// </summary>
    public class Lobby : NetworkBehaviour
    {
        public static Lobby Instance { get; private set; }

        public enum GameMode { FreeForAll, Teams }
        public static readonly string[] ModeTitles = { "Free for all", "Teams" };
        public static readonly string[] ModeBlurbs =
        {
            "everyone against everyone",
            "Blue against Red - teammates' spells never hurt you"
        };

        public static readonly int[] KillChoices = { 5, 10, 15, 20 };

        /// <summary>People and bots together. The maps have six spawn points, three a side.</summary>
        public const int MaxPlayers = 6;

        public NetworkList<LobbyMember> Members;
        public readonly NetworkVariable<int> Map = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> ModeIndex = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> KillsToWin = new NetworkVariable<int>(5);
        public readonly NetworkVariable<float> BotSkill = new NetworkVariable<float>(0.6f);

        /// <summary>A match is on: everyone is in the arena, not the lobby.</summary>
        public readonly NetworkVariable<bool> Playing = new NetworkVariable<bool>(false);

        /// <summary>Kills per side this match, kept here so a player leaving does not take theirs along.</summary>
        public readonly NetworkVariable<int> BlueScore = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> RedScore = new NetworkVariable<int>(0);

        /// <summary>The connection of whoever leads the room, or NoLeader in an empty server's room.</summary>
        public readonly NetworkVariable<ulong> Leader = new NetworkVariable<ulong>(NoLeader);
        public const ulong NoLeader = ulong.MaxValue;

        /// <summary>This machine's player leads the room.</summary>
        public bool LocalLeads
        {
            get { return NetworkManager != null && NetworkManager.IsClient && Leader.Value == NetworkManager.LocalClientId; }
        }

        /// <summary>The leader is the one hosting -- shown as HOST rather than LEADER.</summary>
        public bool LeaderHosts { get { return Leader.Value == NetworkManager.ServerClientId; } }

        public GameMode Mode { get { return (GameMode)ModeIndex.Value; } }

        /// <summary>An online session is running, with its lobby.</summary>
        public static bool Active { get { return Instance != null && Instance.IsSpawned; } }

        /// <summary>The match being played (or about to be) is a team game.</summary>
        public static bool TeamGame { get { return Active && Instance.Mode == GameMode.Teams; } }

        void Awake()
        {
            // in Awake, not at the field: a NetworkList made there leaks its native memory in the editor
            Members = new NetworkList<LobbyMember>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += Joined;
                NetworkManager.OnClientDisconnectCallback += Left;
                Map.Value = NetSession.SelectedArena;
                if (IsHost) Leader.Value = NetworkManager.LocalClientId;
                foreach (ulong id in NetworkManager.ConnectedClientsIds) Joined(id);
            }
            // a server nobody plays on is no member of its own room
            if (IsClient) IntroduceServerRpc(new FixedString32Bytes(NetSession.SafeName(NetSession.PlayerName)));
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= Joined;
                NetworkManager.OnClientDisconnectCallback -= Left;
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        // ---------------------------------------------------------------- reading

        public int IndexOfClient(ulong clientId)
        {
            for (int i = 0; i < Members.Count; i++)
                if (!Members[i].IsBot && Members[i].ClientId == clientId) return i;
            return -1;
        }

        public int IndexOfBot(int number)
        {
            for (int i = 0; i < Members.Count; i++)
                if (Members[i].BotNumber == number) return i;
            return -1;
        }

        public int Count(int team)
        {
            int n = 0;
            for (int i = 0; i < Members.Count; i++) if (Members[i].Team == team) n++;
            return n;
        }

        public int BotCount
        {
            get { int n = 0; for (int i = 0; i < Members.Count; i++) if (Members[i].IsBot) n++; return n; }
        }

        /// <summary>The side a member plays on in the match: 0 in a free-for-all.</summary>
        public int PlaysOn(LobbyMember m)
        {
            return Mode == GameMode.Teams ? m.Team : Teams.None;
        }

        /// <summary>Server: the side a person plays on, for the body about to be spawned.</summary>
        public static int TeamOfClient(ulong clientId)
        {
            if (!Active) return Teams.None;
            int i = Instance.IndexOfClient(clientId);
            return i < 0 ? Teams.None : Instance.PlaysOn(Instance.Members[i]);
        }

        public static string NameOf(LobbyMember m)
        {
            string n = m.Name.ToString();
            if (!string.IsNullOrEmpty(n)) return n;
            return m.IsBot ? "Bot " + m.BotNumber : "Player " + (m.ClientId + 1);
        }

        public int Score(int team) { return team == 1 ? BlueScore.Value : team == 2 ? RedScore.Value : 0; }

        /// <summary>Whether the leader can start now; if not, why not -- shown on the button.</summary>
        public bool CanStart(out string why)
        {
            why = "";
            if (Playing.Value) { why = "a match is on"; return false; }
            if (Members.Count < 2) { why = "needs a second player - invite someone or add a bot"; return false; }
            if (Mode == GameMode.Teams && (Count(1) == 0 || Count(2) == 0))
            {
                why = "each team needs at least one player";
                return false;
            }
            for (int i = 0; i < Members.Count; i++)
            {
                LobbyMember m = Members[i];
                if (m.IsBot || m.ClientId == Leader.Value || m.Ready) continue;
                why = "waiting for " + NameOf(m) + " to be ready";
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- people coming and going (server)

        void Joined(ulong clientId) { Admit(clientId); }

        /// <summary>
        /// Server: a person into the room, on the smaller side, unless they are in already. Also called by
        /// the arena before it spawns a latecomer: Netcode reports the connection only once the newcomer
        /// has finished loading the map, which can be after the arena already wants to know their side.
        /// </summary>
        public void Admit(ulong clientId)
        {
            if (!IsServer || IndexOfClient(clientId) >= 0) return;
            if (clientId == NetworkManager.ServerClientId && !IsHost) return;

            // a room full of bots makes way for a person
            if (Members.Count >= MaxPlayers)
                for (int i = Members.Count - 1; i >= 0; i--)
                    if (Members[i].IsBot) { RemoveBot(Members[i].BotNumber); break; }

            Members.Add(new LobbyMember { ClientId = clientId, Team = SmallerTeam() });
            if (Leader.Value == NoLeader) Leader.Value = clientId;
            Debug.Log("[Lobby] " + clientId + " joined, " + Members.Count + " in the room");
        }

        void Left(ulong clientId)
        {
            if (!IsServer) return;
            int i = IndexOfClient(clientId);
            if (i >= 0) Members.RemoveAt(i);
            if (Leader.Value != clientId) return;

            // the next person in leads; on a server left with nobody, the room starts over empty
            Leader.Value = NoLeader;
            for (int k = 0; k < Members.Count; k++)
                if (!Members[k].IsBot) { Leader.Value = Members[k].ClientId; break; }
            if (Leader.Value == NoLeader && !IsHost) Empty();
            else Debug.Log("[Lobby] " + Leader.Value + " leads now");
        }

        /// <summary>
        /// Server, nobody left in the room: no match goes on for bots alone, and the next person finds the
        /// room as new -- none of the last group's bots, the default settings.
        /// </summary>
        void Empty()
        {
            for (int i = Members.Count - 1; i >= 0; i--)
                if (Members[i].IsBot) Members.RemoveAt(i);
            ModeIndex.Value = (int)GameMode.FreeForAll;
            KillsToWin.Value = 5;
            if (Playing.Value)
            {
                Playing.Value = false;
                NetworkManager.SceneManager.LoadScene(NetSession.LobbyScene, LoadSceneMode.Single);
            }
            Debug.Log("[Lobby] everyone has left, the room is empty again");
        }

        int SmallerTeam()
        {
            return Count(1) <= Count(2) ? 1 : 2;
        }

        // ---------------------------------------------------------------- what anybody may ask

        /// <summary>Your name, as typed in the lobby, for everyone in the room.</summary>
        public void Rename(string name)
        {
            IntroduceServerRpc(new FixedString32Bytes(NetSession.SafeName(name)));
        }

        [ServerRpc(RequireOwnership = false)]
        void IntroduceServerRpc(FixedString32Bytes name, ServerRpcParams p = default)
        {
            // A newcomer introduces themselves while still loading in -- before Netcode reports their
            // connection, which is when they would otherwise be added. Dropped then, their name never
            // arrived and they stayed "Player 2". Taken in here instead.
            Admit(p.Receive.SenderClientId);
            int i = IndexOfClient(p.Receive.SenderClientId);
            if (i < 0) return;
            LobbyMember m = Members[i];
            m.Name = new FixedString32Bytes(NetSession.SafeName(name.ToString()));
            Members[i] = m;
        }

        /// <summary>Changes sides. Not while a match is on, and not into a full side.</summary>
        public void ChooseTeam(int team) { ChooseTeamServerRpc(team); }

        [ServerRpc(RequireOwnership = false)]
        void ChooseTeamServerRpc(int team, ServerRpcParams p = default)
        {
            if (Playing.Value || (team != 1 && team != 2)) return;
            int i = IndexOfClient(p.Receive.SenderClientId);
            if (i < 0 || Members[i].Team == team || Count(team) >= Teams.PerTeam) return;
            LobbyMember m = Members[i];
            m.Team = team;
            m.Ready = false;
            Members[i] = m;
        }

        public void SetReady(bool ready) { SetReadyServerRpc(ready); }

        [ServerRpc(RequireOwnership = false)]
        void SetReadyServerRpc(bool ready, ServerRpcParams p = default)
        {
            int i = IndexOfClient(p.Receive.SenderClientId);
            if (i < 0 || Members[i].Ready == ready) return;
            LobbyMember m = Members[i];
            m.Ready = ready;
            Members[i] = m;
        }

        // ---------------------------------------------------------------- what only the leader may do
        //
        // Called on the server, they act. Called anywhere else they go to the server, which acts only if
        // they came from the leader -- in a hosted room the leader is the server and they never travel;
        // on a dedicated server they always do.

        bool FromLeader(ServerRpcParams p) { return p.Receive.SenderClientId == Leader.Value; }

        public void SetMap(int map)
        {
            if (!IsServer) { SetMapServerRpc(map); return; }
            if (Playing.Value) return;
            map = Mathf.Clamp(map, 0, NetSession.ArenaScenes.Length - 1);
            if (Map.Value == map) return;
            Map.Value = map;
            NetSession.SelectedArena = map;
            Unready();
        }

        [ServerRpc(RequireOwnership = false)]
        void SetMapServerRpc(int map, ServerRpcParams p = default) { if (FromLeader(p)) SetMap(map); }

        public void SetMode(GameMode mode)
        {
            if (!IsServer) { SetModeServerRpc((int)mode); return; }
            if (Playing.Value || Mode == mode) return;
            ModeIndex.Value = (int)mode;
            // the usual length of each: a free-for-all to 5, a team game (two or three a side) to 10
            KillsToWin.Value = mode == GameMode.Teams ? 10 : 5;
            if (mode == GameMode.Teams) Rebalance();
            Unready();
        }

        [ServerRpc(RequireOwnership = false)]
        void SetModeServerRpc(int mode, ServerRpcParams p = default)
        {
            if (FromLeader(p) && (mode == 0 || mode == 1)) SetMode((GameMode)mode);
        }

        public void SetKillsToWin(int kills)
        {
            if (!IsServer) { SetKillsToWinServerRpc(kills); return; }
            if (Playing.Value || KillsToWin.Value == kills) return;
            KillsToWin.Value = Mathf.Clamp(kills, 1, 99);
            Unready();
        }

        [ServerRpc(RequireOwnership = false)]
        void SetKillsToWinServerRpc(int kills, ServerRpcParams p = default) { if (FromLeader(p)) SetKillsToWin(kills); }

        float skillSentAt = -1f;

        public void SetBotSkill(float skill)
        {
            if (!IsServer)
            {
                // a slider being dragged: not a message every frame
                if (Mathf.Abs(BotSkill.Value - skill) <= 0.005f || Time.unscaledTime - skillSentAt < 0.15f) return;
                skillSentAt = Time.unscaledTime;
                SetBotSkillServerRpc(skill);
                return;
            }
            skill = Mathf.Clamp01(skill);
            if (Mathf.Abs(BotSkill.Value - skill) > 0.005f) BotSkill.Value = skill;
            BotBrain.Skill = skill;
        }

        [ServerRpc(RequireOwnership = false)]
        void SetBotSkillServerRpc(float skill, ServerRpcParams p = default) { if (FromLeader(p)) SetBotSkill(skill); }

        /// <summary>A bot, on the given side (or the smaller one).</summary>
        public void AddBot(int team = 0)
        {
            if (!IsServer) { AddBotServerRpc(team); return; }
            if (Playing.Value || Members.Count >= MaxPlayers) return;
            if (team != 1 && team != 2) team = SmallerTeam();
            if (Mode == GameMode.Teams && Count(team) >= Teams.PerTeam) return;

            int number = 1;
            while (IndexOfBot(number) >= 0) number++;
            Members.Add(new LobbyMember { ClientId = NetworkManager.ServerClientId, BotNumber = number,
                                          Name = new FixedString32Bytes("Bot " + number), Team = team, Ready = true });
        }

        [ServerRpc(RequireOwnership = false)]
        void AddBotServerRpc(int team, ServerRpcParams p = default) { if (FromLeader(p)) AddBot(team); }

        public void RemoveBot(int number)
        {
            if (!IsServer) { RemoveBotServerRpc(number); return; }
            int i = IndexOfBot(number);
            if (i >= 0) Members.RemoveAt(i);

            // mid-match (a person joining a full room): its body leaves the arena too
            if (!Playing.Value) return;
            foreach (PlayerNet p in PlayerNet.All.ToArray())
                if (p != null && p.IsBot && p.BotNumber.Value == number && p.NetworkObject.IsSpawned)
                    p.NetworkObject.Despawn(true);
        }

        [ServerRpc(RequireOwnership = false)]
        void RemoveBotServerRpc(int number, ServerRpcParams p = default) { if (FromLeader(p) && !Playing.Value) RemoveBot(number); }

        /// <summary>A bot over to the other side.</summary>
        public void SwapBot(int number)
        {
            if (!IsServer) { SwapBotServerRpc(number); return; }
            if (Playing.Value) return;
            int i = IndexOfBot(number);
            if (i < 0) return;
            LobbyMember m = Members[i];
            int other = m.Team == 1 ? 2 : 1;
            if (Count(other) >= Teams.PerTeam) return;
            m.Team = other;
            Members[i] = m;
        }

        [ServerRpc(RequireOwnership = false)]
        void SwapBotServerRpc(int number, ServerRpcParams p = default) { if (FromLeader(p)) SwapBot(number); }

        /// <summary>
        /// Into a team game with more than three on one side: the extra ones over to the other, bots
        /// first, so nobody who chose a side is moved while a bot could be.
        /// </summary>
        void Rebalance()
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = Members.Count - 1; i >= 0; i--)
                {
                    LobbyMember m = Members[i];
                    if ((pass == 0) != m.IsBot) continue;
                    int other = m.Team == 1 ? 2 : 1;
                    if (Count(m.Team) <= Teams.PerTeam || Count(other) >= Teams.PerTeam) continue;
                    m.Team = other;
                    Members[i] = m;
                }
        }

        /// <summary>Settings changed under everybody: they say they are ready again, for what it is now.</summary>
        void Unready()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                LobbyMember m = Members[i];
                if (m.IsBot || !m.Ready) continue;
                m.Ready = false;
                Members[i] = m;
            }
        }

        /// <summary>Everyone into the arena.</summary>
        public void StartMatch()
        {
            if (!IsServer) { StartMatchServerRpc(); return; }
            string why;
            if (!CanStart(out why)) return;

            Playing.Value = true;
            BlueScore.Value = 0;
            RedScore.Value = 0;
            NetSession.SelectedArena = Map.Value;
            BotBrain.IgnoreHumans = false;              // a training-room setting, not for a real match
            BotBrain.Skill = BotSkill.Value;
            Debug.Log("[Lobby] starting " + ModeTitles[ModeIndex.Value] + " on " + NetSession.ArenaTitles[Map.Value]
                      + " to " + KillsToWin.Value + ", " + Members.Count + " players");
            NetworkManager.SceneManager.LoadScene(NetSession.ArenaScenes[Map.Value], LoadSceneMode.Single);
        }

        [ServerRpc(RequireOwnership = false)]
        void StartMatchServerRpc(ServerRpcParams p = default) { if (FromLeader(p)) StartMatch(); }

        /// <summary>Everyone back to the lobby: the match is over, or the leader ended it.</summary>
        public void BackToLobby()
        {
            if (!IsServer) { BackToLobbyServerRpc(); return; }
            if (!Playing.Value) return;
            Playing.Value = false;
            Unready();
            NetworkManager.SceneManager.LoadScene(NetSession.LobbyScene, LoadSceneMode.Single);
        }

        [ServerRpc(RequireOwnership = false)]
        void BackToLobbyServerRpc(ServerRpcParams p = default) { if (FromLeader(p)) BackToLobby(); }

        /// <summary>Server: a kill for a side.</summary>
        public void AddScore(int team)
        {
            if (!IsServer) return;
            if (team == 1) BlueScore.Value++;
            else if (team == 2) RedScore.Value++;
        }
    }
}
