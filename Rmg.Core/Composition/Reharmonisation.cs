using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How a reharmonised chord is substituted (<see cref="Reharmonisation.Kinds" />).</summary>
public enum ReharmonisationKind
{
    /// <summary>A chord a third away in the section's scale, which shares two of its three notes with the chord it replaces, vi for I.</summary>
    Third,

    /// <summary>The next chord's dominant, a fifth above it in Mixolydian on its root, a secondary dominant leading into it.</summary>
    Applied,

    /// <summary>A major chord turned minor, Dorian on its root, borrowed from the parallel minor, the iv for IV at a cadence.</summary>
    Borrowed
}

/// <summary>
///     A later appearance reharmonised: the song's last appearance of a section heard before, a last chorus most often, now
///     and then playing some of its pattern's chords as substitutes, the same at every pass, a fade's passes of it straight
///     after it among them, the likelier at a phrase's end, where an arranger changes them: a chord a third away in the
///     section's scale, the songs' own, or a genre's secondary dominant leading into the next chord or a major chord
///     borrowed minor; but a section on one chord, a vamp; planned before the lines are placed, and decided under the melody
///     once it is, as it would be over the chords it replaces, a substitute kept only where the tune fits it, as an arranger
///     keeps the tune and changes the chords around it.
/// </summary>
/// <param name="Appearance">The appearance reharmonised, by its place.</param>
/// <param name="Substitutes">
///     Each chord that may take a substitute: where it starts, where the next change is, the substitute's root from the
///     chord's, in scale steps, and the steps of its scale raised or lowered, none where it is the section's.
/// </param>
internal sealed record ReharmonisationPlan(int Appearance, ImmutableArray<(double At, double Next, int Step, StepAlterations? Scale)> Substitutes);

/// <inheritdoc cref="ReharmonisationPlan" />
internal static class Reharmonisation
{
    /// <summary>The chance a song reharmonises its last repeated appearance, by the progression facet: never at the plain end, and as often at the wild as in the middle.</summary>
    public static ByConvention Chance { get; } = new(0, 0.25, 0.25);

    /// <summary>How many of the appearance's chords take a substitute, a chance each: a chord in a phrase's last bar, where an arranger reharmonises, the likelier.</summary>
    public const double Share = 0.3, PhraseEndShare = 0.8;

    /// <summary>How a chord is substituted, and how likely, a genre's pool of them: a third away, the songs' own, a secondary dominant or a chord borrowed minor, a genre's.</summary>
    public static ImmutableArray<(ReharmonisationKind Kind, ByConvention Weight)> Kinds { get; } =
        [(ReharmonisationKind.Third, new(1, 1, 1)), (ReharmonisationKind.Applied, new(0, 0, 0)), (ReharmonisationKind.Borrowed, new(0, 0, 0))];

    /// <summary>A substitute's root from the chord's, in scale steps, and how likely: a third below, as vi for I and ii for IV, the likelier, and a third above, as iii for I.</summary>
    public static ImmutableArray<Weighted<int>> Substitutes { get; } = [new(1, -2), new(0.5, 2)];

