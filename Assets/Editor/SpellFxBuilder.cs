using MageCast.Gestures;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Fills Resources/SpellFx.asset with the effect prefabs each spell uses. Rerunnable: it only sets
    /// the spells it knows about and leaves anything else in the asset alone.
    ///
    /// The effects come from the Vefects packs in Assets/Vefects, which are bought assets and kept out of
    /// the public repository. Where a pack is missing the entry is simply left empty and the spell falls
    /// back to the placeholder sphere.
    /// </summary>
    public static class SpellFxBuilder
    {
        const string AssetPath = "Assets/Resources/SpellFx.asset";
        const string Magic = "Assets/Vefects/Stylized VFX/Stylized VFX Shuriken/Skills/Magic Attacks/";

        [MenuItem("Tools/Arena/Assign Spell Effects")]
        public static void Assign()
        {
            SpellFx fx = AssetDatabase.LoadAssetAtPath<SpellFx>(AssetPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<SpellFx>();
                AssetDatabase.CreateAsset(fx, AssetPath);
            }

            // Fire: the Vefects fire magic set -- a pentacle flare in the hands, a trailing fireball and a
            // burst, each carrying its own sound (cast, a flight loop, the hit).
            Set(fx, "FIRE", Magic + "Fire/VFX_Fire_Magic_Cast.prefab", Magic + "Fire/VFX_Fire_Magic_Projectile.prefab",
                Magic + "Fire/VFX_Fire_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.5f,
                // the pentacle is a plane facing its local X; turned so it faces along the shot, toward the camera
                castTurn: new Vector3(0f, -90f, 0f));

            // Ice: the matching ice set, same pieces, same orientation.
            Set(fx, "ICE", Magic + "Ice/VFX_Ice_Magic_Cast.prefab", Magic + "Ice/VFX_Ice_Magic_Projectile.prefab",
                Magic + "Ice/VFX_Ice_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.5f,
                castTurn: new Vector3(0f, -90f, 0f));

            // Lightning: the electric set is pink-violet; lightning here is yellow, so it is used through
            // recoloured copies (see Recolour).
            // Made fresh each time, so a change of hue is picked up.
            AssetDatabase.DeleteAsset(Recoloured);
            string[] bolt = new string[3];
            string[] parts = { "Cast", "Projectile", "Hit" };
            for (int i = 0; i < 3; i++)
                bolt[i] = Recolour(Magic + "Electric/VFX_Electric_Magic_" + parts[i] + ".prefab", LightningHue, LightningSaturation);
            // the bolt is thin (radius 0.18) and fast; at 1:1 its effect is a speck, so it is drawn twice as big
            Set(fx, "LIGHTNING", bolt[0], bolt[1], bolt[2], projectileScale: 2f, castScale: 0.22f, impactScale: 0.5f,
                castTurn: new Vector3(0f, -90f, 0f));

            // Air: the pack has no wind attack, but its "sound" set -- rings and pressure waves -- reads as a
            // gust once it is the colour of air, and the AoE pack's air sounds replace its own.
            string[] gust = new string[3];
            AudioClip[] gustSounds = {
                AreaSound("Air", "Area_Cast_01"),      // the gust leaving the hands
                AreaSound("Air", "Area_Loop_01"),      // wind while it flies
                AreaSound("Air", "Burst_01") };        // the burst where it lands
            for (int i = 0; i < 3; i++)
                gust[i] = Recolour(Magic + "Sound/VFX_Sound_Magic_" + parts[i] + ".prefab", AirHue, AirSaturation, gustSounds[i],
                                   // what makes it music rather than wind: the notes, the staff lines, the emblem
                                   "PS_VFX_Notes", "PS_VFX_Rings", "PS_VFX_Flare_Black");
            Set(fx, "AIR", gust[0], gust[1], gust[2], projectileScale: 0.8f, castScale: 0.22f, impactScale: 0.6f,
                castTurn: new Vector3(0f, -90f, 0f));

            // Patches: the AoE pack's area effects, with its area sounds (a start, a loop, an end).
            SetZone(fx, "FIRE", "Fire");
            SetZone(fx, "ICE", "Ice");
            SetZone(fx, "AIR", "Air");

            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpellFx] assigned: " + fx.entries.Count + " spell(s) with effects");
        }

        static void Set(SpellFx fx, string spell, string cast, string projectile, string impact,
                        float projectileScale, float castScale, float impactScale, Vector3 castTurn = default(Vector3))
        {
            SpellFx.Entry e = fx.entries.Find(x => x.spell == spell);
            if (e == null) { e = new SpellFx.Entry { spell = spell }; fx.entries.Add(e); }

            e.cast = AssetDatabase.LoadAssetAtPath<GameObject>(cast ?? "");
            e.projectile = AssetDatabase.LoadAssetAtPath<GameObject>(projectile ?? "");
            e.impact = AssetDatabase.LoadAssetAtPath<GameObject>(impact ?? "");
            e.projectileScale = projectileScale;
            e.castScale = castScale;
            e.impactScale = impactScale;
            e.castTurn = castTurn;

            if (e.cast == null || e.projectile == null || e.impact == null)
                Debug.LogWarning("[SpellFx] " + spell + ": some effects are missing - is the Vefects pack in Assets/Vefects?");
        }

        const string Area = "Assets/Vefects/Stylized AoE VFX/";

        /// <summary>
        /// A spell's patch: VFX/{element}/Particles/VFX_{element}_Area_01 and its three sounds. The sounds
        /// sit in one of two folders, by who made them, so both are tried.
        /// </summary>
        static void SetZone(SpellFx fx, string spell, string element)
        {
            SpellFx.Entry e = fx.entries.Find(x => x.spell == spell);
            if (e == null) { e = new SpellFx.Entry { spell = spell }; fx.entries.Add(e); }

            e.zone = AssetDatabase.LoadAssetAtPath<GameObject>(Area + "VFX/" + element + "/Particles/VFX_" + element + "_Area_01.prefab");
            e.zoneAuthoredRadius = 3f;
            e.zoneLeadIn = 2f;
            e.zoneStart = AreaSound(element, "Area_Cast_01");
            e.zoneLoop = AreaSound(element, "Area_Loop_01");
            e.zoneEnd = AreaSound(element, "Area_End_01");

            if (e.zone == null) Debug.LogWarning("[SpellFx] " + spell + ": no area effect - is the Stylized AoE pack in Assets/Vefects?");
            else MakeMeshesReadable(e.zone);
        }

        /// <summary>
        /// ZoneFx reshapes the area effects' rings and discs to follow a patch cut short by an edge, which
        /// needs their vertices at runtime. The pack imports its meshes unreadable; this turns that on
        /// for the ones an area effect uses.
        /// </summary>
        static void MakeMeshesReadable(GameObject prefab)
        {
            foreach (ParticleSystemRenderer r in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r.renderMode != ParticleSystemRenderMode.Mesh || r.mesh == null || r.mesh.isReadable) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.mesh)) as ModelImporter;
                if (importer == null || importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        static AudioClip AreaSound(string element, string part)
        {
            foreach (string who in new[] { "Chinchi", "Sergi" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    Area + "Audio/WAV/" + who + "/SFX_Vefects_Stylized_AoE_" + element + "_" + part + ".wav");
                if (clip != null) return clip;
            }
            return null;
        }

        // ---------------------------------------------------------------- recolouring

        /// <summary>Electric yellow: a warm yellow, a little paler than pure, so the cores still read white-hot.</summary>
        const float LightningHue = 0.14f;
        const float LightningSaturation = 0.85f;

        /// <summary>Air: the spell's own pale mint, kept pale -- wind is barely coloured.</summary>
        const float AirHue = 0.43f;
        const float AirSaturation = 0.35f;

        /// <summary>
        /// Recoloured copies live inside the pack's folder: they are the pack's art, so they stay out of the
        /// public repository with it.
        /// </summary>
        const string Recoloured = "Assets/Vefects/_MageCast";

        /// <summary>
        /// Copies an effect prefab with every colour in it moved to one hue: particle colours and
        /// gradients, lights, and the textures that carry colour of their own (the electric flares and
        /// bolts are painted pink, so tinting the particles alone would leave them pink). Brightness,
        /// transparency and anything already grey or white are kept, so it still glows the same way.
        /// Rerunnable: the copies are simply made again. Returns the copy's path, or null.
        /// </summary>
        static string Recolour(string sourcePath, float hue, float saturation, AudioClip sound = null, params string[] drop)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null) return null;

            string folder = Recoloured + "/" + System.IO.Path.GetFileNameWithoutExtension(sourcePath).Replace("VFX_", "");
            EnsureFolder(folder);
            string target = folder + "/" + System.IO.Path.GetFileName(sourcePath);
            AssetDatabase.DeleteAsset(target);
            AssetDatabase.CopyAsset(sourcePath, target);

            var materials = new System.Collections.Generic.Dictionary<Material, Material>();
            GameObject root = PrefabUtility.LoadPrefabContents(target);
            try
            {
                // parts that do not belong to the spell this is being made into
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && t != root.transform && System.Array.IndexOf(drop, t.name) >= 0)
                        Object.DestroyImmediate(t.gameObject);

                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startColor = Shift(main.startColor, hue, saturation);
                    var life = ps.colorOverLifetime;
                    life.color = Shift(life.color, hue, saturation);
                    var speed = ps.colorBySpeed;
                    speed.color = Shift(speed.color, hue, saturation);
                    var trails = ps.trails;
                    trails.colorOverLifetime = Shift(trails.colorOverLifetime, hue, saturation);
                    trails.colorOverTrail = Shift(trails.colorOverTrail, hue, saturation);

                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    if (r == null) continue;
                    Material[] shared = r.sharedMaterials;
                    for (int i = 0; i < shared.Length; i++) shared[i] = CopyMaterial(shared[i], folder, hue, saturation, materials);
                    r.sharedMaterials = shared;
                    if (r.trailMaterial != null) r.trailMaterial = CopyMaterial(r.trailMaterial, folder, hue, saturation, materials);
                }
                foreach (Light l in root.GetComponentsInChildren<Light>(true)) l.color = Shift(l.color, hue, saturation);
                if (sound != null)
                    foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true)) a.clip = sound;
                foreach (TrailRenderer t in root.GetComponentsInChildren<TrailRenderer>(true)) t.colorGradient = Shift(t.colorGradient, hue, saturation);
                foreach (LineRenderer l in root.GetComponentsInChildren<LineRenderer>(true)) l.colorGradient = Shift(l.colorGradient, hue, saturation);

                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return target;
        }

        static Material CopyMaterial(Material m, string folder, float hue, float saturation,
                                     System.Collections.Generic.Dictionary<Material, Material> done)
        {
            if (m == null) return null;
            Material copy;
            if (done.TryGetValue(m, out copy)) return copy;

            copy = new Material(m);
            var shader = m.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string name = shader.GetPropertyName(i);
                var type = shader.GetPropertyType(i);
                if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
                    copy.SetColor(name, Shift(m.GetColor(name), hue, saturation));
                // Only the main picture. The other texture slots (distortion, cutout, noise) pack data into
                // their channels, and "recolouring" those would scramble what they mean.
                else if (type == UnityEngine.Rendering.ShaderPropertyType.Texture && (name == "_Texture" || name == "_MainTex"))
                {
                    Texture2D tex = m.GetTexture(name) as Texture2D;
                    Texture2D shifted = RecolourTexture(tex, hue, saturation);
                    if (shifted != null) copy.SetTexture(name, shifted);
                }
            }
            string path = folder + "/" + m.name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            done[m] = copy;
            return copy;
        }

        /// <summary>
        /// A recoloured copy of a texture, if it carries colour at all -- a grey mask is tinted by the
        /// particle colour and needs no copy. Read back through the GPU because the pack's textures are
        /// compressed and not readable. Shared between prefabs: made once per texture.
        /// </summary>
        static Texture2D RecolourTexture(Texture2D tex, float hue, float saturation)
        {
            if (tex == null) return null;
            string path = Recoloured + "/Textures/" + tex.name + "_Hue" + Mathf.RoundToInt(hue * 100f) + "_Sat" + Mathf.RoundToInt(saturation * 100f) + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            int w = tex.width, h = tex.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = readable.GetPixels();
            bool coloured = false;
            for (int i = 0; i < pixels.Length && !coloured; i++)
            {
                float hh, s, v;
                Color.RGBToHSV(pixels[i], out hh, out s, out v);
                coloured = v > 0.2f && s > 0.15f;
            }
            if (!coloured) { Object.DestroyImmediate(readable); return null; }

            for (int i = 0; i < pixels.Length; i++) pixels[i] = Shift(pixels[i], hue, saturation);
            readable.SetPixels(pixels);
            readable.Apply();

            EnsureFolder(Recoloured + "/Textures");
            System.IO.File.WriteAllBytes(path, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path);

            // imported the way the original was, so it blends and wraps the same
            var source = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            var copy = AssetImporter.GetAtPath(path) as TextureImporter;
            if (source != null && copy != null)
            {
                copy.textureType = source.textureType;
                copy.sRGBTexture = source.sRGBTexture;
                copy.alphaSource = source.alphaSource;
                copy.alphaIsTransparency = source.alphaIsTransparency;
                copy.wrapMode = source.wrapMode;
                copy.mipmapEnabled = source.mipmapEnabled;
                copy.maxTextureSize = source.maxTextureSize;
                copy.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Shift(Color c, float hue, float saturation)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (s < 0.05f) return c;                    // white and grey stay as they are
            Color shifted = Color.HSVToRGB(hue, Mathf.Clamp01(s * saturation), v, true);
            shifted.a = c.a;
            return shifted;
        }

        static Gradient Shift(Gradient g, float hue, float saturation)
        {
            if (g == null) return null;
            GradientColorKey[] keys = g.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Shift(keys[i].color, hue, saturation);
            var copy = new Gradient { mode = g.mode };
            copy.SetKeys(keys, g.alphaKeys);
            return copy;
        }

        static ParticleSystem.MinMaxGradient Shift(ParticleSystem.MinMaxGradient m, float hue, float saturation)
        {
            switch (m.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(Shift(m.color, hue, saturation));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(Shift(m.colorMin, hue, saturation), Shift(m.colorMax, hue, saturation));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(Shift(m.gradient, hue, saturation));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Shift(m.gradientMin, hue, saturation), Shift(m.gradientMax, hue, saturation));
                case ParticleSystemGradientMode.RandomColor:
                    var r = new ParticleSystem.MinMaxGradient(Shift(m.gradient, hue, saturation));
                    r.mode = ParticleSystemGradientMode.RandomColor;
                    return r;
            }
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
