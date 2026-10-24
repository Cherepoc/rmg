using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A later appearance reharmonised: the song's last appearance of a section heard before, a last chorus most often, now
///     and then playing some of its pattern's chords as substitutes a third away, the same at every pass, which share two of their three notes with the
///     chords they replace, in the section's own scale, but a section on one chord, a vamp; planned before the lines are
///     placed, and decided under the melody once it is, which sings its tune as heard before, a substitute kept only where
///     the tune fits it, as an arranger keeps the tune and changes the chords around it.
/// </summary>
/// <param name="Appearance">The appearance reharmonised, by its place.</param>
/// <param name="Substitutes">Each chord that may take a substitute: where it starts, where the next change is, and the substitute's root from the chord's, in scale steps.</param>
internal sealed record ReharmonisationPlan(int Appearance, ImmutableArray<(double At, double Next, int Step)> Substitutes);

/// <inheritdoc cref="ReharmonisationPlan" />
internal static class Reharmonisation
{
    /// <summary>The chance a song reharmonises its last repeated appearance, by the progression facet: never at the plain end, and as often at the wild as in the middle.</summary>
    public static ByConvention Chance { get; } = new(0, 0.25, 0.25);

    /// <summary>How many of the appearance's chords take a substitute, a chance each.</summary>
    public const double Share = 0.5;

    /// <summary>A substitute's root from the chord's, in scale steps, and how likely: a third below, as vi for I and ii for IV, the likelier, and a third above, as iii for I.</summary>
    public static ImmutableArray<Weighted<int>> Substitutes { get; } = [new(1, -2), new(0.5, 2)];

    /// <summary>
    ///     The substitutes the appearance may take, never a chord moved by semitones; none where the song does not
    ///     reharmonise or has no section heard again, the appearance's melody then singing its tune as heard before
    ///     (<see cref="LinePattern.Place" />) and the substitutes decided under it (<see cref="Apply" />).
    /// </summary>
    /// <param name="moved">Where a key change moves a chord or leads into one, which keeps it.</param>
    /// <param name="last">The last appearance played in full, by its place: the last section, or its drawn repeat, not the passes a fade or a strip adds after it.</param>
    public static ReharmonisationPlan? Plan(IGenerationContext context, double unconventionality, Genre genre, SongMap map, StateTimelineMap common, IEnumerable<double> moved, int last)
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
        var substituted = new List<(double At, double Next, int Step)>();
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
                substituted.Add((inSpan[i], i + 1 < inSpan.Length ? inSpan[i + 1] : span.End, step.Value));
            }
        }

        return substituted.Count == 0 ? null : new ReharmonisationPlan(later, [..substituted]);
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
        (double At, double Next, int Step)[] kept = [];
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

    /// <summary>The substitutes as the song's state: a root moved from each chord to the next change.</summary>
    private static StateTimelineMap RootsOf(ImmutableArray<(double At, double Next, int Step)> substitutes, double duration)
    {
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> items) => items.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        return StateTimelineMap.Create(duration,
        [
            StateTimeline.Create(duration, StateKinds.ChordRoot, Once(substitutes.SelectMany(x => new[] { x.Step.ToTimelineItem(x.At), 0.ToTimelineItem(x.Next) }))).WithLayer("Reharmonisation")
        ]);
    }
}
