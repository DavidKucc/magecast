using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Whether the mouse is really locked to the game. In the exe that is Cursor.lockState; in a browser
    /// it is asked of the page (Plugins/WebGL/WebPointer.jslib), because Esc releases the lock behind
    /// Unity's back and Cursor.lockState goes on reporting the lock the game asked for.
    /// </summary>
    public static class WebPointer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int WebPointerIsLocked();
#endif

        public static bool IsLocked
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return WebPointerIsLocked() != 0;
#else
                return Cursor.lockState == CursorLockMode.Locked;
#endif
            }
        }
    }
}
