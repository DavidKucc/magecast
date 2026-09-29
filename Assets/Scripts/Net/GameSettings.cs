using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Player settings, kept between sessions. On the web PlayerPrefs lives in the browser's storage,
    /// so a tester's settings survive a page reload too.
    ///
    /// The panel is drawn from here so the main menu and the Esc menu show the same thing and cannot
    /// drift apart.
    /// </summary>
    public static class GameSettings
    {
        const string SensitivityKey = "MouseSensitivity";
        const string MasterKey = "MasterVolume";
        const string SpellKey = "SpellVolume";
        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 4f;

        /// <summary>Spells start a little under full: the packs mix them loud, for a demo of one at a time.</summary>
        const float DefaultSpellVolume = 0.7f;

        static float? sensitivity, master, spells;

        /// <summary>
        /// Multiplies how far the camera turns per unit of mouse movement. 1 is the tuned default.
        /// Aiming only -- drawing a rune has its own sensitivity, because a value that feels right for
        /// flicking the camera is far too twitchy for tracing a shape.
        /// </summary>
        public static float MouseSensitivity
        {
            get { return Read(ref sensitivity, SensitivityKey, 1f); }
            set { Write(ref sensitivity, SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); }
        }

        /// <summary>Everything the game plays, 0-1.</summary>
        public static float MasterVolume
        {
            get { return Read(ref master, MasterKey, 1f); }
            set
            {
                Write(ref master, MasterKey, Mathf.Clamp01(value));
                AudioListener.volume = MasterVolume;
            }
        }

        /// <summary>Spells, their patches and the barrier, 0-1, on top of the master volume.</summary>
        public static float SpellVolume
        {
            get { return Read(ref spells, SpellKey, DefaultSpellVolume); }
            set { Write(ref spells, SpellKey, Mathf.Clamp01(value)); }
        }

        /// <summary>Puts the saved volume into effect -- once, when the game starts.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
        }

        static float Read(ref float? cache, string key, float fallback)
        {
            if (!cache.HasValue) cache = PlayerPrefs.GetFloat(key, fallback);
            return cache.Value;
        }

        static void Write(ref float? cache, string key, float v)
        {
            if (cache.HasValue && Mathf.Approximately(cache.Value, v)) return;
            cache = v;
            PlayerPrefs.SetFloat(key, v);
            PlayerPrefs.Save();
        }

        static GUIStyle label, value;

        /// <summary>How tall Draw's column is, for anything that frames it.</summary>
        public const float Height = 3 * 54f + 36f;

        /// <summary>Draws the settings into a column starting at y, and returns where it ended.</summary>
        public static float Draw(float x, float y, float width)
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                label.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
                value = new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            }

            // stepped in 0.05 so a value can be told to someone and set again exactly
            MouseSensitivity = Slider(x, ref y, width, "Mouse sensitivity", MouseSensitivity,
                                      MinSensitivity, MaxSensitivity, MouseSensitivity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            MasterVolume = Slider(x, ref y, width, "Volume", MasterVolume, 0f, 1f, Mathf.RoundToInt(MasterVolume * 100f) + " %");
            SpellVolume = Slider(x, ref y, width, "Spell sounds", SpellVolume, 0f, 1f, Mathf.RoundToInt(SpellVolume * 100f) + " %");

            if (GUI.Button(new Rect(x, y, 120f, 26f), "Defaults"))
            {
                MouseSensitivity = 1f;
                MasterVolume = 1f;
                SpellVolume = DefaultSpellVolume;
            }
            return y + 36f;
        }

        static float Slider(float x, ref float y, float width, string name, float current, float min, float max, string shown)
        {
            GUI.Label(new Rect(x, y, width, 24f), name, label);
            GUI.Label(new Rect(x, y, width, 24f), shown, value);
            y += 26f;
            float raw = GUI.HorizontalSlider(new Rect(x, y, width, 20f), current, min, max);
            y += 28f;
            return Mathf.Round(raw * 20f) / 20f;
        }
    }
}
