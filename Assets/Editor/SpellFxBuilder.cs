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
                Magic + "Fire/VFX_Fire_Magic_Hit.prefab", projectileScale: 1f, castScale: 0.22f, impactScale: 0.5f);

            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpellFx] assigned: " + fx.entries.Count + " spell(s) with effects");
        }

        static void Set(SpellFx fx, string spell, string cast, string projectile, string impact,
                        float projectileScale, float castScale, float impactScale)
        {
            SpellFx.Entry e = fx.entries.Find(x => x.spell == spell);
            if (e == null) { e = new SpellFx.Entry { spell = spell }; fx.entries.Add(e); }

            e.cast = AssetDatabase.LoadAssetAtPath<GameObject>(cast);
            e.projectile = AssetDatabase.LoadAssetAtPath<GameObject>(projectile);
            e.impact = AssetDatabase.LoadAssetAtPath<GameObject>(impact);
            e.projectileScale = projectileScale;
            e.castScale = castScale;
            e.impactScale = impactScale;

            if (e.cast == null || e.projectile == null || e.impact == null)
                Debug.LogWarning("[SpellFx] " + spell + ": some effects are missing - is the Vefects pack in Assets/Vefects?");
        }
    }
}
