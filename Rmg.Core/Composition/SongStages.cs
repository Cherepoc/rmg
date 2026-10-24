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
/// <param name="Polymeters">The sections a figure runs across the bar lines in, which the figures keep to and the pushes, a mutating groove, stop time's hits and the ghosts keep out of, held as <see cref="Hold.Polymeter" /> (<see cref="SongStages.Polymeter" />).</param>
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
    ///     Where the song plays in stop time as stop time played it, its phrases and bars of ensemble hits that a part
    ///     carries, none before it or where it is left out: the stages after it keep to these, the stages before it to the
    ///     phrases drawn (<see cref="SongDecisions.StopTime" />).
    /// </summary>
    public ImmutableArray<(double Start, double End, double BarDuration)> Stopped { get; init; } = [];

    /// <summary>
    ///     The stretches the stages have made that the others keep out of, each written by the stage that made it: a fill, a
    ///     kick-out, which the bass leaves with the kick (<see cref="SongStages.KickOutBass" />), a polymeter's sections, a
    ///     drum solo's turns, a calling verse's call and its answer (<see cref="Composition.CallAndResponse" />).
    /// </summary>
    public ImmutableArray<HeldSpan> Held { get; init; } = [];

    /// <summary>The appearance reharmonised and the substitutes it may take, decided under its melody once it is placed (<see cref="SongStages.Lines" />); none where none is.</summary>
    public ReharmonisationPlan? Reharmonisation { get; init; }

    /// <summary>Where the drums fill, from a fill's start to its line, which the stages that play around a fill keep out of.</summary>
    public (double From, double To)[] FillSpans => [..Held.Where(x => x.Kind == Hold.Fill).Select(x => (x.Start, x.End))];

    /// <summary>
    ///     What the song holds, which a stage keeps out of by the kinds it names (<see cref="Hold" />): what the stages wrote
    ///     (<see cref="Held" />), a fill, a kick-out, a polymeter's sections, a drum solo's turns and a calling verse's call
    ///     and answer; a drop's build, its stop time as played, its solos, and where the form stops a part
    ///     (<see cref="TimelineEdits.StopsOf" />).
    /// </summary>
    public Holds Holds(SongDecisions song) => new(
    [
        ..Held,
        ..song.Form.Builds.Select(x => new HeldSpan(Hold.Build, x.From, x.To, null)),
        ..Stopped.Select(x => new HeldSpan(Hold.Stop, x.Start, x.End, null)),
        ..song.Solos.Keys.Select(x => song.Form.Map.Sections[x]).Select(x => new HeldSpan(Hold.Solo, x.Start, x.End, null)),
        ..song.Tracks.Definitions.Keys.SelectMany(track => song.Form.Edits.StopsOf(track).Select(x => new HeldSpan(Hold.Cut, x.From, x.To, track)))
    ]);
}

/// <summary>What a stretch of the song is held by, which a stage keeps out of by the kinds it names.</summary>
[Flags]
internal enum Hold
{
    /// <summary>The drums' fill, from its first note to its line.</summary>
    Fill = 1,

    /// <summary>Stop time, as it played, and a section's bar of ensemble hits.</summary>
    Stop = 2,

    /// <summary>A drum solo, or the drums' turns of a solo they trade, as the drum solos played them.</summary>
    DrumSolo = 4,

    /// <summary>A part stopped by the form: a stop ending, a stripped ending's exits, a silent drop.</summary>
    Cut = 8,

    /// <summary>A calling verse's first half, the melody's call.</summary>
    Call = 16,

    /// <summary>A calling verse's second half, a part's answer.</summary>
    Answer = 32,

    /// <summary>The kick taken out before a line, the groove playing on.</summary>
    KickOut = 64,

    /// <summary>The snare rolling into a drop, faster each bar.</summary>
    Build = 128,

    /// <summary>A solo, every one, a pitched part's or the drums'.</summary>
    Solo = 256,

    /// <summary>A section running a figure across its bar lines, its cycle kept (<see cref="SongDraft.Polymeters" />).</summary>
    Polymeter = 512
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

