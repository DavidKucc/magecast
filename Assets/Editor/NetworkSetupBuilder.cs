using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Everything multiplayer needs that is an asset rather than code, built in one go:
    ///
    ///   Resources/RuntimeMaterials   templates so runtime spells keep their glow in a build
    ///   Prefabs/Player               what every player is spawned from
    ///   Resources/NetworkManager     the network, loaded by whichever scene runs first
    ///   Scenes/MainMenu              the front door
    ///   Arena_Blockout               the scene player taken out, the match director put in
    ///   Build Settings               menu first, arena second
    ///
    /// Same rule as the rest of the project: nothing assembled by hand, so it can all be rebuilt.
    /// </summary>
    public static class NetworkSetupBuilder
    {
        const string ResourcesFolder = "Assets/Resources";
        const string MaterialsFolder = "Assets/Resources/RuntimeMaterials";
        const string ManagerPath = "Assets/Resources/NetworkManager.prefab";
        const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string ArenaScenePath = "Assets/Scenes/Arena_Blockout.unity";

        [MenuItem("Tools/Arena/Build Network Setup")]
        public static void BuildAll()
        {
            // saved without asking: this rebuilds scenes, and a dialog would stall it when run from a tool
            EditorSceneManager.SaveOpenScenes();

            BuildMaterials();
            GameObject player = PlayerRigBuilder.BuildPrefab();
            BuildManager(player);
            BuildMenuScene();
            PrepareArenaScene();
            SetBuildScenes();

            AssetDatabase.SaveAssets();
            Debug.Log("[Net] setup built: player prefab, network manager, menu scene, arena prepared, build scenes set");
        }

        // ---------------------------------------------------------------- materials

        [MenuItem("Tools/Arena/Build Runtime Materials")]
        public static void BuildMaterials()
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder(MaterialsFolder);

            Material emissive = Asset(MaterialsFolder + "/Emissive.mat", "Standard");
            emissive.EnableKeyword("_EMISSION");
            emissive.SetColor("_EmissionColor", Color.white);
            emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(emissive);

            Material fade = Asset(MaterialsFolder + "/Fade.mat", "Standard");
            MageCast.Gestures.CastShield.MakeTransparent(fade);
            fade.EnableKeyword("_EMISSION");
            fade.SetColor("_EmissionColor", Color.white);
            fade.color = new Color(1f, 1f, 1f, 0.4f);
            fade.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(fade);

            Asset(MaterialsFolder + "/Line.mat", "Sprites/Default");
            AssetDatabase.SaveAssets();

            // Once more after the save. On a freshly created asset the Standard shader's own keyword
            // validation ran on import and dropped _EMISSION -- measured, the first build shipped a
            // template with no emission at all, which is exactly the variant this file exists to keep.
            emissive.EnableKeyword("_EMISSION");
            fade.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(emissive);
            EditorUtility.SetDirty(fade);
            AssetDatabase.SaveAssets();
        }

        static Material Asset(string path, string shader)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader == null || m.shader.name != shader)
            {
                m.shader = Shader.Find(shader);
            }
            return m;
        }

        // ---------------------------------------------------------------- network manager

        static void BuildManager(GameObject playerPrefab)
        {
            EnsureFolder(ResourcesFolder);

            GameObject go = new GameObject("NetworkManager");
            NetworkManager manager = go.AddComponent<NetworkManager>();
            UnityTransport transport = go.AddComponent<UnityTransport>();

            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.PlayerPrefab = null;          // spawned by MatchDirector, at a spawn point
            manager.NetworkConfig.TickRate = 30;

            NetSession session = go.AddComponent<NetSession>();
            SerializedObject so = new SerializedObject(session);
            so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, ManagerPath);
            Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- scenes

        static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.075f, 0.1f);
            camGo.AddComponent<AudioListener>();

            GameObject menu = new GameObject("Menu");
            menu.AddComponent<MainMenu>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        static void PrepareArenaScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);

            // The player used to live in the scene. Now there is one per person, spawned by the network,
            // and a leftover in the scene would be an extra body nobody controls.
            GameObject old = GameObject.Find("Player");
            if (old != null) Object.DestroyImmediate(old);

            PlayerRigBuilder.EnsureGameCamera();

            GameObject match = GameObject.Find("Match");
            if (match == null) match = new GameObject("Match");
            if (match.GetComponent<MatchDirector>() == null) match.AddComponent<MatchDirector>();
            if (match.GetComponent<ArenaHud>() == null) match.AddComponent<ArenaHud>();

            // Every dummy is networked, so both players watch the same one take the same hit.
            int dummies = 0;
            foreach (MageCast.Combat.TrainingDummy d in Object.FindObjectsOfType<MageCast.Combat.TrainingDummy>())
            {
                if (d.GetComponent<NetworkObject>() == null) d.gameObject.AddComponent<NetworkObject>();
                dummies++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Net] arena prepared: " + dummies + " networked dummies, match director in place");
        }

        static void SetBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(ArenaScenePath, true),
                new EditorBuildSettingsScene(ArenaTempleBuilder.ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
