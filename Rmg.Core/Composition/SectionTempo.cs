using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A section playing at a tempo of its own, every time it plays, by the feel facet of the song's unconventionality:
///     never in the plainest song, which keeps its tempo; now and then at the middle, a chorus or a bridge the likelier,
///     a little slower or faster most often; and every section but the first at the wild end, by any step. The tempo
///     changes where the section starts, and the ending slows from the last section's.
/// </summary>
internal static class SectionTempo
{
    /// <summary>The chance a section, but the song's first, plays at a tempo of its own, before its contrast leans it.</summary>
    public static ByConvention Chance { get; } = new(0, 0.04, 1);

    /// <summary>
    ///     The tempos a section plays at, as multiples of the song's: a little slower or faster most often, and as far as
    ///     two thirds or half again as likely as any other at the wild end.
    /// </summary>
    public static ImmutableArray<(double Factor, ByConvention Weight)> Factors { get; } =
    [
        (0.9, new ByConvention(0.25, 0.25, 1)),
        (1 / 0.9, new ByConvention(0.25, 0.25, 1)),
        (0.8, new ByConvention(0.12, 0.12, 1)),
        (1.25, new ByConvention(0.12, 0.12, 1)),
        (0.75, new ByConvention(0.06, 0.06, 1)),
        (4 / 3.0, new ByConvention(0.06, 0.06, 1)),
        (2 / 3.0, new ByConvention(0.02, 0.02, 1)),
        (1.5, new ByConvention(0.02, 0.02, 1))
    ];

    /// <summary>Every section's tempo as a multiple of the song's, by its id, each drawing from a sequence of its own.</summary>
    /// <param name="sections">Every section's draws, by its id.</param>
    /// <param name="unconventionality">The feel facet of the song's unconventionality.</param>
    /// <param name="sectionIds">The song's sections in its order.</param>
    /// <param name="roles">What each section does in the song's form, by its id.</param>
    public static ImmutableDictionary<int, double> Generate(
        Func<int, IGenerationContext> sections,
        double unconventionality,
        IReadOnlyList<int> sectionIds,
        IReadOnlyDictionary<int, SectionRole> roles
    )
    {
        return sectionIds.Distinct().ToImmutableDictionary(id => id, id =>
        {
            var drawing = sections(id);
            var hasOwn = drawing.TestProbability(SectionContrast.Of(roles[id]).Chance(Chance.At(unconventionality), 1));
            var factor = drawing.Pick(ByConvention.Weigh(Factors, unconventionality));
            // the first section sets the song's tempo
            return hasOwn && id != sectionIds[0] ? factor : 1;
        });
    }

    /// <summary>The tempo, as a multiple of the song's, from every section's start on; none for a song that keeps its tempo.</summary>
    public static StateTimelineMap ToStateTimelineMap(ImmutableDictionary<int, double> tempos, SongMap map, double duration)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (tempos.Values.All(x => x == 1))
            return StateTimelineMap.Create(duration);

        IStateTimeline tempo = StateTimeline.Create(
                duration,
                StateKinds.Tempo,
                map.Sections.Select(x => tempos[x.SectionId].ToTimelineItem(x.Start))
            )
            .WithLayer("Section tempo");
        return new[] { tempo }.ToStateTimelineMap(duration);
    }
}
