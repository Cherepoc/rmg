using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A line doubled in a section now and then: by another part, note for note, the other part's own notes there giving
///     way: the counter-melody a third or a sixth above or below the melody, as a second voice harmonises a tune, and the
///     bass with the riff, in its own register, as a band plays a riff together. A doubled note keeps its step above its
///     chord's root, moved by the interval, so that it stays in the scale over the same chords; a part plays it from its own
///     first note in the section to its last, and a section where either part rests is not doubled; but under a riff on a
///     driven guitar the bass plays every phrase of a section it plays in, the riff's where it would rest. A twin, the riff's,
///     plays the whole line wherever it plays, in every section, a third above or a sixth below, having no notes of its
///     own; or by its own part an octave apart, the melody's, the counter-melody's or the riff's, the likelier the louder
///     the section and a little the wilder the song, a part playing one doubling at a time. The bass with the riff is a
///     convention, always in the plainest songs and never in the wildest, the harmony alike at every end, and the twin's
///     interval leans by the song's melody facet: a third or a sixth in a middling song, and the wilder the song, the oftener a
///     fourth, a fifth, a second or a seventh. A twin harmonises a lead line, up the neck, in the sections it leads
///     (<see cref="TwinLeadChance" />), and where a genre tunes its riff down, the riff and its twin play an octave up where the twin plays, where
///     the songs' riffs do, rather than thirds in the mud.
/// </summary>
internal static class LineDoubling
{
    /// <summary>Where the octaves' sequences are keyed, by their part, past any pair's.</summary>
    private const int OctavePlaces = 1000;

    /// <summary>
    ///     The steps a twin plays apart from the riff: a third above and a sixth below, the plainest, and a fourth above, a
    ///     fifth below, a second above and a seventh below, the wilder.
    /// </summary>
    public static ImmutableArray<(int Step, ByConvention Weight)> TwinSteps { get; } =
    [
        (2, new ByConvention(1, 0.55, 0.2)),
        (-5, new ByConvention(1, 0.35, 0.2)),
        (3, new ByConvention(0, 0.05, 0.2)),
        (-4, new ByConvention(0, 0.05, 0.2)),
        (1, new ByConvention(0, 0, 0.1)),
        (-6, new ByConvention(0, 0, 0.1))
    ];

    /// <summary>The chance the counter-melody harmonises the melody in a section, a third or a sixth apart.</summary>
    public static ByConvention HarmonyChance { get; } = new(0.25, 0.25, 0.25);

    /// <summary>
    ///     The chance a section's harmonised melody plays in a block, three voices, as a horn section plays a soli: the
    ///     counter-melody's note stacking its chord's under it, from a minor third down, where its note is the chord's, and
    ///     alone where not, as a passing note is (<see cref="CompositionStateKinds.LineVoices" />); a genre's alone.
    /// </summary>
    public static ByConvention BlockChance { get; } = new(0, 0, 0);

    /// <summary>The notes a block's counter-melody stacks, its own and its chord's under it: with the melody's, three voices.</summary>
    internal const int BlockVoices = 2;

    /// <summary>
    ///     The chance the bass plays the riff with it in a section, as a band plays a riff together, by the melody facet: a
    ///     convention, every time in a plain band, half the time in a middling one, and never in the wildest, whose bass
    ///     goes its own way.
    /// </summary>
    public static ByConvention RiffChance { get; } = new(1, 0.5, 0);

    /// <summary>
    ///     The chance the counter-melody imitates the melody in a section, its phrase a bar later, an octave below, each note
    ///     on the same step above the chord it falls on as the melody's above its own, as a canon moves with the harmony: a
    ///     genre's alone.
    /// </summary>
    public static ByConvention ImitationChance { get; } = new(0, 0, 0);

