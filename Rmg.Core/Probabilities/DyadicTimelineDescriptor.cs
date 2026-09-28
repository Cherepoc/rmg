namespace Rmg.Core.Probabilities;

public sealed class DyadicTimelineDescriptor
{
    /// <param name="restart">How often the cycles start again, such as every half bar; the duration for never.</param>
    public DyadicTimelineDescriptor(double duration, double period, double phase, int maxRank, double restart)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(restart);

        Duration = duration;
        Period = period;
        Phase = phase;
        MaxRank = maxRank;
        Restart = restart;
    }

    /// <summary>How often the cycles start again, the last before cut off.</summary>
    public double Restart { get; }

    public double Duration { get; }
    public double Period { get; }
    public double Phase { get; }
    public int MaxRank { get; }
}
