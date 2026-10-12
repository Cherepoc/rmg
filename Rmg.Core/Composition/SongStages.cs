using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The song as the stages make it after it is put together: its state and events, then its notes, and what a stage
///     hands on to another.
/// </summary>
/// <param name="Notes">The notes, once realized (<see cref="SongStages.RealizeNotes" />); none before.</param>
/// <param name="Runs">The drums' runs, which the fills play, the lines walk with and the stages that play around a fill keep out of (<see cref="FillSpans" />).</param>
/// <param name="Chains">How each key change is led into, which the bass's pedal holds under and the reharmonisation and the inner lines keep the chords of (<see cref="SongStages.KeyChanges" />).</param>
/// <param name="Figures">Every section's figures, by its id and the player, which a mutating groove leaves as they are and a doubling does not replace (<see cref="Composition.Figures" />).</param>
/// <param name="Polymeters">The sections a figure runs across the bar lines in, which the figures, the pushes and a mutating groove keep to (<see cref="SongStages.Polymeter" />).</param>
internal sealed record SongDraft(
    TrackEventStateTimelineMap<StateMap> Timeline,
    ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? Notes,
    IReadOnlyList<PlayedRun> Runs,
    ImmutableArray<(double Change, ImmutableArray<double> Chords, KeyChange.Device Device)> Chains,
    ImmutableDictionary<int, ImmutableDictionary<FigurePlayer, string>> Figures,
    ImmutableHashSet<int> Polymeters
)
{
    /// <summary>
    ///     Where the song plays in stop time as stop time played it, its phrases that a part carries, none before it or where
    ///     it is left out: the stages after it keep to these, the stages before it to the phrases drawn (<see cref="SongDecisions.StopTime" />).
    /// </summary>
    public ImmutableArray<(double Start, double End, double BarDuration)> Stopped { get; init; } = [];

    /// <summary>A calling verse's halves, the melody's call and a part's answer, which the stages after keep out of (<see cref="Composition.CallAndResponse" />).</summary>
    public ImmutableArray<HeldSpan> Calls { get; init; } = [];

    /// <summary>Where the drums fill, from a run's first note to its line, which the stages that play around a fill keep out of.</summary>
    public (double From, double To)[] FillSpans => [..Runs.Where(x => !x.Positions.IsEmpty).Select(x => (x.Positions.Min(), x.Line))];

    /// <summary>
    ///     What the song holds, which a stage keeps out of by the kinds it names (<see cref="Hold" />): the drums' fills, its
    ///     stop time as played, its drum solos and trades, and where the form stops a part (<see cref="TimelineEdits.StopsOf" />).
    /// </summary>
    public Holds Holds(SongDecisions song) => new(
    [
        ..FillSpans.Select(x => new HeldSpan(Hold.Fill, x.From, x.To, null)),
        ..Stopped.Select(x => new HeldSpan(Hold.Stop, x.Start, x.End, null)),
        ..song.Solos.Where(x => x.Value.IsDrumSolo || x.Value.TradesWithDrums).Select(x => song.Form.Map.Sections[x.Key])
            .Select(x => new HeldSpan(Hold.DrumSolo, x.Start, x.End, null)),
        ..song.Tracks.Definitions.Keys.SelectMany(track => song.Form.Edits.StopsOf(track).Select(x => new HeldSpan(Hold.Cut, x.From, x.To, track))),
        ..Calls
    ]);
}

/// <summary>What a stretch of the song is held by, which a stage keeps out of by the kinds it names.</summary>
[Flags]
internal enum Hold
{
    /// <summary>The drums' fill, from its first note to its line.</summary>
    Fill = 1,

    /// <summary>Stop time, as it played.</summary>
    Stop = 2,

    /// <summary>A drum solo, or a solo the drums trade.</summary>
    DrumSolo = 4,

    /// <summary>A part stopped by the form: a stop ending, a stripped ending's exits, a silent drop.</summary>
    Cut = 8,

    /// <summary>A calling verse's first half, the melody's call.</summary>
    Call = 16,

    /// <summary>A calling verse's second half, a part's answer.</summary>
    Answer = 32
}

/// <summary>A stretch the song holds, of a kind, a part's own or the whole band's.</summary>
/// <param name="Track">The track it holds; none for every track.</param>
internal readonly record struct HeldSpan(Hold Kind, double Start, double End, int? Track);