    /// <summary>The stretches of a kind given, the band's and a track's own where one is asked of, in the song's order.</summary>
    public IEnumerable<HeldSpan> Of(Hold kinds, int? track = null) => spans.Where(x => Matches(x, kinds, track)).OrderBy(x => x.Start);

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
        new("Drum solos", Composition.DrumSolos.Apply, CanSkip: true),
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
        new("Surf drops", SurfDrops.Apply, CanSkip: true),
        new("Mix filter", Composition.MixFilter.Apply, CanSkip: true),
        new("Timing", Pocket.Apply, CanSkip: true),
        new("Kick-out bass", KickOutBass, CanSkip: true),
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
            id => song.SectionOf(id).LinesFrom,
            LastFull(song),
            song.Genre
        );
        var sectionTempos = SectionTempo.Generate(place => song.AppearanceStream(SongStream.SectionTempo, place), song.Facets[Facet.Feel], [..song.Played], structure.Roles, song.Genre, song.Departs);
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
            .MergeStateTimelineMap(SectionTempo.ToStateTimelineMap(sectionTempos, form.Map, form.Map.Duration, place => song.AppearanceStream(SongStream.TempoRamps, place),
                SectionTempo.RampChance.At(song.Facets[Facet.Form], song.Genre.Chance(GenreChoices.SectionChanges, "eased"))))
            .MergeStateMap(song.CommonStateMap)
            .MergeStateMap(Groove.ToStateMap(song.Swing));
        return draft with { Timeline = timeline, Chains = chains };
    }

    /// <summary>A later appearance's chords now and then substituted, from a stream of its own, planned here and decided under the melody as the lines place it (<see cref="Reharmonisation" />).</summary>
    internal static SongDraft Reharmonise(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Reharmonisation = Composition.Reharmonisation.Plan(song.Stream(SongStream.Reharmonisation), song.Facets[Facet.Progression], song.Genre, song.Form.Map,
                draft.Timeline.CommonStateTimelineMap, draft.Chains.SelectMany(x => x.Chords.Append(x.Change)), LastFull(song))
        };

    /// <summary>
    ///     The last section's place, which a key lifts at and a reharmonisation takes: its repeat where it plays twice, before
    ///     an outro or a riff section closing the song.
    /// </summary>
    private static int LastFull(SongDecisions song) =>
        song.SectionIds.Count - 1 + (song.Repeats ? 1 : 0) - (song.Structure.Roles[song.SectionIds[^1]] is SectionRole.Outro or SectionRole.Intro ? 1 : 0);

    /// <summary>
    ///     A section's figure of its own length run across the bar lines now and then, from a stream of its own for each
    ///     section, before the drums mark the lines and the lines are placed over it.
    /// </summary>
    internal static SongDraft Polymeter(SongDecisions song, SongDraft draft)
    {
        var stopped = song.Form.Map.Sections.Where(s => song.StopTime.Any(p => p.Start < s.End - 1e-9 && p.End > s.Start + 1e-9)).Select(s => s.SectionId).ToHashSet();
        var drawn = Composition.Polymeter.Generate(
            id => song.Stream(SongStream.Polymeter, id),
            id => song.SectionOf(id).Facets[Facet.Feel],
            song.SectionMeters,
            [..song.SectionIds],
            song.Absent,
            song.Genre,
            song.Pinned
        );
        // a section that loops another's lines runs its source's cycle, or none where its source runs none, its own drawn all
        // the same
        var polymeters = song.SectionIds.Distinct().Select(id => (Id: id, Plan: drawn.GetValueOrDefault(song.SectionOf(id).LinesFrom ?? id))).Where(x => x.Plan is not null)
            .Where(x => !song.SectionOf(x.Id).Breakdown && !stopped.Contains(x.Id)).ToImmutableDictionary(x => x.Id, x => x.Plan!);
        // but a breakdown, its kick locked to the riff, and a section in stop time, whose band hits the bar's first beat,
        // drawn all the same
        StateTrace.RecordSong(TracePoints.Polymeter, string.Join(", ", polymeters.Select(x => $"{x.Key} {x.Value}")), polymeters);
        var holds = draft.Holds(song);
        return draft with
        {
            Polymeters = [..polymeters.Keys],
            // held where it plays, an intro's window as its section's first bars
            Held = [..draft.Held, ..song.Form.Map.PlayedSections.Where(x => polymeters.ContainsKey(x.SectionId)).Select(x => new HeldSpan(Hold.Polymeter, x.Start, x.End, null))],
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
        // what each section's fills keep on the kick, from a sequence of its own, by its fills facet and the genre's pool
        var feet = song.SectionIds.Distinct().ToDictionary(id => id, id => song.Stream(SongStream.FillFeet, id)
            .Pick(ByConvention.Weigh(FillLayers.Feet, song.SectionOf(id).Facets[Facet.Fills], song.Genre.Pool<FillFeet>(GenreChoices.FillFeet))));
        var fills = new FillGenerator(song.Stream(SongStream.Fills), song.Tracks, song.Of(Facet.Fills), song.Meter, track => song.Stream(SongStream.UnheardLandings, track), song.Genre,
            id => feet[id]);
        // a stop-time phrase's end as heavy as a section's, the drums filling the band back in as often
        var stops = song.StopTime;
        var hits = song.EnsembleHits;
        var lines = song.Form.Lines.Select(x => stops.Any(p => Math.Abs(p.End - x.Position) < 1e-6) ? x with { Weight = Math.Max(x.Weight, 0) } : x).ToImmutableArray();
        // the runs handed on to the lines' walks and the drums' touches, none where the drums mark the form, which plays none
        // a drum heard only where the groove is taken out, in stop time or a drum solo, which clears it, landed elsewhere
        var drumSolos = song.Solos.Where(x => x.Value.IsDrumSolo || x.Value.TradesWithDrums).Select(x => song.Form.Map.Sections[x.Key]).Select(x => (x.Start, x.End, x.Meter.BarDuration));
        var timeline = fills.Generate(draft.Timeline, lines, song.Form.Map, [..stops, ..hits, ..drumSolos]);
        StateTrace.RecordSong(TracePoints.KickOuts, string.Join(", ", fills.KickOuts.Select(x => $"{x.From}-{x.To}")), fills.KickOuts.ToImmutableArray());
        return draft with
        {
            Timeline = timeline,
            // none where the drums mark the form, which plays no fill nor kick-out
            Runs = song.DrumJob == DrumJob.Marks ? [] : fills.Runs,
            Held =
            [
                ..draft.Held,
                ..(song.DrumJob == DrumJob.Marks ? [] : fills.Spans).Select(x => new HeldSpan(Hold.Fill, x.From, x.To, null)),
                ..(song.DrumJob == DrumJob.Marks ? [] : fills.KickOuts).Select(x => new HeldSpan(Hold.KickOut, x.From, x.To, null))
            ]
        };
    }

    /// <summary>A bigger section announced, the hi-hat opening and the snare on its head before it.</summary>
    internal static SongDraft LiftCues(SongDecisions song, SongDraft draft) =>
        draft with { Timeline = Composition.LiftCues.Apply(draft.Timeline, song.Form.Lines, song.Form.Map) };

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
        // each line's phrases louder at their peaks as far as the genre plays its parts' velocity (VelocityLayers.SpreadOf)
        TrackEventStateTimelineMap<StateMap> Place(TrackEventStateTimelineMap<StateMap> timeline, KeyValuePair<int, IInstrumentTrack> track, LineProfile profile) =>
            SongFormGenerator.QuoteHook(LinePattern.Place(timeline, track.Key, (PitchInstrumentTrack)track.Value, profile with { PhraseArc = profile.PhraseArc * VelocityLayers.SpreadOf(song.Genre), PhraseSwell = profile.PhraseSwell * VelocityLayers.SpreadOf(song.Genre) }, map),
                track.Key, map, hookChorus);
        var (melody, bass, counter, riff) = (song.Track(TrackRole.Melody), song.Track(TrackRole.Bass), song.Track(TrackRole.CounterMelody), song.Track(TrackRole.Riff));

        // a reharmonised appearance's substitutes decided under the melody as it is placed, before the other lines are
        // placed over them, as an arranger keeps the tune and changes the chords around it
        var timeline = Place(draft.Timeline, melody, MelodyLayers.LineOf(song.Genre));
        timeline = Reharmonisation.Apply(timeline, draft.Reharmonisation, map, melody, MelodyLayers.LineOf(song.Genre));
        timeline = Place(timeline, bass, BassLeadingLayers.Line);
        (timeline, _) = LineFills.Apply(timeline, bass.Key, (PitchInstrumentTrack)bass.Value, draft.Runs, place => song.Stream(SongStream.BassFills, place), map, LineFills.Bass, ImmutableHashSet<double>.Empty);
        (timeline, var melodyWalks) = LineFills.Apply(timeline, melody.Key, (PitchInstrumentTrack)melody.Value, draft.Runs, place => song.Stream(SongStream.MelodyFills, place), map, LineFills.Melody, ImmutableHashSet<double>.Empty);
        timeline = Place(timeline, counter, CounterLayers.Line);
        (timeline, var counterWalks) = LineFills.Apply(timeline, counter.Key, (PitchInstrumentTrack)counter.Value, draft.Runs, place => song.Stream(SongStream.CounterFills, place), map, LineFills.CounterMelody, melodyWalks);
        // and running into a change of section now and then, a genre's, the likelier the louder the section it runs into,
        // where neither line walks
        var changes = song.Form.Lines.Where(x => x.Ending != x.Next).Select(x => new PlayedRun(x.Position, [], x.Lift)).ToArray();
        (timeline, _) = LineFills.Apply(timeline, counter.Key, (PitchInstrumentTrack)counter.Value, changes, place => song.Stream(SongStream.CounterRuns, place), map,
            LineFills.Run with { Chance = LineFills.RunChance.At(song.Facets[Facet.Melody], song.Genre.Chance(GenreChoices.LineRun, "counterMelody")) }, melodyWalks.Union(counterWalks));
        timeline = Place(timeline, riff, RiffLayers.Line);
        // and the riff turning round into its next cycle now and then, where its section's pattern starts again within it, but
        // in a polymeter, whose cycle is not its bar's; its walk from the last pulse of the bar before, a dotted quarter in 6/8
        var restarts = map.Plays.Where(x => !x.IsEnding && !draft.Polymeters.Contains(x.Span.SectionId))
            .SelectMany(play => play.Span.Patterns.Skip(1).Select(pattern => (pattern.Start, play.Span.Meter)))
            .Select(x => new PlayedRun(x.Start, [x.Start - (x.Meter.BarDuration - x.Meter.Pulses[^1]), x.Start - LineFills.ShortestNote], 0)).ToArray();
        (timeline, _) = LineFills.Apply(timeline, riff.Key, (PitchInstrumentTrack)riff.Value, restarts, place => song.Stream(SongStream.RiffTurnarounds, place), map,
            LineFills.Riff with { Chance = LineFills.TurnaroundChance.At(song.Facets[Facet.Melody], song.Genre.Chance(GenreChoices.Riff, "turnaround")) }, ImmutableHashSet<double>.Empty);
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
                index => song.Sections[index].TwinLead,
                index => song.Sections[index].Resting.Contains(TrackRole.RiffTwin),
                (index, role) => draft.Figures.GetValueOrDefault(song.Form.Map.Sections[index].SectionId)?.Keys.Any(x => Figures.RoleOf(x) == role) == true,
                // as the section's form plays it, a heavy section's guitars driven and a calm one's clean
                index => song.Tracks.Definitions.Any(x => x.Value is PitchInstrumentTrack { Role: TrackRole.Riff } riff
                                                          && (song.Sections[index].Form.Guitars.Contains(x.Key)
                                                              ? song.Sections[index].Form.Kind == SectionFormKind.Heavy
                                                              : InstrumentTraits.Has(riff.InstrumentCode, InstrumentTrait.Driven))),
                (index, phrase) => song.Sections[index].PhraseSilent is var silent && phrase < silent.Length && silent[phrase].Contains(TrackRole.Bass),
                Math.Max(0, -Registers.OctavesOf(song.Genre, TrackRole.Riff)),
                song.Genre,
                song.Tracks.Definitions
            )
        };

    /// <summary>The riff landing on a chord where a section starts now and then, its twin as it is, and the melody leaning on double stops through a section now and then.</summary>
    internal static SongDraft LineChordsStage(SongDecisions song, SongDraft draft)
    {
        var (riff, melody) = (song.Track(TrackRole.Riff), song.Track(TrackRole.Melody));
        var timeline = LineChords.Apply(draft.Timeline, riff.Key, (PitchInstrumentTrack)riff.Value, song.Form.Map, index => song.AppearanceStream(SongStream.LineChords, index), LineChords.Riff, song.Genre, song.Facets[Facet.Melody]);
        timeline = LineChords.Apply(timeline, melody.Key, (PitchInstrumentTrack)melody.Value, song.Form.Map, index => song.AppearanceStream(SongStream.MelodyChords, index), LineChords.Melody, song.Genre, song.Facets[Facet.Melody]);
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
                (index, place) => place == 0 ? song.AppearanceStream(SongStream.SoloLines, index) : song.AppearanceStream(SongStream.SoloLines, index, place),
                (index, place) => place == 0 ? song.AppearanceStream(SongStream.SoloChords, index) : song.AppearanceStream(SongStream.SoloChords, index, place),
                (index, place) => place == 0 ? song.AppearanceStream(SongStream.SoloMotifs, index) : song.AppearanceStream(SongStream.SoloMotifs, index, place),
                (index, place) => place == 0 ? song.AppearanceStream(SongStream.SoloPickups, index) : song.AppearanceStream(SongStream.SoloPickups, index, place)
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

    /// <summary>
    ///     The bass out with the kick where the drums take it out, the low end leaving together into the downbeat: its notes
    ///     there taken out, and one sounding into the span ending where it starts.
    /// </summary>
    internal static SongDraft KickOutBass(SongDecisions song, SongDraft draft)
    {
        var notes = draft.Notes!;
        foreach (var track in song.Roles.Where(x => x.Value == TrackRole.Bass).Select(x => x.Key).Where(notes.ContainsKey))
        {
            var timeline = notes[track];
            foreach (var (_, from, to, _) in draft.Holds(song).Of(Hold.KickOut))
                timeline = timeline.Cleared(from, to);
            notes = notes.SetItem(track, timeline);
        }

        return draft with { Notes = notes };
    }

    /// <summary>The parts' instruments over the song, switched and articulated by every section's sound facet.</summary>
    internal static SongDraft Instruments(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Notes = InstrumentsOverTime.Apply(
                NotesOf(draft),
                song.Tracks.Definitions,
                song.Form.Map,
                [..song.Sections.Select(x => x.Facets[Facet.Sound])],
                // a twin as its riff, drawing from the riff's sequences
                (section, track) => song.Stream(SongStream.InstrumentsOverTime, section, song.DrawsAs(track)),
                (section, track) => song.Stream(SongStream.InstrumentFamilies, section, song.DrawsAs(track)),
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
                // the quote's first note sliding in from the chorus's note before it, which the quote leaves out, struck plain
                var quoted = x.Value.Where(n => n.IsWithin(chorus.Start, chorus.Start + lead)).Select((n, i) => (i == 0 && n.Value.State.GetStateValue(CompositionStateKinds.Bend) == (int)BendKind.Slide
                    ? n.Value with { State = n.Value.State.With(CompositionStateKinds.Bend, 0).With(CompositionStateKinds.BendDepth, 0).With(CompositionStateKinds.BendRise, 0) }
                    : n.Value).ToTimelineItem(n.Position - chorus.Start));
                return EventTimeline.Create(x.Value.Duration,
                    x.Value.Where(n => !n.IsWithin(0, lead) && !(anticipates && Math.Abs(n.Position - lead) < 1e-6)).Concat(quoted).OrderBy(n => n.Position));
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
                draft.Holds(song),
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
