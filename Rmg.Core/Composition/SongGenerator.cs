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
        return GenerateSong(Random.Shared.Next());
    }

    /// <summary>
    ///     Generates a song. The same seed always results in the same song in this version; changes to the generator
    ///     may turn a seed into a different song, which is accepted to keep the generator simple.
    /// </summary>
    public static Song GenerateSong(int seed)
    {
        return GenerateSong(seed, ProgressionSettings.Default);
    }

    internal static Song GenerateSong(int seed, ProgressionSettings progressionSettings)
    {
        // every stage draws from its own random sequence, derived from the seed by the stage, so a change to what one
        // stage draws leaves what the others draw as it was
        var root = new GenerationContext(seed);
        IGenerationContext Stream(SongStream stream) => CreateStream(seed, stream);

        // how far the rhythm strays from convention, which every rhythm layer from the tracks' own on is scaled by
        var rhythmicUnconventionality = RhythmicUnconventionality.Generate(Stream(SongStream.Rhythm));
        var tracks = SongTracks.Create(
            Stream(SongStream.Tracks),
            rhythmicUnconventionality,
            Stream(SongStream.DrumStrokes),
            Stream(SongStream.DrumRoles),
            DrumSetups.Pick(Stream(SongStream.DrumSetup), rhythmicUnconventionality.Tilt)
        );
        StateTrace.Record(TracePoints.DrumSetup, FillGenerator.DrumsTrace, 0, 0, StateMap.Default, 0, tracks.DrumSetup.ToString(), tracks.DrumSetup);

        // the song's chords gather around its unconventionality, and a section's around its own shift of it
        var harmonyContext = Stream(SongStream.Harmony);
        var unconventionality = HarmonicUnconventionality.Generate(harmonyContext);
        var scale = Scales.Pick(harmonyContext);
        var songStateMap = CreateSongStateMap(Stream(SongStream.SongState), unconventionality, rhythmicUnconventionality);
        // how busy the melody is, which a section moves
        var melodyBusyness = MelodyBusyness.Generate(Stream(SongStream.Melody));

        var sectionIds = SongStructureGenerator.Generate(Stream(SongStream.Structure))
            .SelectMany(part => part.SectionIds)
            .ToArray();

        // how loud and busy each section is meant to be, from the song's and where and how often the section plays
        var dynamicsContext = Stream(SongStream.Dynamics);
        songStateMap = songStateMap.MergeWith(SectionEnergy.GenerateSong(dynamicsContext));
        var sectionEnergies = SectionEnergy.GenerateSections(dynamicsContext, sectionIds);

        // the song's key, which the sections place their melodies in
        var commonStateMap = CreateCommonStateMap(Stream(SongStream.Common));

        var sectionGenerator = new SectionGenerator(
            root,
            Seeds.Derive(seed, (int)SongStream.Sections),
            progressionSettings,
            tracks,
            unconventionality,
            rhythmicUnconventionality,
            melodyBusyness,
            scale,
            songStateMap,
            sectionEnergies,
            PercussionSections.GenerateSong(Stream(SongStream.Percussion)),
            commonStateMap.GetStateValue(StateKinds.KeyOffset)
        );
        // how the song starts and ends around its sections, decided before them: the one the song ends with leads home
        // to the tonic, where the ending lands
        var formGenerator = new SongFormGenerator(
            Stream(SongStream.Form),
            Stream(SongStream.Intro),
            rhythmicUnconventionality,
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role)
        );
        var plan = formGenerator.Plan(sectionIds);

        // every section is generated once, where it first plays
        var generateSection = ((Func<int, GeneratedSection>)(id => sectionGenerator.Generate(new SectionPlan(id, id == plan.TonicHomeSectionId, id == sectionIds[0]))))
            .CacheGeneratedValues();
        // and its melody placed afresh every time it plays, varied from the first as far as the song improvises
        var improvisation = MelodyLayers.GenerateImprovisation(Stream(SongStream.MelodyImprovisation), rhythmicUnconventionality.Tilt);
        StateTrace.Record(TracePoints.MelodyImprovisation, SongTracks.MelodyTrack, 0, 0, StateMap.Default, 0, $"{improvisation:F2}", improvisation);
        // a song that fades out plays its last section once more, over which it fades
        int[] played = plan.Ending == EndingKind.Fade ? [..sectionIds, sectionIds[^1]] : sectionIds;
        var sections = played.Select((id, index) => generateSection(id).Appear(played.Take(index).Count(x => x == id), improvisation)).ToArray();

        // the song put together as planned, and the lines the drums mark
        var form = formGenerator.Assemble(plan, played, sections);
        var songTrackNoteTimelineMap = form.Edits.ApplyTo(form.Blocks.Unroll());
        songTrackNoteTimelineMap = songTrackNoteTimelineMap.MergeStateTimelineMap(form.SongState).MergeStateMap(commonStateMap);

        // the drums mark the lines, now that the song is put together
        songTrackNoteTimelineMap = new FillGenerator(Stream(SongStream.Fills), tracks, rhythmicUnconventionality)
            .Generate(songTrackNoteTimelineMap, form.Lines, form.Map);

        // the lines placed over the whole song, as they go on from section to section and lead into the next: the melody,
        // and the bass
        var melodyTrack = tracks.Definitions.Single(x => x.Value.Role == TrackRole.Melody);
        songTrackNoteTimelineMap = LinePattern.Place(songTrackNoteTimelineMap, melodyTrack.Key, (PitchInstrumentTrack)melodyTrack.Value, MelodyLayers.Line);
        var bassTrack = tracks.Definitions.Single(x => x.Value.Role == TrackRole.Bass);
        songTrackNoteTimelineMap = LinePattern.Place(songTrackNoteTimelineMap, bassTrack.Key, (PitchInstrumentTrack)bassTrack.Value, BassLeadingLayers.Line);

        // and last the notes, decided from the state of the whole song, in its order, none sounding into a stop
        var notes = form.Edits.CutNotes(
            Realizer.Realize(tracks.Definitions, songTrackNoteTimelineMap),
            tracks.Definitions.ToDictionary(x => x.Key, x => x.Value.Role)
        );

        return new Song(songTrackNoteTimelineMap.Duration, tracks.Definitions, songTrackNoteTimelineMap, form.Map, notes);
    }

    /// <summary>The random sequence a stage of the song of the given seed draws from.</summary>
    internal static IGenerationContext CreateStream(int seed, SongStream stream)
    {
        return new GenerationContext(Seeds.Derive(seed, (int)stream));
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
    private static StateMap CreateCommonStateMap(IGenerationContext context)
    {
        return new StateMapBuilder("Song")
            .Add(StateKinds.KeyOffset, Generators.Int(0, 12))
            .Add(StateKinds.Tempo, TempoGenerator)
            .AddNoteDurationLayer()
            .ToStateMap(context);
    }
}

/// <summary>How the state along a section's 4-bar pattern changes. The steps are in beats; a bar is 4 beats.</summary>
/// <param name="ChordShapeStep">How often the chord shape of the progression can change.</param>
/// <param name="NoteStateStep">How often the velocity and note duration of the progression can change.</param>
/// <param name="PoolSize">How many values each state picks from along the pattern; 0 draws a new value every step.</param>
internal sealed record ProgressionSettings(double ChordShapeStep, double NoteStateStep, int PoolSize)
{
    public static ProgressionSettings Default { get; } = new(4, 4, 4);
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
    Intro = 17
}