/// <summary>Every stretch the song holds, asked by the kinds a stage keeps out of.</summary>
internal sealed class Holds(ImmutableArray<HeldSpan> spans)
{
    /// <summary>Whether a stretch of a kind given meets [from, to): the band's, and a track's own where one is asked of, any part's where none is.</summary>
    public bool Meets(Hold kinds, double from, double to, int? track = null) =>
        spans.Any(x => Matches(x, kinds, track) && x.Start < to - 1e-9 && x.End > from + 1e-9);

    /// <summary>Whether a position lies in a stretch of a kind given, a hair early counting as at its start.</summary>
    public bool At(Hold kinds, double position, int? track = null) =>
        spans.Any(x => Matches(x, kinds, track) && Span.Within(position, x.Start, x.End));

    /// <summary>Where the next stretch of a kind given starts after a position, which a note lengthened stops at; none for none after it.</summary>
    public double NextAfter(Hold kinds, double position, int? track = null) =>
        spans.Where(x => Matches(x, kinds, track) && x.Start > position + 1e-9).Select(x => x.Start).DefaultIfEmpty(double.PositiveInfinity).Min();

    private static bool Matches(HeldSpan span, Hold kinds, int? track) => (span.Kind & kinds) != 0 && (span.Track is null || track is null || span.Track == track);
}

/// <summary>A stage of the song after it is put together: its name, what it does, and whether a test may leave it out.</summary>
/// <param name="CanSkip">
///     Whether it only edits the song, so that a test may leave it out: nothing after it needs what it hands on, a
///     stage that reads it only keeping out of its way, as the mutations keep a figure's and a polymeter's sections as
///     they are, and with it left out keeping out of nothing.
/// </param>
internal sealed record SongStage(string Name, Func<SongDecisions, SongDraft, SongDraft> Apply, bool CanSkip);

/// <summary>
///     The song's stages after it is put together, in their order, which is their correctness: a stage that reads
///     another's work comes after it, such as the lines after the fills they walk with, and a stage that keeps another's
///     intent before it would undo it, such as a hook's quote before the lines lead into the first section. A stage after
///     the notes edits them, but for state only the render reads, as the mix filter's, which no note keeps.
/// </summary>
internal static class SongStages
{
    public static ImmutableArray<SongStage> All { get; } =
    [
        new("Key changes", KeyChanges, CanSkip: false),
        new("Reharmonisation", Reharmonise, CanSkip: true),
        new("Inner lines", Composition.InnerLines.Apply, CanSkip: true),
        new("Polymeter", Polymeter, CanSkip: true),
        new("Figures", Figures.Apply, CanSkip: true),
        new("Mutations", Composition.Mutations.Apply, CanSkip: true),
        new("Fills", Fills, CanSkip: false),
        new("Lift cues", LiftCues, CanSkip: true),
        new("Drum solos", DrumSolos, CanSkip: true),
        new("Lines", Lines, CanSkip: false),
        new("Doubling", Doubling, CanSkip: true),
        new("Call and response", Composition.CallAndResponse.Apply, CanSkip: true),
        new("Line chords", LineChordsStage, CanSkip: true),
        new("Chugs", Chugs.Apply, CanSkip: true),
        new("Pedal", SectionPedals.Apply, CanSkip: true),
        new("Solos", Solos, CanSkip: true),
        new("Solo answers", SoloAnswers, CanSkip: true),
        new("Notes", RealizeNotes, CanSkip: false),
        new("Drum marks", DrumMarks.Apply, CanSkip: true),
        new("Stop time", Composition.StopTime.Apply, CanSkip: true),
        new("Pushes", Composition.Pushes.Apply, CanSkip: true),
        new("Hat rolls", Composition.HatRolls.Apply, CanSkip: true),
        new("Ghosts", Composition.Ghosts.Apply, CanSkip: true),
        new("Drum ornaments", Composition.DrumOrnaments.Apply, CanSkip: true),
        new("Instruments", Instruments, CanSkip: true),
        new("Ornaments", Composition.Ornaments.Apply, CanSkip: true),
        new("Articulation", Articulations.Apply, CanSkip: true),
        new("Mix filter", Composition.MixFilter.Apply, CanSkip: true),
        new("Timing", Pocket.Apply, CanSkip: true),
        new("Expression", ExpressionStage, CanSkip: true),
        new("Hook intro", HookIntro, CanSkip: true),
        new("Silent start", Composition.SilentStart.Apply, CanSkip: true),
        new("Risers", Composition.Risers.Apply, CanSkip: true)
    ];

