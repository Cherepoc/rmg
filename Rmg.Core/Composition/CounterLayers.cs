namespace Rmg.Core.Composition;

/// <summary>
///     A counter-melody's settings: a second line under the melody, as strings or a horn play one in a chorus, slower
///     than the melody, stepwise, a note of the chord on its strong beats, and no phrase shape of its own, placed by the
///     same rules (<see cref="Line" />) and going on from note to note through the song.
/// </summary>
internal static class CounterLayers
{
    public static LineProfile Line { get; } = new(
        RangeWidth: 14,
        RegisterPull: 7,
        LeapSize: 5,
        StrongestWeakRank: 1,
        RepeatChance: 0.25,
        MaxLeapChance: 0.2,
        ContinueChance: 0.85,
        AimOdds: 16,
        RegisterFreedom: 0,
        RegisterFreedomSpread: 0,
        ContourShare: 0,
        ImprovisationShare: 0.5
    );

    /// <summary>How much the counter-melody moves by step rather than by leap (<see cref="CompositionStateKinds.LineStepwiseness" />).</summary>
    public const double Stepwiseness = 0.9;

    /// <summary>How much slower its cycles are than a track's, as a step of their period's power: twice as long.</summary>
    public const int SlowerBy = 1;

    /// <summary>How much sparser its patterns are than a track's.</summary>
    public const double Fullness = -0.2;

    /// <summary>How often it leads into a chord change by step, as the melody's leading.</summary>
    public const double Leading = 0.5;
}
