using MageCast.Combat;
using MageCast.Gestures;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Your own health, bottom left. Deliberately far from the spell readout in the top left -- those
    /// two are read at different moments and putting them together means neither gets noticed.
    ///
    /// A hit has to be felt, not just counted: the bar shakes and its edge flashes red, the part it took
    /// stays lit for a moment and then drains away, so you see how big a bite it was, the number
    /// of it rises out of the bar (a burn's small ticks add up into one number instead of a stream), and
    /// the edges of the screen redden for an instant. Notches every 10 make the size of a bite
    /// readable at a glance. Low on health, the bar pulses red. What you are suffering -- burning, wet,
    /// frozen -- sits over it as chips.
    ///
    /// Stamina is a ring beside the crosshair, where the eyes are while running: it shows up when you
    /// sprint, empties round the clock, turns orange when low and red and throbbing once run dry --
    /// with a mark at the point where sprinting comes back -- and flashes as it fills and goes.
    ///
    /// In the menus' look (MenuArt), scaled with the screen.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHud : MonoBehaviour
    {
        Health health;
        PlayerMotor motor;
        GUIStyle number, maxNumber, label, popup, chip;

        float shown = -1f;          // the fill as drawn: catches up with a heal, follows a hit at once
        float ghost = -1f;          // the part just lost, still lit, draining after a moment
        float lastValue = -1f;
        float hitAt = -99f, hitSize;
        float popupAmount, popupAt = -99f, popupX;
        float staminaAlpha, lastStamina = 1f, staminaFullAt = -99f, drainingUntil = -99f;

        /// <summary>The panel's right edge and height on screen, for the spell bar to keep clear of.</summary>
        public static float Right { get; private set; }
        public static float Height { get; private set; }

        const float W = 300f, H = 66f, Margin = 16f;

        void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<PlayerMotor>();
        }

        void OnEnable()
        {
            if (health != null) health.Changed += OnChanged;
        }

        void OnDisable()
        {
            if (health != null) health.Changed -= OnChanged;
        }

        void OnChanged(float value, float max)
        {
            if (lastValue < 0f) lastValue = value;
            float lost = lastValue - value;
            if (lost > 0.01f)
            {
                // the lit part starts at whatever was there before -- several quick hits make one long bite
                ghost = Mathf.Max(ghost, lastValue);
                hitAt = Time.unscaledTime;
                hitSize = Mathf.Clamp01(lost / 25f);
                // a burn's ticks, or two hits close together, count up in one number
                if (Time.unscaledTime - popupAt < 0.7f) popupAmount += lost;
                else popupAmount = lost;
                popupAt = Time.unscaledTime;
                popupX = Mathf.Clamp01(value / Mathf.Max(1f, max));
            }
            lastValue = value;
        }

        void Styles()
        {
            if (number != null) return;
            number = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            maxNumber = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerLeft };
            label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            popup = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            chip = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        static Color FillColour(float f)
        {
            Color green = new Color(0.35f, 0.88f, 0.45f), yellow = new Color(1f, 0.78f, 0.25f), red = new Color(0.95f, 0.26f, 0.2f);
            return f > 0.5f ? Color.Lerp(yellow, green, (f - 0.5f) / 0.5f) : Color.Lerp(red, yellow, Mathf.Clamp01((f - 0.15f) / 0.35f));
        }

        void OnGUI()
        {
            if (health == null || Event.current.type != EventType.Repaint) return;
            Styles();

            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            float max = Mathf.Max(1f, health.Max), current = health.Current;
            if (lastValue < 0f) lastValue = current;
            if (shown < 0f) shown = current;
            if (ghost < current) ghost = current;

            // a heal (a respawn) fills in; a hit is shown at once
            shown = current < shown ? current : Mathf.MoveTowards(shown, current, max * 1.6f * dt);
            // the lit part holds for a moment after the last hit, then drains, faster the longer it waits
            float since = now - hitAt;
            if (since > 0.6f) ghost = Mathf.MoveTowards(ghost, current, max * (0.18f + 0.5f * Mathf.Clamp01(since - 0.6f)) * dt);

            float s = Mathf.Clamp(Screen.height / 1000f, 0.9f, 1.25f);
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float VH = Screen.height / s;

            EdgeFlash(now, current / max);

            // shaken by a hit, harder by a bigger one
            float shake = Mathf.Clamp01(1f - since / 0.35f) * (3f + 9f * hitSize);
            Vector2 jolt = shake > 0f ? new Vector2(Mathf.Sin(now * 91f), Mathf.Cos(now * 77f)) * shake : Vector2.zero;
            Rect panel = new Rect(Margin + jolt.x, VH - Margin - H + jolt.y, W, H);
            Right = (Margin + W) * s;
            Height = (H + 28f) * s;

            float f = shown / max, fg = ghost / max;
            bool low = current / max < 0.3f && current > 0f;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(now * 6f) : 0f;
            float flash = Mathf.Clamp01(1f - since / 0.3f);

            // the plate
            if (low) MenuArt.Glow(panel.center, W * 1.4f, new Color(1f, 0.15f, 0.1f, 0.18f * pulse));
            MenuArt.RoundRect(new Rect(panel.x, panel.y + 4f, W, H), new Color(0f, 0f, 0f, 0.35f), 12f);
            MenuArt.RoundRect(panel, new Color(0.045f, 0.05f, 0.072f, 0.86f), 12f);
            Color edge = Color.Lerp(new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.32f), new Color(1f, 0.25f, 0.2f, 0.95f), Mathf.Max(flash, pulse * 0.8f));
            MenuArt.RoundRect(panel, edge, 12f, 1.3f + flash * 1.2f);

            // the label and the number
            label.normal.textColor = new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.85f);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 6f, 80f, 18f), "HEALTH", label);
            Color numberColour = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.3f), Mathf.Max(flash, low ? 0.6f : 0f));
            MenuArt.GlowText(new Rect(panel.xMax - 108f, panel.y + 2f, 60f, 30f), Mathf.CeilToInt(current).ToString(), number, numberColour, 0f);
            maxNumber.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(panel.xMax - 46f, panel.y + 4f, 40f, 24f), "/ " + Mathf.CeilToInt(max), maxNumber);

            // the bar: dark trough, the lit part just lost, the health left, notches every 10
            Rect bar = new Rect(panel.x + 12f, panel.y + 34f, W - 24f, 20f);
            MenuArt.RoundRect(bar, new Color(0f, 0f, 0f, 0.55f), 6f);
            if (fg > f + 0.001f)
            {
                Rect lost = new Rect(bar.x + bar.width * f, bar.y, bar.width * (fg - f), bar.height);
                Color lit = Color.Lerp(new Color(1f, 0.45f, 0.35f, 0.9f), new Color(1f, 0.95f, 0.9f, 1f), flash);
                MenuArt.RoundRect(lost, lit, 4f);
            }
            if (f > 0.001f)
            {
                Rect fill = new Rect(bar.x, bar.y, Mathf.Max(8f, bar.width * f), bar.height);
                Color c = FillColour(current / max);
                if (low) c = Color.Lerp(c, new Color(1f, 0.6f, 0.55f), pulse * 0.35f);
                MenuArt.RoundRect(fill, c, 6f);
                // a sheen along the top, and the edge brighter, as if lit from inside
                MenuArt.RoundRect(new Rect(fill.x + 2f, fill.y + 2f, fill.width - 4f, fill.height * 0.38f), new Color(1f, 1f, 1f, 0.22f), 4f);
                if (shown < current - 0.5f) MenuArt.Glow(new Vector2(fill.xMax, fill.center.y), 46f, new Color(0.6f, 1f, 0.65f, 0.6f));
            }
            for (int k = 10; k < max; k += 10)
            {
                float x = bar.x + bar.width * (k / max);
                MenuArt.RoundRect(new Rect(x - 0.75f, bar.y + 3f, 1.5f, bar.height - 6f), new Color(0f, 0f, 0f, k % 50 == 0 ? 0.55f : 0.32f), 0f);
            }
            MenuArt.RoundRect(bar, new Color(1f, 1f, 1f, 0.12f), 6f, 1f);

            // how much the last hit took, rising out of the bar where it bit
            float pt = (now - popupAt) / 1.5f;
            if (pt < 1f && popupAmount >= 0.5f)
            {
                float a = pt < 0.7f ? 1f : 1f - (pt - 0.7f) / 0.3f;
                float grow = 1f + 0.6f * Mathf.Clamp01(1f - pt * 6f);
                // above the plate, over where the bar was bitten, rising
                Vector2 at = new Vector2(Mathf.Clamp(bar.x + bar.width * popupX + 14f, bar.x + 30f, bar.xMax - 30f),
                                         panel.y - 18f - 30f * Mathf.Sqrt(pt));
                Matrix4x4 m = GUI.matrix;
                GUI.matrix = m * Matrix4x4.TRS(at, Quaternion.identity, new Vector3(grow, grow, 1f)) * Matrix4x4.Translate(-at);
                MenuArt.GlowText(new Rect(at.x - 60f, at.y - 16f, 120f, 32f), "-" + Mathf.RoundToInt(popupAmount), popup,
                                 new Color(1f, 0.32f, 0.26f, a), 1.2f);
                GUI.matrix = m;
            }

            Chips(panel);

            GUI.matrix = keep;
            StaminaWheel(now, dt);
        }

        /// <summary>The edges of the screen redden for an instant with a hit, and throb while low.</summary>
        void EdgeFlash(float now, float f)
        {
            float since = now - hitAt;
            float hit = Mathf.Clamp01(1f - since / 0.45f) * (0.25f + 0.45f * hitSize);
            float low = f > 0f && f < 0.3f ? (0.12f + 0.1f * Mathf.Sin(now * 6f)) * (1f - f / 0.3f) : 0f;
            float a = Mathf.Max(hit, low);
            if (a <= 0.005f) return;
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0.85f, 0.05f, 0.02f, a);
            GUI.DrawTexture(new Rect(-Screen.width * 0.1f, -Screen.height * 0.1f, Screen.width * 1.2f, Screen.height * 1.2f), MenuArt.Edges);
            GUI.color = Color.white;
            GUI.matrix = keep;
        }

        /// <summary>
        /// Stamina: a ring just right of and below the crosshair, constant in pixels like the crosshair.
        /// Full, it shows for a moment with a flash and goes; in use, it empties round the clock from the
        /// top, gold, orange when low, red and throbbing once dry -- with a white mark where sprinting
        /// comes back -- and a bright head while it is draining.
        /// </summary>
        void StaminaWheel(float now, float dt)
        {
            if (motor == null) return;
            float st = Mathf.Clamp01(motor.Stamina);
            bool full = st >= 0.999f;
            if (st < lastStamina - 1e-4f) drainingUntil = now + 0.15f;
            if (full && lastStamina < 0.999f) staminaFullAt = now;
            lastStamina = st;
            bool show = !full || now - staminaFullAt < 0.6f;
            staminaAlpha = Mathf.MoveTowards(staminaAlpha, show ? 1f : 0f, dt * (show ? 6f : 2.5f));
            if (staminaAlpha <= 0.01f) return;

            float px = Mathf.Clamp(Screen.height / 1000f, 0.9f, 1.4f);
            Vector2 c = new Vector2(Screen.width * 0.5f + 64f * px, Screen.height * 0.5f + 40f * px);
            float r = 20f * px, dotSize = 6.5f * px;
            const int N = 64;
            float a = staminaAlpha;
            bool winded = motor.Winded;
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 9f);

            Color col = winded ? Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(1f, 0.45f, 0.25f), pulse)
                       : st < 0.3f ? new Color(1f, 0.55f, 0.2f)
                       : Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(0.75f, 0.95f, 0.5f), Mathf.InverseLerp(0.6f, 1f, st));

            // a soft halo, warmer while it drains
            bool draining = now < drainingUntil;
            MenuArt.Glow(c, r * 4.2f, new Color(col.r, col.g, col.b, (draining ? 0.16f : 0.08f) * a));

            // the empty track, then the stamina left, round from the top
            GUI.color = new Color(0f, 0f, 0f, 0.5f * a);
            for (int i = 0; i < N; i++) DotAt(c, r, i / (float)N, dotSize + 2f * px);
            GUI.color = new Color(1f, 1f, 1f, 0.12f * a);
            for (int i = 0; i < N; i++) DotAt(c, r, i / (float)N, dotSize);
            int filled = Mathf.RoundToInt(st * N);
            GUI.color = new Color(col.r, col.g, col.b, a);
            for (int i = 0; i < filled; i++) DotAt(c, r, i / (float)N, dotSize);

            // the head of the arc, bright while it drains
            if (filled > 0 && draining)
            {
                float t = st;
                Vector2 head = c + new Vector2(Mathf.Sin(t * Mathf.PI * 2f), -Mathf.Cos(t * Mathf.PI * 2f)) * r;
                MenuArt.Glow(head, dotSize * 4f, new Color(1f, 0.95f, 0.8f, 0.8f * a));
            }

            // dry: where it has to come back to before Shift works again
            if (winded)
            {
                float t = motor.SprintAgainAt;
                Vector2 dir = new Vector2(Mathf.Sin(t * Mathf.PI * 2f), -Mathf.Cos(t * Mathf.PI * 2f));
                MenuArt.Line(c + dir * (r - 5f * px), c + dir * (r + 5f * px), 2.5f * px, new Color(1f, 1f, 1f, 0.9f * a));
            }

            // filled up: a ring flashes out from it
            float since = now - staminaFullAt;
            if (since < 0.45f)
            {
                float k = since / 0.45f, size = (r + 4f * px + 14f * px * k) * 2f;
                MenuArt.RoundRect(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size),
                                  new Color(0.85f, 1f, 0.6f, (1f - k) * 0.8f * a), size * 0.5f, 2f * px);
            }
            GUI.color = Color.white;
        }

        static void DotAt(Vector2 c, float r, float t, float size)
        {
            Vector2 p = c + new Vector2(Mathf.Sin(t * Mathf.PI * 2f), -Mathf.Cos(t * Mathf.PI * 2f)) * r;
            GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), MenuArt.Dot);
        }

        /// <summary>What is being done to you, as small chips above the plate.</summary>
        void Chips(Rect panel)
        {
            float x = panel.x;
            float y = panel.y - 28f;
            if (GetComponent<Burning>() != null) x = Chip(x, y, "BURNING", new Color(1f, 0.5f, 0.15f));
            if (GetComponent<Wet>() != null) x = Chip(x, y, "WET", new Color(0.45f, 0.75f, 1f));
            if (GetComponent<Frozen>() != null) x = Chip(x, y, "FROZEN", new Color(0.75f, 0.95f, 1f));
        }

        float Chip(float x, float y, string text, Color c)
        {
            float w = chip.CalcSize(new GUIContent(text)).x + 18f;
            Rect r = new Rect(x, y, w, 20f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            MenuArt.RoundRect(r, new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.9f), 10f);
            MenuArt.RoundRect(r, new Color(c.r, c.g, c.b, 0.8f * pulse), 10f, 1.2f);
            chip.normal.textColor = c;
            GUI.Label(r, text, chip);
            return x + w + 6f;
        }
    }
}
