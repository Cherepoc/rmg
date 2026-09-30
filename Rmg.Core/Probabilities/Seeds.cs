namespace Rmg.Core.Probabilities;

public static class Seeds
{
    /// <summary>
    ///     Derives a seed for one of several independent random sequences that share the same source seed, all 64 bits of
    ///     it spread over all 64 of the result. The result is the same on every run and platform, unlike
    ///     <see cref="HashCode" />, so songs stay reproducible.
    /// </summary>
    public static ulong Derive(ulong seed, long stream)
    {
        return Mix(seed + 0x9E3779B97F4A7C15 * unchecked((ulong)stream + 1));
    }

    /// <summary>
    ///     Derives a key, such as a note's in its figure (<c>NoteKey</c>), from another and a place, the same on every run
    ///     and platform: a name within a song, rather than a seed of its randomness.
    /// </summary>
    public static int Derive(int key, int place)
    {
        return unchecked((int)Mix(((ulong)(uint)place << 32) | (uint)key));
    }

    /// <summary>A seed of all 64 bits drawn afresh, for a song nobody asked for by its seed.</summary>
    public static ulong Random()
    {
        return BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(sizeof(ulong)));
    }

    /// <summary>The SplitMix64 finalizer, which spreads every bit of the input over the output.</summary>
    internal static ulong Mix(ulong value)
    {
        unchecked
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
            return value ^ (value >> 31);
        }
    }
}
