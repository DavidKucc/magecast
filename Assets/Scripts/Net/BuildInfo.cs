using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// When this copy of the game was built, shown small in the menus.
    ///
    /// Web testing needs it: a browser happily keeps running yesterday's version out of its cache, and
    /// "it does not work" and "you are not running the version where it works" look identical from the
    /// outside. The build writes Resources/BuildInfo.txt just before it builds.
    /// </summary>
    public static class BuildInfo
    {
        static string label;

        public static string Label
        {
            get
            {
                if (label == null)
                {
                    TextAsset text = Resources.Load<TextAsset>("BuildInfo");
                    label = text != null ? "build " + text.text.Trim() : (Application.isEditor ? "editor" : "build ?");
                }
                return label;
            }
        }

        static GUIStyle style;

        /// <summary>The label, small and faint, in the bottom-right corner.</summary>
        public static void DrawCorner()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.LowerRight };
                style.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
            }
            GUI.Label(new Rect(Screen.width - 310f, Screen.height - 26f, 300f, 20f), Label, style);
        }
    }
}
