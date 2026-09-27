using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// A slab of cover planted in the locked direction. Unlike the other three spells this one is not
    /// aimed AT anything -- it is placed, which makes the triangle the one gesture where the direction
    /// lock is a positioning decision rather than a shot.
    ///
    /// It is a real collider, so it blocks projectiles from both sides, including the caster's own.
    /// That is deliberate: a barrier you can shoot through but your opponent cannot is a much bigger
    /// design commitment (teams, ownership, filtering) and it is worth finding out first whether the
    /// honest version is more interesting. Planting a wall you then have to move around is a real
    /// decision; a one-way window is just free value.
    /// </summary>
    public class CastShield : MonoBehaviour
    {
        float remaining;
        float total;
        Material material;
        Color colour;

        public static CastShield Spawn(Vector3 at, Vector3 facing, float width, float height,
                                       float lifetime, Color colour)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CastShield";
            go.transform.position = at + Vector3.up * (height * 0.5f);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z).normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, height, 0.15f);

            Material m = RuntimeMaterials.Fade(colour, 0.42f, 0.8f);
            go.GetComponent<Renderer>().material = m;

            CastShield s = go.AddComponent<CastShield>();
            s.remaining = lifetime;
            s.total = lifetime;
            s.material = m;
            s.colour = colour;
            return s;
        }

        /// <summary>
        /// The Standard shader needs all of this set by hand to blend -- setting only the alpha on the
        /// colour leaves it fully opaque, and an opaque shield you cannot see past is worse than none.
        /// </summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) { Destroy(gameObject); return; }

            // visibly weakening, so the opponent can time a push and the caster knows when to move
            float t = remaining / Mathf.Max(0.01f, total);
            Color c = material.color;
            c.a = Mathf.Lerp(0.12f, 0.42f, t);
            material.color = c;
            material.SetColor("_EmissionColor", colour * Mathf.Lerp(0.15f, 0.8f, t));
        }
    }
}
