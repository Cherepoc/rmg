using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A later appearance reharmonised: the song's last appearance of a section heard before, a last chorus most often, now
///     and then playing some of its pattern's chords as substitutes a third away, the same at every pass, which share two of their three notes with the
///     chords they replace, in the section's own scale, but a section on one chord, a vamp; decided before the lines are
///     placed, so that the melody placed over them fits them as it fits any chord.
/// </summary>
internal static class Reharmonisation
{
    /// <summary>The chance a song reharmonises its last repeated appearance, by the progression facet: never at the plain end, and as often at the wild as in the middle.</summary>
    public static ByConvention Chance { get; } = new(0, 0.25, 0.25);

    /// <summary>How many of the appearance's chords take a substitute, a chance each.</summary>
    public const double Share = 0.5;

    /// <summary>A substitute's root from the chord's, in scale steps, and how likely: a third below, as vi for I and ii for IV, the likelier, and a third above, as iii for I.</summary>
    public static ImmutableArray<Weighted<int>> Substitutes { get; } = [new(1, -2), new(0.5, 2)];

    /// <summary>
    ///     The substitutes, as the song's state: a root moved from each substituted chord to the
    ///     next change, never a chord moved by semitones; none where the song does not reharmonise or has no section heard
    ///     again.
    /// </summary>
    /// <param name="moved">Where a key change moves a chord or leads into one, which keeps it.</param>
    /// <param name="last">The last appearance played in full, by its place: the last section, or its drawn repeat, not the passes a fade or a strip adds after it.</param>
    public static StateTimelineMap Apply(IGenerationContext context, double unconventionality, Genre genre, SongMap map, StateTimelineMap common, IEnumerable<double> moved, int last)
    {
        var reharmonises = context.TestProbability(Chance.At(unconventionality, genre.Chance(GenreChoices.HarmonyDevices, "reharmonised")));
        var sections = map.Sections;
        var later = Enumerable.Range(0, last + 1).LastOrDefault(i => sections.Take(i).Any(x => x.SectionId == sections[i].SectionId), -1);
        var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        // and a chord moved by semitones or in a scale of its own, an applied dominant's, a borrowed chord's or the cadence's
        // raised fifth, whose colour a substitute would lose, kept, and the chord after it, which it slides or resolves into
        var leading = moved.ToHashSet();
        bool Own(double at) => common.GetEffectiveStateMapAt(at) is var state
                               && (state.GetStateValue(StateKinds.ChordShift) != 0 || state.GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } own && own.Value != StepAlterations.None);
        var kept = leading.Concat(changes.Where((x, i) => Own(x) || i > 0 && Own(changes[i - 1]))).ToHashSet();
        var roots = new List<TimelineItem<int>>();
        // a section on one chord, a vamp, which a substitute would undo, left as it is, but the chords leading into a key change
        bool OneChord(SectionSpan first) => changes.Where(x => first.IsWithin(x) && !leading.Contains(x)).Select(x => common.GetEffectiveStateMapAt(x).GetStateValue(StateKinds.ChordRoot)).Distinct().Count() <= 1;
        if (later >= 0 && OneChord(sections.First(x => x.SectionId == sections[later].SectionId)))
            kept.UnionWith(changes);
        if (later >= 0)
        {
            var span = sections[later];
            var inSpan = changes.Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9).ToArray();
            // a substitute drawn once for each chord of the section's pattern, by its place in it, and played at every pass,
            // as a band keeps the changes it learned each time round
            var drawn = new Dictionary<double, int?>();
            for (var i = 0; i < inSpan.Length; i++)
            {
                var place = Math.Round((inSpan[i] - span.Start) % span.Meter.PatternDuration, 6);
                if (!drawn.TryGetValue(place, out var step))
                {
                    // drawn whether the song reharmonises or not, so that its draws are as many
                    var substitutes = context.TestProbability(Share);
                    var drawnStep = context.Pick(Substitutes);
                    drawn[place] = step = substitutes ? drawnStep : null;
                }

                if (!reharmonises || step is null || kept.Contains(inSpan[i]))
                    continue;
                var (at, next) = (inSpan[i], i + 1 < inSpan.Length ? inSpan[i + 1] : span.End);
                roots.AddRange([step.Value.ToTimelineItem(at), 0.ToTimelineItem(next)]);
            }
        }

        StateTrace.RecordSection(TracePoints.Reharmonisation, SectionGenerator.SectionTrace, later >= 0 ? sections[later].SectionId : 0,
            $"{roots.Count / 2} chords", roots.Count == 0 ? null : (later, roots.Where((_, j) => j % 2 == 0).Select(x => x.Position).ToImmutableArray()));
        if (roots.Count == 0)
            return StateTimelineMap.Create(common.Duration);
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> items) => items.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        return StateTimelineMap.Create(common.Duration,
        [
            StateTimeline.Create(common.Duration, StateKinds.ChordRoot, Once(roots)).WithLayer("Reharmonisation")
        ]);
    }
}
