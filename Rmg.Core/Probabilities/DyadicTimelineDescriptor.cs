namespace Rmg.Core.Probabilities;

public sealed class DyadicTimelineDescriptor
{
    /// <param name="restart">How often the cycles start again, such as every half bar; the duration for never.</param>
    /// <param name="split">
    ///     How many parts a cycle splits into first, before every part halves: 2 for a cycle of a power of two of the
    ///     grid's steps, and the odd number it groups them by otherwise, such as a dotted 8th's three 16ths.
    /// </param>
    public DyadicTimelineDescriptor(double duration, double period, double phase, int maxRank, double restart, int split)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(split, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(restart);

        Duration = duration;
        Period = period;
        Phase = phase;
        MaxRank = maxRank;
        Restart = restart;
        Split = split;
    }

    /// <summary>How many parts a cycle splits into first, before every part halves.</summary>
    public int Split { get; }

    /// <summary>How often the cycles start again, the last before cut off.</summary>
    public double Restart { get; }

    public double Duration { get; }
    public double Period { get; }
    public double Phase { get; }
    public int MaxRank { get; }
}