    /// <summary>
    ///     The substitutes the appearance may take, never a chord moved by semitones; none where the song does not
    ///     reharmonise or has no section heard again, the substitutes decided under the melody once it is placed
    ///     (<see cref="Apply" />).
    /// </summary>
    /// <param name="moved">Where a key change moves a chord or leads into one, which keeps it.</param>
    /// <param name="last">The last appearance played in full, by its place: the last section, or its drawn repeat, not the passes a fade or a strip adds after it.</param>
    /// <param name="kinds">The sequence each chord's kind of substitute is drawn from, apart from the rest, so that a genre's leaves the songs' own as they were.</param>
    public static ReharmonisationPlan? Plan(IGenerationContext context, IGenerationContext kinds, double unconventionality, Genre genre, SongMap map, StateTimelineMap common, IEnumerable<double> moved, int last)
    {
        var reharmonises = context.TestProbability(Chance.At(unconventionality, genre.Chance(GenreChoices.HarmonyDevices, "reharmonised")));
        var sections = map.Sections;
        var later = Enumerable.Range(0, last + 1).LastOrDefault(i => map.TimesBefore(i) > 0, -1);
        var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        // and a chord moved by semitones or in a scale of its own, an applied dominant's, a borrowed chord's or the cadence's
        // raised fifth, whose colour a substitute would lose, kept, and the chord after it, which it slides or resolves into
        var leading = moved.ToHashSet();
        bool Own(double at) => common.GetEffectiveStateMapAt(at) is var state
                               && (state.GetStateValue(StateKinds.ChordShift) != 0 || state.GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } own && own.Value != StepAlterations.None);
        var kept = leading.Concat(changes.Where((x, i) => Own(x) || i > 0 && Own(changes[i - 1]))).ToHashSet();
        var substituted = new List<(double At, double Next, int Step, StepAlterations? Scale)>();
        var weights = ByConvention.Weigh(Kinds, unconventionality, genre.Pool<ReharmonisationKind>(GenreChoices.ReharmonisedChords));
        // a section on one chord, a vamp, which a substitute would undo, left as it is, but the chords leading into a key change
        bool OneChord(SectionSpan first) => changes.Where(x => first.IsWithin(x) && !leading.Contains(x)).Select(x => common.GetEffectiveStateMapAt(x).GetStateValue(StateKinds.ChordRoot)).Distinct().Count() <= 1;
        if (later >= 0 && OneChord(sections.First(x => x.SectionId == sections[later].SectionId)))
            kept.UnionWith(changes);
        if (later >= 0)
        {
            // the appearance and the passes of its section straight after it, a fade's, which play its changes on
            var passes = sections.Skip(later).TakeWhile(x => x.SectionId == sections[later].SectionId).ToArray();
            // a substitute drawn once for each chord of the section's pattern, by its place in it, and played at every pass,
            // as a band keeps the changes it learned each time round
            var drawn = new Dictionary<double, (int Step, ReharmonisationKind Kind)?>();
            foreach (var span in passes)
            {
                var inSpan = changes.Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9).ToArray();
                for (var i = 0; i < inSpan.Length; i++)
                {
                    var place = Math.Round((inSpan[i] - span.Start) % span.Meter.PatternDuration, 6);
                    if (!drawn.TryGetValue(place, out var draw))
                    {
                        // drawn whether the song reharmonises or not, so that its draws are as many, a chord in a phrase's last
                        // bar the likelier
                        var atPhraseEnd = place % span.Meter.PhraseDuration >= span.Meter.PhraseDuration - span.Meter.BarDuration - 1e-9;
                        var substitutes = context.TestProbability(atPhraseEnd ? PhraseEndShare : Share);
                        var drawnStep = context.Pick(Substitutes);
                        var kind = kinds.Pick(weights);
                        drawn[place] = draw = substitutes ? (drawnStep, kind) : null;
                    }

                    var next = i + 1 < inSpan.Length ? inSpan[i + 1] : span.End;
                    if (!reharmonises || draw is not var (step, kindOf) || kept.Contains(inSpan[i]) || Substitute(inSpan[i], next, step, kindOf) is not var (moves, scale))
                        continue;
                    substituted.Add((inSpan[i], next, moves, scale));
                }
            }
        }

        return substituted.Count == 0 ? null : new ReharmonisationPlan(later, [..substituted]);

        // a chord's substitute of a kind, its root's move and its scale's steps; none where the kind fits it not: a dominant
        // into a chord with no fifth to resolve to, or that is already its dominant, a borrowed minor of a chord not major
        (int Step, StepAlterations? Scale)? Substitute(double at, double next, int third, ReharmonisationKind kind)
        {
            var state = common.GetEffectiveStateMapAt(at);
            var (offsets, root) = (state.GetStateValue(StateKinds.ScaleOffsets), state.GetStateValue(StateKinds.ChordRoot));
            if (offsets.IsEmpty)
                return null;
            // a step's interval over the chord's root, in semitones, in the section's scale
            int Above(int from, int steps) => (offsets[(from + steps).Mod(offsets.Length)] - offsets[from.Mod(offsets.Length)]).Mod(12);
            switch (kind)
            {
                case ReharmonisationKind.Applied:
                {
                    var target = common.GetEffectiveStateMapAt(next).GetStateValue(StateKinds.ChordRoot);
                    var step = (target + 4 - root).Mod(Scales.StepCount) is var up && up > 3 ? up - Scales.StepCount : up;
                    if (step == 0 || next >= common.Duration - 1e-9 || Above(target, 4) == 6)
                        return null;
                    return (step, ChordScales.Toward(offsets, root + step, Scales.Mixolydian.Offsets, ChordScaleKind.Applied)?.Steps);
                }
                case ReharmonisationKind.Borrowed:
                {
                    return Above(root, 2) == 4 && ChordScales.Toward(offsets, root, Scales.Dorian.Offsets, ChordScaleKind.Borrowed) is { } minor ? (0, minor.Steps) : null;
                }
                default:
                    return (third, null);
            }
        }
    }

    /// <summary>
    ///     The plan's substitutes under the melody as placed, as the song's state: each kept where every note of the melody
    ///     over it on a strong beat fits it, one of its notes or none of them a semitone from it, a ninth, an eleventh or a
    ///     sus note, and where no inner line moves inside the chord, which keeps its own; a root moved from each kept chord to
    ///     the next change, and the melody's notes over it kept at the pitches they sing, their steps counted from the new
    ///     chord's root. The song as it was where none is kept or none planned.
    /// </summary>
    /// <param name="melody">The melody's track, placed.</param>
    /// <param name="profile">The melody's line, whose strong beats a substitute must fit.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(TrackEventStateTimelineMap<StateMap> song, ReharmonisationPlan? plan, SongMap map, KeyValuePair<int, IInstrumentTrack> melody, LineProfile profile)
    {
        var common = song.CommonStateTimelineMap;
        var track = song.TrackTimelineMap[melody.Key];
        var placed = LinePlacement.GetNotes(track, melody.Value, common);
        var pitches = placed.Select(x => LinePlacement.PitchOf(x.Chord, x.Note.Value)).ToArray();
        (double At, double Next, int Step, StepAlterations? Scale)[] kept = [];
        if (plan is not null)
        {
            // the melody's notes over every substitute at once, each substitute judged by its own chord's
            var under = LinePlacement.GetNotes(track, melody.Value, song.MergeStateTimelineMap(RootsOf(plan.Substitutes, common.Duration)).CommonStateTimelineMap);
            var lines = common.GetStateTimeline(CompositionStateKinds.InnerLine).Where(x => x.Value != 0).Select(x => x.Position).ToArray();
            bool IsStrong(LineNote note) => (note.State.GetStateValue(StateKinds.HeldDuration) > 0 ? 0 : note.State.GetStateValue(CompositionStateKinds.BeatRank)) <= profile.StrongestWeakRank;
            bool Fits(int i) => under[i].Classes.Count == 0 || under[i].Classes.Contains(pitches[i].Mod(12)) || !under[i].Classes.Any(x => (x - pitches[i]).Mod(12) is 1 or 11);
            kept =
            [
                ..plan.Substitutes.Where(x => !lines.Any(at => at >= x.At - 1e-9 && at < x.Next - 1e-9)
                                              && Enumerable.Range(0, placed.Length).All(i => placed[i].Position < x.At - 1e-9 || placed[i].Position >= x.Next - 1e-9 || !IsStrong(placed[i]) || Fits(i)))
            ];
        }

        StateTrace.RecordSection(TracePoints.Reharmonisation, SectionGenerator.SectionTrace, plan is null ? 0 : map.Sections[plan.Appearance].SectionId,
            $"{kept.Length} chords", kept.Length == 0 ? null : (plan!.Appearance, kept.Select(x => x.At).ToImmutableArray()));
        if (kept.Length == 0)
            return song;
        var reharmonised = song.MergeStateTimelineMap(RootsOf([..kept], common.Duration));
        var over = LinePlacement.GetNotes(track, melody.Value, reharmonised.CommonStateTimelineMap);
        bool IsOver(double position) => kept.Any(x => position >= x.At - 1e-9 && position < x.Next - 1e-9);
        var notes = placed.Select((x, i) =>
        {
            if (!IsOver(x.Position))
                return x.Note;
            var step = LinePlacement.GetScaleStep(over[i].Chord, pitches[i]);
            return x.Note.Value.With(StateKinds.ScaleStep, step).With(StateKinds.Alteration, pitches[i] - over[i].Chord.GetPitch(step)).ToTimelineItem(x.Position);
        });
        return reharmonised.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>> { [melody.Key] = x => EventTimeline.Create(x.Duration, notes) });
    }

    /// <summary>The substitutes as the song's state: a root moved from each chord to the next change, and a substitute's own scale over it.</summary>
    private static StateTimelineMap RootsOf(ImmutableArray<(double At, double Next, int Step, StepAlterations? Scale)> substitutes, double duration)
    {
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> items) => items.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        IStateTimeline roots = StateTimeline.Create(duration, StateKinds.ChordRoot, Once(substitutes.SelectMany(x => new[] { x.Step.ToTimelineItem(x.At), 0.ToTimelineItem(x.Next) }))).WithLayer("Reharmonisation");
        var scaled = substitutes.Where(x => x.Scale is not null).ToArray();
        return StateTimelineMap.Create(duration, scaled.Length == 0 ? [roots] :
        [
            roots,
            StateTimeline.Create(duration, StateKinds.ScaleAlterations, Once(scaled.SelectMany(x => new[]
                { new LayerValue<StepAlterations>(StateDepths.Note, x.Scale!.Value).ToTimelineItem(x.At), LayerValue<StepAlterations>.Unset.ToTimelineItem(x.Next) }))).WithLayer("Reharmonisation")
        ]);
    }
}
