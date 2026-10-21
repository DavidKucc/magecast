using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace MageCast.World
{
    /// <summary>
    /// The arena camera's finish: bloom, so a glow that is brighter than white spills light around it
    /// the way the spell packs were made to be seen; ACES tone mapping, so a bright sky or a fireball
    /// rolls off instead of clipping flat; a light grade and vignette. Post Processing v2, which on the
    /// web grades through a 2D lookup table (WebGL has no compute shaders) -- measured on the web build.
    ///
    /// This asset (Resources/PostFx, made by Tools > Arena > Build Post Fx) carries the profile and the
    /// package's shaders into a build. The camera gets its layer and a global volume when an arena
    /// loads, so no scene holds a copy that the map builders could drop. Menus and the lobby stay plain.
    /// GameSettings.Effects switches it off for a machine that can't keep up.
    /// </summary>
    public class PostFx : ScriptableObject
    {
        public PostProcessResources resources;
        public PostProcessProfile profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (scene, mode) => Dress(Camera.main);
            Dress(Camera.main);
        }

        static void Dress(Camera cam)
        {
            if (cam == null || cam.GetComponent<ThirdPersonCamera>() == null) return;
            if (cam.GetComponent<PostProcessLayer>() != null) return;
            PostFx fx = Resources.Load<PostFx>("PostFx");
            if (fx == null || fx.resources == null || fx.profile == null) return;

            // on the camera itself, so it goes with the camera's scene
            PostProcessVolume volume = cam.gameObject.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.sharedProfile = fx.profile;

            PostProcessLayer layer = cam.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(fx.resources);
            layer.volumeTrigger = cam.transform;
            layer.volumeLayer = 1 << cam.gameObject.layer;
            // no MSAA on the web; the fast FXAA is one cheap pass over the finished picture
            layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
            layer.fastApproximateAntialiasing.fastMode = true;
            layer.finalBlitToCameraTarget = true;
            layer.enabled = GameSettings.Effects;
        }

        /// <summary>Puts GameSettings.Effects into effect on the camera now showing, if it has them.</summary>
        public static void Apply()
        {
            Camera cam = Camera.main;
            PostProcessLayer layer = cam != null ? cam.GetComponent<PostProcessLayer>() : null;
            if (layer != null) layer.enabled = GameSettings.Effects;
        }
    }
}
