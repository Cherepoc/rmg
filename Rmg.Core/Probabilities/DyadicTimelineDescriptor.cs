using System.Collections.Immutable;

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
        Cycles = DyadicRankTimeline.GenerateCycles(duration, phase, period, restart, split);
    }

    /// <summary>A rhythm over the duration of the cycles given, such as a meter's nodes, each with its own length.</summary>
    public DyadicTimelineDescriptor(double duration, ImmutableArray<RhythmCycle> cycles, int maxRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);

        Duration = duration;
        MaxRank = maxRank;
        Cycles = cycles;
        Period = cycles.IsEmpty ? duration : cycles[0].Length;
        Restart = duration;
        Split = cycles.IsEmpty ? 2 : cycles[0].Split;
    }

    /// <summary>The rhythm's cycles, in order.</summary>
    public ImmutableArray<RhythmCycle> Cycles { get; }

    /// <summary>How many parts a cycle splits into first, before every part halves.</summary>
    public int Split { get; }

    /// <summary>How often the cycles start again, the last before cut off.</summary>
    public double Restart { get; }

    public double Duration { get; }
    public double Period { get; }
    public double Phase { get; }
    public int MaxRank { get; }
}
