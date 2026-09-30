using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A song changing key at the start of a 4-bar pattern, by the scale facet of its unconventionality: never in the
///     plainest song, which keeps its key; at the middle, now and then for a last section that came back before, as a
///     last chorus goes up a whole step or a half; and at the wild end at every pattern's start, by any step. The whole
///     band moves, and the lines, placed after, go on into the new key.
/// </summary>
/// <param name="Position">Where the new key starts, a pattern's start.</param>
/// <param name="Semitones">The new key, as its step from the song's own, from -5 to 6.</param>
public sealed record KeyChange(double Position, int Semitones)
{
    /// <summary>
    ///     The chance a last section that came back before changes key: none of the plainest songs, a few at the middle
    ///     and every one at the wild end.
    /// </summary>
    public static ByConvention LastSectionChance { get; } = new(0, 0.08, 1);

    /// <summary>The chance any other pattern's start changes key, which only the wilder songs take.</summary>
    public static ByConvention ElsewhereChance { get; } = new(0, 0, 1);

    /// <summary>
    ///     The steps a key changes by: up a whole step or a half at the plain end and the middle, as a last chorus goes,
    ///     and any step as likely at the wild end.
    /// </summary>
    public static ImmutableArray<(int Step, ByConvention Weight)> Steps { get; } =
    [
        (1, new ByConvention(0.4, 0.4, 1)),
        (2, new ByConvention(0.6, 0.6, 1)),
        ..new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11 }.Select(x => (x, new ByConvention(0, 0, 1)))
    ];

    /// <param name="context">The last section's draws.</param>
    /// <param name="places">Every other pattern's draws, by its place in the song, each of its own.</param>
    /// <param name="unconventionality">The scale facet of the song's unconventionality.</param>
    /// <param name="sectionIds">The song's sections in its order, before any it plays again to fade out.</param>
    internal static ImmutableArray<KeyChange> Generate(
        IGenerationContext context,
        Func<int, IGenerationContext> places,
        double unconventionality,
        SongMap map,
        IReadOnlyList<int> sectionIds
    )
    {
        var last = sectionIds.Count - 1;
        var cameBack = sectionIds.Take(last).Contains(sectionIds[last]);
        var changes = ImmutableArray.CreateBuilder<KeyChange>();
        var key = 0;
        var place = 0;
        foreach (var (section, index) in map.Sections.Select((x, i) => (x, i)))
        for (var start = section.Start; start < section.End - 1e-9; start += map.Meter.PatternDuration, place++)
        {
            // the song starts in its own key
            if (place == 0) continue;

            var isLastSection = index == last && start == section.Start;
            var drawing = isLastSection ? context : places(place);
            var chance = isLastSection && cameBack ? LastSectionChance : ElsewhereChance;
            var isChanging = drawing.TestProbability(chance.At(unconventionality));
            var step = drawing.Pick(ByConvention.Weigh(Steps, unconventionality));
            if (!isChanging) continue;

            // a key as a step from the song's own, so that the band's register stays where the song put it
            key = (key + step + 5).Mod(12) - 5;
            changes.Add(new KeyChange(start, key));
        }

        return changes.ToImmutable();
    }

    /// <summary>The key's step from the song's own, from each change on; none for a song that keeps its key.</summary>
    internal static StateTimelineMap ToStateTimelineMap(ImmutableArray<KeyChange> changes, double duration)
    {
        return changes.IsEmpty
            ? StateTimelineMap.Create(duration)
            : StateTimelineMap.Create(duration, [StateTimeline.Create(duration, StateKinds.KeyOffset, [..changes.Select(x => x.Semitones.ToTimelineItem(x.Position))])]);
    }
}
