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
        // the stages draw from one random sequence, so their order is part of what a seed makes
        var context = new GenerationContext(seed);

        // how far the rhythm strays from convention, which every rhythm layer from the tracks' own on is scaled by
        var rhythmicUnconventionality = RhythmicUnconventionality.Generate(context);
        var tracks = SongTracks.Create(context, rhythmicUnconventionality);

        // the song's chords gather around its unconventionality, and a section's around its own shift of it
        var unconventionality = HarmonicUnconventionality.Generate(context);
        var scale = Scales.Pick(context);
        var songStateMap = CreateSongStateMap(context, unconventionality, rhythmicUnconventionality);

        var sectionGenerator = new SectionGenerator(
            context,
            progressionSettings,
            tracks,
            unconventionality,
            rhythmicUnconventionality,
            scale,
            songStateMap
        );
        var generateSection = ((Func<int, TrackEventStateTimelineMap<StateMap>>)sectionGenerator.Generate).CacheGeneratedValues();

        var commonStateMap = CreateCommonStateMap(context, scale);

        var songTrackNoteTimelineMap = SongStructureGenerator.Generate(context)
            .SelectMany(part => part.SectionIds)
            .Select(generateSection)
            .Unroll()
            .MergeStateMap(commonStateMap);

        return new Song(songTrackNoteTimelineMap.Duration, tracks.Definitions, songTrackNoteTimelineMap);
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
            .AddRhythmLayer(rhythmicUnconventionality.Scale(RhythmLayers.Song))
            .Add(CompositionStateKinds.Rhythm.MaxRank, 2)
            .Add(CompositionStateKinds.Rhythm.Fullness, RhythmSettings.Fullness)
            .Add(CompositionStateKinds.Rhythm.Variation, RhythmSettings.Variation)
            .AddNoteWalkLayer()
            .Add(CompositionStateKinds.ChordPool.Collection, LayerStates.CreateChordPool(unconventionality))
            .Add(CompositionStateKinds.ChordPool.Index, LayerStates.ChordPoolIndex)
            .ToStateMap(context);
    }

    /// <summary>The song's state that Render reads, the same for every track: its scale, key and tempo.</summary>
    private static StateMap CreateCommonStateMap(IGenerationContext context, Scale scale)
    {
        return new StateMapBuilder("Song")
            .Add(StateKinds.ScaleOffsets, scale.Offsets)
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
