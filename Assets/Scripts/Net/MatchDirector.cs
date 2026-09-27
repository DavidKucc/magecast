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

            if (!manager.IsServer) return;

            foreach (ulong id in manager.ConnectedClientsIds) SpawnFor(id);

            // Latecomers load the arena after the host is already in it; they are ready for a body
            // once Netcode says their copy of the scene is in step with ours.
            manager.SceneManager.OnSynchronizeComplete += SpawnFor;
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
        /// The spawn point furthest from every other living player, so nobody appears in somebody's
        /// crosshair. The first player in gets the middle A spawn, which faces into the arena.
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
            float bestDistance = -1f;
            foreach (Transform t in instance.spawns)
            {
                float nearest = float.MaxValue;
                foreach (Vector3 o in others) nearest = Mathf.Min(nearest, Vector3.Distance(t.position, o));
                if (nearest > bestDistance) { bestDistance = nearest; best = t; }
            }
            return best;
        }
    }
}
