using UnityEngine;
using UnityEngine.UI;

namespace MageCast.Combat
{
    /// <summary>
    /// A bar floating above something with Health, built at runtime so there is no prefab to wire up.
    ///
    /// World-space UI rather than quads made of primitives: a primitive uses the Standard shader and
    /// would be lit, so the bar would go dark in shadow exactly when you most need to read it. UI
    /// Images are unlit and stay legible from any angle.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] float height = 2.6f;        // above the object's own origin
        [SerializeField] float width = 1.4f;         // metres
        [SerializeField] float thickness = 0.16f;
        [SerializeField] float hideBeyond = 45f;     // metres; no point drawing a bar you cannot read

        Health health;
        Transform holder;
        RectTransform fill;
        Image fillImage;
        Canvas canvas;

        void Awake()
        {
            health = GetComponent<Health>();
            Build();
            Refresh(health.Current, health.Max);
            health.Changed += Refresh;
        }

        void OnDestroy()
        {
            if (health != null) health.Changed -= Refresh;
        }

        void Build()
        {
            GameObject go = new GameObject("HealthBar");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            holder = go.transform;

            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform crt = (RectTransform)go.transform;
            crt.sizeDelta = new Vector2(width, thickness);
            crt.localScale = Vector3.one;

            // dark backing, so the bar reads against both the sky and the floor
            GameObject bg = NewImage("Back", crt, new Color(0f, 0f, 0f, 0.65f));
            RectTransform brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-0.03f, -0.03f);
            brt.offsetMax = new Vector2(0.03f, 0.03f);

            GameObject fillGo = NewImage("Fill", crt, Color.white);
            fill = (RectTransform)fillGo.transform;
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            fill.pivot = new Vector2(0f, 0.5f);
            fillImage = fillGo.GetComponent<Image>();
        }

        static GameObject NewImage(string name, Transform parent, Color colour)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return go;
        }

        void Refresh(float current, float max)
        {
            float f = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            // scale rather than width, so it shrinks from the left edge without relayout
            fill.anchorMax = new Vector2(f, 1f);
            fill.offsetMax = Vector2.zero;

            // green through amber to red -- readable at a glance and colour-blind-survivable via length
            fillImage.color = f > 0.5f
                ? Color.Lerp(new Color(0.95f, 0.75f, 0.2f), new Color(0.35f, 0.85f, 0.35f), (f - 0.5f) * 2f)
                : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.75f, 0.2f), f * 2f);
        }

        void LateUpdate()
        {
            // Teardown order is not guaranteed: leaving play mode can destroy the bar's own objects
            // before this component stops ticking, and touching them then throws every frame until it
            // does. Cheap guard, and it keeps the console usable for real errors.
            if (holder == null || canvas == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            float distance = Vector3.Distance(cam.transform.position, holder.position);
            bool visible = distance <= hideBeyond;
            if (canvas.enabled != visible) canvas.enabled = visible;
            if (!visible) return;

            // Face the camera plane, not the camera position. Aiming at the position makes the bar
            // rock as you strafe past it; matching the camera's own forward keeps every bar parallel.
            holder.rotation = cam.transform.rotation;
        }
    }
}
