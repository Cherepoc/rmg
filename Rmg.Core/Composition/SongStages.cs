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
        new("Instruments", Instruments, CanSkip: true),
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
            ids.Count - 1 + (song.Repeats ? 1 : 0) - (structure.Roles[ids[^1]] == SectionRole.Outro ? 1 : 0)
        );
        var sectionTempos = SectionTempo.Generate(place => song.Stream(SongStream.SectionTempo, place), song.Facets[Facet.Feel], [..song.Played], structure.Roles);
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
            song.Absent
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

        var timeline = Place(draft.Timeline, melody, MelodyLayers.Line);
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
                section => song.Stream(SongStream.TwinTimbres, section)
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
        return draft with
        {
            Timeline = KeyChange.Pedal(draft.Timeline, bass.Key, (PitchInstrumentTrack)bass.Value, draft.Chains.Where(x => x.Device == KeyChange.Device.Pedal).Select(x => (x.Chords[0], x.Change)))
        };
    }

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
                index => song.Stream(SongStream.SoloAnswers, index)
            )
        };
    }

    /// <summary>The notes, decided from the state of the whole song, in its order, none sounding into a stop.</summary>
    internal static SongDraft RealizeNotes(SongDecisions song, SongDraft draft) =>
        draft with { Notes = song.Form.Edits.CutNotes(Realizer.Realize(song.Tracks.Definitions, draft.Timeline, song.Form.Map.MeterAt), song.Roles) };

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
    internal static SongDraft Run(SongDecisions song, SongDraft draft, IReadOnlySet<string> skipped, CancellationToken cancellation)
    {
        foreach (var name in skipped)
            if (All.SingleOrDefault(x => x.Name == name) is not { CanSkip: true })
                throw new ArgumentException($"No stage {name} that may be left out.", nameof(skipped));

        foreach (var stage in All.Where(x => !skipped.Contains(x.Name)))
        {
            cancellation.ThrowIfCancellationRequested();
            draft = stage.Apply(song, draft);
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
