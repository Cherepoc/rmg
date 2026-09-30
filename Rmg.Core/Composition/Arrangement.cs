using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Which of the band's parts a section leaves out: now and then the drums, for a breakdown, the bass or the chords,
///     or the melody, for a section of the band alone, and more often than not the pad, which a section adds to lift
///     it, each the likelier the less energy the section has, and the melody
///     all but never where the section has a verse's, a pre-chorus's or a chorus's role, which a tune carries. A section
///     keeps its harmony: where both the bass and the chords would rest, the chords play.
/// </summary>
internal static class Arrangement
{
    /// <summary>The parts that may rest, and the chance each does, at a section of middling energy.</summary>
    public static ImmutableArray<(TrackRole Part, double Chance)> Rests { get; } =
    [
        (TrackRole.Drum, 0.08),
        (TrackRole.Bass, 0.05),
        (TrackRole.Chords, 0.05),
        (TrackRole.Melody, 0.1),
        (TrackRole.Pad, 0.6),
        (TrackRole.CounterMelody, 0.75),
        (TrackRole.Riff, 0.4),
        (TrackRole.Rhythm, 0.3)
    ];

    /// <summary>How far a role that a tune carries keeps its melody, as the odds against it resting.</summary>
    public const double TuneOdds = 8;

    /// <summary>The parts the section leaves out, the song's absent ones among them.</summary>
    /// <param name="energy">How the section's energy leans the draws: the more, the fewer parts rest.</param>
    /// <param name="absent">The parts the song leaves out (<see cref="SongParts" />), which rest whatever the draws.</param>
    public static ImmutableHashSet<TrackRole> DrawRests(IGenerationContext context, Tilt energy, SectionRole role, ImmutableHashSet<TrackRole> absent)
    {
        var tune = Tilt.Of(TuneOdds, role is SectionRole.Verse or SectionRole.PreChorus or SectionRole.Chorus ? -1 : 0);
        var resting = Rests
            .Where(x => context.TestProbability((x.Part == TrackRole.Melody ? tune : Tilt.None).Chance(energy.Chance(x.Chance, -1), 1)))
            .Select(x => x.Part)
            .Union(absent)
            .ToHashSet();

        // a section keeps its harmony, where the song has the chords
        if (resting.Contains(TrackRole.Bass) && resting.Contains(TrackRole.Chords) && !absent.Contains(TrackRole.Chords))
            resting.Remove(TrackRole.Chords);
        return resting.ToImmutableHashSet();
    }
}
