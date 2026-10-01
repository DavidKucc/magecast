using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Puts players into the arena: the server spawns one character per connected player at a spawn
    /// point, including anyone who joins halfway through.
    ///
    /// Also what makes the arena scene playable on its own. Press Play on it in the editor with no
    /// game running and it starts a training session by itself -- the arena was always tested that
    /// way, and having to go through the menu for every tweak would be a tax on every tweak.
    /// </summary>
    public class MatchDirector : MonoBehaviour
    {
        static MatchDirector instance;

        /// <summary>A match is won by the first to this many kills; then everyone starts over.</summary>
        public const int KillsToWin = 5;
        /// <summary>How long the result stays up before the next match starts.</summary>
        public const float IntermissionSeconds = 6f;

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

            foreach (ulong id in manager.ConnectedClientsIds) SpawnFor(id);

            // Latecomers load the arena after the host is already in it; they are ready for a body
            // once Netcode says their copy of the scene is in step with ours.
            manager.SceneManager.OnSynchronizeComplete += SpawnFor;
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
                manager.SceneManager.OnSynchronizeComplete -= SpawnFor;
        }

        void SpawnFor(ulong clientId)
        {
            NetworkClient client;
            if (!manager.ConnectedClients.TryGetValue(clientId, out client)) return;
            if (client.PlayerObject != null) return;

            GameObject prefab = NetSession.Instance != null ? NetSession.Instance.PlayerPrefab : null;
            if (prefab == null) { Debug.LogError("[Match] no player prefab on the NetworkManager"); return; }

            Transform spawn = PickSpawn(null);
            Vector3 at = spawn != null ? spawn.position : Vector3.zero;
            Quaternion facing = spawn != null ? spawn.rotation : Quaternion.identity;

            GameObject player = Instantiate(prefab, at + Vector3.up * 0.1f, facing);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
            Debug.Log("[Match] player " + clientId + " in at " + (spawn != null ? spawn.name : "the origin"));
        }

        /// <summary>
        /// Server: a kill was just counted. At the limit the match ends -- the result goes up everywhere,
        /// and a few seconds later the scores are wiped and everyone starts again from opposite spawns.
        /// Training has no matches.
        /// </summary>
        public static void Scored(PlayerNet killer)
        {
            if (instance == null || instance.matchOver || killer == null) return;
            if (NetSession.Instance != null && NetSession.Instance.Current == NetSession.Mode.Training) return;
            if (killer.Kills.Value < KillsToWin) return;

            instance.matchOver = true;
            var score = new System.Text.StringBuilder();
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (score.Length > 0) score.Append("    ");
                score.Append(p.DisplayName).Append(' ').Append(p.Kills.Value);
            }
            killer.MatchWonClientRpc(killer.DisplayName, score.ToString());
            instance.StartCoroutine(instance.NextMatch());
        }

        IEnumerator NextMatch()
        {
            yield return new WaitForSeconds(IntermissionSeconds);
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

        /// <summary>
        /// The spawn point furthest from every other living player that none of them can see, so nobody
        /// appears in somebody's crosshair. Only when every point is in sight does it settle for the
        /// furthest one. The first player in gets the middle A spawn, which faces into the arena.
        /// </summary>
        public static Transform PickSpawn(PlayerNet forWhom)
        {
            if (instance == null || instance.spawns.Count == 0) return null;

            var others = new List<Vector3>();
            foreach (PlayerNet p in PlayerNet.All)
            {
                if (p == forWhom) continue;
                Combat.Health h = p.GetComponent<Combat.Health>();
                if (h != null && h.IsDead) continue;
                others.Add(p.transform.position);
            }

            if (others.Count == 0)
            {
                foreach (Transform t in instance.spawns) if (t.name == "SpawnA_1") return t;
                return instance.spawns[0];
            }

            Transform best = null;
            float bestScore = float.MinValue;
            foreach (Transform t in instance.spawns)
            {
                float nearest = float.MaxValue;
                bool seen = false;
                foreach (Vector3 o in others)
                {
                    nearest = Mathf.Min(nearest, Vector3.Distance(t.position, o));
                    if (!seen && InSight(o + Vector3.up * 1.6f, t.position + Vector3.up * 1.6f)) seen = true;
                }
                float score = nearest - (seen ? 1000f : 0f);
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
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
