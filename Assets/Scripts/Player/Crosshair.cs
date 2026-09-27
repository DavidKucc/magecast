using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MageCast
{
    /// <summary>
    /// Builds its own screen-space crosshair at runtime, so there is no prefab or sprite to wire up.
    /// Four ticks and an optional centre dot, each with a dark backing -- a plain white crosshair
    /// disappears against the sky, and half of this arena is sky when you look up.
    ///
    /// It marks camera-forward, which is what the cast mechanic will lock to. Worth knowing: the
    /// camera sits half a metre off the shoulder, so for anything closer than a few metres the
    /// crosshair and the line out of the player's hands are not the same line. When spells arrive,
    /// fire them from the player towards the point the camera ray hits, not straight down
    /// camera-forward, or close-range casts will visibly miss what the crosshair covered.
    /// </summary>
    public class Crosshair : MonoBehaviour
    {
        [Header("Shape (pixels)")]
        [SerializeField] float gap = 5f;
        [SerializeField] float length = 8f;
        [SerializeField] float thickness = 2f;
        [SerializeField] bool centreDot = true;

        [Header("Colour")]
        [SerializeField] Color tint = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] Color outline = new Color(0f, 0f, 0f, 0.55f);

        RectTransform root;
        readonly List<Image> bars = new List<Image>();

        void Awake()
        {
            GameObject canvasGo = new GameObject("CrosshairCanvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            // constant pixel size: a crosshair that scales with resolution stops being a crosshair
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            GameObject rootGo = new GameObject("Crosshair", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            float arm = gap + length * 0.5f;
            Tick(new Vector2(0f, arm), new Vector2(thickness, length));
            Tick(new Vector2(0f, -arm), new Vector2(thickness, length));
            Tick(new Vector2(arm, 0f), new Vector2(length, thickness));
            Tick(new Vector2(-arm, 0f), new Vector2(length, thickness));
            if (centreDot) Tick(Vector2.zero, new Vector2(thickness, thickness));
        }

        void Tick(Vector2 pos, Vector2 size)
        {
            Rect(pos, size + new Vector2(2f, 2f), outline);   // backing first, so it sits behind
            bars.Add(Rect(pos, size, tint));                  // only the bright half gets retinted
        }

        Image Rect(Vector2 pos, Vector2 size, Color c)
        {
            GameObject go = new GameObject("Bar", typeof(RectTransform));
            go.transform.SetParent(root, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Hide or show the whole thing -- the gesture system will want this while drawing.</summary>
        public void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Recolour to the spell currently held ready, or null to go back to normal. While a spell is
        /// waiting to be sent, the crosshair is the only place the player is looking, so it is the only
        /// place that reminder can go without adding clutter somewhere they will not read it.
        /// </summary>
        public void SetTint(Color? colour)
        {
            Color c = colour ?? tint;
            if (colour.HasValue) c.a = tint.a;   // keep the crosshair's own opacity, not the spell's
            for (int i = 0; i < bars.Count; i++)
                if (bars[i] != null) bars[i].color = c;
        }
    }
}
