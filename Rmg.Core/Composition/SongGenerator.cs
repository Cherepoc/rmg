using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     Generates songs, in stages that each draw from the song's seed in turn: the tracks and their instruments
///     (<see cref="SongTracks" />), the song's harmony and state, its structure of parts and sections, and every
///     section (<see cref="SectionGenerator" />) with its bar state (<see cref="BarStateGenerator" />) and its tracks'
///     patterns (<see cref="PatternGenerator" />). A section that comes back is played as it was first generated.
/// </summary>
public static class SongGenerator
{
    // the song's speed as a multiple of 120 BPM, from 90 to about 175 BPM: 120 itself is the likeliest, and the finer
    // a multiplier's fraction, the less likely it is
    private static readonly Func<IGenerationContext, double> TempoGenerator = Generators.DyadicMultiplier(
        WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.75),
        4,
        0.75,
        1.5
    );

    public static Song GenerateSong()
    {
        return GenerateSong(Seeds.Random());
    }

    /// <summary>
    ///     Generates a song. The same seed always results in the same song in this version; changes to the generator
    ///     may turn a seed into a different song, which is accepted to keep the generator simple.
    /// </summary>
    public static Song GenerateSong(ulong seed)
    {
        return GenerateSong(seed, ProgressionSettings.Default);
    }

    /// <summary>
    ///     Generates a song with what is given used as given, such as its unconventionality's base, from 0 for the
    ///     plainest to 1 for the wildest, or the parts it has, and the rest drawn as <see cref="GenerateSong(int)" />
    ///     draws it.
    /// </summary>
    public static Song GenerateSong(ulong seed, SongOverrides overrides)
    {
        if (overrides.Base is < 0 or > 1 or double.NaN)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Base, "An unconventionality is from 0 to 1.");
        if (overrides.Facets?.Values.Any(x => x is < 0 or > 1 or double.NaN) == true)
            throw new ArgumentOutOfRangeException(nameof(overrides), "A facet is from 0 to 1.");
        if (overrides.MeterOption is < 0 || overrides.MeterOption >= Meter.Options.Length)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.MeterOption, $"A meter is one of the {Meter.Options.Length} options.");
        if (overrides.Tempo is < 0 || overrides.Tempo >= TempoOptions.Length)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Tempo, $"A tempo is one of the {TempoOptions.Length} options.");
        if (overrides.Key is < 0 or > 11)
            throw new ArgumentOutOfRangeException(nameof(overrides), overrides.Key, "A key is from 0 to 11.");

        return GenerateSong(seed, ProgressionSettings.Default, overrides);
    }

    internal static Song GenerateSong(ulong seed, ProgressionSettings progressionSettings) => GenerateSong(seed, progressionSettings, SongOverrides.None);

    /// <param name="overrides">What a test sets in place of the song's own draws.</param>
    internal static Song GenerateSong(ulong seed, ProgressionSettings progressionSettings, SongOverrides overrides)
    {
        // every stage draws from its own random sequence, derived from the seed by the stage, so a change to what one
        // stage draws leaves what the others draw as it was
        var root = new GenerationContext(seed);
        IGenerationContext Stream(SongStream stream) => CreateStream(seed, stream);

        // how far the song strays from convention, and every facet of it, drawn around the song's; the rhythm strays as
        // the groove does, which every rhythm layer from the tracks' own on is scaled by
        var unconventionalityBase = overrides.Base ?? Unconventionality.DrawBase(Stream(SongStream.Rhythm));
        var songUnconventionality = Unconventionality.Generate(unconventionalityBase, facet => CreateStream(seed, SongStream.Unconventionality, facet));
        if (overrides.Facets is { } facets)
            songUnconventionality = songUnconventionality with { Facets = songUnconventionality.Facets.SetItems(facets) };
        StateTrace.Record(TracePoints.SongUnconventionality, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{songUnconventionality.Base:F2}", songUnconventionality);
        var rhythmicUnconventionality = new RhythmicUnconventionality(songUnconventionality[Facet.Groove]);
        // a facet's value as the rhythm's unconventionality, for a choice that leans by it
        RhythmicUnconventionality Of(Facet facet) => new(songUnconventionality[facet]);
        StateTrace.Record(TracePoints.SongRhythm, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{rhythmicUnconventionality.Value:F2}", rhythmicUnconventionality);
        var tracks = SongTracks.Create(
            Stream(SongStream.Tracks),
            Stream(SongStream.Panning),
            Stream(SongStream.Pad),
            Stream(SongStream.CounterMelody),
            Stream(SongStream.Riff),
            Stream(SongStream.RhythmPart),
            rhythmicUnconventionality,
            Stream(SongStream.DrumStrokes),
            Stream(SongStream.DrumRoles),
            overrides.DrumSetup ?? DrumSetups.Pick(Stream(SongStream.DrumSetup), rhythmicUnconventionality.Value)
        );
        // the parts the song leaves out, the pad, the counter-melody and the drums now and then, each from a sequence
        // of its own, and any given in or out
        var absent = SongParts.DrawAbsent(part => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.Parts), (int)part)), overrides.Parts);
        StateTrace.Record(TracePoints.SongParts, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", absent.Order()), absent);
        StateTrace.Record(TracePoints.DrumSetup, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, tracks.DrumSetup.ToString(), tracks.DrumSetup);

        // the song's chords gather around its unconventionality, and a section's around its own shift of it
        var harmonyContext = Stream(SongStream.Harmony);
        var unconventionality = new HarmonicUnconventionality(songUnconventionality[Facet.Chords]);
        var scale = Scales.Pick(harmonyContext, songUnconventionality[Facet.Scale]);
        StateTrace.Record(TracePoints.SongHarmony, SongTracks.ChordsTrack, 0, 0, StateMap.Default, 0, $"{unconventionality.Chords:F2} {scale.Name}", (unconventionality, scale));
        // the feel the song plays in, straight or a tuplet, from a sequence of its own, by its feel facet
        var feel = Feels.DrawSong(Stream(SongStream.Feel), songUnconventionality[Facet.Feel]);
        StateTrace.Record(TracePoints.Feel, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{feel}", feel);
        var songStateMap = CreateSongStateMap(Stream(SongStream.SongState), unconventionality, rhythmicUnconventionality)
            .MergeWith(Feels.At(StateDepths.Song, feel));
        // how busy the melody is, which a section moves
        var melodyBusyness = MelodyBusyness.Generate(Stream(SongStream.Melody));

        // the song's form: one of the forms songs are written in, its sections playing their roles, or one of its own
        // the meter the song's bars are in
        var meter = overrides.Meter ?? Meter.Draw(Stream(SongStream.Meter), songUnconventionality[Facet.Feel], overrides.MeterOption);
        StateTrace.Record(TracePoints.Meter, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, meter.ToString(), meter);

        var structure = SongForms.Generate(Stream(SongStream.SongForm), Stream(SongStream.Structure), songUnconventionality[Facet.Form]);
        StateTrace.Record(TracePoints.SongForm, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(" ", structure.SectionIds.Select(x => structure.Roles[x])), structure);
        var sectionIds = structure.SectionIds.ToArray();

        // how loud and busy each section is meant to be, from the song's and where and how often the section plays
        var dynamicsContext = Stream(SongStream.Dynamics);
        songStateMap = songStateMap.MergeWith(SectionEnergy.GenerateSong(dynamicsContext));
        var sectionEnergies = SectionEnergy.GenerateSections(dynamicsContext, sectionIds);

        // the song's key, which the sections place their melodies in
        var commonStateMap = CreateCommonStateMap(Stream(SongStream.Common), overrides);
        // how the song swings, which its tempo sets the notes of
        var grooveContext = Stream(SongStream.Groove);
        var swing = Groove.Generate(grooveContext, commonStateMap.GetStateValue(StateKinds.Tempo), songUnconventionality[Facet.Feel], meter);
        StateTrace.Record(TracePoints.Swing, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, $"{swing.Delay:F3} of {swing.Period}", swing);

        var sectionGenerator = new SectionGenerator(
            root,
            Seeds.Derive(seed, (int)SongStream.Sections),
            progressionSettings,
            tracks,
            songUnconventionality,
            melodyBusyness,
            scale,
            songStateMap,
            sectionEnergies,
            PercussionSections.GenerateSong(Stream(SongStream.Percussion)),
            commonStateMap.GetStateValue(StateKinds.KeyOffset),
            absent
        );
        // how the song starts and ends around its sections, decided before them: the one the song ends with leads home
        // to the tonic, where the ending lands
        var formGenerator = new SongFormGenerator(
            Stream(SongStream.Form),
            Stream(SongStream.Intro),
            Of(Facet.Form),
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role),
            !absent.Contains(TrackRole.Drum),
            meter
        );
        var plan = formGenerator.Plan(sectionIds);

        // every section is generated once, where it first plays
        var generateSection = ((Func<int, GeneratedSection>)(id => sectionGenerator.Generate(new SectionPlan(id, id == plan.TonicHomeSectionId, id == sectionIds[0], structure.Roles[id], meter))))
            .CacheGeneratedValues();
        // and its melody placed afresh every time it plays, varied from the first as far as the song improvises
        var improvisation = MelodyLayers.GenerateImprovisation(Stream(SongStream.MelodyImprovisation), songUnconventionality[Facet.Melody]);
        StateTrace.Record(TracePoints.MelodyImprovisation, SongTracks.MelodyTrack, 0, 0, StateMap.Default, 0, $"{improvisation:F2}", improvisation);
        // a song that fades out plays its last section once more, over which it fades, twice where it plays its pattern
        // only once, so that the fade takes eight bars at least
        var fading = plan.Ending != EndingKind.Fade ? 0 : generateSection(sectionIds[^1]).Plays == 1 ? 2 : 1;
        int[] played = [..sectionIds, ..Enumerable.Repeat(sectionIds[^1], fading)];
        // the later in the song, the more, and with the energy of its own place in the song against the section's average,
        // its parts only joining as it comes back
        double Place(int index) => played.Length > 1 ? index / (double)(played.Length - 1) : 0.5;
        var averagePlaces = played.Select((id, index) => (id, index)).GroupBy(x => x.id).ToDictionary(x => x.Key, x => x.Average(y => Place(y.index)));
        var restedBefore = new Dictionary<int, ImmutableHashSet<TrackRole>>();
        var solos = new Dictionary<int, SoloPlan>();
        var sections = new List<GeneratedSection>();
        for (var index = 0; index < played.Length; index++)
        {
            var id = played[index];
            // a later appearance played as a solo now and then, from a sequence of its own, by the section's facets
            var solo = Solos.Draw(new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.Solos), index)), index, generateSection(id).Facets, SongParts.All.Except(absent).ToArray());
            var section = generateSection(id).Appear(
                played.Take(index).Count(x => x == id),
                Tilt.Of(MelodyLayers.ImprovisationGrowth, Place(index)).Chance(improvisation, 1),
                SectionEnergy.AppearanceStep(Place(index), averagePlaces[id]),
                restedBefore.GetValueOrDefault(id, []),
                // an intro of entries builds the first section's parts up itself
                index == 0 && plan.Intro == IntroKind.Entries,
                solo
            );
            if (solo is not null)
            {
                solos[index] = solo;
                StateTrace.Record(TracePoints.Solo, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{string.Join(", ", solo.Soloists)} {solo.Mode}", (index, solo));
            }
            restedBefore[id] = section.Resting;
            StateTrace.Record(TracePoints.Arrangement, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, string.Join(", ", section.Resting.Order()), section.Resting);
            StateTrace.Record(TracePoints.Texture, SectionGenerator.SectionTrace, id, 0, StateMap.Default, 0, $"{section.TextureKind}", (index, section.TextureKind, section.PhraseSilent));
            sections.Add(section);
        }

        // the song put together as planned, and the lines the drums mark
        var form = formGenerator.Assemble(plan, played, sections);
        var songTrackNoteTimelineMap = form.Edits.ApplyTo(form.Blocks.Unroll());
        // a song may change key at a pattern's start: a middling one now and then going up for a last section that came
        // back before, as for a last chorus, or playing a section, a chorus the likelier, in a key of its own, and the
        // wilder ones anywhere, each place and each section drawing from a stream of its own
        var keyChanges = KeyChange.Generate(
            Stream(SongStream.KeyChange),
            place => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.KeyChange), place)),
            id => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.SectionKey), id)),
            songUnconventionality[Facet.Scale],
            form.Map,
            sectionIds,
            structure.Roles
        );
        // and play a section at a tempo of its own, a chorus the likelier, each section drawing from a stream of its own
        var sectionTempos = SectionTempo.Generate(
            id => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.SectionTempo), id)),
            songUnconventionality[Facet.Feel],
            sectionIds,
            structure.Roles
        );
        StateTrace.Record(TracePoints.SectionTempo, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, string.Join(", ", sectionTempos.Select(x => $"{x.Key} {x.Value:F2}")), sectionTempos);
        StateTrace.Record(TracePoints.KeyChange, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, keyChanges.IsEmpty ? "none" : string.Join(", ", keyChanges), keyChanges);
        songTrackNoteTimelineMap = songTrackNoteTimelineMap.MergeStateTimelineMap(form.SongState)
            .MergeStateTimelineMap(KeyChange.ToStateTimelineMap(keyChanges, form.Map.Duration))
            .MergeStateTimelineMap(SectionTempo.ToStateTimelineMap(sectionTempos, form.Map, form.Map.Duration))
            .MergeStateMap(commonStateMap)
            .MergeStateMap(Groove.ToStateMap(swing, grooveContext));

        // the drums mark the lines, now that the song is put together
        var fills = new FillGenerator(Stream(SongStream.Fills), tracks, Of(Facet.Fills), meter);
        songTrackNoteTimelineMap = fills.Generate(songTrackNoteTimelineMap, form.Lines, form.Map);

        // the lines placed over the whole song, as they go on from section to section and lead into the next: the melody,
        // and the bass
        var melodyTrack = tracks.Definitions.Single(x => x.Value.Role == TrackRole.Melody);
        songTrackNoteTimelineMap = LinePattern.Place(songTrackNoteTimelineMap, melodyTrack.Key, (PitchInstrumentTrack)melodyTrack.Value, MelodyLayers.Line);
        var bassTrack = tracks.Definitions.Single(x => x.Value.Role == TrackRole.Bass);
        songTrackNoteTimelineMap = LinePattern.Place(songTrackNoteTimelineMap, bassTrack.Key, (PitchInstrumentTrack)bassTrack.Value, BassLeadingLayers.Line);
        // and the bass walking with the drums' runs into a new section now and then, from where it is placed
        songTrackNoteTimelineMap = BassFills.Apply(songTrackNoteTimelineMap, bassTrack.Key, (PitchInstrumentTrack)bassTrack.Value, fills.Runs, Stream(SongStream.BassFills), form.Map.MeterAt);
        var counterTrack = tracks.Definitions.Single(x => x.Value.Role == TrackRole.CounterMelody);
        songTrackNoteTimelineMap = LinePattern.Place(songTrackNoteTimelineMap, counterTrack.Key, (PitchInstrumentTrack)counterTrack.Value, CounterLayers.Line);
        var riffTrack = tracks.Definitions.Single(x => x.Value.Role == TrackRole.Riff);
        songTrackNoteTimelineMap = LinePattern.Place(songTrackNoteTimelineMap, riffTrack.Key, (PitchInstrumentTrack)riffTrack.Value, RiffLayers.Line);
        // and a line doubled by another part in a section now and then, each pair and section from a sequence of its own
        songTrackNoteTimelineMap = LineDoubling.Apply(
            songTrackNoteTimelineMap,
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role),
            form.Map,
            (pair, section) => new GenerationContext(Seeds.Derive(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.LineDoubling), pair), section))
        );
        // and every pitched solo's line played by its soloists
        songTrackNoteTimelineMap = Solos.Apply(
            songTrackNoteTimelineMap,
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role),
            form.Map,
            solos,
            index => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.SoloLines), index))
        );

        // and last the notes, decided from the state of the whole song, in its order, none sounding into a stop
        var notes = form.Edits.CutNotes(
            Realizer.Realize(tracks.Definitions, songTrackNoteTimelineMap, form.Map.MeterAt),
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role)
        );
        // and the parts' instruments over the song, switched and articulated by every section's sound facet
        notes = InstrumentsOverTime.Apply(
            notes,
            tracks.Definitions,
            form.Map,
            [..sections.Select(x => x.Facets[Facet.Sound])],
            (section, track) => new GenerationContext(Seeds.Derive(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.InstrumentsOverTime), section), track))
        );
        // and how the parts are played beyond their notes, by every section's sound facet
        notes = Expression.Apply(
            notes,
            tracks.Definitions,
            form.Map,
            songUnconventionality[Facet.Sound],
            [..sections.Select(x => x.Facets[Facet.Sound])],
            track => new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.PartSound), track)),
            (section, track) => new GenerationContext(Seeds.Derive(Seeds.Derive(Seeds.Derive(seed, (int)SongStream.Expression), section), track))
        );

        return new Song(songTrackNoteTimelineMap.Duration, meter, tracks.Definitions, songTrackNoteTimelineMap, form.Map, notes, new SongDraws(songUnconventionality, [..SongParts.All.Except(absent)], tracks.DrumSetup));
    }

    /// <summary>The random sequence a stage of the song of the given seed draws from.</summary>
    internal static IGenerationContext CreateStream(ulong seed, SongStream stream)
    {
        return new GenerationContext(Seeds.Derive(seed, (int)stream));
    }

    /// <summary>A stream's own sequence for a facet of the song's unconventionality.</summary>
    internal static IGenerationContext CreateStream(ulong seed, SongStream stream, Facet facet)
    {
        return new GenerationContext(Seeds.Derive(Seeds.Derive(seed, (int)stream), (int)facet));
    }

    /// <summary>
    ///     The song's layer of the generation state, which every section starts from: its rhythm, its note walk, and
    ///     the first entries of the chord pool, which the song's chords make the likeliest.
    /// </summary>
    private static StateMap CreateSongStateMap(
        IGenerationContext context,
        HarmonicUnconventionality unconventionality,
        RhythmicUnconventionality rhythmicUnconventionality
    )
    {
        return new StateMapBuilder("Song")
            .AddRhythmLayer(rhythmicUnconventionality.Lean(RhythmLayers.Song))
            .Add(CompositionStateKinds.Rhythm.MaxRank, 2)
            .Add(CompositionStateKinds.Rhythm.Fullness, RhythmSettings.Fullness)
            .Add(CompositionStateKinds.Rhythm.Variation, RhythmSettings.Variation)
            .AddNoteWalkLayer()
            .Add(CompositionStateKinds.ChordPool.Collection, LayerStates.CreateChordPool(unconventionality))
            .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
            .ToStateMap(context);
    }

    /// <summary>
    ///     The song's state that Render reads, the same for every track: its key and tempo. Its scale is each section's,
    ///     most often the song's.
    /// </summary>
    /// <summary>The song's key and tempo, drawn, and replaced where given, so that giving one leaves every other draw as it was.</summary>
    private static StateMap CreateCommonStateMap(IGenerationContext context, SongOverrides overrides)
    {
        var drawn = new StateMapBuilder("Song")
            .Add(StateKinds.KeyOffset, Generators.Int(0, 12))
            .Add(StateKinds.Tempo, TempoGenerator)
            .AddNoteDurationLayer()
            .ToStateMap(context);
        if (overrides.Key is { } key)
            drawn = drawn.With(StateKinds.KeyOffset, key);
        return overrides.Tempo is { } tempo ? drawn.With(StateKinds.Tempo, TempoOptions[tempo]) : drawn;
    }

    /// <summary>The tempos a song may play at, as multiples of <see cref="Meter.BaseTempo" />, slowest first: every one the draw can land on.</summary>
    public static ImmutableArray<double> TempoOptions { get; } =
    [
        ..Enumerable.Range(0, 5)
            .SelectMany(rank => DyadicRankDistribution.GetMultiplierDistributionSlice(rank, 0.75, 1.5))
            .Select(x => x.Position)
            .Distinct()
            .Order()
    ];
}