    /// <summary>
    ///     The song's keys and tempos: a key change at a pattern's start, a middling song's now and then going up for a last
    ///     section that came back before, as for a last chorus, or a section, a chorus the likelier, in a key of its own,
    ///     and the wilder ones' anywhere, each place and each section from a stream of its own; a section at a tempo of its
    ///     own; and a key change led into by a chain of chords now and then; with the song's state, its swing among it.
    /// </summary>
    internal static SongDraft KeyChanges(SongDecisions song, SongDraft draft)
    {
        var (form, structure, ids) = (song.Form, song.Structure, song.SectionIds);
        var keyChanges = KeyChange.Generate(
            song.Stream(SongStream.KeyChange),
            (index, pattern) => song.AppearanceStream(SongStream.KeyChange, index, pattern),
            id => song.Stream(SongStream.SectionKey, id),
            song.Facets[Facet.Scale],
            form.Map,
            [..ids],
            structure.Roles,
            // the last section's lift, at its repeat where it plays twice, before the outro where there is one
            ids.Count - 1 + (song.Repeats ? 1 : 0) - (structure.Roles[ids[^1]] == SectionRole.Outro ? 1 : 0),
            song.Genre
        );
        var sectionTempos = SectionTempo.Generate(place => song.AppearanceStream(SongStream.SectionTempo, place), song.Facets[Facet.Feel], [..song.Played], structure.Roles, song.Genre);
        StateTrace.RecordSong(TracePoints.SectionTempo, string.Join(", ", sectionTempos.Select(x => $"{x:F2}")), sectionTempos);
        StateTrace.RecordSong(TracePoints.KeyChange, keyChanges.IsEmpty ? "none" : string.Join(", ", keyChanges), keyChanges);
        var homes = draft.Timeline.TrackTimelineMap.First(x => song.Tracks.Definitions[x.Key] is PitchInstrumentTrack).Value.StateTimelineMap;
        var (preparedKeys, preparedRoots, chains) = KeyChange.Prepare(
            keyChanges,
            draft.Timeline.CommonStateTimelineMap,
            form.Map,
            song.Facets[Facet.Scale],
            place => song.Stream(SongStream.KeyPreparation, place),
            // the sections' homes, which every pitched part's state holds over the progression's roots
            position => homes.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.ChordRoot)
        );
        StateTrace.RecordSong(TracePoints.KeyPreparation, string.Join(", ", chains.Select(x => $"{x.Chords.Length} into {x.Change}")), chains);
        var timeline = draft.Timeline.MergeStateTimelineMap(form.SongState)
            .MergeStateTimelineMap(KeyChange.ToStateTimelineMap(preparedKeys, form.Map.Duration))
            .MergeStateTimelineMap(preparedRoots)
            .MergeStateTimelineMap(SectionTempo.ToStateTimelineMap(sectionTempos, form.Map, form.Map.Duration, place => song.AppearanceStream(SongStream.TempoRamps, place)))
            .MergeStateMap(song.CommonStateMap)
            .MergeStateMap(Groove.ToStateMap(song.Swing));
        return draft with { Timeline = timeline, Chains = chains };
    }

    /// <summary>A later appearance's chords now and then substituted, from a stream of its own, before the lines are placed over them (<see cref="Reharmonisation" />).</summary>
    internal static SongDraft Reharmonise(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Timeline = draft.Timeline.MergeStateTimelineMap(Reharmonisation.Apply(song.Stream(SongStream.Reharmonisation), song.Facets[Facet.Progression], song.Genre, song.Form.Map,
                draft.Timeline.CommonStateTimelineMap, draft.Chains.SelectMany(x => x.Chords.Append(x.Change))))
        };

    /// <summary>
    ///     A section's figure of its own length run across the bar lines now and then, from a stream of its own for each
    ///     section, before the drums mark the lines and the lines are placed over it.
    /// </summary>
    internal static SongDraft Polymeter(SongDecisions song, SongDraft draft)
    {
        var stopped = song.Form.Map.Sections.Where(s => song.StopTime.Any(p => p.Start < s.End - 1e-9 && p.End > s.Start + 1e-9)).Select(s => s.SectionId).ToHashSet();
        var polymeters = Composition.Polymeter.Generate(
            id => song.Stream(SongStream.Polymeter, id),
            id => song.SectionOf(id).Facets[Facet.Feel],
            song.SectionMeters,
            [..song.SectionIds],
            song.Absent,
            song.Genre,
            song.Pinned
        ).Where(x => !song.SectionOf(x.Key).Breakdown && !stopped.Contains(x.Key)).ToImmutableDictionary();
        // but a breakdown, its kick locked to the riff, and a section in stop time, whose band hits the bar's first beat,
        // drawn all the same
        StateTrace.RecordSong(TracePoints.Polymeter, string.Join(", ", polymeters.Select(x => $"{x.Key} {x.Value}")), polymeters);
        var holds = draft.Holds(song);
        return draft with
        {
            Polymeters = [..polymeters.Keys],
            Timeline = Composition.Polymeter.Apply(
                draft.Timeline,
                song.Playing,
                DrumGroups.Kick.Tracks,
                DrumGroups.Snare.Tracks,
                song.Form.Map,
                polymeters,
                id => song.SectionOf(id).Bindings,
                // never into a stop the form makes, a stop ending's silence
                (track, position) => holds.At(Hold.Cut, position, track)
            )
        };
    }

    /// <summary>The drums marking the lines, now that the song is put together, and the runs they play, which the lines walk with.</summary>
    internal static SongDraft Fills(SongDecisions song, SongDraft draft)
    {
        var fills = new FillGenerator(song.Stream(SongStream.Fills), song.Tracks, song.Of(Facet.Fills), song.Meter, track => song.Stream(SongStream.UnheardLandings, track), song.Genre);
        // a stop-time phrase's end as heavy as a section's, the drums filling the band back in as often
        var stops = song.StopTime;
        var lines = song.Form.Lines.Select(x => stops.Any(p => Math.Abs(p.End - x.Position) < 1e-6) ? x with { Weight = Math.Max(x.Weight, 0) } : x).ToImmutableArray();
        // the runs handed on to the lines' walks and the drums' touches, none where the drums mark the form, which plays none
        // a drum heard only where the groove is taken out, in stop time or a drum solo, which clears it, landed elsewhere
        var drumSolos = song.Solos.Where(x => x.Value.IsDrumSolo || x.Value.TradesWithDrums).Select(x => song.Form.Map.Sections[x.Key]).Select(x => (x.Start, x.End, x.Meter.BarDuration));
        return draft with { Timeline = fills.Generate(draft.Timeline, lines, song.Form.Map, [..stops, ..drumSolos]), Runs = song.DrumJob == DrumJob.Marks ? [] : fills.Runs };
    }

    /// <summary>A bigger section announced, the hi-hat opening and the snare on its head before it.</summary>
    internal static SongDraft LiftCues(SongDecisions song, SongDraft draft) =>
        draft with { Timeline = Composition.LiftCues.Apply(draft.Timeline, song.Form.Lines, song.Form.Map) };

    /// <summary>The drum solos, the drummer's own, over the groove they quote.</summary>
    internal static SongDraft DrumSolos(SongDecisions song, SongDraft draft)
    {
        var holds = draft.Holds(song);
        return draft with
        {
            Timeline = Composition.DrumSolos.Apply(
                draft.Timeline,
                song.Tracks.SongDrums,
                song.Form.Map,
                song.Solos,
                song.Soloists.GetValueOrDefault(TrackRole.Drum),
                index => song.AppearanceStream(SongStream.DrumSolo, index),
                index => song.AppearanceStream(SongStream.DrumSoloStacks, index),
                index => song.AppearanceStream(SongStream.DrumSoloFeet, index),
                index => song.AppearanceStream(SongStream.DrumSoloPlayers, index),
                index => song.AppearanceStream(SongStream.DrumSoloOstinato, index),
                index => song.AppearanceStream(SongStream.DrumSoloStickings, index),
                index => song.AppearanceStream(SongStream.DrumSoloMotifs, index),
                index => song.SectionOf(song.Played[index]).Facets[Facet.Groove],
                song.Form.Drops,
                // where the form next stops the drums, which a drum solo keeps out of as every drum stage does
                position => song.Tracks.Definitions.Where(x => x.Value is PercussionInstrumentTrack).Select(x => holds.NextAfter(Hold.Cut, position, x.Key)).DefaultIfEmpty(double.PositiveInfinity).Min(),
                song.Genre,
                song.Meter,
                DrumSounds.OddsOf(song.Genre, GenreChoices.DrumSounds)
            )
        };
    }

    /// <summary>
    ///     The lines placed over the whole song, as they go on from section to section and lead into the next, a hook
    ///     intro's notes of each as its chorus plays them: the melody and the bass, then the bass walking with the drums'
    ///     runs into a new section now and then and the melody leading into it where it rests, each from a sequence of its
    ///     own; the counter-melody, leading into a section where the melody does not; and the riff.
    /// </summary>
    internal static SongDraft Lines(SongDecisions song, SongDraft draft)
    {
        var map = song.Form.Map;
        var hookChorus = song.FirstChorus?.Span.Start ?? 0;
        TrackEventStateTimelineMap<StateMap> Place(TrackEventStateTimelineMap<StateMap> timeline, KeyValuePair<int, IInstrumentTrack> track, LineProfile profile) =>
            SongFormGenerator.QuoteHook(LinePattern.Place(timeline, track.Key, (PitchInstrumentTrack)track.Value, profile, map), track.Key, map, hookChorus);
        var (melody, bass, counter, riff) = (song.Track(TrackRole.Melody), song.Track(TrackRole.Bass), song.Track(TrackRole.CounterMelody), song.Track(TrackRole.Riff));

        var timeline = Place(draft.Timeline, melody, MelodyLayers.LineOf(song.Genre));
        timeline = Place(timeline, bass, BassLeadingLayers.Line);
        (timeline, _) = LineFills.Apply(timeline, bass.Key, (PitchInstrumentTrack)bass.Value, draft.Runs, place => song.Stream(SongStream.BassFills, place), map, LineFills.Bass, ImmutableHashSet<double>.Empty);
        (timeline, var melodyWalks) = LineFills.Apply(timeline, melody.Key, (PitchInstrumentTrack)melody.Value, draft.Runs, place => song.Stream(SongStream.MelodyFills, place), map, LineFills.Melody, ImmutableHashSet<double>.Empty);
        timeline = Place(timeline, counter, CounterLayers.Line);
        (timeline, _) = LineFills.Apply(timeline, counter.Key, (PitchInstrumentTrack)counter.Value, draft.Runs, place => song.Stream(SongStream.CounterFills, place), map, LineFills.CounterMelody, melodyWalks);
        timeline = Place(timeline, riff, RiffLayers.Line);
        return draft with { Timeline = timeline };
    }

    /// <summary>A line doubled in a section now and then, by another part or by its own an octave apart, each pair and section from a sequence of its own.</summary>
    internal static SongDraft Doubling(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Timeline = LineDoubling.Apply(
                draft.Timeline,
                // the parts the song has, which a twin left out is not
                song.Playing,
                song.Form.Map,
                song.Facets[Facet.Melody],
                song.Facets[Facet.Sound],
                song.Energies,
                (pair, section) => song.Stream(SongStream.LineDoubling, pair, section),
                section => song.Stream(SongStream.TwinTimbres, section),
                section => song.Stream(SongStream.BlockHarmony, section),
                index => song.Sections[index].RiffPowerChords || song.Sections[index].Chugs,
                index => song.Sections[index].Resting.Contains(TrackRole.RiffTwin),
                (index, role) => draft.Figures.GetValueOrDefault(song.Form.Map.Sections[index].SectionId)?.Keys.Any(x => Figures.RoleOf(x) == role) == true,
                // as the section's form plays it, a heavy section's guitars driven and a calm one's clean
                index => song.Tracks.Definitions.Any(x => x.Value is PitchInstrumentTrack { Role: TrackRole.Riff } riff
                                                          && (song.Sections[index].Form.Guitars.Contains(x.Key)
                                                              ? song.Sections[index].Form.Kind == SectionFormKind.Heavy
                                                              : InstrumentTraits.Has(riff.InstrumentCode, InstrumentTrait.Driven))),
                song.Genre
            )
        };

    /// <summary>The riff landing on a chord where a section starts now and then, its twin as it is, and the melody leaning on double stops through a section now and then.</summary>
    internal static SongDraft LineChordsStage(SongDecisions song, SongDraft draft)
    {
        var (riff, melody) = (song.Track(TrackRole.Riff), song.Track(TrackRole.Melody));
        var timeline = LineChords.Apply(draft.Timeline, riff.Key, (PitchInstrumentTrack)riff.Value, song.Form.Map, index => song.AppearanceStream(SongStream.LineChords, index), LineChords.Riff);
        timeline = LineChords.Apply(timeline, melody.Key, (PitchInstrumentTrack)melody.Value, song.Form.Map, index => song.AppearanceStream(SongStream.MelodyChords, index), LineChords.Melody);
        return draft with { Timeline = timeline };
    }

    /// <summary>Every pitched solo's line played by its soloists.</summary>
    internal static SongDraft Solos(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Timeline = Composition.Solos.Apply(
                draft.Timeline,
                song.Roles,
                song.Tracks.Definitions,
                song.Form.Map,
                song.Soloists,
                song.Solos,
                index => song.AppearanceStream(SongStream.SoloLines, index),
                index => song.AppearanceStream(SongStream.SoloChords, index),
                index => song.AppearanceStream(SongStream.SoloMotifs, index)
            )
        };

    /// <summary>
    ///     The solo part answering the melody in its gaps now and then outside its solos, in its soloist's vocabulary, the
    ///     one its solos are played in, drawn from the same sequence.
    /// </summary>
    internal static SongDraft SoloAnswers(SongDecisions song, SongDraft draft)
    {
        // a song with the solo part, whose soloist its decisions drew
        if (!song.Soloists.ContainsKey(TrackRole.Solo))
            return draft;
        return draft with
        {
            Timeline = Composition.SoloAnswers.Apply(
                draft.Timeline,
                song.Roles,
                song.Tracks.Definitions,
                song.Form.Map,
                song.Solos,
                song.Soloists[TrackRole.Solo],
                index => song.Sections[index].Facets[Facet.Melody],
                index => (song.AppearanceStream(SongStream.SoloAnswers, index), song.AppearanceStream(SongStream.SoloAnswerVoicings, index), song.AppearanceStream(SongStream.SoloAnswerMotifs, index)),
                song.Genre,
                draft.Holds(song)
            )
        };
    }

    /// <summary>The notes, decided from the state of the whole song, in its order, none sounding into a stop.</summary>
    internal static SongDraft RealizeNotes(SongDecisions song, SongDraft draft) =>
        draft with { Notes = song.Form.Edits.CutNotes(Realizer.Realize(song.Tracks.Definitions, draft.Timeline, song.Form.Map.PlaceOf), song.Roles) };

    /// <summary>The parts' instruments over the song, switched and articulated by every section's sound facet.</summary>
    internal static SongDraft Instruments(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Notes = InstrumentsOverTime.Apply(
                NotesOf(draft),
                song.Tracks.Definitions,
                song.Form.Map,
                [..song.Sections.Select(x => x.Facets[Facet.Sound])],
                (section, track) => song.Stream(SongStream.InstrumentsOverTime, section, track),
                (section, track) => song.Stream(SongStream.InstrumentFamilies, section, track),
                song.Genre
            )
        };

    /// <summary>
    ///     A hook intro as its chorus plays: every part's notes of the chorus's first phrase as every stage before made them,
    ///     the twin's, the chugs', a figure's, a polymeter's, stop time's, the louder form's and the expression's among them,
    ///     played again over the intro but its last bar, which leads into the chorus as its own; a chorus played as a solo
    ///     left for the intro to quote as written.
    /// </summary>
    internal static SongDraft HookIntro(SongDecisions song, SongDraft draft)
    {
        var map = song.Form.Map;
        if (map.Intro.Kind != IntroKind.Hook)
            return draft;
        var lead = map.Origin - map.Meter.BarDuration;
        if (song.FirstChorus is not var (chorus, index) || song.Solos.ContainsKey(index) || lead <= 1e-9)
            return draft;

        // but a chorus in a key or at a tempo of its own, which the intro's would move, and which the lines' quote plays as it is
        bool Alike<T>(StateKind<T> kind) where T : notnull
        {
            var timeline = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(kind);
            return !timeline.Any(x => x.Position > 1e-9 && x.Position < lead - 1e-9 || x.Position > chorus.Start + 1e-9 && x.Position < chorus.Start + lead - 1e-9)
                   && Equals(timeline.GetEffectiveValueAt(chorus.Start), timeline.GetEffectiveValueAt(0));
        }

        if (!Alike(StateKinds.KeyOffset) || !Alike(StateKinds.Tempo))
            return draft;

        // a part whose chorus anticipates the change at its phrase's last bar, a note struck an eighth before it and held over
        // it, which the quote plays, leaving its own strike on that bar out, as its chorus does
        bool Anticipates(EventTimeline<RealizedNote> notes) => notes.Any(n => Math.Abs(n.Position - (chorus.Start + lead - Pushes.Early)) < 1e-6 && n.Position + n.Value.Duration > chorus.Start + lead + 1e-6)
                                                              && !notes.Any(n => Math.Abs(n.Position - (chorus.Start + lead)) < 1e-6);
        return draft with
        {
            Notes = NotesOf(draft).ToImmutableSortedDictionary(x => x.Key, x =>
            {
                var anticipates = Anticipates(x.Value);
                return EventTimeline.Create(x.Value.Duration,
                    x.Value.Where(n => !n.IsWithin(0, lead) && !(anticipates && Math.Abs(n.Position - lead) < 1e-6))
                        .Concat(x.Value.Where(n => n.IsWithin(chorus.Start, chorus.Start + lead)).Select(n => n.Value.ToTimelineItem(n.Position - chorus.Start)))
                        .OrderBy(n => n.Position));
            })
        };
    }

    /// <summary>How the parts are played beyond their notes, by every section's sound facet.</summary>
    internal static SongDraft ExpressionStage(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Notes = Expression.Apply(
                NotesOf(draft),
                song.Tracks.Definitions,
                song.Form.Map,
                song.Facets[Facet.Sound],
                song.Sound,
                [..song.Sections.Select(x => x.Facets[Facet.Sound])],
                song.Energies,
                song.Soloists,
                track => song.Stream(SongStream.PartSound, song.DrawsAs(track)),
                (section, track) => song.Stream(SongStream.Expression, section, song.DrawsAs(track)),
                song.Stream(SongStream.Tone),
                song.Stream(SongStream.Tape),
                song.Stream(SongStream.Pinch),
                track => song.Stream(SongStream.Pinch, song.DrawsAs(track)),
                position => Meter.SecondsPerBeat(draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(position)),
                song.Genre,
                [..draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position)],
                song.Form.Edits.CutsOf,
                [..draft.Stopped.Select(x => (x.Start, x.End))],
                song.IntroSource,
                song.DrawsAs
            )
        };

    internal static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> NotesOf(SongDraft draft) =>
        draft.Notes ?? throw new InvalidOperationException("The notes are played before they are realized.");

    /// <summary>
    ///     The song made by every stage in order, each recorded with how many events or notes every track has after it, a
    ///     stage given left out where it may be, and the song's cancellation asked between them.
    /// </summary>
    /// <param name="skipped">The stages a test leaves out, by their names; each one that may be.</param>
    /// <param name="observe">What is done with the song after each stage, by its name, such as a test's checks of it.</param>
    internal static SongDraft Run(SongDecisions song, SongDraft draft, IReadOnlySet<string> skipped, Action<string, SongDraft> observe, CancellationToken cancellation)
    {
        // the stages left out checked as the song is asked for (SongGenerator.GenerateSong)
        foreach (var stage in All.Where(x => !skipped.Contains(x.Name)))
        {
            cancellation.ThrowIfCancellationRequested();
            draft = stage.Apply(song, draft);
            observe(stage.Name, draft);
            if (StateTrace.IsRunning)
            {
                ImmutableSortedDictionary<int, int> counts = draft.Notes is { } notes
                    ? notes.ToImmutableSortedDictionary(x => x.Key, x => x.Value.Count)
                    : draft.Timeline.TrackTimelineMap.ToImmutableSortedDictionary(x => x.Key, x => x.Value.EventTimeline.Count);
                StateTrace.RecordSong(TracePoints.SongStage, stage.Name, (stage.Name, counts));
            }
        }

        return draft;
    }
}