    /// <summary>
    ///     Who doubles whom, by how many scale steps, weighed by the song's melody facet, the chance of a section, alike at
    ///     every end, the option of <see cref="GenreChoices.Doubling" /> it is a genre's by, none for a twin, which doubles
    ///     wherever it plays, whether the target is the source's twin, and how many bars later it plays the source's notes.
    /// </summary>
    public static ImmutableArray<(TrackRole Source, TrackRole Target, ImmutableArray<(int Step, ByConvention Weight)> Steps, ByConvention Chance, string? Option, bool IsTwin, int Bars)> Pairs { get; } =
    [
        (TrackRole.Melody, TrackRole.CounterMelody, [(2, Any), (-2, Any), (5, Any), (-5, Any), (3, Never), (-3, Never), (4, Never), (-4, Never)], HarmonyChance, "harmony", false, 0),
        (TrackRole.Riff, TrackRole.Bass, [(0, Any)], RiffChance, "riff", false, 0),
        (TrackRole.Riff, TrackRole.RiffTwin, TwinSteps, Any, null, true, 0),
        (TrackRole.Melody, TrackRole.CounterMelody, [(-7, Any)], ImitationChance, "imitation", false, 1)
    ];


    /// <summary>
    ///     The chance a section of a song with a twin is the twin's, a lead line: the riff in single notes, busier, played as a
    ///     soloist plays, and its twin with it; a section that is not plays no twin. As often in a plain song as a wild one,
    ///     as whether the song has a twin leans already (<see cref="SongParts.TwinChance" />).
    /// </summary>
    public static ByConvention TwinLeadChance { get; } = new(0.5, 0.5, 0.5);

    /// <summary>How much fuller a twin lead's riff is than the song's, a lead line busier than a riff.</summary>
    public const double TwinLeadFullness = 0.3;

    /// <summary>The chance a counter-melody doubling the melody plays it in the melody's sound, twin leads a third apart.</summary>
    public const double TwinTimbreChance = 0.5;

    /// <summary>A step a doubling plays whatever the song's melody facet.</summary>
    private static ByConvention Any => new(1, 1, 1);

    /// <summary>A step the songs' doubling never plays, a genre's alone: the melody harmonised in fourths or fifths, an organum's.</summary>
    private static ByConvention Never => new(0, 0, 0);