/// <summary>How the state along a section's 4-bar pattern changes. The steps are in bars.</summary>
/// <param name="ChordShapeStep">How often the chord shape of the progression can change.</param>
/// <param name="NoteStateStep">How often the velocity and note duration of the progression can change.</param>
/// <param name="PoolSize">How many values each state picks from along the pattern; 0 draws a new value every step.</param>
internal sealed record ProgressionSettings(double ChordShapeStep, double NoteStateStep, int PoolSize)
{
    public static ProgressionSettings Default { get; } = new(1, 1, 4);
}

/// <summary>
///     The random sequences of a song's stages, each derived from the song's seed by its number, which must stay as it
///     is: a new stage takes a new number.
/// </summary>
internal enum SongStream
{
    Rhythm = 1,
    Tracks = 2,
    Harmony = 3,
    SongState = 4,
    Melody = 5,
    Common = 6,
    Structure = 7,
    Sections = 8,
    Form = 9,
    Fills = 10,
    Dynamics = 11,
    Percussion = 12,
    DrumStrokes = 13,
    DrumRoles = 14,
    DrumSetup = 15,
    MelodyImprovisation = 16,
    Intro = 17,
    Groove = 18,
    Panning = 19,
    SongForm = 20,
    Pad = 21,
    KeyChange = 22,
    CounterMelody = 23,
    BassFills = 24,
    Meter = 25,
    Unconventionality = 26,
    Feel = 27,
    Parts = 28,
    Riff = 29,
    LineDoubling = 30,
    Solos = 31,
    SoloLines = 32,
    InstrumentsOverTime = 33,
    Expression = 34,
    PartSound = 35,
    SectionKey = 36,
    RhythmPart = 37,
    SectionTempo = 38
}
