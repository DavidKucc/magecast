using System.Collections.Generic;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Text that floats up from something in the world and fades: the name of a spell over whoever
    /// cast it, the damage over whoever took it.
    ///
    /// Drawn on screen at the projected position rather than as a mesh in the world. A world-space
    /// label shrinks to nothing at range and hides behind the very cover a fight is fought around,
    /// and the whole point of it is that the other player reads it mid-fight -- "fire is coming"
    /// has to be legible at 20 m.
    ///
    /// Damage merges per target. A burn ticks four times a second, and four numbers a second stacked
    /// on top of each other is noise; one number that counts up while you stand in it is information.
    /// </summary>
    public class WorldPopups : MonoBehaviour
    {
        class Popup
        {
            public Transform Anchor;
            public Vector3 Point;        // used when there is no anchor, or it has gone
            public float Height;
            public string Text;
            public float Amount;         // damage only; the text is rebuilt from it when merged
            public bool IsDamage;
            public Color Colour;
            public int Size;
            public float Born;
            public float Life;
            public float Rise;
            public float Drift;          // sideways, so two numbers in a row do not overlap
        }

        const float MergeWindow = 0.45f;

        static WorldPopups instance;
        readonly List<Popup> popups = new List<Popup>();
        GUIStyle style;
        int driftFlip;

        static WorldPopups Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("WorldPopups");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<WorldPopups>();
                }
                return instance;
            }
        }

        /// <summary>A spell's name over its caster, fading slowly -- the "what just came out" read.</summary>
        public static void Spell(Transform caster, string text, Color colour, bool big)
        {
            if (caster == null) return;
            Instance.Add(new Popup {
                // above the name tag (2.95), so the two never sit on top of each other
                Anchor = caster, Point = caster.position, Height = 3.25f, Text = text,
                Colour = colour, Size = big ? 30 : 24, Life = 1.8f, Rise = 0.55f });
        }

        /// <summary>A plain word over somebody -- FIZZLE, INTERRUPTED, DOWN.</summary>
        public static void Word(Transform over, string text, Color colour)
        {
            if (over == null) return;
            Instance.Add(new Popup {
                Anchor = over, Point = over.position, Height = 3.25f, Text = text,
                Colour = colour, Size = 20, Life = 1.3f, Rise = 0.4f });
        }

        public static void Damage(Transform target, float amount)
        {
            if (target == null || amount <= 0f) return;
            WorldPopups w = Instance;

            // add to a number still rising from the same target instead of starting another one
            for (int i = w.popups.Count - 1; i >= 0; i--)
            {
                Popup p = w.popups[i];
                if (!p.IsDamage || p.Anchor != target || Time.time - p.Born > MergeWindow) continue;
                p.Amount += amount;
                p.Text = Format(p.Amount);
                p.Born = Time.time;
                p.Size = SizeFor(p.Amount);
                return;
            }

            w.driftFlip = 1 - w.driftFlip;
            w.Add(new Popup {
                Anchor = target, Point = target.position, Height = 2.1f, Text = Format(amount),
                Amount = amount, IsDamage = true, Colour = new Color(1f, 0.92f, 0.55f),
                Size = SizeFor(amount), Life = 1.1f, Rise = 0.9f,
                Drift = (w.driftFlip == 0 ? -1f : 1f) * 0.25f });
        }

        static string Format(float amount)
        {
            // one decimal only while it is small, so a burn tick reads as "1.5" and a hit as "22"
            return amount < 10f ? amount.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
                               : Mathf.RoundToInt(amount).ToString();
        }

        static int SizeFor(float amount)
        {
            return Mathf.RoundToInt(Mathf.Lerp(18f, 34f, Mathf.Clamp01(amount / 40f)));
        }

        void Add(Popup p)
        {
            p.Born = Time.time;
            popups.Add(p);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            if (style == null)
                style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                    clipping = TextClipping.Overflow
                };

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup p = popups[i];
                float age = Time.time - p.Born;
                if (age >= p.Life) { popups.RemoveAt(i); continue; }

                if (p.Anchor != null) p.Point = p.Anchor.position;
                float t = age / p.Life;

                // eased up and out: fast at first, then hanging while it fades
                float lift = p.Rise * (1f - (1f - t) * (1f - t));
                Vector3 world = p.Point + Vector3.up * (p.Height + lift) + cam.transform.right * (p.Drift * t);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0.1f) continue;

                // a little smaller at range, but never below readable
                float distanceScale = Mathf.Clamp(12f / screen.z, 0.7f, 1.25f);
                int size = Mathf.RoundToInt(p.Size * distanceScale);

                // a pop on arrival, then a fade over the last 40%
                float pop = age < 0.12f ? Mathf.Lerp(1.35f, 1f, age / 0.12f) : 1f;
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;

                style.fontSize = Mathf.RoundToInt(size * pop);
                Rect r = new Rect(screen.x - 200f, Screen.height - screen.y - 30f, 400f, 60f);

                style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), p.Text, style);
                style.normal.textColor = new Color(p.Colour.r, p.Colour.g, p.Colour.b, alpha);
                GUI.Label(r, p.Text, style);
            }
        }
    }
}
