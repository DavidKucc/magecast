using UnityEditor;
using UnityEngine;

namespace MageCast.EditorTools
{
    /// <summary>
    /// Where the character's BODY comes from -- deliberately separate from where its animation comes
    /// from, because the two have very different lifetimes in this project.
    ///
    /// The model is a skin. Unity retargets any humanoid clip onto any humanoid model, so swapping the
    /// character costs nothing: the controller, the blend trees, the upper-body mask and the stride
    /// matching all carry over untouched. That only stays true if no other file hardcodes one
    /// particular character, which is the whole job of this one.
    ///
    /// Drop a humanoid character into Assets/Characters and it gets used. With nothing there it falls
    /// back to the Vexa pack, so the project still runs on a machine that does not have a skin yet.
    /// </summary>
    public static class CharacterSkin
    {
        public const string SkinFolder = "Assets/Characters";

        /// <summary>
        /// The model to wear, or null if there is nothing to wear at all.
        ///
        /// Null is a supported answer: PlayerRigBuilder falls back to the capsule the whole game was
        /// built on before there was any art, so an empty skin folder costs a look, not a broken scene.
        /// </summary>
        public static GameObject Prefab()
        {
            GameObject installed = FirstIn(SkinFolder, "t:Prefab");
            if (installed == null) installed = FirstIn(SkinFolder, "t:Model");
            return installed;
        }

        /// <summary>
        /// The skin's humanoid avatar.
        ///
        /// Taken from the prefab's own Animator when it has one, and otherwise hunted for in the
        /// character's folder -- an avatar lives inside the FBX as a sub-asset, which is usually a
        /// different file from the prefab and not somewhere a path constant should be guessing at.
        /// </summary>
        public static Avatar AvatarFor(GameObject prefab)
        {
            if (prefab == null) return null;

            Animator animator = prefab.GetComponentInChildren<Animator>();
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                return animator.avatar;

            string root = RootFolderOf(prefab);
            if (root == null) return null;

            Avatar best = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Avatar", new[] { root }))
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    Avatar a = o as Avatar;
                    if (a == null || !a.isHuman) continue;
                    // a name match wins, so a pack shipping several rigs still lands on the right one
                    if (a.name.Contains(prefab.name) || prefab.name.Contains(a.name)) return a;
                    if (best == null) best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// The scale contract: a player is two metres, whatever the artist modelled.
        ///
        /// Applied to anything wearing the skin, including the throwaway probes the animator builder
        /// measures on -- a stride sampled at the model's native size and then played on a body scaled
        /// to 2 m is measured in the wrong units, which shows up as feet skating by exactly that ratio.
        /// </summary>
        public const float StandardHeight = 2f;

        public static float NormaliseHeight(GameObject instance)
        {
            if (instance == null) return 1f;

            Bounds b = new Bounds();
            bool any = false;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any || b.size.y < 0.1f) return 1f;

            float scale = StandardHeight / b.size.y;
            instance.transform.localScale = Vector3.one * scale;
            return scale;
        }

        public static string Describe(GameObject prefab)
        {
            return prefab == null ? "none installed in " + SkinFolder : prefab.name;
        }

        static GameObject FirstIn(string folder, string filter)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;

            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                if (go.GetComponentInChildren<SkinnedMeshRenderer>() == null) continue;  // not a body
                return go;
            }
            return null;
        }

        /// <summary>The character pack's own root, one level above the Prefabs/ or Meshes/ it sits in.</summary>
        static string RootFolderOf(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path)) return null;

            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            return AssetDatabase.IsValidFolder(parent) && parent != "Assets" ? parent : folder;
        }
    }
}
