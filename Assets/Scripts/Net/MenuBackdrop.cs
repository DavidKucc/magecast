using System.Collections.Generic;
using MageCast.Gestures;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// What is behind the main menu, and the part of it you can play with:
    ///
    ///  - the map you would train on, drifting slowly and leaning away from the mouse, faded into the
    ///    next one when another is picked or pointed at;
    ///  - embers rising out of the bottom of the screen, which the cursor pushes aside, and a warm light
    ///    following the cursor;
    ///  - a canvas: hold the button down anywhere off the menu and draw. A rune the game knows goes off
    ///    -- its clean shape flares in its element's colour, sparks burst out and its name shows -- the
    ///    same reading of strokes the game uses, so it is practice as much as a toy. Click one of the
    ///    runes under the title and it is drawn for you, to show how it goes.
    ///
    /// Everything here is in screen pixels, drawn before the menu's own scaling.
    /// </summary>
    public class MenuBackdrop
    {
        Texture2D[] maps;
        int shown = -1, previous = -1;
        float fade = 1f;

        struct Ember
        {
            public Vector2 At, Velocity;
            public float Size, Age, Life, Phase;
            public Color Colour;
        }
        readonly List<Ember> embers = new List<Ember>();
        const int Embers = 70;

        /// <summary>The stroke being drawn on the canvas, screen pixels, top left.</summary>
        readonly List<Vector2> stroke = new List<Vector2>();
        bool drawing;
        float releasedAt = -1f;
        Color strokeColour = MenuArt.Gold;

        struct Flare
        {
            public int Rune;
            public Rect Box;
            public float Born;
        }
        readonly List<Flare> flares = new List<Flare>();

        // a rune being drawn for you after its icon was clicked
        int demoRune = -1;
        float demoStarted;
        Rect demoBox;
        List<Vector2> demoPoints;
        const float DemoSeconds = 0.9f;

        GUIStyle caption;
        float lastFrame = -1f;

        public bool Drawing { get { return drawing || demoRune >= 0; } }

        public MenuBackdrop()
        {
            maps = new Texture2D[NetSession.ArenaScenes.Length];
            for (int i = 0; i < maps.Length; i++)
                maps[i] = Resources.Load<Texture2D>("MapPreviews/" + NetSession.ArenaScenes[i]);
        }

        /// <summary>
        /// Draws it all; <paramref name="map"/> is the one to show. <paramref name="blocked"/> is where the
        /// menu is, which a stroke may not start on.
        /// </summary>
        public void Draw(int map, Rect blocked)
        {
            float W = Screen.width, H = Screen.height;
            Event e = Event.current;
            bool repaint = e.type == EventType.Repaint;
            float dt = 0f;
            if (repaint)
            {
                dt = lastFrame < 0f ? 0f : Mathf.Clamp(Time.unscaledTime - lastFrame, 0f, 0.1f);
                lastFrame = Time.unscaledTime;
            }

            Vector2 mouse = new Vector2(Input.mousePosition.x, H - Input.mousePosition.y);
            Vector2 lean = new Vector2(mouse.x / Mathf.Max(1f, W) - 0.5f, mouse.y / Mathf.Max(1f, H) - 0.5f);

            Canvas(e, blocked);
            if (!repaint) return;

            // the map
            if (map != shown)
            {
                previous = shown;
                shown = map;
                fade = 0f;
            }
            fade = Mathf.MoveTowards(fade, 1f, dt / 0.6f);
            GUI.color = new Color(0.05f, 0.055f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            Map(previous, 1f, lean);
            Map(shown, Mathf.SmoothStep(0f, 1f, fade), lean);

            // darkened, more so at the edges, and warmest round the cursor
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.78f);      // blended in linear space: this reads as about half
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(-W * 0.1f, -H * 0.1f, W * 1.2f, H * 1.2f), VignetteTexture());
            GUI.color = Color.white;
            MenuArt.Glow(mouse, 360f, new Color(1f, 0.7f, 0.35f, 0.07f));

            Embers_(dt, mouse, W, H);
            Flares();
            Trail();
            Demo();
        }

        static Texture2D vignette;
        static Texture2D VignetteTexture()
        {
            if (vignette != null) return vignette;
            const int size = 128;
            vignette = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    vignette.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.92f, Mathf.InverseLerp(0.35f, 1.1f, d))));
                }
            vignette.Apply();
            return vignette;
        }

        void Map(int index, float alpha, Vector2 lean)
        {
            if (index < 0 || index >= maps.Length || maps[index] == null || alpha <= 0f) return;
            float W = Screen.width, H = Screen.height, t = Time.unscaledTime;
            float margin = Mathf.Max(W, H) * 0.07f;
            Vector2 drift = new Vector2(Mathf.Sin(t * 0.045f) * margin * 0.4f, Mathf.Cos(t * 0.037f) * margin * 0.3f);
            Vector2 shift = drift - lean * margin * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(-margin + shift.x, -margin + shift.y, W + margin * 2f, H + margin * 2f), maps[index], ScaleMode.ScaleAndCrop);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- embers

        void Embers_(float dt, Vector2 mouse, float W, float H)
        {
            while (embers.Count < Embers) embers.Add(NewEmber(W, H, embers.Count < Embers / 2 ? Random.Range(0f, H) : H + 10f, null));

            for (int i = embers.Count - 1; i >= 0; i--)
            {
                Ember m = embers[i];
                m.Age += dt;

                // the cursor pushes them aside
                Vector2 away = m.At - mouse;
                float d = away.magnitude;
                if (d < 150f && d > 0.01f) m.Velocity += away / d * (1f - d / 150f) * 900f * dt;

                m.Velocity.y = Mathf.Lerp(m.Velocity.y, -35f - m.Size * 4f, dt * 0.8f);
                m.Velocity.x = Mathf.Lerp(m.Velocity.x, Mathf.Sin(m.Phase + Time.unscaledTime * 0.7f) * 14f, dt * 0.8f);
                m.At += m.Velocity * dt;

                if (m.At.y < -20f || m.Age > m.Life || m.At.x < -40f || m.At.x > W + 40f)
                {
                    if (i >= Embers) { embers.RemoveAt(i); continue; }          // a spark from a rune: gone
                    m = NewEmber(W, H, H + 10f, null);
                }
                embers[i] = m;

                float life = Mathf.Clamp01(m.Age / 0.4f) * Mathf.Clamp01((m.Life - m.Age) / 0.8f);
                float flicker = 0.65f + 0.35f * Mathf.Sin(m.Phase * 3f + Time.unscaledTime * 6f);
                Color c = m.Colour;
                c.a *= life * flicker;
                MenuArt.Glow(m.At, m.Size * 4f, new Color(c.r, c.g, c.b, c.a * 0.35f));
                MenuArt.Glow(m.At, m.Size, c);
            }
        }

        static Ember NewEmber(float W, float H, float y, Color? colour)
        {
            float size = Random.Range(3f, 9f);
            return new Ember
            {
                At = new Vector2(Random.Range(0f, W), y),
                Velocity = new Vector2(Random.Range(-10f, 10f), -Random.Range(20f, 60f)),
                Size = size,
                Life = Random.Range(8f, 20f),
                Phase = Random.Range(0f, 10f),
                Colour = colour ?? Color.Lerp(new Color(1f, 0.55f, 0.2f, 0.9f), new Color(1f, 0.85f, 0.5f, 0.9f), Random.value)
            };
        }

        /// <summary>Sparks thrown out of a rune that went off.</summary>
        void Burst(Vector2 at, Color colour, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(90f, 380f);
                embers.Add(new Ember
                {
                    At = at,
                    Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed,
                    Size = Random.Range(4f, 11f),
                    Life = Random.Range(0.8f, 1.6f),
                    Phase = Random.Range(0f, 10f),
                    Colour = Color.Lerp(colour, Color.white, Random.Range(0f, 0.35f))
                });
            }
        }

        // ---------------------------------------------------------------- the canvas

        void Canvas(Event e, Rect blocked)
        {
            if (demoRune >= 0) return;
            // the event's own position, not the frame's: a slow frame still gets every point of the stroke
            // (drawn before any scaling, so this is screen pixels from the top left)
            Vector2 p = e.mousePosition;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button != 0 || blocked.Contains(p)) return;
                    drawing = true;
                    stroke.Clear();
                    stroke.Add(p);
                    strokeColour = MenuArt.Gold;
                    releasedAt = -1f;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!drawing) return;
                    if ((p - stroke[stroke.Count - 1]).sqrMagnitude >= 25f) stroke.Add(p);
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (!drawing) return;
                    drawing = false;
                    releasedAt = Time.unscaledTime;
                    Read();
                    e.Use();
                    break;
            }
        }

        /// <summary>What was drawn, read the way the game reads it (points y up, as the game takes them).</summary>
        void Read()
        {
            if (stroke.Count < 4) return;
            Vector2 min = stroke[0], max = stroke[0];
            var up = new List<Vector2>(stroke.Count);
            foreach (Vector2 q in stroke)
            {
                min = Vector2.Min(min, q);
                max = Vector2.Max(max, q);
                up.Add(new Vector2(q.x, Screen.height - q.y));
            }
            if (Mathf.Max(max.x - min.x, max.y - min.y) < 60f) return;            // a click, or a scribble

            int rune = MenuArt.RuneIndex(RuneSegments.Recognise(up).Name);
            if (rune < 0) { strokeColour = new Color(0.7f, 0.7f, 0.75f); return; }
            strokeColour = MenuArt.ElementColours[rune];
            float size = Mathf.Max(max.x - min.x, max.y - min.y) * 1.05f;
            Vector2 c = (min + max) * 0.5f;
            Fire(rune, new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size));
        }

        void Fire(int rune, Rect box)
        {
            flares.Add(new Flare { Rune = rune, Box = box, Born = Time.unscaledTime });
            Burst(box.center, MenuArt.ElementColours[rune], 46);
        }

        /// <summary>Draws a rune for you, as a hand would, in <paramref name="box"/> (screen pixels).</summary>
        public void Demonstrate(int rune, Rect box)
        {
            if (rune < 0 || rune >= MenuArt.RuneNames.Length) return;
            demoRune = rune;
            demoStarted = Time.unscaledTime;
            demoBox = box;
            demoPoints = GestureTemplates.RunePoints(MenuArt.RuneNames[rune], 48);
            drawing = false;
            stroke.Clear();
        }

        void Demo()
        {
            if (demoRune < 0 || demoPoints == null || demoPoints.Count < 2) return;
            float t = Mathf.Clamp01((Time.unscaledTime - demoStarted) / DemoSeconds);
            Color colour = MenuArt.ElementColours[demoRune];
            Vector2 c = demoBox.center;
            float half = demoBox.width * 0.5f;
            int upTo = Mathf.Max(1, Mathf.RoundToInt(t * (demoPoints.Count - 1)));
            for (int i = 1; i <= upTo; i++)
            {
                Vector2 a = c + new Vector2(demoPoints[i - 1].x, -demoPoints[i - 1].y) * half;
                Vector2 b = c + new Vector2(demoPoints[i].x, -demoPoints[i].y) * half;
                MenuArt.Line(a, b, 22f, new Color(colour.r, colour.g, colour.b, 0.18f));
                MenuArt.Line(a, b, 6f, new Color(1f, 0.95f, 0.85f, 0.95f));
            }
            Vector2 tip = c + new Vector2(demoPoints[upTo].x, -demoPoints[upTo].y) * half;
            MenuArt.Glow(tip, 46f, new Color(colour.r, colour.g, colour.b, 0.8f));
            if (t >= 1f)
            {
                int rune = demoRune;
                demoRune = -1;
                Fire(rune, demoBox);
            }
        }

        void Trail()
        {
            if (stroke.Count < 2) return;
            float fadeOut = releasedAt < 0f ? 1f : 1f - Mathf.Clamp01((Time.unscaledTime - releasedAt) / 0.5f);
            if (fadeOut <= 0f) { stroke.Clear(); return; }
            Color c = drawing ? MenuArt.Gold : strokeColour;
            for (int i = 1; i < stroke.Count; i++)
            {
                // fresher towards the pen
                float along = 0.45f + 0.55f * i / (stroke.Count - 1f);
                MenuArt.Line(stroke[i - 1], stroke[i], 20f, new Color(c.r, c.g, c.b, 0.16f * fadeOut * along));
                MenuArt.Line(stroke[i - 1], stroke[i], 5f, new Color(Mathf.Lerp(c.r, 1f, 0.5f), Mathf.Lerp(c.g, 1f, 0.5f), Mathf.Lerp(c.b, 1f, 0.5f), 0.9f * fadeOut * along));
            }
            if (drawing) MenuArt.Glow(stroke[stroke.Count - 1], 40f, new Color(c.r, c.g, c.b, 0.7f));
        }

        void Flares()
        {
            if (caption == null)
                caption = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            for (int i = flares.Count - 1; i >= 0; i--)
            {
                Flare f = flares[i];
                float t = (Time.unscaledTime - f.Born) / 1.6f;
                if (t >= 1f) { flares.RemoveAt(i); continue; }
                Color colour = MenuArt.ElementColours[f.Rune];
                float alpha = t < 0.08f ? t / 0.08f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.08f) / 0.92f);
                Rect box = MenuArt.Grow(f.Box, f.Box.width * 0.08f * t);

                MenuArt.Glow(box.center, box.width * 2.2f, new Color(colour.r, colour.g, colour.b, 0.3f * alpha));
                MenuArt.Rune(MenuArt.RuneNames[f.Rune], box, new Color(Mathf.Lerp(colour.r, 1f, 0.35f), Mathf.Lerp(colour.g, 1f, 0.35f), Mathf.Lerp(colour.b, 1f, 0.35f), alpha), 9f, 1.4f);

                Rect label = new Rect(box.center.x - 200f, box.yMax + 14f - 10f * t, 400f, 40f);
                MenuArt.GlowText(label, MenuArt.Elements[f.Rune], caption, new Color(colour.r, colour.g, colour.b, alpha), 1.2f);
            }
        }
    }
}
