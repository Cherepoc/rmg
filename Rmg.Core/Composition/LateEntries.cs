using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The parts a song holds back at its start and brings in at a later section, the rest of the band as it was, as the
///     drums come in at the second verse or the bass at the first chorus: each part on its own, by the form facet, never
///     in a plain song and as often at the wild end as at the middle, so that one part, several or none enter late,
///     apart or together. A part enters where a section starts, from the song's second to the first that came before,
///     the louder the likelier; never the melody, which an intro holds back, nor the chords, so that the song's opening
///     keeps its harmony.
/// </summary>
internal static class LateEntries
{
    /// <summary>The parts that may enter late, and the chance each does, by the form facet.</summary>
    public static ImmutableArray<(TrackRole Part, ByConvention Chance)> Chances { get; } =
    [
        (TrackRole.Drum, new ByConvention(0, 0.1, 0.1)),
        (TrackRole.Bass, new ByConvention(0, 0.05, 0.05)),
        (TrackRole.Riff, new ByConvention(0, 0.05, 0.05)),
        (TrackRole.Rhythm, new ByConvention(0, 0.05, 0.05))
    ];

    /// <summary>How much likelier a part enters at a section of an energy of 1 than at one of 0, as the odds of 1.</summary>
    public const double EnergyOdds = 4;

    /// <summary>Every part the song holds back, and the appearance, by its place in the song, it enters at.</summary>
    /// <param name="played">The song's sections in its order, by their ids.</param>
    /// <param name="energies">Every appearance's energy, by its place.</param>
    /// <param name="form">The song's form facet.</param>
    /// <param name="absent">The parts the song leaves out, which are never held back.</param>
    public static ImmutableDictionary<TrackRole, int> Generate(
        IGenerationContext context,
        IReadOnlyList<int> played,
        IReadOnlyList<double> energies,
        double form,
        ImmutableHashSet<TrackRole> absent
    )
    {
        // the song's opening, to where a section first comes back, or all of it where none does
        var firstReturn = Enumerable.Range(1, Math.Max(0, played.Count - 1)).FirstOrDefault(i => SongMap.TimesBefore(played, i) > 0, played.Count - 1);
        ImmutableArray<Weighted<int>> entries = [..Enumerable.Range(1, Math.Max(0, firstReturn)).Select(i => new Weighted<int>(Tilt.Of(EnergyOdds, energies[i]).Weigh(1, 1), i))];
        var held = ImmutableDictionary.CreateBuilder<TrackRole, int>();
        foreach (var (part, chance) in Chances)
        {
            // drawn every time, so that the draws stay as they are
            var holds = context.TestProbability(chance.At(form));
            var entry = entries.IsEmpty ? 0 : context.Pick(entries);
            if (holds && entry > 0 && !absent.Contains(part))
                held[part] = entry;
        }

        return held.ToImmutable();
    }
}
