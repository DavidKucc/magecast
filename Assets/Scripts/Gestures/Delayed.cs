using System;
using System.Collections;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Runs something a moment later, for spells that are gone by the time their effect lands -- a
    /// projectile destroys itself on impact, so it cannot wait for anything itself. Lives in the scene
    /// it was started in: leaving the arena drops whatever was still waiting.
    /// </summary>
    public class Delayed : MonoBehaviour
    {
        public static void Run(float seconds, Action action)
        {
            if (action == null) return;
            var go = new GameObject("Delayed");
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Delayed>().StartCoroutine(After(go, seconds, action));
        }

        static IEnumerator After(GameObject host, float seconds, Action action)
        {
            yield return new WaitForSeconds(seconds);
            try { action(); }
            finally { Destroy(host); }
        }
    }
}
