using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A later appearance reharmonised: the song's last appearance of a section heard before, a last chorus most often, now
///     and then playing some of its chords as substitutes a third away, which share two of their three notes with the
///     chords they replace, in the section's own scale; decided before the lines are placed, so that the melody placed over
///     them fits them as it fits any chord.
/// </summary>
internal static class Reharmonisation
{
    /// <summary>The chance a song reharmonises its last repeated appearance, by the progression facet: never at the plain end, and as often at the wild as in the middle.</summary>
    public static ByConvention Chance { get; } = new(0, 0.25, 0.25);

    /// <summary>How many of the appearance's chords take a substitute, a chance each.</summary>
    public const double Share = 0.5;

    /// <summary>A substitute's root from the chord's, in scale steps, and how likely: a third below, as vi for I and ii for IV, the likelier, and a third above, as iii for I.</summary>
    public static ImmutableArray<Weighted<int>> Substitutes { get; } = [new(1, -2), new(0.5, 2)];

    /// <summary>The section's own scale, deeper than a chord's own (<see cref="ChordScales" />), which a substitute plays in.</summary>
    private static LayerValue<StepAlterations> SectionsScale { get; } = new(StateDepths.BarPattern, StepAlterations.None);

    /// <summary>
    ///     The substitutes, as the song's state: a root moved and the section's scale from each substituted chord to the
    ///     next change, never a chord moved by semitones; none where the song does not reharmonise or has no section heard
    ///     again.
    /// </summary>
    /// <param name="moved">Where a key change moves a chord or leads into one, which keeps it.</param>
    public static StateTimelineMap Apply(IGenerationContext context, double unconventionality, Genre genre, SongMap map, StateTimelineMap common, IEnumerable<double> moved)
    {
        var reharmonises = context.TestProbability(Chance.At(unconventionality, genre.Chance(GenreChoices.HarmonyDevices, "reharmonised")));
        var sections = map.Sections;
        var later = Enumerable.Range(0, sections.Length).LastOrDefault(i => sections.Take(i).Any(x => x.SectionId == sections[i].SectionId), -1);
        var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        // and a chord moved by semitones, and the chord it slides into, kept, and a chord in a scale of its own, an applied
        // dominant's, a borrowed chord's or the cadence's raised fifth, whose colour a substitute would lose
        var kept = moved.Concat(changes.Where((x, i) => common.GetEffectiveStateMapAt(x).GetStateValue(StateKinds.ChordShift) != 0
                                                       || i > 0 && common.GetEffectiveStateMapAt(changes[i - 1]).GetStateValue(StateKinds.ChordShift) != 0
                                                       || common.GetEffectiveStateMapAt(x).GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } own && own.Value != StepAlterations.None)).ToHashSet();
        var (roots, scales) = (new List<TimelineItem<int>>(), new List<TimelineItem<LayerValue<StepAlterations>>>());
        if (later >= 0)
        {
            var span = sections[later];
            var inSpan = changes.Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9).ToArray();
            for (var i = 0; i < inSpan.Length; i++)
            {
                // drawn whether the song reharmonises or not, so that its draws are as many
                var substitutes = context.TestProbability(Share);
                var step = context.Pick(Substitutes);
                if (!reharmonises || !substitutes || kept.Contains(inSpan[i]))
                    continue;
                var (at, next) = (inSpan[i], i + 1 < inSpan.Length ? inSpan[i + 1] : span.End);
                roots.AddRange([step.ToTimelineItem(at), 0.ToTimelineItem(next)]);
                scales.AddRange([SectionsScale.ToTimelineItem(at), LayerValue<StepAlterations>.Unset.ToTimelineItem(next)]);
            }
        }

        StateTrace.RecordSection(TracePoints.Reharmonisation, SectionGenerator.SectionTrace, later >= 0 ? sections[later].SectionId : 0,
            $"{roots.Count / 2} chords", roots.Count == 0 ? null : (later, roots.Where((_, j) => j % 2 == 0).Select(x => x.Position).ToImmutableArray()));
        if (roots.Count == 0)
            return StateTimelineMap.Create(common.Duration);
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> items) => items.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        return StateTimelineMap.Create(common.Duration,
        [
            StateTimeline.Create(common.Duration, StateKinds.ChordRoot, Once(roots)).WithLayer("Reharmonisation"),
            StateTimeline.Create(common.Duration, StateKinds.ScaleAlterations, Once(scales)).WithLayer("Reharmonisation")
        ]);
    }
}
