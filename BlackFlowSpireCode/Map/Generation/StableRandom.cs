using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace BlackFlowSpire.Map.Generation;

// SHA-256 + SplitMix64: results do not depend on Random implementation, process hash salt,
// collection hash ordering, or the game's combat/reward RNG streams.
internal sealed class StableRandom
{
    private ulong _state;

    public StableRandom(string seed, int actIndex, string stream)
    {
        var material = FormattableString.Invariant($"BlackFlowSpire/map/v1\0{seed}\0{actIndex}\0{stream}");
        _state = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private ulong NextUInt64()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            var value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    public int Next(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);
        var bound = (ulong)exclusiveMax;
        var threshold = unchecked(0UL - bound) % bound;
        ulong value;
        do { value = NextUInt64(); } while (value < threshold);
        return (int)(value % bound);
    }

    public void Shuffle<T>(IList<T> values)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
