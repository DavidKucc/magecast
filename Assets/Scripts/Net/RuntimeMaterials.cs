using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Materials for everything made at runtime -- projectiles, flashes, patches, shields, lines.
    ///
    /// These used to be `new Material(Shader.Find("Standard"))`, which works in the editor and quietly
    /// breaks in a built game: a build only contains the shader variants that some material in it
    /// actually uses, and nothing in the arena is emissive or see-through. So every glowing bolt would
    /// have shipped without its glow and every barrier as a solid block, and it would only have shown
    /// up on the tester's machine. The templates in Resources/RuntimeMaterials carry the keywords, so
    /// the build keeps exactly those variants.
    /// </summary>
    public static class RuntimeMaterials
    {
        static Material emissive, fade, line;

        /// <summary>Opaque, lit, glowing in its own colour.</summary>
        public static Material Emissive(Color colour, float glow)
        {
            Material m = new Material(Template(ref emissive, "RuntimeMaterials/Emissive", "Standard"));
            m.color = colour;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", colour * glow);
            return m;
        }

        /// <summary>See-through and glowing -- the barrier.</summary>
        public static Material Fade(Color colour, float alpha, float glow)
        {
            Material m = new Material(Template(ref fade, "RuntimeMaterials/Fade", "Standard"));
            m.color = new Color(colour.r, colour.g, colour.b, alpha);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", colour * glow);
            return m;
        }

        /// <summary>Unlit vertex colour, for LineRenderers.</summary>
        public static Material Line()
        {
            return new Material(Template(ref line, "RuntimeMaterials/Line", "Sprites/Default"));
        }

        static Material Template(ref Material cached, string path, string fallbackShader)
        {
            if (cached != null) return cached;
            cached = Resources.Load<Material>(path);
            if (cached == null)
            {
                // Only reachable before Tools > Arena > Build Network Setup has made the templates.
                // Fine in the editor, which has every shader; wrong in a build, hence the warning.
                Debug.LogWarning("[RuntimeMaterials] missing Resources/" + path + ", using " + fallbackShader);
                cached = new Material(Shader.Find(fallbackShader));
            }
            return cached;
        }
    }
}
