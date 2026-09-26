using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     The notes of the tracks of a section, bar by bar. A section's 4-bar pattern gives every track four bars, each
///     picked from four seeded bar patterns, so bars come back; a bar pattern has its own rhythm and its notes' state,
///     and takes the chord of the bar it plays in.
/// </summary>
internal sealed class PatternGenerator
{
    private const double BarDuration = 4;
    private const int BarPatternPoolSize = 4;

    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    private static readonly Func<IGenerationContext, double> ArticulationOffset = Generators.SplineValue();
    private static readonly Func<IGenerationContext, double> ChordRootNoteOffset = Generators.SplineValue();
    private static readonly Func<IGenerationContext, double> ChordNoteOffset = Generators.SplineValue();
    private static readonly Func<IGenerationContext, double> PatternChordNoteOffset = Generators.SplineValue();

    private readonly IGenerationContext _context;
    private readonly ImmutableSortedDictionary<int, IInstrumentTrack> _trackDefinitions;

    // the rhythm of a bar pattern, by its resolved settings, which many bar patterns share
    private readonly Func<StateMap, DyadicRankThresholdPattern> _rhythmPatternGenerator;

    // a bar pattern's own layer, drawn for every track and bar
    private readonly Func<IGenerationContext, StateMap> _barPatternLayerGenerator = new StateMapBuilder("Bar pattern", perTrack: true)
        .AddRhythmLayer(RhythmLayers.BarPattern)
        .AddNoteWalkLayer()
        .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.BarPattern))
        .AddNoteDurationLayer()
        // no chord root offset here: every track plays the progression's chord, and a track leaves it only by moving
        // its root from note to note
        .ToStateMapGenerator();

    public PatternGenerator(IGenerationContext context, ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions)
    {
        _context = context;
        _trackDefinitions = trackDefinitions;
        _rhythmPatternGenerator = ((Func<StateMap, DyadicRankThresholdPattern>)GenerateRhythmPattern)
            .CacheGeneratedValues()
            .MapInput((StateMap stateMap) => ResolveRhythm(stateMap));
    }

    /// <summary>
    ///     The four bars of the tracks' pattern in a section. Four sets of seeds are drawn, one seed per track, and
    ///     every bar takes one of them, so the tracks change their patterns together and bars come back.
    /// </summary>
    /// <param name="trackStateMaps">Every track's state in the section.</param>
    /// <param name="barStateTimelineMap">The state that changes by bar, such as the chord, along the 4-bar pattern.</param>
    public TrackEventStateTimelineMap<StateMap> GenerateBars(
        int sectionId,
        ImmutableDictionary<int, StateMap> trackStateMaps,
        StateTimelineMap barStateTimelineMap
    )
    {
        var trackSeedMapGenerator = (IGenerationContext innerContext) =>
            trackStateMaps.Keys.ToDictionary(x => x, _ => SeedGenerator(innerContext));
        var trackSeedMaps = Generators.Sequence(trackSeedMapGenerator, BarPatternPoolSize)(_context);
        var trackSeedMapSelector = Generators.ItemSelector(trackSeedMaps);
        return Generators.Sequence(trackSeedMapSelector, Progressions.BarCount)(_context)
            .Select((trackSeedMap, barIndex) =>
                {
                    var trackNotePatterns = trackSeedMap.Select(x =>
                        new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                            x.Key,
                            GenerateBar(x.Key, x.Value, trackStateMaps[x.Key], barStateTimelineMap, sectionId, barIndex)
                        )
                    );
                    return TrackEventStateTimelineMap.Create(BarDuration, trackNotePatterns, StateTimelineMap.Create(BarDuration));
                }
            )
            .Unroll();
    }

    /// <summary>A track's bar: its bar pattern's state over the track's, and the notes of its rhythm.</summary>
    private EventStateTimelineMap<StateMap> GenerateBar(
        int trackNumber,
        int seed,
        StateMap trackStateMap,
        StateTimelineMap barStateTimelineMap,
        int sectionId,
        int barIndex
    )
    {
        var patternSeeds = CreatePatternSeeds(seed);
        var trackGenerationContext = _context.CreateContext(patternSeeds.TrackState);
        var stateMap = new StateMapBuilder("Bar pattern", perTrack: true)
            .Add(_barPatternLayerGenerator)
            .Add(CompositionStateKinds.Rhythm.Seed, patternSeeds.Rhythm)
            .Add(CompositionStateKinds.ValueSeed, seed)
            .ToStateMap(trackGenerationContext)
            .MergeWith(trackStateMap)
            .MergeWith(CreatePatternChordNoteOffset(_trackDefinitions[trackNumber], trackStateMap, trackGenerationContext));
        StateTrace.Record("Bar pattern", trackNumber, sectionId, barIndex, stateMap);

        var notePattern = GenerateNotes(stateMap, barStateTimelineMap, barIndex * BarDuration, trackNumber, sectionId, barIndex);
        return notePattern.GeneratedTimeline.ToEventStateTimelineMap(stateMap.OfScope(StateScope.Render));
    }

    /// <summary>
    ///     The notes of a bar pattern: one on every beat its rhythm plays, each with its offsets walked on from the note
    ///     before, its velocity accented by how strong its beat is, and the chord of the bar at its position.
    /// </summary>
    /// <param name="barStateTimelineMap">The state along the 4-bar pattern, in which this bar starts at patternStart.</param>
    private DyadicRankItemPattern<StateMap> GenerateNotes(
        StateMap stateMap,
        StateTimelineMap barStateTimelineMap,
        double patternStart,
        int trackNumber,
        int sectionId,
        int barIndex
    )
    {
        var rhythmPattern = _rhythmPatternGenerator(stateMap);

        var patternSeeds = CreatePatternSeeds(stateMap.GetStateValue(CompositionStateKinds.ValueSeed));
        var changingContext = _context.CreateContext(patternSeeds.StateChanges);
        var changingStateTimelineMap = Generators.SequentialTimeline(CreateBeatLayerGenerator(stateMap), 1, 4)(changingContext)
            .ToStateTimelineMap();

        var articulationOffsetGenerator = changingStateTimelineMap.ToIncrementalGenerator(
            CompositionStateKinds.IncrementalArticulationOffset,
            ArticulationOffset
        );
        var chordRootNoteOffsetGenerator = changingStateTimelineMap.ToIncrementalGenerator(
            CompositionStateKinds.IncrementalChordRootNoteOffset,
            ChordRootNoteOffset
        );
        var chordNoteOffsetGenerator = changingStateTimelineMap.ToIncrementalGenerator(
            CompositionStateKinds.IncrementalChordNoteOffset,
            ChordNoteOffset
        );

        var isMelody = trackNumber == SongTracks.MelodyTrack;
        // the bar pattern's seed names its motif
        var motif = stateMap.GetStateValue(CompositionStateKinds.ValueSeed);
        var stepwiseness = stateMap.GetStateValue(CompositionStateKinds.MelodyStepwiseness);
        var noteStateMapGenerator = (IGenerationContext innerContext, double position, int rank) =>
        {
            var builder = new StateMapBuilder("Note", perTrack: true)
                .Add(StateKinds.ArticulationOffset, articulationOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordRootNoteOffset, chordRootNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordNoteOffset, chordNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.Velocity, BeatAccent.CreateVelocityGenerator(rank, rhythmPattern.MaxRank).Then(x => x * VelocityLayers.Note))
                .AddNoteDurationLayer()
                .Add(StateKinds.BeatRank, rank)
                .Add(GetChord(stateMap, barStateTimelineMap, patternStart, position, trackNumber, sectionId, barIndex));
            // a melody note draws where it means to go from the bar pattern's own sequence, so the bar's shape comes
            // back with the bar
            if (isMelody)
                builder
                    .Add(StateKinds.MelodyStep, context => MelodyLayers.GenerateStep(context, stepwiseness))
                    .Add(StateKinds.MelodyMotif, motif);
            return builder.ToStateMap(innerContext);
        };

        return DyadicRankItemPattern<StateMap>.Create(
            _context,
            rhythmPattern,
            innerContext => (position, rank) => noteStateMapGenerator(innerContext, position, rank),
            patternSeeds.NoteValues
        );
    }

    /// <summary>
    ///     The chord at a note, as the state Render reads: the bar's own chord if it has a role in the phrase, and the
    ///     chord pool's pick otherwise. The chord can change within the pattern, so each note takes it at its position.
    /// </summary>
    private static StateMap GetChord(
        StateMap stateMap,
        StateTimelineMap barStateTimelineMap,
        double patternStart,
        double position,
        int trackNumber,
        int sectionId,
        int barIndex
    )
    {
        var barStateMap = barStateTimelineMap.GetEffectiveStateMapAt(patternStart + position);
        var chordStateMap = stateMap.MergeWith(barStateMap.Subset([CompositionStateKinds.ChordPool.Index]));
        // the chord at the note: its shape from the pool and index, and its root from the section's home and the
        // progression
        if (StateTrace.IsRunning)
            StateTrace.Record(
                "Chord",
                trackNumber,
                sectionId,
                barIndex,
                stateMap
                    .Subset([CompositionStateKinds.ChordPool.Collection, CompositionStateKinds.ChordPool.Index, StateKinds.ChordRootNoteOffset])
                    .MergeWith(
                        barStateMap.Subset([CompositionStateKinds.ChordPool.Index, StateKinds.ChordRootNoteOffset, CompositionStateKinds.RoleChord])
                    ),
                position
            );

        var roleChord = barStateMap.GetStateValue(CompositionStateKinds.RoleChord);
        var chord = roleChord.IsEmpty ? CompositionStateKinds.ChordPool.Pick(chordStateMap) : roleChord[0];
        return StateMap.FromStates(
            [
                StateKinds.ChordNotePitchOffsets.CreateState(chord.Heights),
                StateKinds.ChordVoicingFixed.CreateState(chord.IsVoicingFixed ? 1 : 0)
            ]
        );
    }

    /// <summary>The state of the walk that changes by beat along a bar pattern, over the pattern's own.</summary>
    private static Func<IGenerationContext, StateMap> CreateBeatLayerGenerator(StateMap stateMap)
    {
        return new StateMapBuilder("Beat", perTrack: true)
            .AddNoteWalkLayer()
            .Add(
                stateMap.Subset(
                    [
                        ..CompositionStateKinds.IncrementalArticulationOffset.GetAll(),
                        ..CompositionStateKinds.IncrementalChordRootNoteOffset.GetAll(),
                        ..CompositionStateKinds.IncrementalChordNoteOffset.GetAll()
                    ]
                )
            )
            .ToStateMapGenerator();
    }

    /// <summary>The rhythm of a bar pattern, by its resolved settings: its period, phase, ranks and seed.</summary>
    private DyadicRankThresholdPattern GenerateRhythmPattern(StateMap stateMap)
    {
        var period = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.Value) * BarDuration;
        var phase = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Value) * BarDuration;
        var maxRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.MaxRank);
        var seed = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Seed);
        var rankOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.RankOffset);

        return DyadicRankThresholdPattern.Create(
            _context,
            seed,
            WeightUtil.CreateGeometricRankWeightFunc(rankOffset, 0, 1.0, 0.5),
            new DyadicTimelineDescriptor(BarDuration, period, phase, maxRank)
        );
    }

    /// <summary>
    ///     The rhythm settings of a bar pattern, which its layers added up, folded into their ranges and turned into a
    ///     period and a phase.
    /// </summary>
    private StateMap ResolveRhythm(StateMap stateMap)
    {
        var rankOffsetState = stateMap.GetState(CompositionStateKinds.Rhythm.RankOffset);
        var seedValueState = stateMap.GetState(CompositionStateKinds.Rhythm.Seed);

        var maxRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.MaxRank)
            .BounceInBounds(0, 2);

        var periodPower = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.Power)
            .BounceInBounds(-2, 1);
        var periodPrimeMultiplier = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.PrimeIndex)
            .BounceInBounds(-RhythmPeriod.MaxPrimeIndex, RhythmPeriod.MaxPrimeIndex)
            .ToRhythmPeriodValue();
        var periodValue = Math.Pow(2, periodPower) * periodPrimeMultiplier;

        var phaseRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Rank)
            .BounceInBounds(0, 2);
        var phaseRankedOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.RankedOffset)
            .BounceInBounds(-1, 1);
        var phaseValue = DyadicRankDistribution.GetHalfOffset(phaseRank, phaseRankedOffset) * periodValue;

        return new StateMapBuilder("Resolved rhythm", perTrack: true)
            .Add(CompositionStateKinds.Rhythm.Period.Value, periodValue)
            .Add(CompositionStateKinds.Rhythm.Phase.Value, phaseValue)
            .Add(CompositionStateKinds.Rhythm.MaxRank, maxRank)
            .Add(rankOffsetState.Map(x => x.BounceInBounds(0, maxRank)))
            .Add(seedValueState)
            .ToStateMap(_context);
    }

    /// <summary>The seeds of the random sequences that make up one pattern of a track.</summary>
    internal static PatternSeeds CreatePatternSeeds(int seed)
    {
        return new PatternSeeds(
            Seeds.Derive(seed, 0),
            Seeds.Derive(seed, 1),
            Seeds.Derive(seed, 2),
            Seeds.Derive(seed, 3)
        );
    }

    /// <summary>
    ///     Where in the chord a pitched track's bar pattern starts: the note-to-note walk of the pattern goes on from
    ///     there. It is scaled like that walk, so a track that plays whole chords (a zero multiplier) gets nothing and
    ///     keeps playing whole chords.
    /// </summary>
    private static StateMap CreatePatternChordNoteOffset(
        IInstrumentTrack trackDefinition,
        StateMap trackStateMap,
        IGenerationContext context
    )
    {
        if (trackDefinition is not PitchInstrumentTrack)
            return StateMap.Default;

        var multiplier = trackStateMap.GetStateValue(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier);
        if (multiplier.IsEqualToByEpsilon(0))
            return StateMap.Default;

        return StateMap.FromStates([StateKinds.ChordNoteOffset.CreateState([PatternChordNoteOffset(context) * multiplier])]);
    }
}

