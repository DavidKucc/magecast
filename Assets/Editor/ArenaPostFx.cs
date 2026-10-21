using System.IO;
using MageCast.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Builds the arena camera's post-processing (World.PostFx) from the constants below, and captures
    /// a view with and without it so a change is judged side by side and by numbers, not by memory.
    ///
    ///   bloom     only what is brighter than white: spells, lit runes, the sun, fire -- not pale stone
    ///   grade     ACES tone mapping, so a bright sky or a fireball rolls off instead of clipping flat;
    ///             it pulls the middle tones down, which the exposure gives back
    ///   vignette  light, to hold the eye in the middle where the aim is
    ///
    /// The capture renders through a temporary camera into a texture -- the editor's game view comes
    /// out paler than a build, a camera rendering into a texture matches the web build.
    /// </summary>
    public static class ArenaPostFx
    {
        public const string AssetPath = "Assets/Resources/PostFx.asset";
        const string ProfilePath = "Assets/Settings/Arena_PostFx.asset";
        const string ResourcesPath = "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";

        const float BloomThreshold = 1f, BloomKnee = 0.5f, BloomIntensity = 4f, BloomDiffusion = 7f;
        const float Exposure = 1f, Contrast = 5f, Saturation = 5f;
        const float VignetteIntensity = 0.2f, VignetteSmoothness = 0.5f;

        [MenuItem("Tools/Arena/Build Post Fx")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Bloom bloom = Effect<Bloom>(profile);
            bloom.threshold.Override(BloomThreshold);
            bloom.softKnee.Override(BloomKnee);
            bloom.intensity.Override(BloomIntensity);
            bloom.diffusion.Override(BloomDiffusion);

            ColorGrading grade = Effect<ColorGrading>(profile);
            grade.gradingMode.Override(GradingMode.HighDefinitionRange);
            grade.tonemapper.Override(Tonemapper.ACES);
            grade.postExposure.Override(Exposure);
            grade.contrast.Override(Contrast);
            grade.saturation.Override(Saturation);

            Vignette vignette = Effect<Vignette>(profile);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(VignetteSmoothness);

            PostFx fx = AssetDatabase.LoadAssetAtPath<PostFx>(AssetPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<PostFx>();
                AssetDatabase.CreateAsset(fx, AssetPath);
            }
            fx.resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>(ResourcesPath);
            fx.profile = profile;

            EditorUtility.SetDirty(profile);
            foreach (PostProcessEffectSettings s in profile.settings) EditorUtility.SetDirty(s);
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[PostFx] built " + ProfilePath + " (" + profile.settings.Count + " effects)");
        }

        static T Effect<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
        {
            T e = profile.GetSetting<T>();
            if (e == null)
            {
                e = profile.AddSettings<T>();
                AssetDatabase.AddObjectToAsset(e, profile);
            }
            e.enabled.Override(true);
            return e;
        }

        /// <summary>
        /// Renders the open scene from a pose into a PNG, with or without the effects, and returns its mean
        /// brightness and how much of it is clipped to white -- what tone mapping is there to fix.
        /// </summary>
        public static string Capture(string file, Vector3 position, Vector3 euler, bool effects, int width = 1280, int height = 720)
        {
            PostFx fx = AssetDatabase.LoadAssetAtPath<PostFx>(AssetPath);
            GameObject go = new GameObject("PostFx Capture") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture hdr = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR);
            RenderTexture ldr = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                Camera cam = go.AddComponent<Camera>();
                if (Camera.main != null) cam.CopyFrom(Camera.main);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
                cam.allowHDR = true;
                cam.targetTexture = hdr;
                if (effects && fx != null)
                {
                    PostProcessVolume volume = go.AddComponent<PostProcessVolume>();
                    volume.isGlobal = true;
                    volume.sharedProfile = fx.profile;
                    PostProcessLayer layer = go.AddComponent<PostProcessLayer>();
                    layer.Init(fx.resources);
                    layer.volumeTrigger = go.transform;
                    layer.volumeLayer = 1 << go.layer;
                    layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                    layer.fastApproximateAntialiasing.fastMode = true;
                }
                cam.Render();
                Graphics.Blit(hdr, ldr);

                RenderTexture.active = ldr;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, tex.EncodeToPNG());

                Color32[] px = tex.GetPixels32();
                double sum = 0;
                int clipped = 0;
                foreach (Color32 c in px)
                {
                    sum += 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
                    if (c.r >= 254 || c.g >= 254 || c.b >= 254) clipped++;
                }
                return string.Format("{0}: mean {1:0.0}/255, clipped {2:0.00}%", Path.GetFileName(file),
                                     sum / px.Length, 100.0 * clipped / px.Length);
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(go);
                hdr.Release();
                Object.DestroyImmediate(hdr);
                RenderTexture.ReleaseTemporary(ldr);
                Object.DestroyImmediate(tex);
            }
        }
    }
}
