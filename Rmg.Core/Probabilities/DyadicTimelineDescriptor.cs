namespace Rmg.Core.Probabilities;

public sealed class DyadicTimelineDescriptor
{
    public double Duration { get; }
    public double Period { get; }
    public double Phase { get; }
    public int MaxRank { get; }

    public DyadicTimelineDescriptor(double duration, double period, double phase, int maxRank)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(period);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRank);

        Duration = duration;
        Period = period;
        Phase = phase;
        MaxRank = maxRank;
    }
}