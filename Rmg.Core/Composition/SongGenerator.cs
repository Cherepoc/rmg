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
        var generationContext = new GenerationContext();

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

        var trackDefinitionStateMapGenerator = (StateMap stateMap) =>
        {
            return new IState[]
                {
                    CompositionStateKinds.Rhythm.Period.Power.CreateState(
                        rhythmPatternPeriodPowerGenerator(generationContext)),
                    CompositionStateKinds.Rhythm.Period.PrimeIndex.CreateState(
                        rhythmPatternPeriodPrimeIndexGenerator(generationContext)),
                    CompositionStateKinds.Rhythm.Phase.Rank.CreateState(
                        rhythmPatternPhaseRankGenerator(generationContext)),
                    CompositionStateKinds.Rhythm.Phase.RankedOffset.CreateState(
                        rhythmPatternPhaseRankedOffsetGenerator(generationContext)),
                    CompositionStateKinds.Rhythm.MaxRank.CreateState(rhythmPatternMaxRankGenerator(generationContext)),
                    CompositionStateKinds.Rhythm.RankOffset.CreateState(
                        rhythmPatternRankOffsetGenerator(generationContext)),
                    CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset.CreateState(
                        notePatternConsecutiveOffsetGenerator(generationContext)),
                    CompositionStateKinds.IncrementalArticulationOffset.RandomOffset.CreateState(
                        notePatternRandomOffsetGenerator(generationContext)),
                    CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset.CreateState(
                        notePatternConsecutiveOffsetGenerator(generationContext)),
                    CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset.CreateState(
                        notePatternRandomOffsetGenerator(generationContext)),
                    CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset.CreateState(
                        notePatternConsecutiveOffsetGenerator(generationContext)),
                    CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset.CreateState(
                        notePatternRandomOffsetGenerator(generationContext)),
                    StateKinds.Velocity.CreateState(velocityGenerator(generationContext)),
                    StateKinds.QuarterNoteDurationPower.CreateState(
                        quarterNoteDurationPowerGenerator(generationContext)),
                    StateKinds.NextNoteDurationFactor.CreateState(nextNoteDurationFactorGenerator(generationContext)),
                }
                .Concat(stateMap.States)
                .ToStateMap();
        };

        var trackDefinitions = new Dictionary<int, IInstrumentTrack>
        {
            // kick
            [1] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default),
                PercussionInstrumentDefinition.Definitions[0].ArticulationCodes
            ),
            // snare
            [2] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(new IState[]
                    {
                        CompositionStateKinds.Rhythm.Phase.Rank.CreateState(1)
                    }
                    .ToStateMap()
                ),
                PercussionInstrumentDefinition.Definitions[2].ArticulationCodes
            ),
            // hi-hat
            [3] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(new IState[]
                    {
                        CompositionStateKinds.Rhythm.Period.Power.CreateState(-1)
                    }
                    .ToStateMap()
                ),
                PercussionInstrumentDefinition.Definitions[4].ArticulationCodes
            ),
            // chords instrument
            [4] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new IState[]
                        {
                            CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier.CreateState(0),
                            CompositionStateKinds.IncrementalChordNoteOffset.Multiplier.CreateState(0),
                        }
                        .ToStateMap()
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
                    new IState[]
                        {
                            CompositionStateKinds.IncrementalChordRootNoteOffset.Multiplier
                                .CreateState(closeToZeroIncrementalOffsetMultiplierGenerator(generationContext)),
                            CompositionStateKinds.IncrementalChordNoteOffset.Multiplier
                                .CreateState(closeToOneIncrementalOffsetMultiplierGenerator(generationContext)),
                        }
                        .ToStateMap()
                ),
                pitchInstrumentCodeGenerator(),
                -3,
                -2
            )
        };

        ImmutableArray<TrackGroup> trackGroups =
        [
            new([1, 2, 3], trackDefinitionStateMapGenerator(StateMap.Default)),
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

                return new IState[]
                    {
                        CompositionStateKinds.Rhythm.Period.Value.CreateState(periodValue),
                        CompositionStateKinds.Rhythm.Phase.Value.CreateState(phaseValue),
                        CompositionStateKinds.Rhythm.MaxRank.CreateState(maxRank),
                        CompositionStateKinds.Rhythm.Intensity.CreateState(intensity),
                        rankOffsetState.Map(x => x.BounceInBounds(0, maxRank)),
                        seedValueState
                    }
                    .ToStateMap();
            });

        var notePatternGenerator = (StateMap stateMap) =>
        {
            var rhythmPattern = cachedRhythmPatternGenerator(stateMap);

            var incrementalArticulationOffsetGenerator = stateMap.ToIncrementalGenerator(
                CompositionStateKinds.IncrementalArticulationOffset,
                articulationOffsetGenerator
            );
            var incrementalChordRootNoteOffsetGenerator = stateMap.ToIncrementalGenerator(
                CompositionStateKinds.IncrementalChordRootNoteOffset,
                chordRootNoteOffsetGenerator
            );
            var incrementalChordNoteOffsetGenerator = stateMap.ToIncrementalGenerator(
                CompositionStateKinds.IncrementalChordNoteOffset,
                chordNoteOffsetGenerator
            );

            var seed = stateMap.GetStateValue(CompositionStateKinds.ValueSeed.Value);

            var chordNoteInScaleOffsets = stateMap
                .SelectValueFromCollectionByIndex(CompositionStateKinds.ChordNoteInScaleOffsets)
                .ToKind(StateKinds.ChordNoteInScaleOffsets);

            return DyadicRankItemPattern<StateMap>.Create(
                generationContext,
                rhythmPattern,
                innerContext => (int valueRank) =>
                {
                    var articulationOffset = incrementalArticulationOffsetGenerator(innerContext);
                    var chordRootNoteOffset = incrementalChordRootNoteOffsetGenerator(innerContext);
                    var chordNoteOffset = incrementalChordNoteOffsetGenerator(innerContext);

                    return new IState[]
                        {
                            StateKinds.ArticulationOffset.CreateState(articulationOffset),
                            StateKinds.ChordRootNoteOffset.CreateState(chordRootNoteOffset),
                            StateKinds.ChordNoteOffset.CreateState(chordNoteOffset),
                            StateKinds.Velocity.CreateState(velocityGenerator(innerContext)),
                            StateKinds.QuarterNoteDurationPower.CreateState(
                                quarterNoteDurationPowerGenerator(innerContext)),
                            StateKinds.NextNoteDurationFactor.CreateState(
                                nextNoteDurationFactorGenerator(innerContext)),
                            chordNoteInScaleOffsets
                        }
                        .ToStateMap();
                },
                seed
            );
        };

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
                            var innerStateMap = new IState[]
                                {
                                    CompositionStateKinds.Rhythm.Seed.Value.CreateState(seedValue),
                                    CompositionStateKinds.ValueSeed.Value.CreateState(seedValue),
                                    CompositionStateKinds.Rhythm.Period.Power.CreateState(
                                        rhythmPatternPeriodPowerGenerator(trackGenerationContext)),
                                    CompositionStateKinds.Rhythm.Period.PrimeIndex.CreateState(
                                        rhythmPatternPeriodPrimeIndexGenerator(trackGenerationContext)),
                                    CompositionStateKinds.Rhythm.Phase.Rank.CreateState(
                                        rhythmPatternPhaseRankGenerator(trackGenerationContext)),
                                    CompositionStateKinds.Rhythm.Phase.RankedOffset.CreateState(
                                        rhythmPatternPhaseRankedOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.Rhythm.MaxRank.CreateState(
                                        rhythmPatternMaxRankGenerator(trackGenerationContext)),
                                    CompositionStateKinds.Rhythm.RankOffset.CreateState(
                                        rhythmPatternRankOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset.CreateState(
                                        notePatternConsecutiveOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.IncrementalArticulationOffset.RandomOffset.CreateState(
                                        notePatternRandomOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset.CreateState(
                                        notePatternConsecutiveOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset.CreateState(
                                        notePatternRandomOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset.CreateState(
                                        notePatternConsecutiveOffsetGenerator(trackGenerationContext)),
                                    CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset.CreateState(
                                        notePatternRandomOffsetGenerator(trackGenerationContext)),
                                    StateKinds.Velocity.CreateState(velocityGenerator(trackGenerationContext)),
                                    StateKinds.QuarterNoteDurationPower.CreateState(
                                        quarterNoteDurationPowerGenerator(trackGenerationContext)),
                                    StateKinds.NextNoteDurationFactor.CreateState(
                                        nextNoteDurationFactorGenerator(trackGenerationContext)),
                                    StateKinds.ChordRootNoteOffset.CreateState([
                                        chordRootOffsetGenerator(trackGenerationContext)
                                    ]),
                                }
                                .ToStateMap()
                                .MergeWith(trackStateMap);
                            var notePattern = notePatternGenerator(innerStateMap);
                            var innerEventStateTimelineMap = notePattern.GeneratedTimeline
                                .ToEventStateTimelineMap(innerStateMap.Subset(StateKinds.GetAll()));
                            return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                                trackNumber,
                                innerEventStateTimelineMap
                            );
                        });
                    return TrackEventStateTimelineMap.Create(4, trackNotePatters, StateTimelineMap.Empty);
                })
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
            var maxOffset = Math.Abs(Generators.SplineValue((6 - noteCount) / 6.0)(generationContext)) * 2;
            var noteOffsetRange = maxOffset / noteCount / 2;
            for (int i = 0; i < noteCount; i++)
            {
                var offset = noteInChordOffsetGenerator(generationContext) * noteOffsetRange;
                var chordNoteOffset = maxOffset * (i + 1) / noteCount + offset;
                chordOffsetCollectionBuilder.Add(chordNoteOffset);
            }

            return chordOffsetCollectionBuilder.ToImmutableArray();
        };
        var chordCollectionGenerator = Generators.Sequence(chordGenerator, 2);
        var chordIndexOffsetGenerator = Generators.Rank(HalfWeightRankGenerator, -2, 2);

        var commonStateHigherPatternGenerator = () =>
        {
            var seeds = Generators.Sequence(seedValueGenerator, 4)(generationContext);
            var seedSelector = Generators.ItemSelector(seeds);
            return Generators.Sequence(seedSelector, 4)(generationContext)
                .Select((seedValue, index) =>
                {
                    var innerGenerationContext = generationContext.CreateContext(seedValue);
                    return new IState[]
                        {
                            StateKinds.Velocity.CreateState(velocityGenerator(innerGenerationContext)),
                            StateKinds.QuarterNoteDurationPower.CreateState(
                                quarterNoteDurationPowerGenerator(innerGenerationContext)),
                            StateKinds.NextNoteDurationFactor.CreateState(
                                nextNoteDurationFactorGenerator(innerGenerationContext)),
                            StateKinds.ChordRootNoteOffset.CreateState([
                                chordRootOffsetGenerator(innerGenerationContext)
                            ]),
                            CompositionStateKinds.ChordNoteInScaleOffsets.Index.CreateState(
                                chordIndexOffsetGenerator(innerGenerationContext)),
                        }
                        .ToStateMap()
                        .ToTimelineItem(index * 4);
                })
                .ToStateTimelineMap(16);
        };

        var songStateMap = new IState[]
            {
                CompositionStateKinds.Rhythm.Period.Power.CreateState(
                    rhythmPatternPeriodPowerGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Period.PrimeIndex.CreateState(
                    rhythmPatternPeriodPrimeIndexGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Phase.Rank.CreateState(rhythmPatternPhaseRankGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Phase.RankedOffset.CreateState(
                    rhythmPatternPhaseRankedOffsetGenerator(generationContext)),
                CompositionStateKinds.Rhythm.MaxRank.CreateState(2),
                CompositionStateKinds.Rhythm.RankOffset.CreateState(
                    rhythmPatternRankOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalArticulationOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Collection.CreateState(
                    chordCollectionGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Index.CreateState(
                    chordIndexOffsetGenerator(generationContext)),
            }
            .ToStateMap();

        // statemap applied to all the instruments in a section
        var sectionStateMapGenerator = () => new IState[]
            {
                CompositionStateKinds.Rhythm.Period.Power.CreateState(
                    rhythmPatternPeriodPowerGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Period.PrimeIndex.CreateState(
                    rhythmPatternPeriodPrimeIndexGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Phase.Rank.CreateState(
                    rhythmPatternPhaseRankGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Phase.RankedOffset.CreateState(
                    rhythmPatternPhaseRankedOffsetGenerator(generationContext)),
                CompositionStateKinds.Rhythm.MaxRank.CreateState(
                    rhythmPatternMaxRankGenerator(generationContext)),
                CompositionStateKinds.Rhythm.RankOffset.CreateState(
                    rhythmPatternRankOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalArticulationOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Collection.CreateState(
                    chordCollectionGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Index.CreateState(
                    chordIndexOffsetGenerator(generationContext)),
                StateKinds.Velocity.CreateState(velocityGenerator(generationContext)),
                StateKinds.QuarterNoteDurationPower.CreateState(
                    quarterNoteDurationPowerGenerator(generationContext)),
                StateKinds.NextNoteDurationFactor.CreateState(nextNoteDurationFactorGenerator(generationContext)),
                StateKinds.ChordRootNoteOffset.CreateState([chordRootOffsetGenerator(generationContext)]),
            }
            .ToStateMap();

        // statemap applied to all the instruments in a group in a section (no chord root note state)
        var trackGroupSectionStateMapGenerator = () => new IState[]
            {
                CompositionStateKinds.Rhythm.Period.Power.CreateState(
                    rhythmPatternPeriodPowerGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Period.PrimeIndex.CreateState(
                    rhythmPatternPeriodPrimeIndexGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Phase.Rank.CreateState(
                    rhythmPatternPhaseRankGenerator(generationContext)),
                CompositionStateKinds.Rhythm.Phase.RankedOffset.CreateState(
                    rhythmPatternPhaseRankedOffsetGenerator(generationContext)),
                CompositionStateKinds.Rhythm.MaxRank.CreateState(
                    rhythmPatternMaxRankGenerator(generationContext)),
                CompositionStateKinds.Rhythm.RankOffset.CreateState(
                    rhythmPatternRankOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalArticulationOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalArticulationOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordRootNoteOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordRootNoteOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordNoteOffset.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.IncrementalChordNoteOffset.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Collection.CreateState(
                    chordCollectionGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Index.CreateState(
                    chordIndexOffsetGenerator(generationContext)),
                StateKinds.Velocity.CreateState(velocityGenerator(generationContext)),
                StateKinds.QuarterNoteDurationPower.CreateState(
                    quarterNoteDurationPowerGenerator(generationContext)),
                StateKinds.NextNoteDurationFactor.CreateState(nextNoteDurationFactorGenerator(generationContext)),
            }
            .ToStateMap();

        var nonGroupedTrackNumbers = trackDefinitions.Keys
            .Except(trackGroups.SelectMany(x => x.TrackNumbers))
            .ToImmutableSortedSet();
        var songSectionGenerator = (string sectionName) =>
        {
            var sectionStateMap = sectionStateMapGenerator().MergeWith(songStateMap);

            var trackTimelineMaps = new List<TrackEventStateTimelineMap<StateMap>>();
            
            foreach (var group in trackGroups)
            {
                var groupStateMap = trackGroupSectionStateMapGenerator()
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

        var commonStateMap = new IState[]
            {
                StateKinds.ScaleOffsets.CreateState([0, 2, 3, 5, 7, 8, 10]),
                StateKinds.KeyOffset.CreateState(generationContext.GenerateInt(0, 12)),
                StateKinds.Tempo.CreateState(1),
                StateKinds.QuarterNoteDurationPower.CreateState(quarterNoteDurationPowerGenerator(generationContext)),
                StateKinds.NextNoteDurationFactor.CreateState(nextNoteDurationFactorGenerator(generationContext)),
            }
            .ToStateMap();

        var songSectionCount = Generators.Int(6, 10)(generationContext);
        var songTrackNoteTimelineMap = SongSectionArchetypes.GenerateSongStructure(generationContext, songSectionCount)
            .Select(x => cachedSongSectionGenerator(x.Name))
            .Unroll()
            .MergeStateMap(commonStateMap);

        return new Song(songTrackNoteTimelineMap.Duration, trackDefinitions.ToImmutableSortedDictionary(),
            songTrackNoteTimelineMap);
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

    private static Func<IGenerationContext, ImmutableArray<double>> ToIncrementalGenerator(
        this StateMap stateMap,
        CompositionStateKinds.IncrementalStateKinds stateKindGroup,
        Func<IGenerationContext, double> randomValueGenerator
    )
    {
        var multiplier = stateMap.GetStateValue(stateKindGroup.Multiplier);
        if (multiplier.IsEqualToByEpsilon(0))
            return _ => ImmutableArray<double>.Empty;

        var consecutiveOffset = stateMap.GetStateValue(stateKindGroup.ConsecutiveOffset) * multiplier;
        var randomOffset = stateMap.GetStateValue(stateKindGroup.RandomOffset) * multiplier;
        var currentValue = 0.0;
        return innerContext =>
        {
            var randomValue = randomValueGenerator(innerContext);
            var value = currentValue + randomValue * randomOffset;
            currentValue += consecutiveOffset;
            return [value];
        };
    }
}