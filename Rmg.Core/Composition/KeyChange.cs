using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A song changing key at the start of a 4-bar pattern, by the scale facet of its unconventionality: never in the
///     plainest song, which keeps its key; at the middle, now and then for a last section that came back before, as a
///     last chorus goes up a whole step or a half, or for a section in a key of its own, a chorus or a bridge the
///     likelier, up or down a fourth most often, every time it plays; and at the wild end at every pattern's start and in
///     every section but the first, by any step. The whole band moves, and the lines, placed after, go on into the new
///     key.
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

    /// <summary>
    ///     The chance a section, but the song's first, plays in a key of its own, before its contrast leans it: none of the
    ///     plainest songs, few at the middle and every one at the wild end.
    /// </summary>
    public static ByConvention SectionChance { get; } = new(0, 0.08, 1);

    /// <summary>
    ///     The steps a section's own key is from the song's: up or down a fourth most often, a whole step now and then, and
    ///     any step as likely at the wild end.
    /// </summary>
    public static ImmutableArray<(int Step, ByConvention Weight)> SectionSteps { get; } =
    [
        (5, new ByConvention(0.3, 0.3, 1)),
        (7, new ByConvention(0.25, 0.25, 1)),
        (2, new ByConvention(0.2, 0.2, 1)),
        (10, new ByConvention(0.1, 0.1, 1)),
        (3, new ByConvention(0.1, 0.1, 1)),
        (9, new ByConvention(0.05, 0.05, 1)),
        ..new[] { 1, 4, 6, 8, 11 }.Select(x => (x, new ByConvention(0, 0, 1)))
    ];

    /// <param name="context">The last section's draws.</param>
    /// <param name="places">Every other pattern's draws, by its place in the song, each of its own.</param>
    /// <param name="sections">Every section's draws of its own key, by its id, each of its own.</param>
    /// <param name="unconventionality">The scale facet of the song's unconventionality.</param>
    /// <param name="sectionIds">The song's sections in its order, before any it plays again to fade out.</param>
    /// <param name="roles">What each section does in the song's form, by its id.</param>
    /// <param name="liftAt">The appearance, by its place, a last section's lift may come at: the last section, or its repeat.</param>
    internal static ImmutableArray<KeyChange> Generate(
        IGenerationContext context,
        Func<int, IGenerationContext> places,
        Func<int, IGenerationContext> sections,
        double unconventionality,
        SongMap map,
        IReadOnlyList<int> sectionIds,
        IReadOnlyDictionary<int, SectionRole> roles,
        int liftAt
    )
    {
        var last = sectionIds.Count - 1;
        var cameBack = sectionIds.Take(last).Contains(sectionIds[last]);
        var ownKeys = sectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var drawing = sections(id);
            var hasOwn = drawing.TestProbability(SectionContrast.Of(roles[id]).Chance(SectionChance.At(unconventionality), 1));
            var step = drawing.Pick(ByConvention.Weigh(SectionSteps, unconventionality));
            // the first section sets the song's key
            return hasOwn && id != sectionIds[0] ? step : 0;
        });
        var changes = ImmutableArray.CreateBuilder<KeyChange>();
        // how far the key has moved for good, and the key playing, both from the song's own
        var lift = 0;
        var key = 0;
        var place = 0;
        foreach (var (section, index) in map.Sections.Select((x, i) => (x, i)))
        for (var start = section.Start; start < section.End - 1e-9; start += section.Meter.PatternDuration, place++)
        {
            // the song starts in its own key
            if (place == 0) continue;

            var isLastSection = index == liftAt && start == section.Start;
            var drawing = isLastSection ? context : places(place);
            var chance = isLastSection && cameBack ? LastSectionChance : ElsewhereChance;
            var isChanging = drawing.TestProbability(chance.At(unconventionality));
            var step = drawing.Pick(ByConvention.Weigh(Steps, unconventionality));
            if (isChanging)
                lift += step;

            // a key as a step from the song's own, so that the band's register stays where the song put it
            var next = (lift + ownKeys[section.SectionId] + 5).Mod(12) - 5;
            if (next == key) continue;

            key = next;
            changes.Add(new KeyChange(start, key));
        }

        return changes.ToImmutable();
    }

    /// <summary>The chance a key change is led into by a chain of chords, rather than jumped to; no wilder than a jump.</summary>
    public const double PrepareChance = 0.5;

    /// <summary>How many chords lead into a key change, by the scale facet: the dominant alone the plainest, a chain the wilder.</summary>
    public static ImmutableArray<(int Chords, ByConvention Weight)> ChainLengths { get; } =
    [
        (1, new ByConvention(1, 0.6, 0.3)),
        (2, new ByConvention(0, 0.3, 0.4)),
        (3, new ByConvention(0, 0.1, 0.3))
    ];

    /// <summary>
    ///     How a chord of a chain leads to the one after it, as a chord of a key: the key's tonic in semitones from the next
    ///     chord's root, and its root's step in that key. The dominant, a fifth above, the plainest; a tritone's substitute,
    ///     a half step above, and the leading tone's chord, a half step below, from the middle out; a mediant, a major third
    ///     above, at the wild end. A chain repeats one, so that chains of fifths, chromatic dominants and cycles of thirds
    ///     come of it.
    /// </summary>
    public static ImmutableArray<((int KeyFromNext, int RootStep) Link, ByConvention Weight)> Links { get; } =
    [
        ((0, 4), new ByConvention(1, 0.75, 0.25)),
        ((-6, 4), new ByConvention(0, 0.15, 0.25)),
        ((0, 6), new ByConvention(0, 0.1, 0.25)),
        ((4, 0), new ByConvention(0, 0, 0.25))
    ];

    /// <summary>
    ///     The key changes with every one prepared that draws to be: the last chords of the pattern before it, as many as its
    ///     chain has and it has, each a key change of one chord whose root its link puts it on, leading to the next, the
    ///     last to the new key's first chord; each change drawing from a sequence of its own. The chords keep their shapes,
    ///     the scale making their quality, a cadence's raised step a minor scale's dominant major.
    /// </summary>
    /// <param name="common">The song's common state, its chords and scales, before any key change.</param>
    /// <param name="streams">A key change's draws, by its place among the song's.</param>
    /// <returns>The key changes, the chains' among them; the roots' moves, a layer of the common state; and every chain, for the trace.</returns>
    internal static (ImmutableArray<KeyChange> Keys, StateTimelineMap Roots, ImmutableArray<(double Change, ImmutableArray<double> Chords)> Chains) Prepare(
        ImmutableArray<KeyChange> changes,
        StateTimelineMap common,
        SongMap map,
        double unconventionality,
        Func<int, IGenerationContext> streams
    )
    {
        var chordStarts = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var keys = changes.ToList();
        var roots = new List<TimelineItem<int>>();
        var chains = ImmutableArray.CreateBuilder<(double, ImmutableArray<double>)>();
        int Root(double position) => common.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
        int RootPitch(double position, int key, int step) => (key + Realizer.GetChord(common.GetEffectiveStateMapAt(position).With(StateKinds.ChordRoot, step)).Chord.GetPitch(0)).Mod(12);
        for (var i = 0; i < changes.Length; i++)
        {
            var context = streams(i);
            var prepares = context.TestProbability(PrepareChance);
            var length = context.Pick(ByConvention.Weigh(ChainLengths, unconventionality));
            var (keyFromNext, rootStep) = context.Pick(ByConvention.Weigh(Links, unconventionality));
            var (change, target) = (changes[i].Position, changes[i].Semitones);
            // the chords of the pattern before, after any change before it
            var since = Math.Max(i > 0 ? changes[i - 1].Position : 0, change - map.MeterAt(Math.Max(0, change - 1e-6)).PatternDuration);
            var chords = chordStarts.Where(x => x > since + 1e-9 && x < change - 1e-9).TakeLast(length).ToArray();
            if (!prepares || chords.Length == 0)
                continue;

            // from the new key's first chord back, every chord the one leading to the one after it
            var next = RootPitch(change, target, Root(change));
            for (var j = chords.Length - 1; j >= 0; j--)
            {
                var key = (next + keyFromNext + 5).Mod(12) - 5;
                keys.Add(new KeyChange(chords[j], key));
                roots.Add((rootStep - Root(chords[j])).ToTimelineItem(chords[j]));
                roots.Add(0.ToTimelineItem(j + 1 < chords.Length ? chords[j + 1] : change));
                next = RootPitch(chords[j], key, rootStep);
            }

            chains.Add((change, [..chords]));
        }

        var duration = common.Duration;
        var moved = roots.Count == 0
            ? StateTimelineMap.Create(duration)
            : StateTimelineMap.Create(duration, [StateTimeline.Create(duration, StateKinds.ChordRoot, [..roots.GroupBy(x => x.Position).Select(x => x.First()).OrderBy(x => x.Position)]).WithLayer("Key change")]);
        return ([..keys.OrderBy(x => x.Position)], moved, chains.ToImmutable());
    }

    /// <summary>The key's step from the song's own, from each change on; none for a song that keeps its key.</summary>
    internal static StateTimelineMap ToStateTimelineMap(ImmutableArray<KeyChange> changes, double duration)
    {
        return changes.IsEmpty
            ? StateTimelineMap.Create(duration)
            : StateTimelineMap.Create(duration, [StateTimeline.Create(duration, StateKinds.KeyOffset, [..changes.Select(x => x.Semitones.ToTimelineItem(x.Position))])]);
    }
}
