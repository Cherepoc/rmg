using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

public static class SongGenerator
{
    private static readonly Func<int, double> HalfWeightRankGenerator =
        WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.5);

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
        var generationContext = new GenerationContext(seed);

        // track definitions

        var pitchInstrumentCodeGenerator = Generators.Int(0, 120).WithContext(generationContext);
        var minOctaveOffsetGenerator = Generators.Int(-2, 1).WithContext(generationContext);
        var maxOctaveOffsetGenerator = Generators.Int(0, 3).WithContext(generationContext);

        var closeToZeroIncrementalOffsetMultiplierGenerator = Generators.AbsSplineValue();
        var closeToOneIncrementalOffsetMultiplierGenerator = Generators.AbsSplineValue().Then(x => 1 - x);

        var quarterNoteDurationPowerGenerator = Generators.SplineValue();
        var nextNoteDurationFactorGenerator = Generators.SplineValue();
        var chordRootOffsetGenerator = Generators.SplineValue();

        var notePatternConsecutiveOffsetGenerator = Generators.SplineValue();
        var notePatternRandomOffsetGenerator = Generators.SplineValue();

        var articulationOffsetGenerator = Generators.SplineValue();
        var chordRootNoteOffsetGenerator = Generators.SplineValue();
        var chordNoteOffsetGenerator = Generators.SplineValue();
        var patternChordNoteOffsetGenerator = Generators.SplineValue();

        // the same state is drawn for a track for the whole song and again for each section, and for the drums as a group,
        // each with the velocity weight and the rhythm layer of its level
        var trackDefinitionStateMapGenerator = (StateMap stateMap, double velocityWeight, RhythmLayer rhythmLayer) => new StateMapBuilder()
            .Add(stateMap)
            .AddRhythmLayer(rhythmLayer)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(velocityWeight))
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .ToStateMap(generationContext);

        var trackDefinitions = new Dictionary<int, IInstrumentTrack>
        {
            // chords instrument
            [4] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new StateMapBuilder()
                        .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier, 0)
                        .Add(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier, 0)
                        .ToStateMap(generationContext),
                    VelocityLayers.Track,
                    RhythmLayers.Track
                ),
                pitchInstrumentCodeGenerator(),
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator()
            ),
            // melody instrument
            [5] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default, VelocityLayers.Track, RhythmLayers.Track),
                pitchInstrumentCodeGenerator(),
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator()
            ),
            // bass instrument
            [6] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new StateMapBuilder()
                        .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier, closeToZeroIncrementalOffsetMultiplierGenerator)
                        .Add(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier, closeToOneIncrementalOffsetMultiplierGenerator)
                        .ToStateMap(generationContext),
                    VelocityLayers.Track,
                    RhythmLayers.Track
                ),
                pitchInstrumentCodeGenerator(),
                -3,
                -2
            )
        };

        // the song has its own drums, and the drums picked by the section kit play in a section
        var songDrums = DrumKitGenerator.SelectSongDrums(generationContext);
        foreach (var drumGroup in DrumGroups.All)
        {
            foreach (var drum in drumGroup.Drums.Where(songDrums.Contains))
            {
                var drumStateMap = drum.ConfigureStateMap(drumGroup.ConfigureStateMap(new StateMapBuilder()))
                    .ToStateMap(generationContext);
                trackDefinitions[DrumGroups.GetTrackNumber(drum)] = new PercussionInstrumentTrack(
                    trackDefinitionStateMapGenerator(drumStateMap, VelocityLayers.Track, RhythmLayers.Track),
                    drum.ArticulationCodes
                );
            }
        }

        // the drums share the rhythm-related state
        ImmutableArray<TrackGroup> trackGroups =
        [
            new(
                [..songDrums.Select(DrumGroups.GetTrackNumber)],
                trackDefinitionStateMapGenerator(StateMap.Default, VelocityLayers.DrumGroup, RhythmLayers.DrumGroup)
            )
        ];

        var rhythmPatternGenerator = (StateMap stateMap) =>
        {
            const double duration = 4;
            var period = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.Value) * duration;
            var phase = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Value) * duration;
            var maxRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.MaxRank);
            var seed = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Seed);
            var rankOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.RankOffset);

            return DyadicRankThresholdPattern.Create(
                generationContext,
                seed,
                WeightUtil.CreateGeometricRankWeightFunc(rankOffset, 0, 1.0, 0.5),
                new DyadicTimelineDescriptor(duration, period, phase, maxRank)
            );
        };
        var cachedRhythmPatternGenerator = rhythmPatternGenerator
            .CacheGeneratedValues()
            .MapInput((StateMap stateMap) =>
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

                    return new StateMapBuilder()
                        .Add(CompositionStateKinds.Rhythm.Period.Value, periodValue)
                        .Add(CompositionStateKinds.Rhythm.Phase.Value, phaseValue)
                        .Add(CompositionStateKinds.Rhythm.MaxRank, maxRank)
                        .Add(rankOffsetState.Map(x => x.BounceInBounds(0, maxRank)))
                        .Add(seedValueState)
                        .ToStateMap(generationContext);
                }
            );

        var notePatternChangingStateMapGenerator = (StateMap stateMap) => new StateMapBuilder()
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
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
        // the bar state timeline covers the whole 4-bar pattern, and this pattern starts at patternStart in it
        var notePatternGenerator = (StateMap stateMap, StateTimelineMap barStateTimelineMap, double patternStart) =>
        {
            var rhythmPattern = cachedRhythmPatternGenerator(stateMap);

            var patternSeeds = CreatePatternSeeds(stateMap.GetStateValue(CompositionStateKinds.ValueSeed));
            var changingContext = generationContext.CreateContext(patternSeeds.StateChanges);
            var changingStateTimelineMap = Generators.SequentialTimeline(notePatternChangingStateMapGenerator(stateMap), 1, 4)(changingContext)
                .ToStateTimelineMap();

            var incrementalArticulationOffsetGenerator = changingStateTimelineMap.ToIncrementalGenerator(
                CompositionStateKinds.IncrementalArticulationOffset,
                articulationOffsetGenerator
            );
            var incrementalChordRootNoteOffsetGenerator = changingStateTimelineMap.ToIncrementalGenerator(
                CompositionStateKinds.IncrementalChordRootNoteOffset,
                chordRootNoteOffsetGenerator
            );
            var incrementalChordNoteOffsetGenerator = changingStateTimelineMap.ToIncrementalGenerator(
                CompositionStateKinds.IncrementalChordNoteOffset,
                chordNoteOffsetGenerator
            );

            // the chord shape can change within the pattern, so each note takes the shape at its position
            var chordNoteInScaleOffsetsGenerator = (double position) => stateMap
                .MergeWith(
                    barStateTimelineMap
                        .GetEffectiveStateMapAt(patternStart + position)
                        .Subset([CompositionStateKinds.ChordNotePitchOffsets.Index])
                )
                .SelectValueFromCollectionByIndex(CompositionStateKinds.ChordNotePitchOffsets)
                .ToKind(StateKinds.ChordNotePitchOffsets);

            var stateMapGenerator = (IGenerationContext innerContext, double position, int rank) => new StateMapBuilder()
                .Add(StateKinds.ArticulationOffset, incrementalArticulationOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordRootNoteOffset, incrementalChordRootNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordNoteOffset, incrementalChordNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.Velocity, BeatAccent.CreateVelocityGenerator(rank, rhythmPattern.MaxRank).Then(x => x * VelocityLayers.Note))
                .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
                .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
                .Add(chordNoteInScaleOffsetsGenerator(position))
                .ToStateMap(innerContext);

            return DyadicRankItemPattern<StateMap>.Create(
                generationContext,
                rhythmPattern,
                innerContext => (position, rank) => stateMapGenerator(innerContext, position, rank),
                patternSeeds.NoteValues
            );
        };

        var noteHigherPatternInnerStateMapGenerator = new StateMapBuilder()
            .AddRhythmLayer(RhythmLayers.BarPattern)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.BarPattern))
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            // no chord root offset here: every track plays the progression's chord, and a track leaves it only by
            // moving its root from note to note
            .ToStateMapGenerator();

        var seedValueGenerator = Generators.Int();
        // the bar state is the chord progression: each bar of the pattern takes the state of the bar it plays in
        var noteHigherPatternGenerator = (ImmutableDictionary<int, StateMap> trackStateMaps, StateTimelineMap barStateTimelineMap) =>
        {
            var trackSeedMapGenerator = (IGenerationContext innerContext) =>
                trackStateMaps.Keys.ToDictionary(x => x, _ => seedValueGenerator(innerContext));
            var trackSeedMaps = Generators.Sequence(trackSeedMapGenerator, 4)(generationContext);
            var trackSeedMapSelector = Generators.ItemSelector(trackSeedMaps);
            return Generators.Sequence(trackSeedMapSelector, 4)(generationContext)
                .Select((trackSeedMap, barIndex) =>
                    {
                        var trackNotePatterns = trackSeedMap
                            .Select(p =>
                                {
                                    var trackNumber = p.Key;
                                    var seedValue = p.Value;

                                    var trackStateMap = trackStateMaps[trackNumber];
                                    var patternSeeds = CreatePatternSeeds(seedValue);
                                    var trackGenerationContext = generationContext.CreateContext(patternSeeds.TrackState);
                                    var innerStateMap = new StateMapBuilder()
                                        .Add(noteHigherPatternInnerStateMapGenerator)
                                        .Add(CompositionStateKinds.Rhythm.Seed, patternSeeds.Rhythm)
                                        .Add(CompositionStateKinds.ValueSeed, seedValue)
                                        .ToStateMap(trackGenerationContext)
                                        .MergeWith(trackStateMap)
                                        .MergeWith(
                                            CreatePatternChordNoteOffset(
                                                trackDefinitions[trackNumber],
                                                trackStateMap,
                                                patternChordNoteOffsetGenerator,
                                                trackGenerationContext
                                            )
                                        );
                                    var notePattern = notePatternGenerator(innerStateMap, barStateTimelineMap, barIndex * 4);
                                    var innerEventStateTimelineMap = notePattern.GeneratedTimeline
                                        .ToEventStateTimelineMap(innerStateMap.Subset(StateKinds.GetAll()));
                                    return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                                        trackNumber,
                                        innerEventStateTimelineMap
                                    );
                                }
                            );
                        return TrackEventStateTimelineMap.Create(4, trackNotePatterns, StateTimelineMap.Create(4));
                    }
                )
                .Unroll();
        };

        // the song's chords gather around the song's weirdness, and a section's around its own shift of it
        var songChordWeirdness = ChordWeirdness.Generate(generationContext);
        var chordCollectionGenerator = (ChordWeirdness weirdness) => Generators.Sequence(weirdness.GenerateChord, 2);
        var chordIndexOffsetGenerator = Generators.Rank(HalfWeightRankGenerator, -2, 2);

        // the state that changes along a section's 4-bar pattern, the chord progression among it; every state has
        // its own timeline, so each can change at its own pace
        IStateTimelineGenerator[] barStateTimelineGenerators =
        [
            StateTimelineGenerator.Create(
                StateKinds.Velocity,
                progressionSettings.NoteStateStep,
                VelocityLayers.CreateGenerator(VelocityLayers.Bar),
                progressionSettings.PoolSize
            ),
            StateTimelineGenerator.Create(
                StateKinds.QuarterNoteDurationPower,
                progressionSettings.NoteStateStep,
                quarterNoteDurationPowerGenerator,
                progressionSettings.PoolSize
            ),
            StateTimelineGenerator.Create(
                StateKinds.NextNoteDurationFactor,
                progressionSettings.NoteStateStep,
                nextNoteDurationFactorGenerator,
                progressionSettings.PoolSize
            ),
            StateTimelineGenerator.Create(
                StateKinds.ChordRootNoteOffset,
                progressionSettings.ChordRootStep,
                chordRootOffsetGenerator.Then(x => ImmutableArray.Create(x)),
                progressionSettings.PoolSize
            ),
            StateTimelineGenerator.Create(
                CompositionStateKinds.ChordNotePitchOffsets.Index,
                progressionSettings.ChordShapeStep,
                chordIndexOffsetGenerator,
                progressionSettings.PoolSize
            ),
        ];
        var commonStateHigherPatternGenerator = () =>
        {
            // each state draws from its own random sequence, so tuning one does not change the others
            var seed = seedValueGenerator(generationContext);
            return StateTimelineMap.Create(
                16,
                barStateTimelineGenerators.Select((generator, index) =>
                    generator.Generate(generationContext.CreateContext(Seeds.Derive(seed, index)), 16)
                )
            );
        };

        var songStateMap = new StateMapBuilder()
            .AddRhythmLayer(RhythmLayers.Song)
            .Add(CompositionStateKinds.Rhythm.MaxRank, 2)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.ChordNotePitchOffsets.Collection, chordCollectionGenerator(songChordWeirdness))
            .Add(CompositionStateKinds.ChordNotePitchOffsets.Index, chordIndexOffsetGenerator)
            .ToStateMap(generationContext);

        var sectionStateMapGenerator = new StateMapBuilder()
            .AddRhythmLayer(RhythmLayers.Section)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.ChordNotePitchOffsets.Index, chordIndexOffsetGenerator)
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.Section))
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .AddCollectionOfOne(StateKinds.ChordRootNoteOffset, chordRootNoteOffsetGenerator)
            .ToStateMapGenerator();

        var trackGroupSectionStateMapGenerator = new StateMapBuilder()
            .AddRhythmLayer(RhythmLayers.SectionDrumGroup)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.SectionDrumGroup))
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .ToStateMapGenerator();

        var nonGroupedTrackNumbers = trackDefinitions.Keys
            .Except(trackGroups.SelectMany(x => x.TrackNumbers))
            .ToImmutableSortedSet();
        var songSectionGenerator = (int sectionId) =>
        {
            var sectionChords = chordCollectionGenerator(songChordWeirdness.GenerateSection(generationContext))(generationContext);
            var sectionStateMap = sectionStateMapGenerator(generationContext)
                .MergeWith(StateMap.FromStates([CompositionStateKinds.ChordNotePitchOffsets.Collection.CreateState(sectionChords)]))
                .MergeWith(songStateMap);
            var activeDrumTrackNumbers = DrumKitGenerator.SelectActiveDrums(generationContext, songDrums)
                .Select(DrumGroups.GetTrackNumber)
                .ToImmutableHashSet();

            // the section state reaches the notes through the track state maps, so the common timeline holds only
            // the state that changes by bar
            var barStateTimelineMap = commonStateHigherPatternGenerator();

            var trackTimelineMaps = new List<TrackEventStateTimelineMap<StateMap>>();

            foreach (var group in trackGroups)
            {
                var groupStateMap = trackGroupSectionStateMapGenerator(generationContext)
                    .MergeWith(group.StateMap)
                    .MergeWith(sectionStateMap);
                var trackStateMaps = new Dictionary<int, StateMap>();
                foreach (var trackNumber in group.TrackNumbers.Where(activeDrumTrackNumbers.Contains))
                {
                    var trackDefinition = trackDefinitions[trackNumber];
                    var combinedStateMap = trackDefinitionStateMapGenerator(GetTrackGenerationStateMap(trackDefinition), VelocityLayers.SectionTrack, RhythmLayers.SectionTrack)
                        .MergeWith(groupStateMap);
                    trackStateMaps[trackNumber] = combinedStateMap;
                }

                if (trackStateMaps.Count == 0)
                    continue;

                var innerPattern = noteHigherPatternGenerator(trackStateMaps.ToImmutableDictionary(), barStateTimelineMap);
                trackTimelineMaps.Add(innerPattern);
            }

            foreach (var trackNumber in nonGroupedTrackNumbers)
            {
                var trackDefinition = trackDefinitions[trackNumber];
                var combinedStateMap = trackDefinitionStateMapGenerator(GetTrackGenerationStateMap(trackDefinition), VelocityLayers.SectionTrack, RhythmLayers.SectionTrack)
                    .MergeWith(sectionStateMap);
                var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = combinedStateMap };
                var innerPattern = noteHigherPatternGenerator(trackStateMaps.ToImmutableDictionary(), barStateTimelineMap);
                trackTimelineMaps.Add(innerPattern);
            }

            var commonStateTimeline = barStateTimelineMap.ToTrackEventStateTimelineMap<StateMap>(16);
            trackTimelineMaps.Add(commonStateTimeline);
            return TrackEventStateTimelineMap.Merge(trackTimelineMaps)
                .Repeat(2);
        };
        var cachedSongSectionGenerator = songSectionGenerator.CacheGeneratedValues();

        var commonStateMap = new StateMapBuilder()
            .Add(StateKinds.ScaleOffsets, [0, 2, 3, 5, 7, 8, 10])
            .Add(StateKinds.KeyOffset, Generators.Int(0, 12))
            .Add(StateKinds.Tempo, TempoGenerator)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .ToStateMap(generationContext);

        var songTrackNoteTimelineMap = SongStructureGenerator.Generate(generationContext)
            .SelectMany(part => part.SectionIds)
            .Select(x => cachedSongSectionGenerator(x))
            .Unroll()
            .MergeStateMap(commonStateMap);

        return new Song(
            songTrackNoteTimelineMap.Duration,
            trackDefinitions.ToImmutableSortedDictionary(),
            songTrackNoteTimelineMap
        );
    }

    /// <summary>
    ///     A layer's steps of the rhythm settings. The period and the phase make the groove, the speed changing more often and the tuplet period by a chance of its own, and the max rank and the
    ///     rank offset how busy it is; the phase's offset among the beats of its rank is drawn around 0 in every layer.
    /// </summary>
    private static StateMapBuilder AddRhythmLayer(this StateMapBuilder builder, RhythmLayer layer)
    {
        return builder
            .Add(CompositionStateKinds.Rhythm.Period.Power, layer.CreateSpeedGenerator())
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, layer.CreateTupletGenerator())
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, layer.CreateGrooveGenerator())
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, Generators.SplineValue())
            .Add(CompositionStateKinds.Rhythm.MaxRank, layer.CreateDensityGenerator())
            .Add(CompositionStateKinds.Rhythm.RankOffset, layer.CreateDensityGenerator());
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
        Func<IGenerationContext, double> offsetGenerator,
        IGenerationContext context
    )
    {
        if (trackDefinition is not PitchInstrumentTrack)
            return StateMap.Default;

        var multiplier = trackStateMap.GetStateValue(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier);
        if (multiplier.IsEqualToByEpsilon(0))
            return StateMap.Default;

        return StateMap.FromStates([StateKinds.ChordNoteOffset.CreateState([offsetGenerator(context) * multiplier])]);
    }

    /// <summary>
    ///     The state a track definition brings into the generation of its notes. Render applies the definition's
    ///     rendered state to every note of the track, so only the composition state is taken here.
    /// </summary>
    internal static StateMap GetTrackGenerationStateMap(IInstrumentTrack trackDefinition)
    {
        return trackDefinition.StateMap.Except(StateKinds.GetAll());
    }

    private static State<ImmutableArray<T>> SelectValueFromCollectionByIndex<T>(
        this StateMap stateMap,
        CompositionStateKinds.CollectionFromCollectionStateKinds<T> stateKindGroup
    )
    {
        var collection = stateMap.GetStateValue(stateKindGroup.Collection);
        var index = stateMap.GetStateValue(stateKindGroup.Index)
            .BounceInBounds(0, collection.Length - 1);
        var value = collection[index];
        return stateKindGroup.Value.CreateState(value);
    }

    private static Func<IGenerationContext, double, ImmutableArray<double>> ToIncrementalGenerator(
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
            // a zero multiplier turns the offset off: no chord note offset means the whole chord plays
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

/// <param name="TrackState">The state a track draws for the pattern, such as its rhythm and offsets.</param>
/// <param name="Rhythm">Which beats of the rhythm play.</param>
/// <param name="StateChanges">How the note offsets change along the pattern.</param>
/// <param name="NoteValues">The values of the notes that play.</param>
internal readonly record struct PatternSeeds(int TrackState, int Rhythm, int StateChanges, int NoteValues);

/// <summary>How the state along a section's 4-bar pattern changes. The steps are in beats; a bar is 4 beats.</summary>
/// <param name="ChordRootStep">How often the chord root of the progression can change.</param>
/// <param name="ChordShapeStep">How often the chord shape of the progression can change.</param>
/// <param name="NoteStateStep">How often the velocity and note duration of the progression can change.</param>
/// <param name="PoolSize">How many values each state picks from along the pattern; 0 draws a new value every step.</param>
internal sealed record ProgressionSettings(double ChordRootStep, double ChordShapeStep, double NoteStateStep, int PoolSize)
{
    public static ProgressionSettings Default { get; } = new(4, 4, 4, 4);
}
