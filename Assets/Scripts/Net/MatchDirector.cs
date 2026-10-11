using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageCast
{
    /// <summary>
    /// Puts players into the arena: the server spawns one character per connected player at a spawn
    /// point, including anyone who joins halfway through, and the lobby's bots -- each on their side.
    ///
    /// Also what makes the arena scene playable on its own. Press Play on it in the editor with no
    /// game running and it starts a training session by itself -- the arena was always tested that
    /// way, and having to go through the menu for every tweak would be a tax on every tweak.
    /// </summary>
    public class MatchDirector : MonoBehaviour
    {
        static MatchDirector instance;

        /// <summary>A match is won by the first to this many kills, when there is no lobby to say otherwise.</summary>
        public const int KillsToWin = 5;
        /// <summary>How long the result stays up before the next match starts (or everyone goes back to the lobby).</summary>
        public const float IntermissionSeconds = 6f;

        /// <summary>Kills that win this match: the lobby's setting, a player's or a side's.</summary>
        public static int Target { get { return Lobby.Active ? Lobby.Instance.KillsToWin.Value : KillsToWin; } }

        bool matchOver;

        NetworkManager manager;
        readonly List<Transform> spawns = new List<Transform>();

        void Awake()
        {
            instance = this;
            Transform root = GameObject.Find("Arena/Spawns") != null ? GameObject.Find("Arena/Spawns").transform : null;
            if (root != null) foreach (Transform t in root) spawns.Add(t);
        }

        void Start()
        {
            manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                NetSession session = NetSession.Ensure();
                if (session == null) return;
                session.StartTraining(false);
                manager = NetworkManager.Singleton;
            }

            // The practice dummies are for training. In a real match they are red capsules soaking up
            // shots meant for the other player. Every machine puts its own copy away -- no network
            // traffic, nothing for a late joiner to miss -- and with the colliders off, the server's
            // copy cannot be hit either.
            if (NetSession.Instance != null && NetSession.Instance.Current != NetSession.Mode.Training)
                foreach (Combat.TrainingDummy dummy in FindObjectsOfType<Combat.TrainingDummy>())
                    PutAway(dummy.gameObject);

            if (!manager.IsServer) return;

            // Whoever is here already: the host, alone in training or in a room that opened straight
            // into the arena.
            foreach (ulong id in manager.ConnectedClientsIds)
                if (id == manager.LocalClientId) SpawnFor(id);

            // Everyone else gets a body once their copy of the map is complete: coming from the lobby
            // they finish loading it after the server did, and latecomers load it on joining.
            manager.SceneManager.OnLoadComplete += LoadedHere;
            manager.SceneManager.OnSynchronizeComplete += SpawnFor;

            // the lobby's bots, each on its side
            if (Lobby.Active)
            {
                BotBrain.Skill = Lobby.Instance.BotSkill.Value;
                foreach (LobbyMember m in Lobby.Instance.Members)
                    if (m.IsBot) SpawnBot(m.BotNumber, Lobby.Instance.PlaysOn(m), Lobby.NameOf(m));
            }
        }

        void LoadedHere(ulong clientId, string sceneName, LoadSceneMode mode)
        {
            if (sceneName == gameObject.scene.name) SpawnFor(clientId);
        }

        // ---------------------------------------------------------------- bots

        static int botsMade;

        /// <summary>Bots in the game right now.</summary>
        public static int BotCount
        {
            get { int n = 0; foreach (PlayerNet p in PlayerNet.All) if (p != null && p.IsBot) n++; return n; }
        }

        /// <summary>Server: a training bot, the next number up, on nobody's side.</summary>
        public static void SpawnBot()
        {
            SpawnBot(++botsMade, Teams.None, null);
        }

        /// <summary>
        /// Server: a computer player, in the same body as everybody's, at the spawn furthest from the
        /// living (on its own side in a team game). It belongs to the server; its BotBrain lives only
        /// there, and everyone else sees it the way they see any other player.
        /// </summary>
        public static void SpawnBot(int number, int team, string name)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || NetSession.Instance == null) return;
            GameObject prefab = NetSession.Instance.PlayerPrefab;
            if (prefab == null) return;

            Transform spawn = PickSpawn(null, team);
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            Quaternion facing = spawn != null ? spawn.rotation : Quaternion.identity;
            PlayerNet.SpawningBot = number;
            GameObject bot = Instantiate(prefab, at + Vector3.up * 0.1f, facing);
            bot.GetComponent<NetworkObject>().Spawn(true);
            PlayerNet net = bot.GetComponent<PlayerNet>();
            net.Team.Value = team;
            if (!string.IsNullOrEmpty(name)) net.PlayerName.Value = new Unity.Collections.FixedString32Bytes(name);
            bot.AddComponent<BotBrain>();
            Debug.Log("[Match] bot " + number + (team != Teams.None ? " (" + Teams.Names[team] + ")" : "")
                      + " in at " + (spawn != null ? spawn.name : "the origin"));
        }

        /// <summary>Server: every bot out of the game.</summary>
        public static void RemoveBots()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            foreach (PlayerNet p in PlayerNet.All.ToArray())
                if (p != null && p.IsBot && p.NetworkObject != null && p.NetworkObject.IsSpawned)
                    p.NetworkObject.Despawn(true);
        }

        static void PutAway(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Canvas c in go.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
            Combat.HealthBar bar = go.GetComponent<Combat.HealthBar>();
            if (bar != null) bar.enabled = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (manager != null && manager.SceneManager != null)
            {
                manager.SceneManager.OnSynchronizeComplete -= SpawnFor;
                manager.SceneManager.OnLoadComplete -= LoadedHere;
            }
        }

        void SpawnFor(ulong clientId)
        {
            NetworkClient client;
            if (!manager.ConnectedClients.TryGetValue(clientId, out client)) return;
            if (client.PlayerObject != null) return;

            GameObject prefab = NetSession.Instance != null ? NetSession.Instance.PlayerPrefab : null;
            if (prefab == null) { Debug.LogError("[Match] no player prefab on the NetworkManager"); return; }

            if (Lobby.Active) Lobby.Instance.Admit(clientId);
            int team = Lobby.TeamOfClient(clientId);
            Transform spawn = PickSpawn(null, team);
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            Quaternion facing = spawn != null ? spawn.rotation : Quaternion.identity;

            GameObject player = Instantiate(prefab, at + Vector3.up * 0.1f, facing);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            player.GetComponent<PlayerNet>().Team.Value = team;
            Debug.Log("[Match] player " + clientId + (team != Teams.None ? " (" + Teams.Names[team] + ")" : "")
                      + " in at " + (spawn != null ? spawn.name : "the origin"));
        }

        /// <summary>
        /// Server: a kill was just counted. At the limit the match ends -- the result goes up everywhere,
        /// and a few seconds later everyone is back in the lobby (or, with no lobby, the scores are wiped
        /// and everyone starts again from opposite spawns). In a team game the side's kills count.
        /// Training has no matches.
        /// </summary>
        public static void Scored(PlayerNet killer)
        {
            if (instance == null || instance.matchOver || killer == null) return;
            if (NetSession.Instance != null && NetSession.Instance.Current == NetSession.Mode.Training) return;

            var score = new System.Text.StringBuilder();
            string winner;
            int side = killer.Team.Value;
            if (Lobby.TeamGame && side != Teams.None)
            {
                Lobby.Instance.AddScore(side);
                if (Lobby.Instance.Score(side) < Target) return;
                winner = Teams.Names[side].ToUpperInvariant() + " TEAM";
                score.Append(Teams.Names[1]).Append(' ').Append(Lobby.Instance.Score(1))
                     .Append("  :  ").Append(Lobby.Instance.Score(2)).Append(' ').Append(Teams.Names[2]);
            }
            else
            {
                if (killer.Kills.Value < Target) return;
                side = Teams.None;
                winner = killer.DisplayName;
                foreach (PlayerNet p in PlayerNet.All)
                {
                    if (score.Length > 0) score.Append("    ");
                    score.Append(p.DisplayName).Append(' ').Append(p.Kills.Value);
                }
            }

            instance.matchOver = true;
            killer.MatchWonClientRpc(winner, score.ToString(), side);
            instance.StartCoroutine(instance.NextMatch());
        }

        IEnumerator NextMatch()
        {
            yield return new WaitForSeconds(IntermissionSeconds);

            if (Lobby.Active)
            {
                Lobby.Instance.BackToLobby();
                yield break;
            }

            matchOver = false;
            var players = new List<PlayerNet>(PlayerNet.All);
            players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            for (int i = 0; i < players.Count; i++)
            {
                players[i].Kills.Value = 0;
                players[i].Deaths.Value = 0;
                players[i].ServerRespawnAt(StartSpawn(i));
            }
            Debug.Log("[Match] next match, " + players.Count + " players");
        }

        /// <summary>
        /// Where the i-th player starts a match: the two middle spawns first (the fair 1v1 pair, facing
        /// each other across the map), then the side ones, alternating sides.
        /// </summary>
        static Transform StartSpawn(int i)
        {
            string[] order = { "SpawnA_1", "SpawnB_1", "SpawnA_0", "SpawnB_0", "SpawnA_2", "SpawnB_2" };
            if (i < order.Length)
                foreach (Transform t in instance.spawns) if (t.name == order[i]) return t;
            return instance.spawns.Count > 0 ? instance.spawns[i % instance.spawns.Count] : null;
        }

        /// <summary>Which spawns are a side's own: A for Blue, B for Red.</summary>
        static bool OnSide(Transform spawn, int team)
        {
            if (team == 1) return spawn.name.StartsWith("SpawnA_");
            if (team == 2) return spawn.name.StartsWith("SpawnB_");
            return true;
        }

        /// <summary>Server: the spawn point for <paramref name="forWhom"/>, on their side.</summary>
        public static Transform PickSpawn(PlayerNet forWhom)
        {
            return PickSpawn(forWhom, forWhom != null ? forWhom.Team.Value : Teams.None);
        }

        /// <summary>
        /// The spawn point furthest from every living enemy that none of them can see, so nobody appears
        /// in somebody's crosshair -- and not on top of anybody already standing there. In a team game,
        /// one of the side's own three; only if the enemy watches all of those, anywhere. Only when every
        /// point is in sight does it settle for the furthest one. The first player in gets the middle spawn
        /// of their side (A for anyone without one), which faces into the arena.
        /// </summary>
        public static Transform PickSpawn(PlayerNet forWhom, int team)
        {
            if (instance == null || instance.spawns.Count == 0) return null;

            var enemies = new List<Vector3>();
            var everyone = new List<Vector3>();
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (p == forWhom) continue;
                Combat.Health h = p.GetComponent<Combat.Health>();
                if (h != null && h.IsDead) continue;
                everyone.Add(p.transform.position);
                if (team == Teams.None || p.Team.Value != team) enemies.Add(p.transform.position);
            }

            Transform best = Best(team, enemies, everyone);
            if (team != Teams.None && (best == null || Score(best, enemies, everyone) < -500f))
            {
                Transform anywhere = Best(Teams.None, enemies, everyone);
                if (anywhere != null && (best == null || Score(anywhere, enemies, everyone) > Score(best, enemies, everyone)))
                    best = anywhere;
            }
            return best;
        }

        static Transform Best(int team, List<Vector3> enemies, List<Vector3> everyone)
        {
            Transform best = null;
            float bestScore = float.MinValue;
            foreach (Transform t in instance.spawns)
            {
                if (!OnSide(t, team)) continue;
                float score = Score(t, enemies, everyone);
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        static float Score(Transform t, List<Vector3> enemies, List<Vector3> everyone)
        {
            float score;
            if (enemies.Count == 0)
            {
                // nobody to hide from: the middle spawn of the side first, then the others
                score = t.name.EndsWith("_1") ? 10f : 0f;
                if (t.name.StartsWith("SpawnA_")) score += 1f;
            }
            else
            {
                float nearest = float.MaxValue;
                bool seen = false;
                foreach (Vector3 o in enemies)
                {
                    nearest = Mathf.Min(nearest, Vector3.Distance(t.position, o));
                    if (!seen && InSight(o + Vector3.up * 1.6f, t.position + Vector3.up * 1.6f)) seen = true;
                }
                score = nearest - (seen ? 1000f : 0f);
            }
            // somebody already standing on it: the next one along
            foreach (Vector3 o in everyone)
                if (Vector3.Distance(t.position, o) < 1.5f) { score -= 100f; break; }
            return score;
        }

        /// <summary>A clear line between two eyes: only the map counts, not people, dummies or barriers.</summary>
        static bool InSight(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            foreach (RaycastHit h in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                Collider c = h.collider;
                if (c.GetComponentInParent<PlayerNet>() != null) continue;
                if (c.GetComponentInParent<Combat.TrainingDummy>() != null) continue;
                if (c.GetComponentInParent<Gestures.CastShield>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
