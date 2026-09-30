using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A riff's settings: a short figure played again and again, as a guitar, a clavinet or a synth plays one under a
///     verse or on its own, in a narrow range, leaping as freely as it steps, a note of the chord on its strong beats, and
///     no phrase shape of its own, placed by the same rules (<see cref="Line" />). Its bars follow a riff's scheme rather
///     than the section's phrase: one bar again and again, two in turn, or one three times and a turnaround.
/// </summary>
internal static class RiffLayers
{
    public static LineProfile Line { get; } = new(
        RangeWidth: 12,
        RegisterPull: 5,
        LeapSize: 5,
        StrongestWeakRank: 1,
        RepeatChance: 0.3,
        MaxLeapChance: 0.35,
        ContinueChance: 0.7,
        AimOdds: 16,
        RegisterFreedom: 0,
        RegisterFreedomSpread: 0,
        ContourShare: 0,
        ImprovisationShare: 0.3
    );

    /// <summary>How much a riff moves by step rather than by leap (<see cref="CompositionStateKinds.LineStepwiseness" />).</summary>
    public const double Stepwiseness = 0.55;

    /// <summary>How much fuller its patterns are than a track's: a riff is busy.</summary>
    public const double Fullness = 0.2;

    /// <summary>How often it leads into a chord change by step.</summary>
    public const double Leading = 0.3;

    /// <summary>The schemes a riff's four bars follow, and how likely each is: one bar, two in turn, a turnaround.</summary>
    public static ImmutableArray<Weighted<PhraseScheme>> Schemes { get; } =
    [
        new(0.45, new PhraseScheme([0, 0, 0, 0], [false, false, false, false])),
        new(0.35, new PhraseScheme([0, 1, 0, 1], [false, false, false, false])),
        new(0.2, new PhraseScheme([0, 0, 0, 1], [false, false, false, false]))
    ];
}
