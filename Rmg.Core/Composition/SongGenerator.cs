using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

public static class SongGenerator
{
    private static readonly Func<int, double> HalfWeightRankGenerator =
        WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.5);

    private static readonly Func<int, double> QuarterWeightRankGenerator =
        WeightUtil.CreateGeometricRankWeightFunc(0, 0, 1.0, 0.25);

    public static Song GenerateSong()
    {
        var generationContext =  new GenerationContext();

        // track definitions

        var pitchInstrumentCodeGenerator = Generators.Int(0, 120).WithContext(generationContext);
        var minOctaveOffsetGenerator = Generators.Int(-2, 1).WithContext(generationContext);
        var maxOctaveOffsetGenerator = Generators.Int(0, 3).WithContext(generationContext);

        var closeToZeroIncrementalOffsetMultiplierGenerator = Generators.AbsSplineValue();
        var closeToOneIncrementalOffsetMultiplierGenerator = Generators.AbsSplineValue().Then(x => 1 - x);

        var velocityGenerator = Generators.SplineValue();
        var quarterNoteDurationPowerGenerator = Generators.SplineValue();
        var nextNoteDurationFactorGenerator = Generators.SplineValue();
        var chordRootOffsetGenerator = Generators.SplineValue();

        var rhythmPatternPeriodPowerGenerator = Generators.Rank(QuarterWeightRankGenerator, -1, 1);
        var rhythmPatternPeriodPrimeIndexGenerator = Generators.Rank(QuarterWeightRankGenerator, -1, 1);
        var rhythmPatternPhaseRankGenerator = Generators.Rank(QuarterWeightRankGenerator, -1, 1);
        var rhythmPatternPhaseRankedOffsetGenerator = Generators.SplineValue();
        var rhythmPatternMaxRankGenerator = Generators.Rank(QuarterWeightRankGenerator, -1, 1);
        var rhythmPatternRankOffsetGenerator = Generators.Rank(QuarterWeightRankGenerator, -1, 1);
        var notePatternConsecutiveOffsetGenerator = Generators.SplineValue();
        var notePatternRandomOffsetGenerator = Generators.SplineValue();

        var articulationOffsetGenerator = Generators.SplineValue();
        var chordRootNoteOffsetGenerator = Generators.SplineValue();
        var chordNoteOffsetGenerator = Generators.SplineValue();

        var trackDefinitionStateMapGenerator = (StateMap stateMap) => new StateMapBuilder()
            .Add(stateMap)
            .Add(CompositionStateKinds.Rhythm.Period.Power, rhythmPatternPeriodPowerGenerator)
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, rhythmPatternPeriodPrimeIndexGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, rhythmPatternPhaseRankGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, rhythmPatternPhaseRankedOffsetGenerator)
            .Add(CompositionStateKinds.Rhythm.MaxRank, rhythmPatternMaxRankGenerator)
            .Add(CompositionStateKinds.Rhythm.RankOffset, rhythmPatternRankOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(StateKinds.Velocity, velocityGenerator)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .ToStateMap(generationContext);

        var trackDefinitions = new Dictionary<int, IInstrumentTrack>
        {
            // kick
            [1] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default),
                PercussionInstrumentDefinition.Definitions[0].ArticulationCodes
            ),
            // snare
            [2] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new StateMapBuilder()
                        .Add(CompositionStateKinds.Rhythm.Phase.Rank, 1)
                        .ToStateMap(generationContext)
                ),
                PercussionInstrumentDefinition.Definitions[2].ArticulationCodes
            ),
            // hi-hat
            [3] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new StateMapBuilder()
                        .Add(CompositionStateKinds.Rhythm.Period.Power, -1)
                        .ToStateMap(generationContext)
                ),
                PercussionInstrumentDefinition.Definitions[4].ArticulationCodes
            ),
            // chords instrument
            [4] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new StateMapBuilder()
                        .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier, 0)
                        .Add(CompositionStateKinds.IncrementalChordNoteOffset.Multiplier, 0)
                        .ToStateMap(generationContext)
                ),
                pitchInstrumentCodeGenerator(),
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator()
            ),
            // melody instrument
            [5] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default),
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
                        .ToStateMap(generationContext)
                ),
                pitchInstrumentCodeGenerator(),
                -3,
                -2
            )
        };

        ImmutableArray<TrackGroup> trackGroups =
        [
            new([1, 2, 3], trackDefinitionStateMapGenerator(StateMap.Default))
        ];

        var rhythmPatternGenerator = (StateMap stateMap) =>
        {
            const double duration = 4;
            var period = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.Value) * duration;
            var phase = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Value) * duration;
            var maxRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.MaxRank);
            var seed = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Seed.Value);
            var intensity = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Intensity);
            var rankOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.RankOffset);

            return DyadicRankThresholdPattern.Create(
                generationContext,
                seed,
                intensity,
                WeightUtil.CreateGeometricRankWeightFunc(rankOffset, 0, 1.0, 0.5),
                new DyadicTimelineDescriptor(duration, period, phase, maxRank)
            );
        };
        var cachedRhythmPatternGenerator = rhythmPatternGenerator
            .CacheGeneratedValues()
            .MapInput((StateMap stateMap) =>
                {
                    var rankOffsetState = stateMap.GetState(CompositionStateKinds.Rhythm.RankOffset);
                    var seedValueState = stateMap.GetState(CompositionStateKinds.Rhythm.Seed.Value);

                    var intensity = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Intensity)
                        .BounceInBounds(0, 2);
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
                        .Add(CompositionStateKinds.Rhythm.Intensity, intensity)
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
        var notePatternGenerator = (StateMap stateMap) =>
        {
            var rhythmPattern = cachedRhythmPatternGenerator(stateMap);

            var seed = stateMap.GetStateValue(CompositionStateKinds.ValueSeed.Value);
            var changingContext = generationContext.CreateContext(seed);
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

            var chordNoteInScaleOffsets = stateMap
                .SelectValueFromCollectionByIndex(CompositionStateKinds.ChordNoteInScaleOffsets)
                .ToKind(StateKinds.ChordNoteInScaleOffsets);

            var stateMapGenerator = (IGenerationContext innerContext, double position) => new StateMapBuilder()
                .Add(StateKinds.ArticulationOffset, incrementalArticulationOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordRootNoteOffset, incrementalChordRootNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordNoteOffset, incrementalChordNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.Velocity, velocityGenerator)
                .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
                .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
                .Add(chordNoteInScaleOffsets)
                .ToStateMap(innerContext);

            return DyadicRankItemPattern<StateMap>.Create(
                generationContext,
                rhythmPattern,
                innerContext => (position, _) => stateMapGenerator(innerContext, position),
                seed
            );
        };

        var noteHigherPatternInnerStateMapGenerator = new StateMapBuilder()
            .Add(CompositionStateKinds.Rhythm.Period.Power, rhythmPatternPeriodPowerGenerator)
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, rhythmPatternPeriodPrimeIndexGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, rhythmPatternPhaseRankGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, rhythmPatternPhaseRankedOffsetGenerator)
            .Add(CompositionStateKinds.Rhythm.MaxRank, rhythmPatternMaxRankGenerator)
            .Add(CompositionStateKinds.Rhythm.RankOffset, rhythmPatternRankOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(StateKinds.Velocity, velocityGenerator)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .AddCollectionOfOne(StateKinds.ChordRootNoteOffset, chordRootOffsetGenerator)
            .ToStateMapGenerator();

        var seedValueGenerator = Generators.Int();
        var noteHigherPatternGenerator = (ImmutableDictionary<int, StateMap> trackStateMaps) =>
        {
            var trackSeedMapGenerator = (IGenerationContext innerContext) =>
                trackStateMaps.Keys.ToDictionary(x => x, _ => seedValueGenerator(innerContext));
            var trackSeedMaps = Generators.Sequence(trackSeedMapGenerator, 4)(generationContext);
            var trackSeedMapSelector = Generators.ItemSelector(trackSeedMaps);
            return Generators.Sequence(trackSeedMapSelector, 4)(generationContext)
                .Select(trackSeedMap =>
                    {
                        var trackNotePatters = trackSeedMap
                            .Select(p =>
                                {
                                    var trackNumber = p.Key;
                                    var seedValue = p.Value;

                                    var trackStateMap = trackStateMaps[trackNumber];
                                    var trackGenerationContext = generationContext.CreateContext(seedValue);
                                    var innerStateMap = new StateMapBuilder()
                                        .Add(noteHigherPatternInnerStateMapGenerator)
                                        .Add(CompositionStateKinds.Rhythm.Seed.Value, seedValue)
                                        .Add(CompositionStateKinds.ValueSeed.Value, seedValue)
                                        .ToStateMap(trackGenerationContext)
                                        .MergeWith(trackStateMap);
                                    var notePattern = notePatternGenerator(innerStateMap);
                                    var innerEventStateTimelineMap = notePattern.GeneratedTimeline
                                        .ToEventStateTimelineMap(innerStateMap.Subset(StateKinds.GetAll()));
                                    return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                                        trackNumber,
                                        innerEventStateTimelineMap
                                    );
                                }
                            );
                        return TrackEventStateTimelineMap.Create(4, trackNotePatters, StateTimelineMap.Empty);
                    }
                )
                .Unroll();
        };

        var chordNoteCountGenerator = Generators.Rank(HalfWeightRankGenerator, -1, 4)
            .Then(x => x + 2);
        var noteInChordOffsetGenerator = Generators.SplineValue();
        var chordGenerator = (IGenerationContext innerContext) =>
        {
            var noteCount = chordNoteCountGenerator(innerContext);
            var chordOffsetCollectionBuilder = ImmutableArray.CreateBuilder<double>(noteCount);

            // we're going to move the probability towards linear the more notes we have
            var maxOffset = Math.Abs(Generators.SplineValue((6 - noteCount) / 6.0)(innerContext)) * 2;
            var noteOffsetRange = maxOffset / noteCount / 2;
            for (var i = 0; i < noteCount; i++)
            {
                var offset = noteInChordOffsetGenerator(innerContext) * noteOffsetRange;
                var chordNoteOffset = maxOffset * (i + 1) / noteCount + offset;
                chordOffsetCollectionBuilder.Add(chordNoteOffset);
            }

            return chordOffsetCollectionBuilder.ToImmutableArray();
        };
        var chordCollectionGenerator = Generators.Sequence(chordGenerator, 2);
        var chordIndexOffsetGenerator = Generators.Rank(HalfWeightRankGenerator, -2, 2);

        var commonStateHigherPatternStateMapGenerator = new StateMapBuilder()
            .Add(StateKinds.Velocity, velocityGenerator)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .AddCollectionOfOne(StateKinds.ChordRootNoteOffset, chordRootOffsetGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Index, chordIndexOffsetGenerator)
            .ToStateMapGenerator();
        var commonStateHigherPatternGenerator = () =>
        {
            var seeds = Generators.Sequence(seedValueGenerator, 4)(generationContext);
            var seedSelector = Generators.ItemSelector(seeds);
            return Generators.Sequence(seedSelector, 4)(generationContext)
                .Select((seedValue, index) =>
                    {
                        var innerGenerationContext = generationContext.CreateContext(seedValue);
                        return commonStateHigherPatternStateMapGenerator(innerGenerationContext)
                            .ToTimelineItem(index * 4);
                    }
                )
                .ToStateTimelineMap(16);
        };

        var songStateMap = new StateMapBuilder()
            .Add(CompositionStateKinds.Rhythm.Period.Power, rhythmPatternPeriodPowerGenerator)
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, rhythmPatternPeriodPrimeIndexGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, rhythmPatternPhaseRankGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, rhythmPatternPhaseRankedOffsetGenerator)
            .Add(CompositionStateKinds.Rhythm.MaxRank, 2)
            .Add(CompositionStateKinds.Rhythm.RankOffset, rhythmPatternRankOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Collection, chordCollectionGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Index, chordIndexOffsetGenerator)
            .ToStateMap(generationContext);

        var sectionStateMapGenerator = new StateMapBuilder()
            .Add(CompositionStateKinds.Rhythm.Period.Power, rhythmPatternPeriodPowerGenerator)
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, rhythmPatternPeriodPrimeIndexGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, rhythmPatternPhaseRankGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, rhythmPatternPhaseRankedOffsetGenerator)
            .Add(CompositionStateKinds.Rhythm.MaxRank, rhythmPatternMaxRankGenerator)
            .Add(CompositionStateKinds.Rhythm.RankOffset, rhythmPatternRankOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Collection, chordCollectionGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Index, chordIndexOffsetGenerator)
            .Add(StateKinds.Velocity, velocityGenerator)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .AddCollectionOfOne(StateKinds.ChordRootNoteOffset, chordRootNoteOffsetGenerator)
            .ToStateMapGenerator();

        var trackGroupSectionStateMapGenerator = new StateMapBuilder()
            .Add(CompositionStateKinds.Rhythm.Period.Power, rhythmPatternPeriodPowerGenerator)
            .Add(CompositionStateKinds.Rhythm.Period.PrimeIndex, rhythmPatternPeriodPrimeIndexGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.Rank, rhythmPatternPhaseRankGenerator)
            .Add(CompositionStateKinds.Rhythm.Phase.RankedOffset, rhythmPatternPhaseRankedOffsetGenerator)
            .Add(CompositionStateKinds.Rhythm.MaxRank, rhythmPatternMaxRankGenerator)
            .Add(CompositionStateKinds.Rhythm.RankOffset, rhythmPatternRankOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalArticulationOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset, notePatternConsecutiveOffsetGenerator)
            .Add(CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset, notePatternRandomOffsetGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Collection, chordCollectionGenerator)
            .Add(CompositionStateKinds.ChordNoteInScaleOffsets.Index, chordIndexOffsetGenerator)
            .Add(StateKinds.Velocity, velocityGenerator)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .ToStateMapGenerator();

        var nonGroupedTrackNumbers = trackDefinitions.Keys
            .Except(trackGroups.SelectMany(x => x.TrackNumbers))
            .ToImmutableSortedSet();
        var songSectionGenerator = (string sectionName) =>
        {
            var sectionStateMap = sectionStateMapGenerator(generationContext).MergeWith(songStateMap);

            var trackTimelineMaps = new List<TrackEventStateTimelineMap<StateMap>>();

            foreach (var group in trackGroups)
            {
                var groupStateMap = trackGroupSectionStateMapGenerator(generationContext)
                    .MergeWith(group.StateMap)
                    .MergeWith(sectionStateMap);
                var trackStateMaps = new Dictionary<int, StateMap>();
                foreach (var trackNumber in group.TrackNumbers)
                {
                    var trackDefinition = trackDefinitions[trackNumber];
                    var combinedStateMap = trackDefinitionStateMapGenerator(trackDefinition.StateMap)
                        .MergeWith(groupStateMap);
                    trackStateMaps[trackNumber] = combinedStateMap;
                }

                var innerPattern = noteHigherPatternGenerator(trackStateMaps.ToImmutableDictionary());
                trackTimelineMaps.Add(innerPattern);
            }

            foreach (var trackNumber in nonGroupedTrackNumbers)
            {
                var trackDefinition = trackDefinitions[trackNumber];
                var combinedStateMap = trackDefinitionStateMapGenerator(trackDefinition.StateMap)
                    .MergeWith(sectionStateMap);
                var trackStateMaps = new Dictionary<int, StateMap> { [trackNumber] = combinedStateMap };
                var innerPattern = noteHigherPatternGenerator(trackStateMaps.ToImmutableDictionary());
                trackTimelineMaps.Add(innerPattern);
            }

            var commonStateTimeline = commonStateHigherPatternGenerator()
                .MergeStateMap(sectionStateMap.Subset(StateKinds.GetAll()))
                .ToTrackEventStateTimelineMap<StateMap>(16);
            trackTimelineMaps.Add(commonStateTimeline);
            return TrackEventStateTimelineMap.Merge(trackTimelineMaps)
                .Repeat(2);
        };
        var cachedSongSectionGenerator = songSectionGenerator.CacheGeneratedValues();

        var commonStateMap = new StateMapBuilder()
            .Add(StateKinds.ScaleOffsets, [0, 2, 3, 5, 7, 8, 10])
            .Add(StateKinds.KeyOffset, Generators.Int(0, 12))
            .Add(StateKinds.Tempo, 1)
            .Add(StateKinds.QuarterNoteDurationPower, quarterNoteDurationPowerGenerator)
            .Add(StateKinds.NextNoteDurationFactor, nextNoteDurationFactorGenerator)
            .ToStateMap(generationContext);

        var songSectionCount = Generators.Int(6, 10)(generationContext);
        var songTrackNoteTimelineMap = SongSectionArchetypes.GenerateSongStructure(generationContext, songSectionCount)
            .Select(x => cachedSongSectionGenerator(x.Name))
            .Unroll()
            .MergeStateMap(commonStateMap);

        return new Song(
            songTrackNoteTimelineMap.Duration,
            trackDefinitions.ToImmutableSortedDictionary(),
            songTrackNoteTimelineMap
        );
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
            var consecutiveOffset = stateMap.GetStateValue(stateKindGroup.ConsecutiveOffset) * multiplier;
            var randomOffset = stateMap.GetStateValue(stateKindGroup.RandomOffset) * multiplier;
            var randomValue = randomValueGenerator(innerContext);
            var value = currentValue + randomValue * randomOffset;
            currentValue += consecutiveOffset;
            return [value];
        };
    }
}
