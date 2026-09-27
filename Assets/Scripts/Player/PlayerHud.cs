using MageCast.Combat;
using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Your own health, bottom left. Deliberately far from the spell readout in the top left -- those
    /// two are read at different moments and putting them together means neither gets noticed.
    ///
    /// OnGUI rather than a canvas because this is placeholder HUD; it will be replaced wholesale once
    /// there is any art direction, and a canvas hierarchy would only be something to throw away.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] float barWidth = 260f;
        [SerializeField] float barHeight = 18f;
        [SerializeField] float margin = 16f;

        Health health;
        GUIStyle style;

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void OnGUI()
        {
            if (health == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };

            float y = Screen.height - margin - barHeight;
            float f = health.Fraction;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(margin - 2f, y - 2f, barWidth + 4f, barHeight + 4f), Texture2D.whiteTexture);

            GUI.color = f > 0.35f ? new Color(0.35f, 0.8f, 0.4f) : new Color(0.9f, 0.3f, 0.25f);
            GUI.DrawTexture(new Rect(margin, y, barWidth * f, barHeight), Texture2D.whiteTexture);

            GUI.color = Color.white;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(margin + 8f, y - 1f, barWidth, barHeight + 4f),
                      Mathf.CeilToInt(health.Current) + " / " + Mathf.CeilToInt(health.Max), style);
        }
    }
}
