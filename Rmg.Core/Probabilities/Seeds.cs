namespace Rmg.Core.Probabilities;

public static class Seeds
{
    /// <summary>
    ///     Derives a seed for one of several independent random sequences that share the same source seed. The result
    ///     is the same on every run and platform, unlike <see cref="HashCode" />, so songs stay reproducible.
    /// </summary>
    public static int Derive(int seed, int stream)
    {
        // the SplitMix64 finalizer spreads every bit of the input over the output
        unchecked
        {
            var value = ((ulong)(uint)stream << 32) | (uint)seed;
            value += 0x9E3779B97F4A7C15;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EB;
            value ^= value >> 31;
            return (int)value;
        }
    }
}
