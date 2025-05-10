using System.Collections.Immutable;
using System.Numerics;
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
                    CompositionStateKinds.Value.ConsecutiveOffset.CreateState(
                        notePatternConsecutiveOffsetGenerator(generationContext)),
                    CompositionStateKinds.Value.RandomOffset.CreateState(
                        notePatternRandomOffsetGenerator(generationContext)),
                    StateKinds.Velocity.CreateState(velocityGenerator(generationContext)),
                    StateKinds.QuarterNoteDurationPower.CreateState(
                        quarterNoteDurationPowerGenerator(generationContext)),
                    StateKinds.NextNoteDurationFactor.CreateState(nextNoteDurationFactorGenerator(generationContext)),
                }
                .Concat(stateMap.States)
                .ToStateMap();
        };

        var singleChordNoteStateMap = new IState[]
            {
                StateKinds.ChordNoteOffset.CreateState([0]),
            }
            .ToStateMap();

        var trackDefinitions = new Dictionary<int, IInstrumentTrack>
        {
            [1] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default),
                PercussionInstrumentDefinition.Definitions[0].ArticulationCodes
            ),
            [2] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default),
                PercussionInstrumentDefinition.Definitions[2].ArticulationCodes
            ),
            [3] = new PercussionInstrumentTrack(
                trackDefinitionStateMapGenerator(StateMap.Default),
                PercussionInstrumentDefinition.Definitions[4].ArticulationCodes
            ),
            // chords instrument
            [4] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new IState[]
                    {
                        CompositionStateKinds.Control.ChordRootOffsetEnabled.CreateState(false),
                        CompositionStateKinds.Control.ChordNoteOffsetEnabled.CreateState(false),
                    }
                    .ToStateMap()
                ),
                pitchInstrumentCodeGenerator(),
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator()
            ),
            // melody instrument
            [5] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(singleChordNoteStateMap),
                pitchInstrumentCodeGenerator(),
                minOctaveOffsetGenerator(),
                maxOctaveOffsetGenerator()
            ),
            // bass instrument
            [6] = new PitchInstrumentTrack(
                trackDefinitionStateMapGenerator(
                    new IState[]
                        {
                            CompositionStateKinds.Control.ChordRootOffsetEnabled.CreateState(false),
                        }
                        .ToStateMap()
                        .MergeWith(singleChordNoteStateMap)
                ),
                pitchInstrumentCodeGenerator(),
                -3,
                -2
            )
        };

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
                var periodPrime = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.PrimeIndex)
                    .BounceInBounds(-RhythmPeriod.MaxPrimeIndex, RhythmPeriod.MaxPrimeIndex)
                    .ToRhythmPeriodValue();

                var phaseRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Rank)
                    .BounceInBounds(0, 2);
                var phaseRankedOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.RankedOffset)
                    .BounceInBounds(-1, 1);
                var phaseValue =
                    DyadicRankDistribution.GetHalfOffset(phaseRank, phaseRankedOffset);

                var periodValue = Math.Pow(2, periodPower) * periodPrime;

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

            var notePatternConsecutiveOffset = stateMap.GetStateValue(CompositionStateKinds.Value.ConsecutiveOffset);
            var notePatternRandomOffset = stateMap.GetStateValue(CompositionStateKinds.Value.RandomOffset);
            var seed = stateMap.GetStateValue(CompositionStateKinds.Value.Seed.Value);

            var chordNoteInScaleOffsets = stateMap
                .SelectValueFromCollectionByIndex(CompositionStateKinds.ChordNoteInScaleOffsets)
                .ToKind(StateKinds.ChordNoteInScaleOffsets);

            var chordRootOffsetEnabled = stateMap.GetStateValue(CompositionStateKinds.Control.ChordRootOffsetEnabled);
            var chordNoteOffsetEnabled = stateMap.GetStateValue(CompositionStateKinds.Control.ChordNoteOffsetEnabled);

            var currentConsecutiveOffset = 0.0;
            return DyadicRankItemPattern<StateMap>.Create(
                generationContext,
                rhythmPattern,
                innerContext => (int valueRank) =>
                {
                    var articulationOffset =
                        currentConsecutiveOffset
                        + articulationOffsetGenerator(innerContext) * notePatternRandomOffset;
                    var chordRootNoteOffset =
                        currentConsecutiveOffset
                        + chordNoteOffsetGenerator(innerContext) * notePatternRandomOffset;
                    currentConsecutiveOffset += notePatternConsecutiveOffset;

                    return new IState[]
                        {
                            StateKinds.ArticulationOffset.CreateState([articulationOffset]),
                            StateKinds.ChordRootNoteOffset.CreateState(
                                chordRootOffsetEnabled
                                    ? [chordRootNoteOffset]
                                    : []
                            ),
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
        var noteHigherPatternGenerator = (StateMap stateMap) =>
        {
            var seeds = Generators.Sequence(seedValueGenerator, 4)(generationContext);
            var seedSelector = Generators.ItemSelector(seeds);
            return Generators.Sequence(seedSelector, 4)(generationContext)
                .Select(seedValue =>
                {
                    var innerGenerationContext = generationContext.CreateContext(seedValue);
                    var innerStateMap = new IState[]
                        {
                            CompositionStateKinds.Rhythm.Seed.Value.CreateState(seedValue),
                            CompositionStateKinds.Value.Seed.Value.CreateState(seedValue),
                            CompositionStateKinds.Rhythm.Period.Power.CreateState(
                                rhythmPatternPeriodPowerGenerator(innerGenerationContext)),
                            CompositionStateKinds.Rhythm.Period.PrimeIndex.CreateState(
                                rhythmPatternPeriodPrimeIndexGenerator(innerGenerationContext)),
                            CompositionStateKinds.Rhythm.Phase.Rank.CreateState(
                                rhythmPatternPhaseRankGenerator(innerGenerationContext)),
                            CompositionStateKinds.Rhythm.Phase.RankedOffset.CreateState(
                                rhythmPatternPhaseRankedOffsetGenerator(innerGenerationContext)),
                            CompositionStateKinds.Rhythm.MaxRank.CreateState(
                                rhythmPatternMaxRankGenerator(innerGenerationContext)),
                            CompositionStateKinds.Rhythm.RankOffset.CreateState(
                                rhythmPatternRankOffsetGenerator(innerGenerationContext)),
                            CompositionStateKinds.Value.ConsecutiveOffset.CreateState(
                                notePatternConsecutiveOffsetGenerator(innerGenerationContext)),
                            CompositionStateKinds.Value.RandomOffset.CreateState(
                                notePatternRandomOffsetGenerator(innerGenerationContext)),
                            StateKinds.Velocity.CreateState(velocityGenerator(innerGenerationContext)),
                            StateKinds.QuarterNoteDurationPower.CreateState(
                                quarterNoteDurationPowerGenerator(innerGenerationContext)),
                            StateKinds.NextNoteDurationFactor.CreateState(
                                nextNoteDurationFactorGenerator(innerGenerationContext)),
                            StateKinds.ChordRootNoteOffset.CreateState([
                                chordRootOffsetGenerator(innerGenerationContext)
                            ]),
                        }
                        .ToStateMap()
                        .MergeWith(stateMap);
                    var notePattern = notePatternGenerator(innerStateMap);
                    return notePattern.GeneratedTimeline;
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
                CompositionStateKinds.Value.ConsecutiveOffset.CreateState(
                    notePatternConsecutiveOffsetGenerator(generationContext)),
                CompositionStateKinds.Value.RandomOffset.CreateState(
                    notePatternRandomOffsetGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Collection.CreateState(
                    chordCollectionGenerator(generationContext)),
                CompositionStateKinds.ChordNoteInScaleOffsets.Index.CreateState(
                    chordIndexOffsetGenerator(generationContext)),
            }
            .ToStateMap();

        var songSectionGenerator = (string sectionName) =>
        {
            var sectionStateMap = new IState[]
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
                    CompositionStateKinds.Value.ConsecutiveOffset.CreateState(
                        notePatternConsecutiveOffsetGenerator(generationContext)),
                    CompositionStateKinds.Value.RandomOffset.CreateState(
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
                .ToStateMap()
                .MergeWith(songStateMap);

            var trackPatterns = trackDefinitions
                .Select(p =>
                {
                    var trackNumber = p.Key;
                    var trackDefinition = p.Value;

                    var combinedStateMap = trackDefinitionStateMapGenerator(trackDefinition.StateMap)
                        .MergeWith(sectionStateMap);
                    var innerPattern = noteHigherPatternGenerator(combinedStateMap);
                    var trackPattern = Enumerable.Repeat(innerPattern, 2)
                        .Unroll()
                        .ToEventStateTimelineMap(trackDefinition.StateMap.Except(CompositionStateKinds.GetAll()));
                    return new KeyValuePair<int, EventStateTimelineMap<StateMap>>(trackNumber, trackPattern);
                });

            var commonStatePattern = commonStateHigherPatternGenerator()
                .MergeStateMap(sectionStateMap.Subset(StateKinds.GetAll()))
                .Repeat(2);
            return TrackEventStateTimelineMap.Create(32, trackPatterns, commonStatePattern);
        };
        Func<string, TrackEventStateTimelineMap<StateMap>> cachedSongSectionGenerator
            = songSectionGenerator.CacheGeneratedValues();

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
}