namespace Rmg.Core.Composition;

/// <summary>
///     The meter every song is in: bars of four beats, and sections made of 4-bar patterns, one bar for every chord of
///     the progression.
/// </summary>
public static class Meter
{
    /// <summary>How long a bar is, in beats.</summary>
    public const double BarDuration = 4;

    /// <summary>How many bars a section's pattern has.</summary>
    public const int PatternBarCount = Progressions.BarCount;

    /// <summary>How long a section's pattern is, in beats.</summary>
    public const double PatternDuration = PatternBarCount * BarDuration;

    /// <summary>The tempo, in beats a minute, that a song's tempo state is a multiple of.</summary>
    public const double BaseTempo = 120;
}
