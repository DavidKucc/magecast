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
    ///
    /// The look: a damage number is outlined in black so it reads over anything, pops in big and
    /// settles with a bounce, and is warmer and bigger the bigger the hit -- pale gold for a tick, gold
    /// for a hit, orange-red with a glow behind it for a big one; it gives a little kick as it grows
    /// when hits merge into it. Words and spell names sit on a dark pill edged in their colour, like
    /// the tags in the HUD.
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
            public float Bumped = -9f;   // when a merged hit last made it grow
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
            // your own losses are counted out on the health panel (PlayerHud), not over your head
            if (PlayerNet.Local != null && target == PlayerNet.Local.transform) return;
            WorldPopups w = Instance;

            // add to a number still rising from the same target instead of starting another one
            for (int i = w.popups.Count - 1; i >= 0; i--)
            {
                Popup p = w.popups[i];
                if (!p.IsDamage || p.Anchor != target || Time.time - p.Born > MergeWindow) continue;
                p.Amount += amount;
                p.Text = Format(p.Amount);
                p.Born = Time.time - 0.12f;          // past the pop-in: it kicks instead (Bumped)
                p.Bumped = Time.time;
                p.Size = SizeFor(p.Amount);
                return;
            }

            w.driftFlip = 1 - w.driftFlip;
            w.Add(new Popup {
                Anchor = target, Point = target.position, Height = 2.1f, Text = Format(amount),
                Amount = amount, IsDamage = true, Colour = new Color(1f, 0.92f, 0.55f),
                Size = SizeFor(amount), Life = 1.2f, Rise = 0.9f,
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
            return Mathf.RoundToInt(Mathf.Lerp(20f, 40f, Mathf.Clamp01(amount / 40f)));
        }

        /// <summary>Pale gold for a tick, gold for a hit, orange-red for a big one.</summary>
        static Color DamageColour(float amount)
        {
            Color tick = new Color(1f, 0.93f, 0.66f), hit = new Color(1f, 0.8f, 0.3f), big = new Color(1f, 0.42f, 0.22f);
            return amount < 12f ? Color.Lerp(tick, hit, amount / 12f) : Color.Lerp(hit, big, Mathf.Clamp01((amount - 12f) / 18f));
        }

        static readonly Vector2[] Around =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f),
            new Vector2(-0.7f, -0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, 0.7f)
        };

        /// <summary>Text with a black outline all round it, so it reads over sky, fire and floor alike.</summary>
        void Outlined(Rect r, string text, Color colour, float width)
        {
            style.normal.textColor = new Color(0.05f, 0.03f, 0.02f, 0.9f * colour.a);
            foreach (Vector2 d in Around)
                GUI.Label(new Rect(r.x + d.x * width, r.y + d.y * width + 1f, r.width, r.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(r, text, style);
        }

        void Awake()
        {
            // Lives across scenes; what was rising over somebody in the arena must not hang over the
            // lobby (or the next map) at the spot where they stood.
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (from, to) => popups.Clear();
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
                // "FIRE III", "FIZZLE" over another player tell you what they hold: only in sight, and
                // only from fighting distance
                float near = 1f;
                if (!p.IsDamage && Sight.IsOtherPlayer(p.Anchor))
                {
                    if (!Sight.Sees(p.Anchor)) continue;
                    near = Sight.Readable(p.Anchor, Sight.ReadRange);
                }
                // what others deal each other, only near you; your own hits and the ones on you, always
                if (p.IsDamage && p.Anchor != null && (PlayerNet.Local == null || p.Anchor != PlayerNet.Local.transform)
                    && !Sight.HitByMe(p.Anchor))
                    near = Sight.Readable(p.Anchor, Sight.DamageRange);
                if (near <= 0.01f) continue;
                float t = age / p.Life;

                // eased up and out: fast at first, then hanging while it fades
                float lift = p.Rise * (1f - (1f - t) * (1f - t));
                Vector3 world = p.Point + Vector3.up * (p.Height + lift) + cam.transform.right * (p.Drift * t);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0.1f) continue;

                // a little smaller at range, but never below readable
                float distanceScale = Mathf.Clamp(12f / screen.z, 0.7f, 1.25f);
                int size = Mathf.RoundToInt(p.Size * distanceScale);

                // in big and settling with a bounce, a kick when a merged hit makes it grow, then a fade
                float pop = 1f;
                if (age < 0.28f)
                {
                    float k = age / 0.28f;
                    pop = 1f + 0.7f * Mathf.Exp(-6f * k) * Mathf.Cos(k * 9f);
                }
                float bump = Time.time - p.Bumped;
                if (bump < 0.2f) pop *= 1f + 0.3f * (1f - bump / 0.2f);
                float alpha = (t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f) * near;

                style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * pop));
                Vector2 at = new Vector2(screen.x, Screen.height - screen.y);
                Rect r = new Rect(at.x - 200f, at.y - 30f, 400f, 60f);

                if (p.IsDamage)
                {
                    Color c = DamageColour(p.Amount);
                    // a hit of yours on somebody else is edged in gold light
                    bool mine = p.Anchor != null && Sight.HitByMe(p.Anchor);
                    if (p.Amount >= 18f || mine)
                        MenuArt.Glow(at, size * (p.Amount >= 18f ? 3.2f : 2.4f),
                                     new Color(c.r, c.g * 0.8f, c.b * 0.6f, (p.Amount >= 18f ? 0.3f : 0.18f) * alpha));
                    Outlined(r, p.Text, new Color(c.r, c.g, c.b, alpha), Mathf.Max(1.5f, size * 0.07f));
                }
                else
                {
                    // a word or a spell's name: on a dark pill edged in its colour
                    Vector2 box = style.CalcSize(new GUIContent(p.Text));
                    Rect pill = new Rect(at.x - box.x * 0.5f - 12f, at.y - box.y * 0.5f - 1f, box.x + 24f, box.y + 2f);
                    MenuArt.RoundRect(pill, new Color(0.03f, 0.035f, 0.05f, 0.72f * alpha), pill.height * 0.5f);
                    MenuArt.RoundRect(pill, new Color(p.Colour.r, p.Colour.g, p.Colour.b, 0.75f * alpha), pill.height * 0.5f, 1.5f);
                    Outlined(r, p.Text, new Color(p.Colour.r, p.Colour.g, p.Colour.b, alpha), 1.2f);
                }
            }
        }
    }
}
