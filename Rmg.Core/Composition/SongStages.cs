using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     What the song decided before it was put together, which every stage after reads and none changes: its seed and
///     streams, its genre, parts, facets and form, its sections as they appear, their energies, solos and soloists, and
///     the song assembled.
/// </summary>
/// <param name="SectionOf">A section as generated, by its id, made once where it first plays.</param>
/// <param name="Sections">Every appearance, in the song's order.</param>
/// <param name="Energies">How loud and busy each appearance is meant to be.</param>
/// <param name="Solos">The appearances played as solos, by their places.</param>
/// <param name="Repeats">Whether the last section plays once more, as a last chorus.</param>
/// <param name="GrooveContext">The groove's stream, its swing's draws made before, the rest of them here.</param>
internal sealed record SongDecisions(
    ulong Seed,
    Genre Genre,
    SongTracks Tracks,
    ImmutableHashSet<TrackRole> Absent,
    Unconventionality Facets,
    Meter Meter,
    SongStructure Structure,
    IReadOnlyList<int> SectionIds,
    IReadOnlyList<int> Played,
    IReadOnlyDictionary<int, Meter> SectionMeters,
    Func<int, GeneratedSection> SectionOf,
    IReadOnlyList<GeneratedSection> Sections,
    IReadOnlyList<double> Energies,
    IReadOnlyDictionary<int, SoloPlan> Solos,
    IReadOnlyDictionary<TrackRole, Soloist> Soloists,
    SongAssembly Form,
    bool Repeats,
    StateMap CommonStateMap,
    Swing Swing,
    IGenerationContext GrooveContext
)
{
    /// <summary>A stage's own stream.</summary>
    public IGenerationContext Stream(SongStream stream) => SongGenerator.CreateStream(Seed, stream);

    /// <summary>A stage's own stream for each place, such as an appearance or a section.</summary>
    public IGenerationContext Stream(SongStream stream, int place) => new GenerationContext(Seeds.Derive(Seeds.Derive(Seed, (int)stream), place));

    /// <summary>A stage's own stream for each pair of places, such as a section and a track.</summary>
    public IGenerationContext Stream(SongStream stream, int place, int other) => new GenerationContext(Seeds.Derive(Seeds.Derive(Seeds.Derive(Seed, (int)stream), place), other));

    /// <summary>A facet's value as the rhythm's unconventionality, for a choice that leans by it.</summary>
    public RhythmicUnconventionality Of(Facet facet) => new(Facets[facet]);

    /// <summary>The parts the song plays, by their tracks.</summary>
    public IReadOnlyDictionary<int, TrackRole> Playing => Tracks.Definitions.Where(x => !Absent.Contains(x.Value.Role)).ToDictionary(x => x.Key, x => x.Value.Role);

    /// <summary>Every part, by its track.</summary>
    public IReadOnlyDictionary<int, TrackRole> Roles => Tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role);

    /// <summary>A part's track, by its role.</summary>
    public KeyValuePair<int, IInstrumentTrack> Track(TrackRole role) => Tracks.Definitions.Single(x => x.Value.Role == role);
}

/// <summary>
///     The song as the stages make it after it is put together: its state and events, then its notes, and what a stage
///     hands on to another.
/// </summary>
/// <param name="Notes">The notes, once realized (<see cref="SongStages.RealizeNotes" />); none before.</param>
/// <param name="Runs">The drums' runs, which the fills play and the lines walk with (<see cref="SongStages.Fills" />).</param>
/// <param name="MelodyWalks">Where the melody leads into a section, which the counter-melody leaves to it (<see cref="SongStages.Lines" />).</param>
/// <param name="Chains">How each key change is led into, which the bass's pedal holds under (<see cref="SongStages.KeyChanges" />).</param>
internal sealed record SongDraft(
    TrackEventStateTimelineMap<StateMap> Timeline,
    ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? Notes,
    IReadOnlyList<PlayedRun> Runs,
    ImmutableHashSet<double> MelodyWalks,
    ImmutableArray<(double Change, ImmutableArray<double> Chords, KeyChange.Device Device)> Chains
);

/// <summary>A stage of the song after it is put together: its name, what it does, and whether a test may leave it out.</summary>
/// <param name="CanSkip">Whether it only edits the song, nothing after it reading what it hands on, so that a test may leave it out.</param>
internal sealed record SongStage(string Name, Func<SongDecisions, SongDraft, SongDraft> Apply, bool CanSkip);

