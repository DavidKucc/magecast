using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Renders every effect in the bought packs (Assets/Vefects) as three stills -- early, middle, late --
    /// plus a line of facts about it, so the whole library can be browsed at a glance instead of by
    /// dropping prefabs into a scene one at a time.
    ///
    /// Works in edit mode: each effect is placed on a small stage far above the arena, simulated to a
    /// moment and rendered from a three-quarter view. The moments are taken from when the effect is
    /// actually busy (its particle count over time), not from its authored lengths, which the packs
    /// pad with long-lived lights and leftovers. Nothing is left in the scene afterwards.
    /// </summary>
    public static class VfxCatalogue
    {
        const string Root = "Assets/Vefects";
        const int Width = 384, Height = 256;
        const float Step = 0.1f;
        const int MaxSteps = 90;
        static readonly float[] Moments = { 0.08f, 0.4f, 0.8f };

        [MenuItem("Tools/Arena/Render VFX Catalogue")]
        static void RenderAll()
        {
            string dir = EditorUtility.SaveFolderPanel("Where to put the catalogue images", "", "vfx");
            if (!string.IsNullOrEmpty(dir)) Render(dir, 0, int.MaxValue);
        }

        /// <summary>Every effect prefab in the packs: anything with particles that is not a mesh or demo part.</summary>
        public static List<string> Effects()
        {
            var list = new List<string>();
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { Root }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p.Contains("/Meshes/") || p.Contains("/Demo/") || p.Contains("_ Extra") || p.Contains("/Others/")
                    || p.Contains("/Scripts/") || p.Contains("_MageCast") || p.Contains("/Shared/Prefabs/") || p.Contains("/Mesh/"))
                    continue;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<ParticleSystem>(true).Length == 0) continue;
                list.Add(p);
            }
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        /// <summary>
        /// Renders effects [from, to) into <paramref name="outDir"/> as NNN_0..2.jpg and appends their
        /// facts to meta.tsv: index, path, active from/to (s), loops, systems, width (m), sounds.
        /// </summary>
        public static int Render(string outDir, int from, int to)
        {
            Directory.CreateDirectory(outDir);
            List<string> list = Effects();
            Vector3 stage = new Vector3(0f, 500f, 0f);
            var inv = CultureInfo.InvariantCulture;

            var stageRoot = new GameObject("__VfxStage");
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(stageRoot.transform);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.085f, 0.09f, 0.115f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.depthTextureMode = DepthTextureMode.Depth;     // the soft glows and fake decals need it
            cam.allowHDR = false;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(stageRoot.transform);
            floor.transform.position = stage;
            floor.transform.localScale = Vector3.one * 6f;
            var floorMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.21f, 0.25f) };
            floorMaterial.SetFloat("_Glossiness", 0.1f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var meta = new StringBuilder();
            int done = 0;

            try
            {
                for (int idx = from; idx < Mathf.Min(to, list.Count); idx++)
                {
                    string path = list[idx];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    bool ground = path.Contains("Stylized AoE") || path.Contains("Smoke Bombs")
                                  || (path.Contains("Anime") && System.Text.RegularExpressions.Regex.IsMatch(prefab.name, "Floor|Heal|Buff|Debuff|Pickup|VFX_Fire$|Bomb"));

                    var go = Object.Instantiate(prefab);
                    go.transform.position = stage + new Vector3(0f, ground ? 0.02f : 1.2f, 0f);
                    foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                        if (mb != null && mb.GetType().Namespace == null) Object.DestroyImmediate(mb);   // demo scripts
                    foreach (AudioSource a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;

                    ParticleSystem[] systems = go.GetComponentsInChildren<ParticleSystem>(true);
                    bool loops = false;
                    foreach (ParticleSystem ps in systems)
                    {
                        var m = ps.main;
                        // an editor with no camera looking this way would otherwise cull them to nothing
                        m.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                        if (m.loop) loops = true;
                    }
                    var roots = new List<ParticleSystem>();
                    foreach (ParticleSystem ps in systems)
                        if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null) roots.Add(ps);

                    // when it is actually busy
                    foreach (ParticleSystem r in roots) r.Simulate(0f, true, true, true);
                    var busy = new int[MaxSteps];
                    int peak = 0;
                    for (int i = 0; i < MaxSteps; i++)
                    {
                        foreach (ParticleSystem r in roots) r.Simulate(Step, true, false, true);
                        int c = 0;
                        foreach (ParticleSystem ps in systems) c += ps.particleCount;
                        busy[i] = c;
                        peak = Mathf.Max(peak, c);
                    }
                    int first = -1, last = -1;
                    for (int i = 0; i < MaxSteps && first < 0; i++) if (busy[i] > 0) first = i;
                    for (int i = MaxSteps - 1; i >= 0 && last < 0; i--) if (busy[i] >= Mathf.Max(1f, peak * 0.15f)) last = i;
                    if (first < 0) { first = 0; last = 5; }
                    if (last < first) last = first;
                    float start = (first + 1) * Step, end = (last + 1) * Step;
                    var times = new float[Moments.Length];
                    for (int k = 0; k < times.Length; k++) times[k] = start + (end - start) * Moments[k];

                    // framed on all three moments together, so what moves stays in the picture; the huge
                    // soft glows are left out of the framing
                    Bounds b = new Bounds(go.transform.position, Vector3.zero);
                    bool any = false;
                    foreach (float t in times)
                    {
                        foreach (ParticleSystem r in roots) r.Simulate(t, true, true, true);
                        foreach (ParticleSystemRenderer r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
                        {
                            ParticleSystem ps = r.GetComponent<ParticleSystem>();
                            if (ps == null || ps.particleCount == 0 || !r.enabled) continue;
                            Bounds rb = r.bounds;
                            if (rb.size.magnitude > 14f || rb.size.magnitude < 0.001f) continue;
                            if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
                        }
                    }
                    if (!any) b = new Bounds(go.transform.position, Vector3.one * 2f);
                    float radius = Mathf.Clamp(b.extents.magnitude, 0.8f, 7f);
                    Vector3 target = b.center;
                    if (ground) target.y = Mathf.Max(target.y, stage.y + 0.4f);
                    cam.transform.position = target + new Vector3(-0.66f, 0.45f, -0.6f).normalized
                                             * (radius / Mathf.Tan(20f * Mathf.Deg2Rad) * 0.62f);
                    cam.transform.LookAt(target);

                    for (int k = 0; k < times.Length; k++)
                    {
                        foreach (ParticleSystem r in roots) r.Simulate(times[k], true, true, true);
                        cam.Render();
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(outDir, idx.ToString("000") + "_" + k + ".jpg"), tex.EncodeToJPG(82));
                    }

                    var sounds = new StringBuilder();
                    foreach (AudioSource a in prefab.GetComponentsInChildren<AudioSource>(true))
                        if (a.clip != null)
                            sounds.Append(sounds.Length > 0 ? ";" : "").Append(a.clip.name).Append('|')
                                  .Append(a.clip.length.ToString("F1", inv)).Append('|').Append(a.loop ? "1" : "0");

                    meta.Append(idx).Append('\t').Append(path.Substring(Root.Length + 1)).Append('\t')
                        .Append(start.ToString("F1", inv)).Append('\t').Append(end.ToString("F1", inv)).Append('\t')
                        .Append(loops ? 1 : 0).Append('\t').Append(systems.Length).Append('\t')
                        .Append(b.size.x.ToString("F1", inv)).Append('\t').Append(sounds).Append('\n');

                    Object.DestroyImmediate(go);
                    done++;
                }
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(stageRoot);
                Object.DestroyImmediate(floorMaterial);
            }
            File.AppendAllText(Path.Combine(outDir, "meta.tsv"), meta.ToString());
            return done;
        }
    }
}
