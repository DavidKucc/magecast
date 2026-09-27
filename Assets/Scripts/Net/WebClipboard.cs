using UnityEngine;

namespace MageCast
{
    /// <summary>
    /// Copy to the clipboard the player actually pastes from. In the exe that is Unity's own; in a
    /// browser it has to go through the page (Plugins/WebGL/WebClipboard.jslib), or the copied room code
    /// never leaves the game.
    /// </summary>
    public static class WebClipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void WebClipboardCopy(string text);
#endif

        public static void Copy(string text)
        {
            GUIUtility.systemCopyBuffer = text;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebClipboardCopy(text);
#endif
        }
    }
}