/// <summary>
///     The song's stages after it is put together, in their order, which is their correctness: a stage that reads
///     another's work comes after it, such as the lines after the fills they walk with, and a stage that keeps another's
///     intent before it would undo it, such as a hook's quote before the lines lead into the first section.
/// </summary>
internal static class SongStages
{
    public static ImmutableArray<SongStage> All { get; } =
    [
        new("Key changes", KeyChanges, CanSkip: false),
        new("Reharmonisation", Reharmonise, CanSkip: true),
        new("Inner lines", InnerLines, CanSkip: true),
        new("Polymeter", Polymeter, CanSkip: true),
        new("Fills", Fills, CanSkip: false),
        new("Lift cues", LiftCues, CanSkip: true),
        new("Drum solos", DrumSolos, CanSkip: true),
        new("Lines", Lines, CanSkip: false),
        new("Doubling", Doubling, CanSkip: true),
        new("Line chords", LineChordsStage, CanSkip: true),
        new("Solos", Solos, CanSkip: true),
        new("Pedal", Pedal, CanSkip: true),
        new("Solo answers", SoloAnswers, CanSkip: true),
        new("Notes", RealizeNotes, CanSkip: false),
        new("Stop time", StopTime, CanSkip: true),
        new("Pushes", Pushes, CanSkip: true),
        new("Hat rolls", HatRolls, CanSkip: true),
        new("Instruments", Instruments, CanSkip: true),
        new("Articulation", Articulation, CanSkip: true),
        new("Timing", Timing, CanSkip: true),
        new("Expression", ExpressionStage, CanSkip: true)
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
            place => song.Stream(SongStream.KeyChange, place),
            id => song.Stream(SongStream.SectionKey, id),
            song.Facets[Facet.Scale],
            form.Map,
            [..ids],
            structure.Roles,
            // the last section's lift, at its repeat where it plays twice, before the outro where there is one
            ids.Count - 1 + (song.Repeats ? 1 : 0) - (structure.Roles[ids[^1]] == SectionRole.Outro ? 1 : 0),
            song.Genre
        );
        var sectionTempos = SectionTempo.Generate(place => song.Stream(SongStream.SectionTempo, place), song.Facets[Facet.Feel], [..song.Played], structure.Roles, song.Genre);
        StateTrace.Record(TracePoints.SectionTempo, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", sectionTempos.Select(x => $"{x:F2}")), sectionTempos);
        StateTrace.Record(TracePoints.KeyChange, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, keyChanges.IsEmpty ? "none" : string.Join(", ", keyChanges), keyChanges);
        var (preparedKeys, preparedRoots, chains) = KeyChange.Prepare(
            keyChanges,
            draft.Timeline.CommonStateTimelineMap,
            form.Map,
            song.Facets[Facet.Scale],
            place => song.Stream(SongStream.KeyPreparation, place)
        );
        StateTrace.Record(TracePoints.KeyPreparation, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", chains.Select(x => $"{x.Chords.Length} into {x.Change}")), chains);
        var timeline = draft.Timeline.MergeStateTimelineMap(form.SongState)
            .MergeStateTimelineMap(KeyChange.ToStateTimelineMap(preparedKeys, form.Map.Duration))
            .MergeStateTimelineMap(preparedRoots)
            .MergeStateTimelineMap(SectionTempo.ToStateTimelineMap(sectionTempos, form.Map, form.Map.Duration, place => song.Stream(SongStream.TempoRamps, place)))
            .MergeStateMap(song.CommonStateMap)
            .MergeStateMap(Groove.ToStateMap(song.Swing, song.GrooveContext));
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
        var polymeters = Composition.Polymeter.Generate(
            id => song.Stream(SongStream.Polymeter, id),
            id => song.SectionOf(id).Facets[Facet.Feel],
            song.SectionMeters,
            [..song.SectionIds],
            song.Absent,
            song.Genre
        );
        StateTrace.Record(TracePoints.Polymeter, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", polymeters.Select(x => $"{x.Key} {x.Value}")), polymeters);
        return draft with
        {
            Timeline = Composition.Polymeter.Apply(
                draft.Timeline,
                song.Playing,
                DrumGroups.Kick.Drums.Select(DrumGroups.GetTrackNumber),
                DrumGroups.Snare.Drums.Select(DrumGroups.GetTrackNumber),
                song.Form.Map,
                polymeters,
                id => song.SectionOf(id).Bindings
            )
        };
    }

    /// <summary>The drums marking the lines, now that the song is put together, and the runs they play, which the lines walk with.</summary>
    internal static SongDraft Fills(SongDecisions song, SongDraft draft)
    {
        var fills = new FillGenerator(song.Stream(SongStream.Fills), song.Tracks, song.Of(Facet.Fills), song.Meter, song.Stream(SongStream.UnheardLandings), song.Genre);
        return draft with { Timeline = fills.Generate(draft.Timeline, song.Form.Lines, song.Form.Map), Runs = fills.Runs };
    }

    /// <summary>A bigger section announced, the hi-hat opening and the snare on its head before it.</summary>
    internal static SongDraft LiftCues(SongDecisions song, SongDraft draft) =>
        draft with { Timeline = Composition.LiftCues.Apply(draft.Timeline, song.Form.Lines) };

    /// <summary>The drum solos, the drummer's own, over the groove they quote.</summary>
    internal static SongDraft DrumSolos(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Timeline = Composition.DrumSolos.Apply(
                draft.Timeline,
                song.Tracks.SongDrums,
                song.Form.Map,
                song.Solos,
                song.Soloists.GetValueOrDefault(TrackRole.Drum),
                index => song.Stream(SongStream.DrumSolo, index),
                index => song.Stream(SongStream.DrumSoloStacks, index),
                index => song.Stream(SongStream.DrumSoloFeet, index),
                index => song.Stream(SongStream.DrumSoloPlayers, index),
                index => song.SectionOf(song.Played[index]).Facets[Facet.Groove],
                song.Meter
            )
        };

    /// <summary>
    ///     The lines placed over the whole song, as they go on from section to section and lead into the next, a hook
    ///     intro's notes of each as its chorus plays them: the melody and the bass, then the bass walking with the drums'
    ///     runs into a new section now and then and the melody leading into it where it rests, each from a sequence of its
    ///     own; the counter-melody, leading into a section where the melody does not; and the riff.
    /// </summary>
    internal static SongDraft Lines(SongDecisions song, SongDraft draft)
    {
        var map = song.Form.Map;
        var hookChorus = map.Sections.FirstOrDefault(x => song.Structure.Roles[x.SectionId] == SectionRole.Chorus)?.Start ?? 0;
        TrackEventStateTimelineMap<StateMap> Place(TrackEventStateTimelineMap<StateMap> timeline, KeyValuePair<int, IInstrumentTrack> track, LineProfile profile) =>
            SongFormGenerator.QuoteHook(LinePattern.Place(timeline, track.Key, (PitchInstrumentTrack)track.Value, profile, map), track.Key, map, hookChorus);
        var (melody, bass, counter, riff) = (song.Track(TrackRole.Melody), song.Track(TrackRole.Bass), song.Track(TrackRole.CounterMelody), song.Track(TrackRole.Riff));

        var timeline = Place(draft.Timeline, melody, MelodyLayers.LineOf(song.Genre));
        timeline = Place(timeline, bass, BassLeadingLayers.Line);
        (timeline, _) = LineFills.Apply(timeline, bass.Key, (PitchInstrumentTrack)bass.Value, draft.Runs, song.Stream(SongStream.BassFills), map.MeterAt, LineFills.Bass, ImmutableHashSet<double>.Empty);
        (timeline, var melodyWalks) = LineFills.Apply(timeline, melody.Key, (PitchInstrumentTrack)melody.Value, draft.Runs, song.Stream(SongStream.MelodyFills), map.MeterAt, LineFills.Melody, ImmutableHashSet<double>.Empty);
        timeline = Place(timeline, counter, CounterLayers.Line);
        if (timeline.TrackTimelineMap.ContainsKey(counter.Key))
            (timeline, _) = LineFills.Apply(timeline, counter.Key, (PitchInstrumentTrack)counter.Value, draft.Runs, song.Stream(SongStream.CounterFills), map.MeterAt, LineFills.CounterMelody, melodyWalks);
        timeline = Place(timeline, riff, RiffLayers.Line);
        return draft with { Timeline = timeline, MelodyWalks = melodyWalks };
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
                index => song.Sections[index].RiffPowerChords,
                song.Genre
            )
        };

    /// <summary>The riff landing on a chord where a section starts now and then, its twin as it is, and the melody leaning on double stops through a section now and then.</summary>
    internal static SongDraft LineChordsStage(SongDecisions song, SongDraft draft)
    {
        var (riff, melody) = (song.Track(TrackRole.Riff), song.Track(TrackRole.Melody));
        var timeline = LineChords.Apply(draft.Timeline, riff.Key, (PitchInstrumentTrack)riff.Value, song.Form.Map, index => song.Stream(SongStream.LineChords, index), LineChords.Riff);
        timeline = LineChords.Apply(timeline, melody.Key, (PitchInstrumentTrack)melody.Value, song.Form.Map, index => song.Stream(SongStream.MelodyChords, index), LineChords.Melody);
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
                index => song.Stream(SongStream.SoloLines, index),
                index => song.Stream(SongStream.SoloChords, index),
                index => song.Stream(SongStream.SoloMotifs, index)
            )
        };

