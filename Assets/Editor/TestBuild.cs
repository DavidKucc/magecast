using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Builds the Windows test copy: a folder with MageCast.exe that can be zipped and sent to a tester.
    ///
    /// Windowed and resizable rather than full screen, because the way this gets tested is two copies
    /// side by side -- and a full-screen game that loses focus to the other copy minimises itself.
    /// </summary>
    public static class TestBuild
    {
        public const string OutputFolder = "Builds/MageCast";

        /// <summary>Stamps the build time into Resources/BuildInfo.txt, which the menus show.</summary>
        static void StampBuildInfo()
        {
            string path = "Assets/Resources/BuildInfo.txt";
            System.IO.File.WriteAllText(path, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        public const string ExeName = "MageCast.exe";

        [MenuItem("Tools/Arena/Build Windows Test")]
        public static void Build()
        {
            // Saved first: a scene left dirty makes the build stop on a "save changes?" dialog, which
            // nobody sees when the build was started from a tool, and it then waits forever.
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            PlayerSettings.productName = "Mage Cast";
            PlayerSettings.companyName = "Mage Cast";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            StampBuildInfo();

            string[] scenes = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] no scenes in Build Settings - run Tools > Arena > Build Network Setup first");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputFolder + "/" + ExeName,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log(string.Format("[Build] OK: {0} ({1:F1} MB, {2:F0} s)", options.locationPathName,
                                        summary.totalSize / (1024f * 1024f), summary.totalTime.TotalSeconds));
            else
                Debug.LogError("[Build] " + summary.result + " with " + summary.totalErrors + " error(s)");
        }

        public const string WebFolder = "Builds/Web";

        /// <summary>
        /// Builds the browser version into Builds/Web, ready to put on GitHub Pages.
        ///
        /// Compressed with gzip AND told to decompress for itself if the server does not. GitHub Pages
        /// serves the compressed files without saying they are compressed, and without the fallback the
        /// page just sits on a black canvas. Stripping is kept minimal because Netcode and the Unity
        /// services look things up by reflection, and a stripped-away type only fails once someone
        /// tries to join a room -- in the browser, where it is hardest to see why.
        ///
        /// Switches the editor back to Windows afterwards, so opening the project next time does not
        /// start by reimporting everything for the web.
        /// </summary>
        [MenuItem("Tools/Arena/Build Web")]
        public static void BuildWeb()
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            PlayerSettings.productName = "Mage Cast";
            PlayerSettings.companyName = "Mage Cast";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            // Named by content, so a new build is a new file the browser has never seen. With fixed names
            // a browser could keep serving the previous build from its cache for a while after publishing.
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            StampBuildInfo();
            PlayerSettings.WebGL.template = "PROJECT:MageCast";     // Assets/WebGLTemplates/MageCast
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Minimal);

            string[] scenes = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] no scenes in Build Settings - run Tools > Arena > Build Network Setup first");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WebFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                // GitHub Pages runs sites through Jekyll, which quietly drops folders starting with an
                // underscore; this file turns that off before it can bite.
                System.IO.File.WriteAllText(System.IO.Path.Combine(WebFolder, ".nojekyll"), "");
                Debug.Log(string.Format("[Build] Web OK: {0} ({1:F1} MB, {2:F0} s)", WebFolder,
                                        summary.totalSize / (1024f * 1024f), summary.totalTime.TotalSeconds));
            }
            else
            {
                Debug.LogError("[Build] Web " + summary.result + " with " + summary.totalErrors + " error(s)");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }
    }
}
