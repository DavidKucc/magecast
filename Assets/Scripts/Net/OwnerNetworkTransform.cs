using Unity.Netcode.Components;

namespace MageCast
{
    /// <summary>
    /// Each player moves their own character and the rest are told where it went.
    ///
    /// The default NetworkTransform makes the server move everyone, which means every keypress waits a
    /// round trip before the character responds -- through a relay that is a tenth of a second of
    /// mush on every step, in a game about dodging. Owner authority costs cheat-resistance, which a
    /// test build between friends does not need.
    /// </summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