    /// <summary>The bass holding the new key's dominant under the bar before a key change led into by a pedal, over whatever it doubles or walks there.</summary>
    internal static SongDraft Pedal(SongDecisions song, SongDraft draft)
    {
        var bass = song.Track(TrackRole.Bass);
        var common = draft.Timeline.CommonStateTimelineMap;
        // a section's pedal now and then, its home or its dominant under its appearances' first phrase, by its progression
        // facet, from a sequence of its own
        var pedalling = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var context = song.Stream(SongStream.SectionPedal, id);
            var pedals = context.TestProbability(SectionPedalChance.At(song.SectionOf(id).Facets[Facet.Progression], song.Genre.Chance(GenreChoices.HarmonyDevices, "pedal")));
            var root = context.Pick(SectionPedalRoots);
            StateTrace.Record(TracePoints.SectionPedal, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, pedals ? $"{root}" : "none", pedals ? root : null);
            return pedals ? root : (int?)null;
        });
        var sections = song.Form.Map.Sections.Where(x => pedalling[x.SectionId] is not null)
            .Select(x => (x.Start, Math.Min(x.End, x.Start + Meter.PhraseBarCount * x.Meter.BarDuration), x.Start,
                common.GetEffectiveStateMapAt(x.Start).GetStateValue(StateKinds.ChordRoot) + pedalling[x.SectionId]!.Value));
        var changes = draft.Chains.Where(x => x.Device == KeyChange.Device.Pedal).Select(x => (x.Chords[0], x.Change, x.Change, 4));
        return draft with { Timeline = KeyChange.Pedal(draft.Timeline, bass.Key, (PitchInstrumentTrack)bass.Value, changes.Concat(sections)) };
    }

    /// <summary>The chance a section holds a pedal under its appearances' first phrase, by its progression facet: never in the plainest songs.</summary>
    public static ByConvention SectionPedalChance { get; } = new(0, 0.06, 0.15);

    /// <summary>A section's pedal's root, in steps above its home, and how likely: the home, a tonic pedal, the likelier, and the dominant.</summary>
    public static ImmutableArray<Weighted<int>> SectionPedalRoots { get; } = [new(1, 0), new(0.6, 4)];

    /// <summary>
    ///     The solo part answering the melody in its gaps now and then outside its solos, in its soloist's vocabulary, the
    ///     one its solos are played in, drawn from the same sequence.
    /// </summary>
    internal static SongDraft SoloAnswers(SongDecisions song, SongDraft draft)
    {
        var solo = song.Tracks.Definitions.Values.OfType<PitchInstrumentTrack>().FirstOrDefault(x => x.Role == TrackRole.Solo);
        if (solo is null || song.Absent.Contains(TrackRole.Solo))
            return draft;
        return draft with
        {
            Timeline = Composition.SoloAnswers.Apply(
                draft.Timeline,
                song.Roles,
                song.Tracks.Definitions,
                song.Form.Map,
                song.Solos,
                song.Soloists.GetValueOrDefault(TrackRole.Solo)
                ?? Soloist.Generate(song.Stream(SongStream.Soloist, (int)TrackRole.Solo), song.Facets[Facet.Melody], song.Facets[Facet.Sound], solo.InstrumentCode),
                index => song.Sections[index].Facets[Facet.Melody],
                index => song.Stream(SongStream.SoloAnswers, index),
                song.Genre
            )
        };
    }

    /// <summary>The notes, decided from the state of the whole song, in its order, none sounding into a stop.</summary>
    internal static SongDraft RealizeNotes(SongDecisions song, SongDraft draft) =>
        draft with { Notes = song.Form.Edits.CutNotes(Realizer.Realize(song.Tracks.Definitions, draft.Timeline, song.Form.Map.MeterAt), song.Roles) };

    /// <summary>
    ///     The chance a section plays its appearances' first phrase in stop time, by its form facet: never in the plainest
    ///     songs, now and then past them; a genre's where it names it, the blues' and rock's.
    /// </summary>
    public static ByConvention StopTimeChance { get; } = new(0, 0.03, 0.03);

    /// <summary>How a section's energy leans the chance, through its odds: a quiet section the likelier, a verse more than a chorus.</summary>
    public const double StopTimeEnergyLean = -1;

    /// <summary>How long a stop-time hit is held at most, in beats.</summary>
    public const double StopTimeHit = 1;

    /// <summary>Where the song plays in stop time: every stop-time section's appearances' first phrase, its start, end and bar.</summary>
    private static (double Start, double End, double BarDuration)[] StopTimePhrases(SongDecisions song)
    {
        var stops = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var chance = SectionEnergy.Tilt(section.Energy, section.Rhythm.Coupling)
                .Chance(StopTimeChance.At(section.Facets[Facet.Form], song.Genre.Chance(GenreChoices.StopTime, "sections")), StopTimeEnergyLean);
            return song.Stream(SongStream.StopTime, id).TestProbability(chance);
        });
        return
        [
            ..song.Form.Map.Sections.Where(x => stops[x.SectionId])
                .Select(x => (x.Start, Math.Min(x.End, x.Start + Meter.PhraseBarCount * x.Meter.BarDuration), x.Meter.BarDuration))
        ];
    }

    /// <summary>
    ///     Stop time: a section's appearances' first phrase, now and then, its band hitting the bar's first beat and resting
    ///     the rest of the bar, a hit held a beat at most, under the lines, the melody's, the counter-melody's and a solo's,
    ///     which go on. A section's decision, from a sequence of its own.
    /// </summary>
    internal static SongDraft StopTime(SongDecisions song, SongDraft draft)
    {
        var phrases = StopTimePhrases(song);
        foreach (var id in song.SectionIds.Distinct())
        {
            var stops = song.Form.Map.Sections.Any(x => x.SectionId == id && phrases.Any(p => Math.Abs(p.Start - x.Start) < 1e-9));
            StateTrace.Record(TracePoints.StopTime, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{stops}", stops);
        }

        if (phrases.Length == 0)
            return draft;

        // where a note falls in stop time: off the bar's first beat, or on it, a hit
        (bool In, bool OnTheBar) Place(double position)
        {
            foreach (var (start, end, bar) in phrases)
                if (position >= start - 1e-9 && position < end - 1e-9)
                    return (true, (position - start + 1e-6) % bar < 2e-6);
            return (false, false);
        }

        var notes = NotesOf(draft);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            if (song.Tracks.Definitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo)
                continue;
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => Place(note.Position) is not (true, false))
                .Select(note => Place(note.Position).In ? (note.Value with { Duration = Math.Min(note.Value.Duration, StopTimeHit) }).ToTimelineItem(note.Position) : note));
        }

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>The chance a section pushes its chord changes, by its groove facet: never in the plainest songs; a genre's where it names it.</summary>
    public static ByConvention PushChance { get; } = new(0, 0.08, 0.15);

    /// <summary>How early a push plays its change, in beats: an eighth.</summary>
    public const double Push = 0.5;

    /// <summary>
    ///     Pushes: in a pushing section, every chord change on a bar's first beat but the section's first played an eighth
    ///     early by the band's harmony, as an anticipation: a part's notes there moved ahead and held to where they ended, a
    ///     bar at most, what they sound over in the eighth before taken out, where the part played the bar before; the
    ///     drums keeping time under it and the lines going on; never in a solo or in stop time. A section's decision, from a
    ///     sequence of its own.
    /// </summary>
    internal static SongDraft Pushes(SongDecisions song, SongDraft draft)
    {
        var pushing = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var section = song.SectionOf(id);
            var pushes = song.Stream(SongStream.Pushes, id).TestProbability(PushChance.At(section.Facets[Facet.Groove], song.Genre.Chance(GenreChoices.Push, "sections")));
            StateTrace.Record(TracePoints.Push, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{pushes}", pushes);
            return pushes;
        });
        // but in a solo, and in stop time and out of it, whose hits stay on the bar
        var changes = draft.Timeline.CommonStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var stopTime = StopTimePhrases(song);
        var pushes = song.Form.Map.Sections.Where((x, i) => pushing[x.SectionId] && !song.Solos.ContainsKey(i))
            .SelectMany(span => changes.Where(x => x > span.Start + 1e-9 && x < span.End - 1e-9 && Math.Abs(Math.IEEERemainder(x - span.Start, span.Meter.BarDuration)) < 1e-6)
                .Select(x => (At: x, span.Meter.BarDuration)))
            .Where(x => !stopTime.Any(p => x.At >= p.Start - 1e-9 && x.At <= p.End + 1e-9))
            .ToArray();
        if (pushes.Length == 0)
            return draft;

        var notes = NotesOf(draft);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            // the drums keep time under the push, and the lines go on
            if (song.Tracks.Definitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo or TrackRole.Drum)
                continue;
            // a part pushes a change where it played in the bar before, not where it comes in
            var own = pushes.Where(x => timeline.Any(n => n.Position >= x.At - x.BarDuration - 1e-9 && n.Position < x.At - 1e-9)).Select(x => x.At).ToArray();
            bool On(double position) => own.Any(x => Math.Abs(x - position) < 1e-6);
            bool Before(double position) => own.Any(x => position > x - Push - 1e-6 && position < x - 1e-6);
            var bar = pushes.Length > 0 ? pushes.Min(x => x.BarDuration) : 0;
            result[track] = EventTimeline.Create(timeline.Duration, timeline
                .Where(note => !Before(note.Position))
                .Select(note => On(note.Position) ? (note.Value with { Duration = Math.Min(note.Value.Duration + Push, bar) }).ToTimelineItem(note.Position - Push) : note)
                .Select(note => own.FirstOrDefault(x => note.Position < x - Push - 1e-6 && note.Position + note.Value.Duration > x - Push + 1e-6) is var cut && cut > 0
                    ? (note.Value with { Duration = cut - Push - note.Position }).ToTimelineItem(note.Position)
                    : note)
                .OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>The chance a section moves a chromatic line inside its held chords, by its chords facet: never in the plainest songs.</summary>
    public static ByConvention InnerLineChance { get; } = new(0, 0.15, 0.3);

    /// <summary>How long a chord is held, in bars, before a line moves inside it.</summary>
    public const int InnerLineBars = 2;

    /// <summary>
    ///     A chromatic line inside a held chord, the line cliché: in a section that takes it, over a chord two bars long or
    ///     longer, the chord's root voice falling a semitone each quarter of the chord, the root, its major seventh, its
    ///     seventh and its sixth, as the chords Cmaj7, C7 and C6 over C, each quarter a change of chord, the rest of the chord
    ///     held; decided as the song's state before the lines are placed, so that the melody placed over it fits it as it fits
    ///     any chord, the chord's scale altered to hold the fallen note where it has it not (a C7's B♭ in C major). A
    ///     section's decision, from a sequence of its own; a chord a key change moves or leads with, or moved by semitones,
    ///     kept, as is one of a sixth or a seventh of its own, and a line stopped where the scale cannot take its next note.
    /// </summary>
    internal static SongDraft InnerLines(SongDecisions song, SongDraft draft)
    {
        var lining = song.SectionIds.Distinct().ToDictionary(id => id, id =>
        {
            var lines = song.Stream(SongStream.InnerLines, id).TestProbability(InnerLineChance.At(song.SectionOf(id).Facets[Facet.Chords], song.Genre.Chance(GenreChoices.HarmonyDevices, "innerLine")));
            StateTrace.Record(TracePoints.InnerLine, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{lines}", lines);
            return lines;
        });
        var common = draft.Timeline.CommonStateTimelineMap;
        var changes = common.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var kept = draft.Chains.SelectMany(x => x.Chords.Append(x.Change)).ToHashSet();
        // a chord the chords play as a triad or a power chord, its root among its notes and none from its sixth up but the
        // root again, which the line passes through: the shape the chords' first note in it has, as every part has it then
        var chords = draft.Timeline.TrackTimelineMap.GetValueOrDefault(song.Track(TrackRole.Chords).Key)?.EventTimeline;
        bool Lines((double Start, double End) chord) =>
            chords?.Where(x => x.Position >= chord.Start - 1e-9 && x.Position < chord.End - 1e-9).Select(x => x.Value).FirstOrDefault() is { } first
            && first.GetStateValue(StateKinds.ChordNotePitchOffsets) is { IsEmpty: false } heights
            && heights.Any(IsRoot) && heights.All(x => IsRoot(x) || x * Realizer.OctaveNoteCount < InnerLineHighest);
        static bool IsRoot(double height) => Math.Abs(height - Math.Round(height)) < 1e-9;
        // every chord held long enough in a section that takes the line, but in a solo: where it starts and ends
        var held = song.Form.Map.Sections.Where((x, i) => lining[x.SectionId] && !song.Solos.ContainsKey(i))
            .SelectMany(span => changes.Where(x => x >= span.Start - 1e-9 && x < span.End - 1e-9)
                .Select(x => (Start: x, End: changes.Where(c => c > x + 1e-9).DefaultIfEmpty(span.End).Min() is var next ? Math.Min(next, span.End) : span.End))
                .Where(x => x.End - x.Start >= InnerLineBars * span.Meter.BarDuration - 1e-9))
            .Where(x => !kept.Contains(x.Start) && common.GetEffectiveStateMapAt(x.Start).GetStateValue(StateKinds.ChordShift) == 0 && Lines(x))
            .ToArray();
        var (falls, marks, scales) = (new List<TimelineItem<int>>(), new List<TimelineItem<int>>(), new List<TimelineItem<LayerValue<StepAlterations>>>());
        foreach (var (start, end) in held)
        {
            for (var quarter = 1; quarter < 4; quarter++)
            {
                var at = start + quarter * (end - start) / 4;
                var state = common.GetEffectiveStateMapAt(at);
                var (offsets, root, chord) = Realizer.GetChord(state);
                // the fallen note a step or two under the root, the step nearest it raised or lowered onto it where the scale
                // has it not, the chord's own alterations kept
                var target = chord.Root - quarter;
                var step = new[] { -1, -2 }.MinBy(x => Math.Abs(chord.GetPitch(x) - target));
                var alterations = state.GetStateValue(StateKinds.ScaleAlterations) is { IsSet: true } own ? own.Value : StepAlterations.None;
                var index = (root + step).Mod(offsets.Length);
                var by = target - chord.GetPitch(step);
                var moved = by == 0 ? alterations
                    : Math.Abs(by) == 1 && alterations.By(index) == 0
                        ? new StepAlterations(alterations.Raised | (by > 0 ? 1 << index : 0), alterations.Lowered | (by < 0 ? 1 << index : 0))
                        : (StepAlterations?)null;
                var unaltered = state.GetStateValue(StateKinds.ScaleOffsets) is { IsEmpty: false } scale ? scale : offsets;
                if (moved is not { } line || Realizer.TryAlterScaleSteps(unaltered, line) is null)
                    break;
                falls.Add(quarter.ToTimelineItem(at));
                marks.Add((InnerLineChange * quarter).ToTimelineItem(at));
                scales.Add(new LayerValue<StepAlterations>(StateDepths.Note, line).ToTimelineItem(at));
            }

            falls.Add(0.ToTimelineItem(end));
            marks.Add(0.ToTimelineItem(end));
            scales.Add(LayerValue<StepAlterations>.Unset.ToTimelineItem(end));
        }

        if (falls.All(x => x.Value == 0))
            return draft;
        static IEnumerable<TimelineItem<T>> Once<T>(IEnumerable<TimelineItem<T>> items) => items.GroupBy(x => x.Position).Select(x => x.Last()).OrderBy(x => x.Position);
        return draft with
        {
            Timeline = draft.Timeline.MergeStateTimelineMap(StateTimelineMap.Create(common.Duration,
            [
                StateTimeline.Create(common.Duration, CompositionStateKinds.InnerLine, Once(falls)).WithLayer("Inner line"),
                StateTimeline.Create(common.Duration, StateKinds.ChordChange, Once(marks)).WithLayer("Inner line"),
                StateTimeline.Create(common.Duration, StateKinds.ScaleAlterations, Once(scales)).WithLayer("Inner line")
            ]))
        };
    }

    /// <summary>How high above its root a chord's notes may reach, in semitones, for a line to fall through it: under its sixth.</summary>
    private const double InnerLineHighest = 8.5;

    /// <summary>How far a quarter of an inner line moves the chord changes' count, so that each is a change of its own, unlike the chords' around it.</summary>
    private const int InnerLineChange = 1000;

    /// <summary>The chance a bar's time drum rolls its last beat, a genre's that the songs never play (<see cref="GenreChoices.HatRolls" />).</summary>
    public static ByConvention HatRollChance { get; } = new(0, 0, 0);

    /// <summary>How fast a roll's notes come, a beat's worth of them, and how likely: 32nds, and 16th triplets the trap's other.</summary>
    public static ImmutableArray<Weighted<int>> HatRollRates { get; } = [new(1, 8), new(1, 6)];

    /// <summary>How long a roll lasts, in beats, at the bar's end, and how likely: its last beat, or the half of it.</summary>
    public static ImmutableArray<Weighted<double>> HatRollLengths { get; } = [new(1, 1), new(1, 0.5)];

    /// <summary>How loud a roll's first note plays against its last, which is the hat's own.</summary>
    public const double HatRollFrom = 0.6;

    /// <summary>
    ///     Hi-hat rolls, the trap's: now and then a bar's last beat, or its half, on the drums keeping time played as a roll of
    ///     32nds or 16th triplets growing louder, each note the drum's own note there, where it plays and no fill does. A
    ///     bar's decision, a genre's, from a sequence of its own for each appearance.
    /// </summary>
    internal static SongDraft HatRolls(SongDecisions song, SongDraft draft)
    {
        var notes = NotesOf(draft);
        var times = notes.Keys.Where(x => song.Tracks.Definitions[x] is PercussionInstrumentTrack definition && DrumRoles.Of(definition.StateMap) == DrumRole.Time).ToArray();
        if (times.Length == 0)
            return draft;

        var rolls = new List<(double From, double To, int Rate)>();
        foreach (var (span, index) in song.Form.Map.Sections.Select((x, i) => (x, i)))
        {
            var context = song.Stream(SongStream.HatRolls, index);
            var chance = HatRollChance.At(song.SectionOf(span.SectionId).Facets[Facet.Groove], song.Genre.Chance(GenreChoices.HatRolls, "bars"));
            var bar = span.Meter.BarDuration;
            for (var end = span.Start + bar; end <= span.End + 1e-9; end += bar)
            {
                // drawn for every bar, so that the draws are as many
                var (rolls1, rate, length) = (context.TestProbability(chance), context.Pick(HatRollRates), context.Pick(HatRollLengths));
                if (rolls1)
                    rolls.Add((end - length, end, rate));
            }
        }

        var result = notes.ToBuilder();
        foreach (var track in times)
        {
            var timeline = notes[track];
            var rolled = timeline.ToList();
            foreach (var (from, to, rate) in rolls)
            {
                // the drum's note where the roll starts, which every note of it plays, where it plays there
                var own = timeline.FirstOrDefault(x => x.Position >= from - 1e-9 && x.Position < to - 1e-9);
                if (own.Value is null)
                    continue;
                rolled.RemoveAll(x => x.Position >= from - 1e-9 && x.Position < to - 1e-9);
                var count = (int)Math.Round((to - from) * rate);
                rolled.AddRange(Enumerable.Range(0, count).Select(k => (own.Value with
                {
                    Velocity = own.Value.Velocity * (HatRollFrom + (1 - HatRollFrom) * k / Math.Max(1, count - 1)),
                    Duration = Math.Min(own.Value.Duration, 1.0 / rate)
                }).ToTimelineItem(from + k / (double)rate)));
            }

            result[track] = EventTimeline.Create(timeline.Duration, rolled.OrderBy(x => x.Position));
        }

        return draft with { Notes = result.ToImmutable() };
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
                (section, track) => song.Stream(SongStream.InstrumentsOverTime, section, track),
                (section, track) => song.Stream(SongStream.InstrumentFamilies, section, track),
                song.Genre
            )
        };

    /// <summary>The chance a song's chords are struck short, stabs, a genre's that the songs never play, and how long a stab sounds, in beats: a 16th.</summary>
    public static ByConvention StabChance { get; } = new(0, 0, 0);

    public const double StabLength = 0.25;

    /// <summary>The chance a part on a piano rolls its held chords, by the sound facet: never in the plainest songs.</summary>
    public static ByConvention RollChance { get; } = new(0, 0.3, 0.5);

    /// <summary>How long a piano's rolled chord takes from its lowest note to its highest, in seconds: slower than a strum.</summary>
    public const double Roll = 0.06;

    /// <summary>How long a guitar's stroke takes across its strings at its quickest and its slowest, in seconds.</summary>
    public const double StrokeFrom = 0.01, StrokeTo = 0.03;

    /// <summary>
    ///     The notes' articulations: in a section that chugs, a guitar's or a picked bass's note that repeats the one before
    ///     on a weak beat palm-muted, its accents and its moves ringing, as a chug riff mutes its eighths on the root and
    ///     rings its accent; the riff's, the rhythm part's and the bass's, by their instruments. And every chord a guitar
    ///     plays strummed, down on a beat's first half and up on its second, as quickly as the song strums, and a piano's held
    ///     chord rolled upward where its part rolls, and the chords struck short where they stab; and a bass in
    ///     octaves, its notes off the beat the note before an octave away.
    /// </summary>
    internal static SongDraft Articulation(SongDecisions song, SongDraft draft)
    {
        var notes = NotesOf(draft);
        var sections = song.Form.Map.Sections;
        var stroke = StrokeFrom + (StrokeTo - StrokeFrom) * song.Stream(SongStream.Strum).GenerateDouble();
        var tempo = draft.Timeline.CommonStateTimelineMap;
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            if (song.Tracks.Definitions[track] is not PitchInstrumentTrack part)
                continue;
            var chugs = Chugs(part.Role, part.InstrumentCode);
            var rolls = song.Stream(SongStream.Roll, track).TestProbability(RollChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "roll")));
            // the chords struck short, stabs, a genre's
            var stabs = part.Role == TrackRole.Chords
                        && song.Stream(SongStream.Stabs, track).TestProbability(StabChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "stabs")));
            var (bottom, top) = Realizer.GetRange(part);
            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select((note, i) =>
            {
                var state = note.Value.State;
                var section = sections.IndexOf(song.Form.Map.SectionAt(note.Position)!);
                var repeats = i > 0 && timeline[i - 1].Value.Pitches.SequenceEqual(note.Value.Pitches);
                var weak = state.GetStateValue(CompositionStateKinds.BeatRank) > 0;
                if (chugs && section >= 0 && song.Sections[section].Chugs && repeats && weak)
                    state = state.With(CompositionStateKinds.PalmMute, 1);
                // a bass in octaves, a note off the beat the note before it an octave away, up where its range has it and down
                // where not, as a disco or a funk bass alternates them; but one leading into the next chord, and a section that
                // chugs, which mutes its repeats instead
                var pitches = note.Value.Pitches;
                var leads = i + 1 < timeline.Count && timeline[i + 1].Value.State.GetStateValue(StateKinds.ChordChange) != note.Value.State.GetStateValue(StateKinds.ChordChange);
                if (part.Role == TrackRole.Bass && section >= 0 && song.Sections[section].OctaveBass && !song.Sections[section].Chugs && i > 0 && weak && !leads
                    && timeline[i - 1].Value.Pitches is var before && (before.All(x => x + 12 <= top) || before.All(x => x - 12 >= bottom)))
                    pitches = [..before.Select(x => before.All(y => y + 12 <= top) ? x + 12 : x - 12)];
                var program = state.GetStateValue(CompositionStateKinds.Program) is var stated and > 0 ? stated - 1 : part.InstrumentCode;
                // the stroke in beats at the tempo there
                double Beats(double seconds) => seconds * tempo.GetEffectiveStateMapAt(note.Position).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo / 60;
                if (note.Value.Pitches.Length > 1 && program is >= 24 and <= 31)
                    // down on the beat and up off it
                    state = state.With(CompositionStateKinds.Strum, note.Position % 1 < 0.5 - 1e-9 ? Beats(stroke) : -Beats(stroke));
                // a piano's chord held a beat or longer rolled upward, slower, where its part rolls
                else if (rolls && note.Value.Pitches.Length >= 3 && note.Value.Duration >= 1 - 1e-9 && program is >= 0 and <= 7)
                    state = state.With(CompositionStateKinds.Strum, Beats(Roll));

                var duration = stabs ? Math.Min(note.Value.Duration, StabLength) : note.Value.Duration;
                return state == note.Value.State && pitches == note.Value.Pitches && duration == note.Value.Duration
                    ? note
                    : (note.Value with { State = state, Pitches = pitches, Duration = duration }).ToTimelineItem(note.Position);
            }));
        }

        // and a line's steps slurred where its part slurs
        foreach (var (track, timeline) in result.ToArray())
            if (song.Tracks.Definitions[track].Role is TrackRole.Melody or TrackRole.CounterMelody or TrackRole.Solo or TrackRole.Riff
                && song.Stream(SongStream.Legato, track).TestProbability(LegatoChance.At(song.Facets[Facet.Sound], song.Genre.Chance(GenreChoices.Articulation, "legato"))))
                result[track] = Slur(timeline);

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>The chance a line's part slurs its steps, by the sound facet: never in the plainest songs, which play every note as struck.</summary>
    public static ByConvention LegatoChance { get; } = new(0, 0.4, 0.6);

    /// <summary>How far a note may step from the note before and be slurred into, in semitones, and the gap it may leave, in beats.</summary>
    public const int SlurStep = 2;

    public const double SlurGap = 0.25;

    /// <summary>
    ///     A line with its steps slurred: a note a step or less from the one before, within its phrase and close after it,
    ///     marked as slurred, its articulation, and the note before held into it with no gap.
    /// </summary>
    internal static EventTimeline<RealizedNote> Slur(EventTimeline<RealizedNote> timeline)
    {
        var items = timeline.ToArray();
        for (var i = 1; i < items.Length; i++)
        {
            var (before, note) = (items[i - 1], items[i]);
            var gap = note.Position - (before.Position + before.Value.Duration);
            if (Math.Abs(note.Value.Pitches[0] - before.Value.Pitches[0]) is 0 or > SlurStep || gap > SlurGap + 1e-9 || note.Position <= before.Position + 1e-9
                || before.Value.State.GetStateValue(StateKinds.HeldDuration) > 0 || note.Value.State.GetStateValue(CompositionStateKinds.LinePhraseStart) != (int)PhraseStart.None)
                continue;
            items[i - 1] = (before.Value with { Duration = note.Position - before.Position }).ToTimelineItem(before.Position);
            items[i] = (note.Value with { State = note.Value.State.With(CompositionStateKinds.Legato, 1) }).ToTimelineItem(note.Position);
        }

        return EventTimeline.Create(timeline.Duration, items);
    }

    /// <summary>
    ///     How far each part plays off the beat, in beats at its middle, late above 0, as a band's pocket: the backbeat a
    ///     little behind, the time on it, the bass a touch ahead, a lead laid back; a rule a role, never a jitter.
    /// </summary>
    public static double OffsetOf(TrackRole role, DrumRole? drum) => (role, drum) switch
    {
        (TrackRole.Drum, DrumRole.Backbeat) => 0.02,
        (TrackRole.Drum, DrumRole.Time) => -0.005,
        (TrackRole.Drum, _) => 0,
        (TrackRole.Bass, _) => -0.01,
        (TrackRole.Melody or TrackRole.Solo, _) => 0.015,
        (TrackRole.CounterMelody, _) => 0.01,
        _ => 0
    };

    /// <summary>How much of its offsets a song plays by its feel facet: none in the plainest, which keep to the click, more in the wildest.</summary>
    public static ByConvention Pocket { get; } = new(0, 1, 1.5);

    /// <summary>How far a genre's pocket moves the offsets at its most and its least, as a factor: four times, or a quarter.</summary>
    public const double GenrePocketReach = 4;

    /// <summary>
    ///     Every note played off the beat as its part's role has it (<see cref="OffsetOf" />), as much as the song's feel
    ///     facet plays it and a genre's pocket moves it, laid back or tight.
    /// </summary>
    internal static SongDraft Timing(SongDecisions song, SongDraft draft)
    {
        var amount = Pocket.At(song.Facets[Facet.Feel]) * Math.Pow(GenrePocketReach, song.Genre.Shift(GenreChoices.Pocket, "laidBack"));
        if (amount == 0)
            return draft;
        var notes = NotesOf(draft);
        var result = notes.ToBuilder();
        foreach (var (track, timeline) in notes)
        {
            // a drum by its role in the song's groove, whatever a section makes of it
            var definition = song.Tracks.Definitions[track];
            var offset = amount * OffsetOf(definition.Role, definition.Role == TrackRole.Drum ? DrumRoles.Of(definition.StateMap) : null);
            result[track] = EventTimeline.Create(timeline.Duration, timeline.Select(note => offset == 0
                ? note
                : (note.Value with { State = note.Value.State.With(CompositionStateKinds.TimingOffset, offset) }).ToTimelineItem(note.Position)));
        }

        return draft with { Notes = result.ToImmutable() };
    }

    /// <summary>Whether a part chugs: the riff, the rhythm part or the bass, on a guitar or a picked bass.</summary>
    private static bool Chugs(TrackRole role, int program) => role is TrackRole.Riff or TrackRole.Rhythm or TrackRole.Bass && program is >= 24 and <= 31 or 34;

    /// <summary>How the parts are played beyond their notes, by every section's sound facet.</summary>
    internal static SongDraft ExpressionStage(SongDecisions song, SongDraft draft) =>
        draft with
        {
            Notes = Expression.Apply(
                NotesOf(draft),
                song.Tracks.Definitions,
                song.Form.Map,
                song.Facets[Facet.Sound],
                song.Stream(SongStream.Effects),
                [..song.Sections.Select(x => x.Facets[Facet.Sound])],
                song.Energies,
                song.Soloists,
                track => song.Stream(SongStream.PartSound, track),
                (section, track) => song.Stream(SongStream.Expression, section, track),
                song.Genre
            )
        };

    private static ImmutableSortedDictionary<int, EventTimeline<RealizedNote>> NotesOf(SongDraft draft) =>
        draft.Notes ?? throw new InvalidOperationException("The notes are played before they are realized.");

    /// <summary>
    ///     The song made by every stage in order, each recorded with how many events or notes every track has after it, a
    ///     stage given left out where it may be, and the song's cancellation asked between them.
    /// </summary>
    /// <param name="skipped">The stages a test leaves out, by their names; each one that may be.</param>
    /// <param name="observe">What is done with the song after each stage, by its name, such as a test's checks of it.</param>
    internal static SongDraft Run(SongDecisions song, SongDraft draft, IReadOnlySet<string> skipped, Action<string, SongDraft> observe, CancellationToken cancellation)
    {
        foreach (var name in skipped)
            if (All.SingleOrDefault(x => x.Name == name) is not { CanSkip: true })
                throw new ArgumentException($"No stage {name} that may be left out.", nameof(skipped));

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
                StateTrace.Record(TracePoints.SongStage, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, stage.Name, (stage.Name, counts));
            }
        }

        return draft;
    }
}
