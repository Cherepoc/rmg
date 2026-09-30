namespace Rmg.Core.Probabilities;

/// <summary>
///     The random sequence a song draws from: xoshiro256**, its 256 bits of state spread from a 64-bit seed by SplitMix64,
///     the same numbers on every run, platform and runtime, which <see cref="System.Random" /> does not promise across
///     versions of .NET.
/// </summary>
internal sealed class Xoshiro256
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public Xoshiro256(ulong seed)
    {
        _s0 = SplitMix(ref seed);
        _s1 = SplitMix(ref seed);
        _s2 = SplitMix(ref seed);
        _s3 = SplitMix(ref seed);
    }

    /// <summary>The next 64 random bits.</summary>
    public ulong Next()
    {
        var result = ulong.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = ulong.RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>A number from 0 up to 1, of 53 random bits, as many as a double holds.</summary>
    public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

    /// <summary>A number from 0 up to the given count, by the high half of the count times the bits, as near even as 64 bits make it.</summary>
    public ulong NextBelow(ulong count) => Math.BigMul(Next(), count, out _);

    private static ulong SplitMix(ref ulong state)
    {
        var value = state += 0x9E3779B97F4A7C15;
        return Seeds.Mix(value);
    }
}