/// <param name="TrackState">The state a track draws for the pattern, such as its rhythm and offsets.</param>
/// <param name="Rhythm">Which beats of the rhythm play.</param>
/// <param name="StateChanges">How the note offsets change along the pattern.</param>
/// <param name="NoteValues">The values of the notes that play.</param>
internal readonly record struct PatternSeeds(int TrackState, int Rhythm, int StateChanges, int NoteValues);

internal static class IncrementalGenerators
{
    /// <summary>
    ///     A walk of an offset from note to note along a pattern: every note moves it on by the consecutive offset
    ///     and strays from it at random by the random offset, both scaled by the multiplier at its position; a zero
    ///     multiplier turns the offset off, which for the chord note means the whole chord plays. It remembers where
    ///     it is, so a walk is made for one pattern and its notes in order.
    /// </summary>
    public static Func<IGenerationContext, double, ImmutableArray<double>> ToIncrementalGenerator(
        this StateTimelineMap stateTimelineMap,
        CompositionStateKinds.IncrementalStateKinds stateKindGroup,
        Func<IGenerationContext, double> randomValueGenerator
    )
    {
        var currentValue = 0.0;
        return (innerContext, position) =>
        {
            var stateMap = stateTimelineMap.GetEffectiveStateMapAt(position);
            var multiplier = stateMap.GetStateValue(stateKindGroup.Multiplier);
            if (multiplier.IsEqualToByEpsilon(0))
                return [];

            var consecutiveOffset = stateMap.GetStateValue(stateKindGroup.ConsecutiveOffset) * multiplier;
            var randomOffset = stateMap.GetStateValue(stateKindGroup.RandomOffset) * multiplier;
            var randomValue = randomValueGenerator(innerContext);
            var value = currentValue + randomValue * randomOffset;
            currentValue += consecutiveOffset;
            return [value];
        };
    }
}
