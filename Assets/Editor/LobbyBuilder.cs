using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageCast.EditorTools
{
    /// <summary>
    /// What the online lobby needs that is an asset rather than code:
    ///
    ///   Prefabs/Lobby                  the networked lobby object, registered on the NetworkManager
    ///   Scenes/Lobby                   the room between matches (LobbyScreen draws it)
    ///   Resources/MapPreviews/*.png    a picture of every map, for the map cards
    ///
    /// Rebuildable like everything else: Tools > Arena > Build Lobby, and Build Map Previews after a map
    /// changes.
    /// </summary>
    public static class LobbyBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Lobby.prefab";
        public const string ScenePath = "Assets/Scenes/Lobby.unity";
        const string ManagerPath = "Assets/Resources/NetworkManager.prefab";
        const string PreviewFolder = "Assets/Resources/MapPreviews";

        [MenuItem("Tools/Arena/Build Lobby")]
        public static void Build()
        {
            EditorSceneManager.SaveOpenScenes();

            GameObject prefab = BuildPrefab();
            WireManager(prefab);
            BuildScene();
            NetworkSetupBuilder.SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lobby] built: prefab, scene, registered on the NetworkManager, in the build");
        }

        public static GameObject BuildPrefab()
        {
            GameObject go = new GameObject("Lobby");
            go.AddComponent<NetworkObject>();
            go.AddComponent<Lobby>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>The NetworkManager prefab gets the lobby prefab, which NetSession registers and spawns.</summary>
        public static void WireManager(GameObject lobbyPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ManagerPath);
            try
            {
                NetSession session = root.GetComponent<NetSession>();
                SerializedObject so = new SerializedObject(session);
                so.FindProperty("lobbyPrefab").objectReferenceValue = lobbyPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ManagerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.075f, 0.1f);
            camGo.AddComponent<AudioListener>();

            new GameObject("LobbyScreen").AddComponent<LobbyScreen>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---------------------------------------------------------------- map pictures

        [MenuItem("Tools/Arena/Build Map Previews")]
        public static void BuildPreviews()
        {
            EditorSceneManager.SaveOpenScenes();
            if (!AssetDatabase.IsValidFolder(PreviewFolder)) AssetDatabase.CreateFolder("Assets/Resources", "MapPreviews");

            foreach (string name in NetSession.ArenaScenes)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Single);

                // the practice dummies are put away in every real match, so not in its picture either
                // (the scene is reopened unsaved afterwards)
                foreach (Combat.TrainingDummy d in Object.FindObjectsOfType<Combat.TrainingDummy>())
                    foreach (Renderer r in d.GetComponentsInChildren<Renderer>()) r.enabled = false;

                string path = PreviewFolder + "/" + name + ".png";
                // Blockout's middle is one tall block: from higher up, so the floor around it shows
                bool steep = name == "Arena_Blockout";
                File.WriteAllBytes(path, Render(steep ? 0.7f : 0.62f, steep ? 0.85f : 0.42f));
                AssetDatabase.ImportAsset(path);
                TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 1024;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
                Debug.Log("[Lobby] map preview " + path);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/" + NetSession.MenuScene + ".unity", OpenSceneMode.Single);
        }

        /// <summary>
        /// The map from above and behind the A side, turned a little off the axis between the two middle
        /// spawns so the picture has depth: the whole floor in view, the far side in the distance.
        /// </summary>
        static byte[] Render(float back, float up)
        {
            Transform a = Find("SpawnA_1"), b = Find("SpawnB_1");
            Vector3 pa = a != null ? a.position : new Vector3(0f, 0f, -15f);
            Vector3 pb = b != null ? b.position : new Vector3(0f, 0f, 15f);
            Vector3 mid = (pa + pb) * 0.5f;
            Vector3 axis = pb - pa;
            axis.y = 0f;
            float length = Mathf.Max(axis.magnitude, 20f);
            Vector3 dir = Quaternion.Euler(0f, 35f, 0f) * axis.normalized;

            const int W = 960, H = 540;
            var go = new GameObject("PreviewCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            cam.transform.position = mid - dir * (length * back) + Vector3.up * (length * up);
            cam.transform.LookAt(mid + Vector3.down * (length * 0.06f));

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture keep = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = keep;

            byte[] png = tex.EncodeToPNG();
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
            return png;
        }

        static Transform Find(string spawn)
        {
            GameObject g = GameObject.Find("Arena/Spawns/" + spawn);
            return g != null ? g.transform : null;
        }
    }
}
