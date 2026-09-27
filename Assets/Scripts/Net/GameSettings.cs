using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Player settings, kept between sessions. On the web PlayerPrefs lives in the browser's storage,
    /// so a tester's settings survive a page reload too.
    ///
    /// One setting so far. The panel is drawn from here so the main menu and the Esc menu show the same
    /// thing and cannot drift apart.
    /// </summary>
    public static class GameSettings
    {
        const string SensitivityKey = "MouseSensitivity";
        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 4f;

        static float? sensitivity;

        /// <summary>
        /// Multiplies how far the camera turns per unit of mouse movement. 1 is the tuned default.
        /// Aiming only -- drawing a rune has its own sensitivity, because a value that feels right for
        /// flicking the camera is far too twitchy for tracing a shape.
        /// </summary>
        public static float MouseSensitivity
        {
            get
            {
                if (!sensitivity.HasValue) sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1f);
                return sensitivity.Value;
            }
            set
            {
                float v = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
                if (sensitivity.HasValue && Mathf.Approximately(sensitivity.Value, v)) return;
                sensitivity = v;
                PlayerPrefs.SetFloat(SensitivityKey, v);
                PlayerPrefs.Save();
            }
        }

        static GUIStyle label, value;

        /// <summary>Draws the settings into a column starting at y, and returns where it ended.</summary>
        public static float Draw(float x, float y, float width)
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                label.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
                value = new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            }

            GUI.Label(new Rect(x, y, width, 24f), "Mouse sensitivity", label);
            GUI.Label(new Rect(x, y, width, 24f),
                      MouseSensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), value);
            y += 28f;

            // stepped in 0.05 so a value can be told to someone and set again exactly
            float raw = GUI.HorizontalSlider(new Rect(x, y, width, 20f), MouseSensitivity, MinSensitivity, MaxSensitivity);
            MouseSensitivity = Mathf.Round(raw * 20f) / 20f;
            y += 26f;

            if (GUI.Button(new Rect(x, y, 120f, 26f), "Default (1.00)")) MouseSensitivity = 1f;
            return y + 36f;
        }
    }
}
