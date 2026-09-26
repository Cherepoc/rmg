using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The state that the layers of a song, from the song down to a bar, each draw a part of: the rhythm settings, the
///     note-to-note walk of the pitch offsets, and the note durations. Every layer draws the same states with the same
///     generators, so they are drawn here, in one order, which the song's seed depends on.
/// </summary>
internal static class LayerStates
{
    public static readonly Func<IGenerationContext, double> QuarterNoteDurationPower = Generators.SplineValue();
    public static readonly Func<IGenerationContext, double> NextNoteDurationFactor = Generators.SplineValue();

    /// <summary>How far the walk moves from note to note, and how far a note strays from it at random.</summary>
    public static readonly Func<IGenerationContext, double> WalkConsecutiveOffset = Generators.SplineValue();
    public static readonly Func<IGenerationContext, double> WalkRandomOffset = Generators.SplineValue();

    /// <summary>Which entry of the chord pool a layer moves the pick to: 0 mostly, then 1 either way, then 2.</summary>
    public static readonly Func<IGenerationContext, int> ChordPoolIndex =
        Generators.Rank(WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.5), -2, 2);

    /// <summary>The chords a layer adds to the pool: two, of its unconventionality.</summary>
    public static Func<IGenerationContext, ImmutableArray<Chord>> CreateChordPool(HarmonicUnconventionality unconventionality)
    {
        return Generators.Sequence(unconventionality.GenerateChord, 2);
    }

    /// <summary>
    ///     A layer's steps of the rhythm settings. The period and the phase make the groove, the speed changing more
    ///     often and the tuplet period by a chance of its own, and the max rank and the rank offset how busy it is; the
    ///     phase's offset among the beats of its rank, and how full the pattern is and how often its cycles change,
    ///     are drawn around 0 in every layer.
    /// </summary>
    public static StateMapBuilder AddRhythmLayer(this StateMapBuilder builder, RhythmLayer layer)
    {
        return builder
            .Add(CompositionStateKinds.Rhythm.Period.Power, layer.CreateSpeedGenerator())
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, layer.CreateTupletGenerator())
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, layer.CreateGrooveGenerator())
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, Generators.SplineValue())
            .Add(CompositionStateKinds.Rhythm.MaxRank, layer.CreateDensityGenerator())
            .Add(CompositionStateKinds.Rhythm.RankOffset, layer.CreateDensityGenerator())
            .Add(CompositionStateKinds.Rhythm.Fullness, layer.CreateFullnessGenerator())
            .Add(CompositionStateKinds.Rhythm.Variation, layer.CreateVariationGenerator());
    }

    /// <summary>A layer's part of the note-to-note walk of the articulation, the chord root and the chord note.</summary>
    public static StateMapBuilder AddNoteWalkLayer(this StateMapBuilder builder)
    {
        return builder
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, WalkConsecutiveOffset)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, WalkRandomOffset)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, WalkConsecutiveOffset)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, WalkRandomOffset)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, WalkConsecutiveOffset)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, WalkRandomOffset);
    }

    /// <summary>A layer's part of the note durations.</summary>
    public static StateMapBuilder AddNoteDurationLayer(this StateMapBuilder builder)
    {
        return builder
            .Add(StateKinds.QuarterNoteDurationPower, QuarterNoteDurationPower)
            .Add(StateKinds.NextNoteDurationFactor, NextNoteDurationFactor);
    }

    /// <summary>
    ///     A layer that only some tracks see: a track's own for the song or a section, or the drums'. It adds its
    ///     rhythm, walk, velocity and duration to the given state, and may not set what every track shares.
    /// </summary>
    public static StateMap CreateTrackLayer(
        IGenerationContext context,
        string layer,
        StateMap stateMap,
        double velocityWeight,
        RhythmLayer rhythmLayer
    )
    {
        return new StateMapBuilder(layer, perTrack: true)
            .Add(stateMap)
            .AddRhythmLayer(rhythmLayer)
            .AddNoteWalkLayer()
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(velocityWeight))
            .AddNoteDurationLayer()
            .ToStateMap(context);
    }
}
