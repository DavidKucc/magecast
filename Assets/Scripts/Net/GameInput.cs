namespace MageCast
{
    /// <summary>
    /// Whether the local player's hands are free. One place to ask, so the motor, the caster and the
    /// camera cannot disagree about it -- a menu that stops the camera but not the caster would let a
    /// click on "Resume" fire a spell.
    /// </summary>
    public static class GameInput
    {
        /// <summary>The Esc menu is open: the mouse belongs to the menu.</summary>
        public static bool MenuOpen;

        /// <summary>The local player is dead and waiting to respawn.</summary>
        public static bool LocalDead;

        /// <summary>
        /// In a browser the mouse only belongs to the game after a click locks it in, and Esc always
        /// lets go of it -- the browser insists, the page cannot refuse. Until it is locked again the
        /// hands stay off the keys too, or the first thing a tester does after alt-tabbing back is
        /// draw a glyph with a mouse that is also moving the page.
        /// </summary>
        public static bool PointerFree
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return UnityEngine.Cursor.lockState != UnityEngine.CursorLockMode.Locked;
#else
                return false;
#endif
            }
        }

        public static bool Blocked { get { return MenuOpen || LocalDead || PointerFree; } }
    }
}
