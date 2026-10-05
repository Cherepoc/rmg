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
    /// <summary>How a key change is led into, where it is: a chain of chords, or a pivot chord both keys share.</summary>
    public enum Device
    {
        Chain,
        Pivot,
        Pedal,
        Sequence
    }

    /// <summary>
    ///     How a key change is led into, by the scale facet: a pivot chord, the smoothest, the plainer, a chain of chords,
    ///     the more striking, a pedal on the new key's dominant under the old key's last bar, the wilder, and the section's
    ///     last pattern, its phrase again, played halfway to the new key, a sequence climbing into it.
    /// </summary>
    public static ImmutableArray<(Device Device, ByConvention Weight)> Devices { get; } =
    [
        (Device.Chain, new ByConvention(1, 0.6, 0.4)),
        (Device.Pivot, new ByConvention(1, 0.3, 0.2)),
        (Device.Pedal, new ByConvention(0, 0.1, 0.4)),
        (Device.Sequence, new ByConvention(0, 0.1, 0.2))
    ];

    /// <summary>The steps of the new key a pivot chord may be on, a predominant's the likeliest, and how likely.</summary>
    public static ImmutableArray<Weighted<int>> PivotSteps { get; } = [new(1, 1), new(1, 3), new(0.6, 5), new(0.3, 2)];

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
    /// <param name="liftAt">The appearance, by its place, a last section's lift may come at: the last section, its repeat, or the one before an outro.</param>
    /// <param name="genre">The genre whose lifts and sections' own keys the song takes, where it names them (<see cref="GenreChoices.KeyChanges" />).</param>
    internal static ImmutableArray<KeyChange> Generate(
        IGenerationContext context,
        Func<int, IGenerationContext> places,
        Func<int, IGenerationContext> sections,
        double unconventionality,
        SongMap map,
        IReadOnlyList<int> sectionIds,
        IReadOnlyDictionary<int, SectionRole> roles,
        int liftAt,
        Genre genre
    )
    {
        var (lastSection, ownKey, elsewhere) = (genre.Chance(GenreChoices.KeyChanges, "lastSection"), genre.Chance(GenreChoices.KeyChanges, "sections"),
            genre.Chance(GenreChoices.KeyChanges, "patterns"));
        // the section the lift may come at having played before
        var cameBack = map.Sections.Take(liftAt).Any(x => x.SectionId == map.Sections[liftAt].SectionId);
        var ownKeys = sectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var drawing = sections(id);
            var hasOwn = drawing.TestProbability(SectionContrast.Of(roles[id]).Chance(SectionChance.At(unconventionality, ownKey), 1));
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
            var chance = isLastSection && cameBack ? LastSectionChance.At(unconventionality, lastSection) : ElsewhereChance.At(unconventionality, elsewhere);
            var isChanging = drawing.TestProbability(chance);
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
    internal static (ImmutableArray<KeyChange> Keys, StateTimelineMap Roots, ImmutableArray<(double Change, ImmutableArray<double> Chords, Device Device)> Chains) Prepare(
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
        // a chord the change puts in a key of its own plays the section's scale there, set deeper than its chord's own
        // scale (ChordScales), which it wins over, as far as the next chord, where the section's shows again
        var scales = new List<TimelineItem<LayerValue<StepAlterations>>>();
        // and unmoved by semitones, as a chromatic passing chord was
        var shifts = new List<TimelineItem<int>>();
        void Plain(double from, double to)
        {
            scales.Add(KeysOwnScale.ToTimelineItem(from));
            scales.Add(LayerValue<StepAlterations>.Unset.ToTimelineItem(to));
            shifts.Add((-common.GetEffectiveStateMapAt(from).GetStateValue(StateKinds.ChordShift)).ToTimelineItem(from));
            shifts.Add(0.ToTimelineItem(to));
        }
        var chains = ImmutableArray.CreateBuilder<(double, ImmutableArray<double>, Device)>();
        int Root(double position) => common.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot);
        // the state where a chord the change moves plays, in the section's scale, as it will (Plain)
        StateMap Moved(double position) => common.GetEffectiveStateMapAt(position).With(StateKinds.ScaleAlterations, KeysOwnScale).With(StateKinds.ChordShift, 0);
        int RootPitch(StateMap state, int key, int step) => (key + Realizer.GetChord(state.With(StateKinds.ChordRoot, step)).Chord.GetPitch(0)).Mod(12);
        // a triad's pitch classes on a step of the scale there, in a key, and the scale's
        int[] Triad(double position, int key, int step) => [..new[] { 0, 2, 4 }.Select(x => (key + Realizer.GetChord(Moved(position).With(StateKinds.ChordRoot, step)).Chord.GetPitch(x)).Mod(12))];
        HashSet<int> Scale(double position, int key) => [..Enumerable.Range(0, Scales.StepCount).Select(x => RootPitch(Moved(position), key, x))];
        for (var i = 0; i < changes.Length; i++)
        {
            var context = streams(i);
            var prepares = context.TestProbability(PrepareChance);
            var length = context.Pick(ByConvention.Weigh(ChainLengths, unconventionality));
            var (keyFromNext, rootStep) = context.Pick(ByConvention.Weigh(Links, unconventionality));
            var device = context.Pick(ByConvention.Weigh(Devices, unconventionality));
            var pivotDraw = context.GenerateDouble();
            var (change, target) = (changes[i].Position, changes[i].Semitones);
            // the chords of the pattern before, after any change before it
            var since = Math.Max(i > 0 ? changes[i - 1].Position : 0, change - map.MeterAt(Math.Max(0, change - 1e-6)).PatternDuration);
            var chords = chordStarts.Where(x => x > since + 1e-9 && x < change - 1e-9).TakeLast(length).ToArray();
            if (!prepares || chords.Length == 0)
                continue;

            // a pivot: the last chord before the change on a step of the new key whose triad the old key has too, as
            // likely as its step is, a chain where none is
            var before = i > 0 ? changes[i - 1].Semitones : 0;
            var old = Scale(chords[^1], before);
            // its triad as it sounds where it plays, the scale and any raised step there, in the new key
            ImmutableArray<Weighted<int>> pivots = [..PivotSteps.Where(x => Triad(chords[^1], target, x.Value).All(old.Contains))];
            // a sequence: the section's last pattern, its phrase again, halfway to the new key, where the section plays its
            // phrase twice and the change leaves a step between
            var pattern = map.MeterAt(change - 1e-6).PatternDuration;
            var section = map.Sections.Last(x => x.Start < change - 1e-9);
            // the way to the new key round the octave, as a whole step up from 5 to -5 is
            var way = (target - before + 6).Mod(12) - 6;
            var halfway = (before + (int)Math.Round(way / 2.0, MidpointRounding.AwayFromZero) + 5).Mod(12) - 5;
            if (device == Device.Sequence && Math.Abs(way) >= 2 && section.Start <= change - 2 * pattern + 1e-9 && (i == 0 || changes[i - 1].Position < change - pattern - 1e-9))
            {
                keys.Add(new KeyChange(change - pattern, halfway));
                chains.Add((change, [change - pattern], Device.Sequence));
                continue;
            }

            // a pedal: the old key's chords as they are, the bass holding the new key's dominant over their last bar
            if (device == Device.Pedal)
            {
                chains.Add((change, [Math.Max(since, change - map.MeterAt(change - 1e-6).BarDuration)], Device.Pedal));
                continue;
            }

            if (device == Device.Pivot && !pivots.IsEmpty)
            {
                // the step the draw falls on among the pivots' weights, drawn whatever the device, so that the draws stay as they are
                var (at, pivot) = (pivotDraw * pivots.Sum(x => x.Weight), pivots[^1].Value);
                foreach (var option in pivots)
                    if ((at -= option.Weight) < 0)
                    {
                        pivot = option.Value;
                        break;
                    }

                keys.Add(new KeyChange(chords[^1], target));
                roots.Add((pivot - Root(chords[^1])).ToTimelineItem(chords[^1]));
                roots.Add(0.ToTimelineItem(change));
                Plain(chords[^1], change);
                chains.Add((change, [chords[^1]], Device.Pivot));
                continue;
            }

            // from the new key's first chord back, every chord the one leading to the one after it
            // the new key's first chord as it plays, in its own scale
            var next = RootPitch(common.GetEffectiveStateMapAt(change), target, Root(change));
            for (var j = chords.Length - 1; j >= 0; j--)
            {
                var key = (next + keyFromNext + 5).Mod(12) - 5;
                keys.Add(new KeyChange(chords[j], key));
                roots.Add((rootStep - Root(chords[j])).ToTimelineItem(chords[j]));
                roots.Add(0.ToTimelineItem(j + 1 < chords.Length ? chords[j + 1] : change));
                Plain(chords[j], j + 1 < chords.Length ? chords[j + 1] : change);
                next = RootPitch(Moved(chords[j]), key, rootStep);
            }

            chains.Add((change, [..chords], Device.Chain));
        }

        var duration = common.Duration;
        var moved = roots.Count == 0
            ? StateTimelineMap.Create(duration)
            : StateTimelineMap.Create(duration,
            [
                StateTimeline.Create(duration, StateKinds.ChordRoot, [..roots.GroupBy(x => x.Position).Select(x => x.First()).OrderBy(x => x.Position)]).WithLayer("Key change"),
                ..scales.Count > 0
                    ? new IStateTimeline[]
                    {
                        StateTimeline.Create(duration, StateKinds.ScaleAlterations, [..scales.GroupBy(x => x.Position).Select(x => x.First()).OrderBy(x => x.Position)]).WithLayer("Key change"),
                        StateTimeline.Create(duration, StateKinds.ChordShift, [..shifts.GroupBy(x => x.Position).Select(x => x.First()).OrderBy(x => x.Position)]).WithLayer("Key change")
                    }
                    : []
            ]);
        return ([..keys.OrderBy(x => x.Position)], moved, chains.ToImmutable());
    }

    /// <summary>The section's own scale, at the depth of a chord the change puts in a key of its own, deeper than its chord's.</summary>
    private static LayerValue<StepAlterations> KeysOwnScale { get; } = new(StateDepths.BarPattern, StepAlterations.None);

    /// <summary>
    ///     The song with the bass holding every pedal's note, the new key's dominant, in the octave nearest each of its notes
    ///     there, its rhythm as it was; made once the bass's line and walks are placed.
    /// </summary>
    /// <param name="pedals">Every pedal: where it starts, and the change it leads to.</param>
    internal static TrackEventStateTimelineMap<StateMap> Pedal(TrackEventStateTimelineMap<StateMap> song, int bassTrack, PitchInstrumentTrack definition, IEnumerable<(double From, double Change)> pedals)
    {
        if (!song.TrackTimelineMap.TryGetValue(bassTrack, out var track))
            return song;

        var common = song.CommonStateTimelineMap;
        ChordContext Chord(StateMap note, double position) => Realizer.GetChord(StateMap.Aggregate([
            note,
            track.GetEffectiveStateMapAt(position),
            definition.StateMap.OfScope(StateScope.Render),
            common.GetEffectiveStateMapAt(position)
        ])).Chord;
        var events = track.EventTimeline.ToArray();
        foreach (var (from, change) in pedals)
        {
            // the new key's dominant, as the chord on its fifth step there has it
            var dominant = Realizer.GetChord(common.GetEffectiveStateMapAt(change).With(StateKinds.ChordRoot, 4)).Chord.GetPitch(0).Mod(12);
            for (var i = 0; i < events.Length; i++)
            {
                var (position, note) = (events[i].Position, events[i].Value);
                if (position < from - 1e-9 || position >= change - 1e-9)
                    continue;

                var chord = Chord(note, position);
                var pitch = chord.GetPitch(note.GetStateValue(StateKinds.ScaleStep)) + note.GetStateValue(StateKinds.Alteration);
                var held = pitch + ((dominant - pitch).Mod(12) is var up && up > 6 ? up - 12 : up);
                var step = LinePlacement.GetScaleStep(chord, held);
                events[i] = note.With(StateKinds.ScaleStep, step).With(StateKinds.Alteration, held - chord.GetPitch(step)).With(CompositionStateKinds.BassPedal, 1).ToTimelineItem(position);
            }
        }

        return song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [bassTrack] = _ => EventTimeline.Create(track.EventTimeline.Duration, events) });
    }

    /// <summary>The key's step from the song's own, from each change on; none for a song that keeps its key.</summary>
    internal static StateTimelineMap ToStateTimelineMap(ImmutableArray<KeyChange> changes, double duration)
    {
        return changes.IsEmpty
            ? StateTimelineMap.Create(duration)
            : StateTimelineMap.Create(duration, [StateTimeline.Create(duration, StateKinds.KeyOffset, [..changes.Select(x => x.Semitones.ToTimelineItem(x.Position))])]);
    }
}