    /// <summary>
    ///     The kinds of interval a doubling plays, by how many scale steps apart, as a genre names them
    ///     (<see cref="GenreChoices.HarmonyInterval" />): thirds and sixths, fourths and fifths, and seconds and sevenths, each
    ///     with its inversion.
    /// </summary>
    public static string IntervalOf(int step) => step.Mod(Scales.StepCount) switch
    {
        2 or 5 => "thirds",
        3 or 4 => "fourths",
        1 or 6 => "seconds",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "A unison has no interval to harmonise in.")
    };

    /// <summary>The parts that play their line in octaves now and then.</summary>
    public static ImmutableArray<TrackRole> OctaveRoles { get; } = [TrackRole.Melody, TrackRole.CounterMelody, TrackRole.Riff];

    /// <summary>
    ///     The chance a part plays a section's line in octaves, by the song's sound facet: a conventional doubling, a plain
    ///     song's too, and a little oftener in a wild one.
    /// </summary>
    public static ByConvention OctaveChance { get; } = new(0.07, 0.11, 0.15);

    /// <summary>How far the section's energy leans the octaves, as a doubled line lifts a loud section.</summary>
    public const double OctaveCoupling = 0.4;

    /// <summary>The song with its lines doubled where each section draws a doubling, from a sequence of the pair's own for each section.</summary>
    /// <param name="streams">A pair's sequence for a section, by the pair's place and the section's.</param>
    /// <param name="melody">The song's melody facet, which the steps lean by.</param>
    /// <param name="sound">The song's sound facet, which the octaves lean by.</param>
    /// <param name="energies">Every appearance's energy, by its place, which the octaves lean by too.</param>
    /// <param name="twinLeads">Whether an appearance is one its twin leads in, by its place, the only kind that plays a twin.</param>
    /// <param name="twinRests">Whether an appearance, by its place, leaves the riff's twin out, which then plays none of the riff.</param>
    /// <param name="playsAFigure">Whether a part plays a figure in an appearance, by its place, which no doubling into it replaces (<see cref="Figures" />).</param>
    /// <param name="drivenRiff">Whether the riff plays on a driven guitar in a section, by its place among the song's sections, whose phrases the bass plays wherever it would rest in them.</param>
    /// <param name="bassSilent">Whether an appearance's texture leaves the bass out of a phrase, by the appearance's place and the phrase's, as one part alone does, which no doubling fills.</param>
    /// <param name="blocks">A section's sequence of whether its harmonised melody plays in a block, by the section's place.</param>
    /// <param name="twinRaise">How many octaves the riff and its twin play up where the twin plays, as many as the genre tunes its riff down.</param>
    /// <param name="genre">The genre whose chances of the doublings a section draws (<see cref="GenreChoices.Doubling" />).</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IReadOnlyDictionary<int, TrackRole> roles,
        SongMap map,
        double melody,
        double sound,
        IReadOnlyList<double> energies,
        Func<int, int, IGenerationContext> streams,
        Func<int, IGenerationContext> timbres,
        Func<int, IGenerationContext> blocks,
        Func<int, bool> twinLeads,
        Func<int, bool> twinRests,
        Func<int, TrackRole, bool> playsAFigure,
        Func<int, bool> drivenRiff,
        Func<int, int, bool> bassSilent,
        int twinRaise,
        Genre genre,
        IReadOnlyDictionary<int, IInstrumentTrack> definitions
    )
    {
        var trackOf = roles.TrackOf();
        // the steps of the chords part's shape at a position, from the chord's root, as its note there reads them; none where
        // it strikes no chord since the change
        var changes = song.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        IReadOnlySet<int>? ShapeAt(double position) =>
            trackOf.TryGetValue(TrackRole.Chords, out var chords) && song.TrackTimelineMap.TryGetValue(chords, out var timeline)
            && Realizer.ChordStateAt(timeline, definitions[chords], song.CommonStateTimelineMap, changes, position) is { } state
                ? Realizer.GetChordNotes(state).Steps.Select(x => x.Mod(Scales.StepCount)).ToHashSet()
                : null;

        // a harmony's note on its chord's third where the chord leaves it out, a sus chord's, moved to the chord's note
        // beside it, the fourth or the second, as a second voice keeps to the chord the band plays
        int Harmonised(int scaleStep, double position) =>
            scaleStep.Mod(Scales.StepCount) == 2 && ShapeAt(position) is { } shape && !shape.Contains(2)
                ? scaleStep + (shape.Contains(3) ? 1 : shape.Contains(1) ? -1 : 0)
                : scaleStep;
        // the parts that double or are doubled in an appearance, by its place, which play no other doubling in any stretch of
        // it, the ending's among them
        var doubling = new HashSet<(int Track, int Index)>();
        // every stretch the song plays as a section, an intro's window and the ending among them, drawn as the section it plays
        // as is; an appearance traced once, in the first stretch it doubles in
        var plays = map.Plays.Select(x => (x.Span, x.Index)).ToArray();
        var traced = new HashSet<(string Point, int Track, int Index)>();
        bool Traces(string point, int track, int index) => traced.Add((point, track, index));
        foreach (var ((source, target, steps, chance, option, isTwin, bars), pair) in Pairs.Select((x, i) => (x, i)))
        {
            // the pair's own steps leaned by a genre's intervals over the songs', so that a genre naming the songs' amounts
            // plays as they do, but an imitation's octave, which harmonises nothing, and the bass with the riff in unison,
            // which no interval of a genre's moves
            var intervals = bars == 0 && steps.Any(x => x.Step != 0) ? genre.Lean<int>(GenreChoices.HarmonyInterval, IntervalOf) : null;
            // a genre whose intervals keep none of the pair's never doubles it
            var allowed = intervals is null || steps.Any(x => intervals(x.Step) is not null);
            if (!trackOf.TryGetValue(source, out var sourceTrack) || !trackOf.TryGetValue(target, out var targetTrack))
                continue;

            // a twin plays over the source's own state, its chords and its bars', having none of its own
            if (isTwin)
            {
                var twin = EventStateTimelineMap.Create(song.Duration, EventTimeline.Create<StateMap>(song.Duration), song.TrackTimelineMap[sourceTrack].StateTimelineMap);
                song = TrackEventStateTimelineMap.Create(song.Duration, song.TrackTimelineMap.SetItem(targetTrack, twin), song.CommonStateTimelineMap);
            }

            foreach (var (span, index) in plays)
            {
                var context = streams(pair, index);
                var isDoubled = context.TestProbability(chance.At(melody, option is null ? null : genre.Chance(GenreChoices.Doubling, option)));
                // the pair's draws made all the same, but a pair a genre's intervals all leave out never doubling
                if (!allowed)
                    continue;
                var step = context.Pick(ByConvention.Lean(steps, melody, intervals));
                var events = song.TrackTimelineMap;
                bool Plays(int track) => events[track].EventTimeline.Any(x => x.IsWithin(span.Start, span.End));
                // a twin in the sections it leads alone, but an appearance that leaves it out, a calm section's
                // nor into a part playing a figure there, the bass's rolling or offbeat rhythm, which it keeps as a pedal is kept,
                // nor the bass with a riff the twin leads in, a lead line, under which the bass plays its own
                if (!isDoubled || !Plays(sourceTrack) || !isTwin && !Plays(targetTrack) || isTwin && (!twinLeads(index) || twinRests(index)) || playsAFigure(index, target)
                    || target == TrackRole.Bass && twinLeads(index))
                    continue;

                // a section a pair doubles already, the counter-melody harmonising it, not imitated too
                if (bars > 0 && doubling.Contains((targetTrack, index)))
                    continue;
                if (bars > 0 && Traces(TracePoints.Imitation, targetTrack, index))
                    StateTrace.RecordSection(TracePoints.Imitation, targetTrack, span.SectionId, $"{source} {bars} bar later", (index, source, bars));
                else if (bars == 0 && Traces(TracePoints.LineDoubling, targetTrack, index))
                    StateTrace.RecordSection(TracePoints.LineDoubling, targetTrack, span.SectionId, $"{source} {step}", (index, source, target, step));
                doubling.Add((sourceTrack, index));
                doubling.Add((targetTrack, index));
                // where the doubling part plays in the section, from its first note to its last, so that it joins no earlier
                // than it would, as into an intro that brings it in
                var own = events[isTwin ? sourceTrack : targetTrack].EventTimeline.Where(x => x.IsWithin(span.Start, span.End)).Select(x => x.Position).ToArray();
                var (from, to) = (own.Min(), own.Max());
                // a counter-melody harmonising the melody in the melody's own sound now and then, as twin leads play, and in
                // a block now and then, its notes stacking their chord's
                var inTimbre = source == TrackRole.Melody && !isTwin && bars == 0 && timbres(index).TestProbability(TwinTimbreChance);
                var block = source == TrackRole.Melody && !isTwin && bars == 0 && blocks(index).TestProbability(BlockChance.At(melody, genre.Chance(GenreChoices.Doubling, "block")));
                // an imitation's notes the bars later it plays them
                var delay = bars * span.Meter.BarDuration;
                if (block && Traces(TracePoints.BlockHarmony, targetTrack, index))
                    StateTrace.RecordSection(TracePoints.BlockHarmony, targetTrack, span.SectionId, $"{step}", index);
                // in every phrase the doubled part plays, its own kept where a texture rests the doubled part, phrase by phrase
                var phrases = span.Phrases.Select(p => (p.Start, p.End))
                    .Where(p => events[sourceTrack].EventTimeline.Any(x => x.IsWithin(p.Start, p.End)))
                    .ToArray();
                // but from the doubling part's walk into the next section on, which it plays as its own, as LineFills placed it
                var walk = events[targetTrack].EventTimeline.Where(x => x.IsWithin(span.Start, span.End) && x.Value.GetStateValue(CompositionStateKinds.Walk) > 0)
                    .Select(x => x.Position).DefaultIfEmpty(double.MaxValue).Min();
                // nor its first note where its walk from the section before leads into it, the note the walk was aimed at
                var walkedInto = events[targetTrack].EventTimeline.Where(x => x.Position < span.Start - 1e-9).Select(x => x.Value.GetStateValue(CompositionStateKinds.Walk)).LastOrDefault() > 0;
                bool Doubles(double position) => position >= from - 1e-9 && position <= to + 1e-9 && position < walk - 1e-9 && !(walkedInto && Math.Abs(position - from) < 1e-9)
                                                 && phrases.Any(p => position >= p.Start - 1e-9 && position < p.End - 1e-9);
                // in unison the source's alteration as it is, a chromatic riff's passing note too; a harmony a step away on the
                // scale, off a third its chord leaves out, but a twin's, which keeps its step all the way
                var doubled = events[sourceTrack].EventTimeline
                    .Where(x => Doubles(x.Position + delay))
                    .Select(x => x.Value.With(StateKinds.ScaleStep, step == 0 || isTwin ? x.Value.GetStateValue(StateKinds.ScaleStep) + step : Harmonised(x.Value.GetStateValue(StateKinds.ScaleStep) + step, x.Position + delay))
                        .With(StateKinds.Alteration, step == 0 ? x.Value.GetStateValue(StateKinds.Alteration) : 0)
                        .With(CompositionStateKinds.Doubled, 1)
                        .With(CompositionStateKinds.SoundsAs, inTimbre ? (int)TrackRole.Melody + 1 : 0)
                        .With(CompositionStateKinds.LineRaise, isTwin ? twinRaise : 0).ToTimelineItem(x.Position + delay))
                    .Select(x => block ? x.Value.With(CompositionStateKinds.LineVoices, Math.Max(x.Value.GetStateValue(CompositionStateKinds.LineVoices), BlockVoices)).ToTimelineItem(x.Position) : x);
                if (bars > 0)
                    doubled = Answering(doubled, events[sourceTrack], events[targetTrack], song.CommonStateTimelineMap);
                var edits = new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                {
                    [targetTrack] = notes => EventTimeline.Create(
                        notes.Duration,
                        notes.Where(x => !Doubles(x.Position)).Concat(doubled).OrderBy(x => x.Position)
                    )
                };
                // the riff up with its twin, as they play
                if (isTwin && twinRaise > 0)
                    edits[sourceTrack] = notes => EventTimeline.Create(notes.Duration,
                        notes.Select(x => Doubles(x.Position) ? x.Value.With(CompositionStateKinds.LineRaise, twinRaise).ToTimelineItem(x.Position) : x));
                song = song.MapTrackEvents(edits);
            }
        }

        // and the bass under a driven riff, as a band's plays under its guitars: in a section it plays in, every phrase the
        // riff plays without it it plays with it, in unison, as the pair does; but in the song's opening, which a riff may
        // start alone, in a section it sits out whole, left to the guitars, a phrase its texture leaves it out of, as the
        // riff opening a section alone, and a section the twin leads, a lead line the bass plays its own under
        if (trackOf.TryGetValue(TrackRole.Riff, out var riffTrack) && trackOf.TryGetValue(TrackRole.Bass, out var bassTrack))
            foreach (var (span, index) in plays.Where(x => x.Index > 0 && drivenRiff(x.Index) && !twinLeads(x.Index)))
            {
                var events = song.TrackTimelineMap;
                TimelineItem<StateMap>[] In(int track, double start, double end) => [..events[track].EventTimeline.Where(x => x.IsWithin(start, end))];
                if (In(bassTrack, span.Start, span.End).Length == 0)
                    continue;

                var filled = new List<TimelineItem<StateMap>>();
                foreach (var (_, start, end) in span.Patterns)
                {
                    if (In(bassTrack, start, end).Length > 0 || In(riffTrack, start, end) is not { Length: > 0 } riff)
                        continue;

                    filled.AddRange(riff.Where(x => !bassSilent(index, span.PhraseOf(x.Position))).Select(x => x.Value.With(CompositionStateKinds.SoundsAs, 0).With(CompositionStateKinds.Doubled, 1)
                        // in its own register, never the twin's raise the riff took
                        .With(CompositionStateKinds.LineRaise, 0).ToTimelineItem(x.Position)));
                }

                if (filled.Count > 0)
                    song = song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                    {
                        [bassTrack] = notes => EventTimeline.Create(notes.Duration, notes.Concat(filled).OrderBy(x => x.Position))
                    });
            }

        // and a line in octaves by its own part, each part and section from a sequence of its own, by the part, apart from
        // the pairs'
        foreach (var (role, place) in OctaveRoles.Select(x => (x, OctavePlaces + (int)x)))
        {
            if (!trackOf.TryGetValue(role, out var track))
                continue;

            foreach (var (span, index) in plays)
            {
                var context = streams(place, index);
                var inOctaves = context.TestProbability(SectionEnergy.Tilt(energies[index], OctaveCoupling).Chance(OctaveChance.At(sound, genre.Chance(GenreChoices.Doubling, "octaves")), 1));
                bool Plays() => song.TrackTimelineMap[track].EventTimeline.Any(x => x.IsWithin(span.Start, span.End));
                if (doubling.Contains((track, index)) || !Plays())
                    continue;

                if (Traces(TracePoints.Octave, track, index))
                    StateTrace.RecordSection(TracePoints.Octave, track, span.SectionId, $"{role} {inOctaves} {energies[index]:F2}", (index, role, energies[index], inOctaves));
                if (!inOctaves)
                    continue;

                song = song.MapTrackEvents(new Dictionary<int, Func<EventTimeline<StateMap>, EventTimeline<StateMap>>>
                {
                    [track] = notes => EventTimeline.Create(
                        notes.Duration,
                        notes.Select(x => x.IsWithin(span.Start, span.End)
                            ? x.Value.With(CompositionStateKinds.Octave, 1).ToTimelineItem(x.Position)
                            : x)
                    )
                });
            }
        }

        return song;
    }

    /// <summary>
    ///     An imitation's notes over the line they imitate as it plays on: a note a second or a seventh from the leader's
    ///     note there, as the leader's passing note heard a bar later over another chord rubs, moved to the nearest of its
    ///     chord's notes that is neither, and left out where none is, as a canon's answer bends to its leader.
    /// </summary>
    private static IEnumerable<TimelineItem<StateMap>> Answering(IEnumerable<TimelineItem<StateMap>> notes, EventStateTimelineMap<StateMap> leader,
        EventStateTimelineMap<StateMap> follower, StateTimelineMap common)
    {
        var leading = leader.EventTimeline.ToArray();
        StateMap Heard(StateMap note, EventStateTimelineMap<StateMap> track, double position) =>
            StateMap.Aggregate([note, track.GetEffectiveStateMapAt(position), common.GetEffectiveStateMapAt(position)]);
        static bool Rubs(int step, int against) => (step - against).Mod(Scales.StepCount) is 1 or 6;
        foreach (var note in notes)
        {
            var at = Array.FindLastIndex(leading, x => x.Position <= note.Position + 1e-9);
            var heard = Heard(note.Value, follower, note.Position);
            var own = heard.GetStateValue(StateKinds.ScaleStep);
            var against = at < 0 ? (int?)null : Heard(leading[at].Value, leader, note.Position).GetStateValue(StateKinds.ScaleStep);
            if (against is not { } other || !Rubs(own, other))
            {
                yield return note;
                continue;
            }

            var chord = Realizer.GetChordNotes(heard).Steps.Select(x => x.Mod(Scales.StepCount)).ToHashSet();
            var moves = new[] { -1, 1, -2, 2 }.Where(x => chord.Contains((own + x).Mod(Scales.StepCount)) && !Rubs(own + x, other)).ToArray();
            if (moves.Length > 0)
                yield return note.Value.With(StateKinds.ScaleStep, note.Value.GetStateValue(StateKinds.ScaleStep) + moves[0]).ToTimelineItem(note.Position);
        }
    }
}
