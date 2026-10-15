using System.Collections.Generic;
using MageCast.Gestures;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// The menus' look, drawn with OnGUI like the rest of them: rounded panels, buttons that answer the
    /// mouse (they lift, glow and ring where they are clicked), glowing text and lines, and the backdrop
    /// -- a map drifting behind the menu, embers rising that shy away from the cursor, and a canvas on
    /// which a rune drawn with the mouse goes off in its element's colours.
    ///
    /// Everything is drawn from a few textures made here at run time; nothing comes from the asset packs,
    /// so it looks the same in a clone of the repository. Plain letters only: a browser build has no system
    /// font to fall back on for anything else.
    /// </summary>
    public static class MenuArt
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        public static readonly Color Panel = new Color(0.06f, 0.065f, 0.09f, 0.86f);

        // ---------------------------------------------------------------- textures

        static Texture2D soft, line, vignette;

        /// <summary>A round glow, white, fading from the middle.</summary>
        public static Texture2D Soft
        {
            get
            {
                if (soft == null) soft = Radial(64, d => Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f));
                return soft;
            }
        }

        /// <summary>The edges of the screen darkened, the middle left alone.</summary>
        static Texture2D Vignette
        {
            get
            {
                if (vignette == null) vignette = Radial(128, d => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d)), true);
                return vignette;
            }
        }

        static Texture2D edges;

        /// <summary>White at the edges of the screen, nothing in the middle -- tinted, a flash or a throb round the view.</summary>
        public static Texture2D Edges
        {
            get
            {
                if (edges == null) edges = Radial(128, d => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.2f, d)));
                return edges;
            }
        }

        /// <summary>A line seen across: bright in the middle, soft at the sides.</summary>
        static Texture2D LineTex
        {
            get
            {
                if (line == null)
                {
                    line = new Texture2D(4, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                    for (int y = 0; y < 32; y++)
                    {
                        float v = Mathf.Abs((y + 0.5f) / 32f - 0.5f) * 2f;
                        float a = Mathf.Pow(Mathf.Clamp01(1f - v), 1.6f);
                        for (int x = 0; x < 4; x++) line.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                    line.Apply();
                }
                return line;
            }
        }

        static Texture2D Radial(int size, System.Func<float, float> alpha, bool black = false)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, black ? new Color(0f, 0f, 0f, alpha(d)) : new Color(1f, 1f, 1f, alpha(d)));
                }
            t.Apply();
            return t;
        }

        // ---------------------------------------------------------------- shapes

        /// <summary>
        /// A rounded rectangle: filled, or with <paramref name="border"/> only its outline. The colour is
        /// given as it should look: the project renders in linear space, and the rounded-rectangle path
        /// takes its colour as linear, unlike GUI.color -- a dark plate came out a washed grey.
        /// </summary>
        public static void RoundRect(Rect r, Color c, float radius, float border = 0f)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c,
                            Vector4.one * border, Vector4.one * radius);
        }

        public static void Glow(Vector2 centre, float size, Color c)
        {
            Color keep = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), Soft);
            GUI.color = keep;
        }

        /// <summary>
        /// A soft line from <paramref name="a"/> to <paramref name="b"/>, round at the ends. Not inside a
        /// scroll view or a clip: the turn is applied after the clip's offset and lands elsewhere.
        /// </summary>
        public static void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.01f) return;
            Matrix4x4 keep = GUI.matrix;
            Color keepColour = GUI.color;
            // turned about its start on top of whatever scaling is in force (RotateAroundPivot gets the
            // pivot wrong under a scaled GUI.matrix)
            GUI.matrix = keep * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, -width * 0.5f, length, width), LineTex);
            GUI.matrix = keep;
            GUI.DrawTexture(new Rect(b.x - width * 0.5f, b.y - width * 0.5f, width, width), Soft);
            GUI.color = keepColour;
        }

        /// <summary>Text with a soft glow and a shadow under it.</summary>
        public static void GlowText(Rect r, string text, GUIStyle style, Color colour, float glow)
        {
            Color keep = style.normal.textColor;
            if (glow > 0f)
            {
                style.normal.textColor = new Color(colour.r, colour.g, colour.b, 0.07f * glow);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    GUI.Label(new Rect(r.x + Mathf.Cos(a) * 4f, r.y + Mathf.Sin(a) * 4f, r.width, r.height), text, style);
                }
            }
            style.normal.textColor = new Color(0f, 0f, 0f, 0.6f * colour.a);
            GUI.Label(new Rect(r.x + 2f, r.y + 3f, r.width, r.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(r, text, style);
            style.normal.textColor = keep;
        }

        public static Rect Grow(Rect r, float by)
        {
            return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
        }

        // ---------------------------------------------------------------- answering the mouse

        static readonly Dictionary<int, float> hovered = new Dictionary<int, float>();
        static float lastRepaint = -1f;

        /// <summary>
        /// How far the mouse has come onto a control, 0..1, eased in and out -- so a button rises to meet
        /// it rather than snapping. Moved on once a frame (OnGUI runs several times in one).
        /// </summary>
        public static float Hover(int key, bool over)
        {
            float h;
            hovered.TryGetValue(key, out h);
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Clamp(Time.unscaledTime - lastRepaint, 0f, 0.1f);
                h = Mathf.MoveTowards(h, over ? 1f : 0f, dt * (over ? 7f : 4f));
                hovered[key] = h;
            }
            return Mathf.SmoothStep(0f, 1f, h);
        }

        /// <summary>Call once at the end of a menu's OnGUI: the clock the hovering eases by.</summary>
        public static void EndFrame()
        {
            if (Event.current.type == EventType.Repaint)
            {
                lastRepaint = Time.unscaledTime;
                DrawRipples();
            }
        }

        public static int Key(string text, Rect r)
        {
            return (text ?? "").GetHashCode() ^ Mathf.RoundToInt(r.x * 7f + r.y * 131f);
        }

        /// <summary>
        /// A button: a dark rounded plate with a gold edge that lifts, brightens and glows as the mouse comes
        /// onto it, a light sweeping across it, and a ring where it is clicked. <paramref name="primary"/>
        /// is warmer and gold from the start -- the thing most people came to press.
        /// </summary>
        public static bool Button(Rect r, string text, GUIStyle style, bool primary = false)
        {
            bool enabled = GUI.enabled;
            bool over = enabled && r.Contains(Event.current.mousePosition);
            float h = Hover(Key(text, r), over);
            float dim = enabled ? 1f : 0.45f;

            Rect d = Grow(r, h * 2f);
            d.y -= h * 2f;
            bool down = over && GUIUtility.hotControl != 0 && Input.GetMouseButton(0);
            if (down) d.y += 2f;

            if (Event.current.type == EventType.Repaint)
            {
                if (h > 0.01f) Glow(d.center, Mathf.Max(d.width, d.height) * 1.5f, new Color(Gold.r, Gold.g, Gold.b, 0.16f * h));
                RoundRect(new Rect(d.x, d.y + 4f, d.width, d.height), new Color(0f, 0f, 0f, 0.35f * dim), 10f);     // shadow
                Color fill = primary
                    ? Color.Lerp(new Color(0.2f, 0.14f, 0.06f, 0.94f), new Color(0.36f, 0.24f, 0.08f, 0.97f), h)
                    : Color.Lerp(new Color(0.08f, 0.085f, 0.12f, 0.88f), new Color(0.15f, 0.15f, 0.2f, 0.94f), h);
                fill.a *= dim;
                RoundRect(d, fill, 10f);

                if (h > 0.01f)
                {
                    // a light sweeping across while it is under the mouse
                    float sweep = Mathf.Repeat(Time.unscaledTime * 0.7f, 1.6f) - 0.3f;
                    GUI.BeginClip(d);
                    Glow(new Vector2(d.width * sweep, d.height * 0.5f), d.height * 2.2f, new Color(1f, 0.95f, 0.8f, 0.09f * h));
                    GUI.EndClip();
                }

                Color edge = Color.Lerp(new Color(Gold.r, Gold.g, Gold.b, primary ? 0.6f : 0.28f), Gold, h);
                edge.a *= dim;
                RoundRect(d, edge, 10f, 1.4f + h * 0.8f);

                Color text0 = primary ? Gold : new Color(0.92f, 0.92f, 0.95f, 1f);
                Color textColour = Color.Lerp(text0, primary ? Color.white : Gold, h);
                textColour.a *= dim;
                Color keep = style.normal.textColor;
                style.normal.textColor = textColour;
                GUI.Label(d, text, style);
                style.normal.textColor = keep;
            }

            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) Ripple(GUI.matrix.MultiplyPoint3x4(Event.current.mousePosition), primary ? Gold : new Color(1f, 1f, 1f, 0.9f));
            return clicked;
        }

        /// <summary>A quiet text button: no plate, an underline drawn in from the middle under the mouse.</summary>
        public static bool Link(Rect r, string text, GUIStyle style)
        {
            bool over = GUI.enabled && r.Contains(Event.current.mousePosition);
            float h = Hover(Key(text, r), over);
            if (Event.current.type == EventType.Repaint)
            {
                Color keep = style.normal.textColor;
                style.normal.textColor = Color.Lerp(new Color(1f, 1f, 1f, GUI.enabled ? 0.65f : 0.3f), Gold, h);
                GUI.Label(r, text, style);
                style.normal.textColor = keep;
                float w = style.CalcSize(new GUIContent(text)).x * h;
                if (w > 0.5f) RoundRect(new Rect(r.center.x - w * 0.5f, r.yMax - 6f, w, 2f), Gold, 1f);
            }
            bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
            if (clicked) Ripple(GUI.matrix.MultiplyPoint3x4(Event.current.mousePosition), Gold);
            return clicked;
        }

        // ---------------------------------------------------------------- rings where things are clicked

        struct Ring { public Vector2 At; public float Born; public Color Colour; }
        static readonly List<Ring> rings = new List<Ring>();

        /// <summary>A ring spreading out from <paramref name="screenPoint"/> (screen pixels, top left).</summary>
        public static void Ripple(Vector2 screenPoint, Color colour)
        {
            rings.Add(new Ring { At = screenPoint, Born = Time.unscaledTime, Colour = colour });
        }

        static void DrawRipples()
        {
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                float t = (Time.unscaledTime - rings[i].Born) / 0.55f;
                if (t >= 1f) { rings.RemoveAt(i); continue; }
                float size = Mathf.Lerp(10f, 120f, 1f - (1f - t) * (1f - t));
                Color c = rings[i].Colour;
                c.a *= (1f - t) * 0.8f;
                RoundRect(new Rect(rings[i].At.x - size * 0.5f, rings[i].At.y - size * 0.5f, size, size), c, size * 0.5f, 2f);
            }
            GUI.matrix = keep;
        }

        // ---------------------------------------------------------------- runes

        /// <summary>The runes the game knows, with the element each one is and its colour.</summary>
        public static readonly string[] RuneNames =
            { GestureTemplates.Kenaz, GestureTemplates.Laguz, GestureTemplates.Sowulo, GestureTemplates.Ehwaz, GestureTemplates.Uruz };
        public static readonly string[] Elements = { "FIRE", "ICE", "LIGHTNING", "AIR", "BARRIER" };
        public static readonly Color[] ElementColours =
        {
            new Color(1f, 0.45f, 0.15f), new Color(0.55f, 0.85f, 1f), new Color(1f, 0.9f, 0.35f),
            new Color(0.75f, 1f, 0.9f), new Color(1f, 0.85f, 0.4f)
        };

        public static int RuneIndex(string name)
        {
            return System.Array.IndexOf(RuneNames, name);
        }

        // ---------------------------------------------------------------- loading

        static GUIStyle loadingTitle, loadingDetail, loadingRune;
        static float loadingSince = -1f, loadingSeen = -1f;

        /// <summary>
        /// The screen while something loads -- a game being joined, a match starting: the map it is for
        /// behind, darkened, and in the middle the runes drawing themselves in turn in their colours, as
        /// on the web page's own loading screen, over <paramref name="headline"/> and <paramref name="detail"/>.
        /// Fades in, so a quick load is not a flash.
        /// </summary>
        public static void Loading(string headline, string detail, Texture2D picture)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (loadingTitle == null)
            {
                loadingTitle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                loadingDetail = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
                loadingRune = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            float now = Time.unscaledTime;
            if (loadingSeen < now - 0.5f) loadingSince = now;          // not drawn for a while: a new load
            loadingSeen = now;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - loadingSince) / 0.35f));
            float W = Screen.width, H = Screen.height;
            Matrix4x4 keep = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;

            GUI.color = new Color(0.03f, 0.035f, 0.05f, a);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            if (picture != null)
            {
                float m = Mathf.Max(W, H) * 0.06f, z = (now - loadingSince) * 6f;
                GUI.color = new Color(1f, 1f, 1f, 0.5f * a);
                GUI.DrawTexture(new Rect(-m - z, -m - z * 0.5f, W + 2f * m + 2f * z, H + 2f * m + z), picture, ScaleMode.ScaleAndCrop);
                // blended in linear space: this much is needed for the map to sit well behind the text
                GUI.color = new Color(0.02f, 0.025f, 0.04f, 0.9f * a);
                GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            // a rune drawing itself, then the next, each in its element's colour
            const float Each = 1.4f;
            float t = (now - loadingSince) / Each;
            int which = Mathf.FloorToInt(t) % RuneNames.Length;
            float part = t - Mathf.Floor(t);
            Color c = ElementColours[which];
            float size = Mathf.Min(150f, H * 0.22f);
            Vector2 centre = new Vector2(W * 0.5f, H * 0.42f);
            Glow(centre, size * 2.6f, new Color(c.r, c.g, c.b, 0.12f * a));
            float drawn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(part / 0.6f));
            float fade = part < 0.8f ? 1f : 1f - (part - 0.8f) / 0.2f;
            PartialRune(RuneNames[which], new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), drawn,
                        new Color(c.r, c.g, c.b, a * fade));
            loadingRune.normal.textColor = new Color(c.r, c.g, c.b, a * fade * Mathf.Clamp01(part / 0.3f));
            GUI.Label(new Rect(0f, centre.y + size * 0.5f + 8f, W, 20f), Elements[which], loadingRune);

            float y = centre.y + size * 0.5f + 40f;
            GlowText(new Rect(0f, y, W, 44f), headline, loadingTitle, new Color(Gold.r, Gold.g, Gold.b, a), 1f);
            int dots = Mathf.FloorToInt(now * 2.5f) % 4;
            loadingDetail.normal.textColor = new Color(1f, 1f, 1f, 0.7f * a);
            GUI.Label(new Rect(W * 0.5f - 300f, y + 48f, 600f, 60f), detail + new string('.', dots), loadingDetail);

            GUI.matrix = keep;
        }

        /// <summary>The first <paramref name="drawn"/> share of a rune's path, with a bright point where the pen is.</summary>
        static void PartialRune(string name, Rect box, float drawn, Color colour)
        {
            Vector2[] corners = GestureTemplates.Corners(name);
            if (corners == null || corners.Length < 2 || colour.a <= 0f) return;
            Vector2 c = box.center;
            float half = box.width * 0.5f;
            float total = 0f;
            for (int i = 1; i < corners.Length; i++) total += Vector2.Distance(corners[i - 1], corners[i]);
            float left = total * drawn;
            Vector2 pen = c + new Vector2(corners[0].x, -corners[0].y) * half;
            for (int i = 1; i < corners.Length && left > 0f; i++)
            {
                float len = Vector2.Distance(corners[i - 1], corners[i]);
                Vector2 a = c + new Vector2(corners[i - 1].x, -corners[i - 1].y) * half;
                Vector2 b = c + new Vector2(corners[i].x, -corners[i].y) * half;
                Vector2 to = len <= left ? b : Vector2.Lerp(a, b, left / len);
                Line(a, to, 20f, new Color(colour.r, colour.g, colour.b, colour.a * 0.25f));
                Line(a, to, 6f, new Color(Mathf.Lerp(colour.r, 1f, 0.35f), Mathf.Lerp(colour.g, 1f, 0.35f), Mathf.Lerp(colour.b, 1f, 0.35f), colour.a));
                pen = to;
                left -= len;
            }
            if (drawn < 1f) Glow(pen, 40f, new Color(colour.r, colour.g, colour.b, colour.a * 0.9f));
        }

        /// <summary>A rune in a box, straight lines the way it is meant to look, glowing.</summary>
        public static void Rune(string name, Rect box, Color colour, float width, float glow = 1f)
        {
            Vector2[] corners = GestureTemplates.Corners(name);
            if (corners == null || corners.Length < 2) return;
            Vector2 c = box.center;
            float half = Mathf.Min(box.width, box.height) * 0.5f;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 0 && glow <= 0f) continue;
                Color col = pass == 0 ? new Color(colour.r, colour.g, colour.b, colour.a * 0.28f * glow) : colour;
                float w = pass == 0 ? width * 3.2f : width;
                for (int i = 1; i < corners.Length; i++)
                    Line(c + new Vector2(corners[i - 1].x, -corners[i - 1].y) * half,
                         c + new Vector2(corners[i].x, -corners[i].y) * half, w, col);
            }
        }
    }
}
