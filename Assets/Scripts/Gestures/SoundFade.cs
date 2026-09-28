using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>Brings one sound down to silence over a while, then stops it.</summary>
    public class SoundFade : MonoBehaviour
    {
        AudioSource source;
        float startAt;
        float length;
        float from;

        /// <summary>Fades <paramref name="a"/> out, starting <paramref name="after"/> seconds from now.</summary>
        public static void Out(AudioSource a, float after, float over)
        {
            if (a == null) return;
            SoundFade f = a.gameObject.AddComponent<SoundFade>();
            f.source = a;
            f.startAt = Time.time + Mathf.Max(0f, after);
            f.length = Mathf.Max(0.01f, over);
            f.from = a.volume;
        }

        void Update()
        {
            if (source == null) { Destroy(this); return; }
            if (Time.time < startAt) return;
            float k = 1f - (Time.time - startAt) / length;
            if (k <= 0f)
            {
                source.Stop();
                Destroy(this);
                return;
            }
            source.volume = from * k;
        }
    }
}
