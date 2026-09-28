using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MageCast.Gestures
{
    /// <summary>
    /// Draws the stroke as it is being made, as a UI mesh built from the screen points. A custom
    /// Graphic rather than a LineRenderer because the stroke lives in screen space, not the world --
    /// and because this needs no sprite, material or prefab to exist.
    ///
    /// This is the player-facing half of the mechanic: if you cannot see the shape forming, you
    /// cannot tell a bad draw from a bad recognizer. The colour tracks the live best guess, so a
    /// stroke that is going wrong looks wrong before you let go.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class GestureTrail : MaskableGraphic
    {
        [SerializeField] float thickness = 4f;
        [SerializeField] Color idleColour = new Color(0.85f, 0.87f, 0.95f, 0.75f);

        readonly List<Vector2> points = new List<Vector2>();
        readonly List<Vector2> shown = new List<Vector2>();
        Color strokeColour;

        protected override void Awake()
        {
            base.Awake();
            strokeColour = idleColour;
            raycastTarget = false;
        }

        public void Begin()
        {
            points.Clear();
            strokeColour = idleColour;
            SetVerticesDirty();
        }

        public void Push(Vector2 screenPoint)
        {
            points.Add(screenPoint);
            SetVerticesDirty();
        }

        /// <summary>Recolour to the spell currently being matched, or back to neutral when nothing fits.</summary>
        public void SetPreview(Color? colour)
        {
            strokeColour = colour ?? idleColour;
            SetVerticesDirty();
        }

        public void Clear()
        {
            points.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 2) return;

            // Lines already finished are drawn straight, corner to corner: the rune forms cleanly under
            // the hand however the hand wobbled, and the line still being drawn follows it.
            List<Vector2> draw = points;
            if (RuneSegments.Enabled)
            {
                RuneSegments.Straighten(points, shown, false);
                draw = shown;
            }
            if (draw.Count < 2) return;

            // The canvas is ScreenSpaceOverlay at constant pixel size and this rect is stretched to
            // fill it, so local space is screen space shifted by half the screen.
            Vector2 half = rectTransform.rect.size * 0.5f;

            for (int i = 1; i < draw.Count; i++)
            {
                Vector2 a = draw[i - 1] - half;
                Vector2 b = draw[i] - half;
                Vector2 delta = b - a;
                if (delta.sqrMagnitude < 1e-4f) continue;

                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);

                // fade the oldest part of the stroke so the head of the line reads as "now"
                float age = i / (float)draw.Count;
                Color c = strokeColour;
                c.a *= Mathf.Lerp(0.35f, 1f, age);

                int root = vh.currentVertCount;
                vh.AddVert(a - normal, c, Vector2.zero);
                vh.AddVert(a + normal, c, Vector2.zero);
                vh.AddVert(b + normal, c, Vector2.zero);
                vh.AddVert(b - normal, c, Vector2.zero);
                vh.AddTriangle(root, root + 1, root + 2);
                vh.AddTriangle(root, root + 2, root + 3);
            }
        }

        /// <summary>Builds a full-screen overlay canvas with a trail on it. No assets required.</summary>
        public static GestureTrail Create(Transform parent)
        {
            GameObject canvasGo = new GameObject("GestureCanvas");
            canvasGo.transform.SetParent(parent, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 99;                       // just under the crosshair
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            GameObject trailGo = new GameObject("Trail", typeof(RectTransform));
            trailGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = (RectTransform)trailGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Add the CanvasRenderer by hand. MaskableGraphic carries [RequireComponent(CanvasRenderer)]
            // but that did not come through on a subclass added at runtime via AddComponent -- the mesh
            // was being built correctly every frame and then thrown away, with nothing to draw it.
            trailGo.AddComponent<CanvasRenderer>();
            return trailGo.AddComponent<GestureTrail>();
        }
    }
}
