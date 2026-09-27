using Unity.Netcode;
using UnityEngine;

namespace MageCast.Gestures
{
    /// <summary>
    /// Everything another machine needs to produce the same spell: which one, how well it was drawn,
    /// and exactly where it left from and went.
    ///
    /// The caster decides all of it, including the aim. That keeps a shot where the shooter saw it
    /// go -- the alternative, re-aiming on the server from a camera it cannot see, would make every
    /// cast land a little off from the crosshair in a way nobody could explain.
    /// </summary>
    public struct CastData : INetworkSerializable
    {
        public byte Spell;          // GestureCaster spell index
        public byte Quality;        // CastQuality
        public float Precision;
        public float SizeScale;
        public Vector3 Muzzle;      // where a projectile leaves from
        public Vector3 Direction;
        public Vector3 Feet;        // where the caster stood -- a barrier is planted from here

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Spell);
            s.SerializeValue(ref Quality);
            s.SerializeValue(ref Precision);
            s.SerializeValue(ref SizeScale);
            s.SerializeValue(ref Muzzle);
            s.SerializeValue(ref Direction);
            s.SerializeValue(ref Feet);
        }
    }

    /// <summary>How a draw ended, as far as anyone watching is concerned.</summary>
    public enum StrokeOutcome : byte
    {
        Held,          // became a spell in the hand, waiting to be sent
        Sent,          // left the hand
        Banked,        // went into a slot
        Fizzle,
        Cancelled,
        Interrupted,
        Lost,          // held too long and let go
        WindUp         // a banked spell is about to leave -- the slot cast's tell
    }
}
