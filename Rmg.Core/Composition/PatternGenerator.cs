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

    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    private static readonly Func<IGenerationContext, double> ArticulationOffset = Generators.SplineValue();
    private static readonly Func<IGenerationContext, double> ChordRootNoteOffset = Generators.SplineValue();
    private static readonly Func<IGenerationContext, double> ChordNoteOffset = Generators.SplineValue();
    private static readonly Func<IGenerationContext, double> PatternChordNoteOffset = Generators.SplineValue();

    private readonly IGenerationContext _context;
    private readonly ImmutableSortedDictionary<int, IInstrumentTrack> _trackDefinitions;

    // the rhythm of a bar pattern, by its resolved settings, which many bar patterns share
    private readonly Func<StateMap, DyadicRankThresholdPattern> _rhythmPatternGenerator;

    public PatternGenerator(IGenerationContext context, ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions)
    {
        _context = context;
        _trackDefinitions = trackDefinitions;
        _rhythmPatternGenerator = ((Func<StateMap, DyadicRankThresholdPattern>)GenerateRhythmPattern)
            .CacheGeneratedValues()
            .MapInput((StateMap stateMap) => ResolveRhythm(stateMap));
    }

    /// <summary>
    ///     The four bars of the tracks' pattern in a section, as the section's phrase scheme has them: a set of seeds is
    ///     drawn for every letter of the scheme, one seed per track, and every bar takes its letter's, so the tracks
    ///     change their patterns together and bars come back where the scheme repeats them.
    /// </summary>
    /// <param name="trackStateMaps">Every track's state in the section.</param>
    /// <param name="barStateTimelineMap">The state that changes by bar, such as the chord, along the 4-bar pattern.</param>
    /// <param name="context">The section's random sequence.</param>
    public GeneratedBars GenerateBars(
        IGenerationContext context,
        int sectionId,
        ImmutableDictionary<int, StateMap> trackStateMaps,
        StateTimelineMap barStateTimelineMap,
        SectionRhythm sectionRhythm
    )
    {
        // a bar pattern's own layer, drawn for every track and bar; no chord root offset here: every track plays the
        // progression's chord, and a track leaves it only by moving its root from note to note
        var barPatternLayerGenerator = new StateMapBuilder("Bar pattern", perTrack: true)
            .AddRhythmLayer(sectionRhythm.Unconventionality.Scale(RhythmLayers.BarPattern))
            .AddNoteWalkLayer()
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.BarPattern))
            .AddNoteDurationLayer()
            .ToStateMapGenerator();

        var scheme = sectionRhythm.Scheme;
        var trackSeedMapGenerator = (IGenerationContext innerContext) =>
            trackStateMaps.Keys.ToDictionary(x => x, _ => SeedGenerator(innerContext));
        var trackSeedMaps = Generators.Sequence(trackSeedMapGenerator, scheme.PatternCount)(context);
        var feels = new List<BarFeel>();
        var timeline = scheme.Letters
            .Select((letter, barIndex) =>
                {
                    var trackNotePatterns = trackSeedMaps[letter].Select(x =>
                        new KeyValuePair<int, EventStateTimelineMap<StateMap>>(
                            x.Key,
                            GenerateBar(
                                x.Key,
                                x.Value,
                                trackStateMaps[x.Key],
                                barStateTimelineMap,
                                sectionId,
                                barIndex,
                                barPatternLayerGenerator,
                                scheme.IsVaried[barIndex],
                                scheme.ToString(),
                                feels
                            )
                        )
                    );
                    return TrackEventStateTimelineMap.Create(Meter.BarDuration, trackNotePatterns, StateTimelineMap.Create(Meter.BarDuration));
                }
            )
            .Unroll();
        return new GeneratedBars(timeline, [..feels]);
    }

    /// <summary>A track's bar: its bar pattern's state over the track's, and the notes of its rhythm.</summary>
    private EventStateTimelineMap<StateMap> GenerateBar(
        int trackNumber,
        int seed,
        StateMap trackStateMap,
        StateTimelineMap barStateTimelineMap,
        int sectionId,
        int barIndex,
        Func<IGenerationContext, StateMap> barPatternLayerGenerator,
        bool isVaried,
        string scheme,
        List<BarFeel> feels
    )
    {
        var patternSeeds = CreatePatternSeeds(seed);
        var trackGenerationContext = _context.CreateContext(patternSeeds.TrackState);
        var builder = new StateMapBuilder("Bar pattern", perTrack: true)
            .Add(barPatternLayerGenerator)
            .Add(CompositionStateKinds.Rhythm.Seed, patternSeeds.Rhythm)
            .Add(CompositionStateKinds.ValueSeed, seed);
        // a varied repeat plays its bar pattern with its cycles drawn afresh more often: it starts as the first did
        if (isVaried)
            builder.Add(CompositionStateKinds.Rhythm.Variation, PhraseSchemes.VariedRepeatVariation);
        var stateMap = builder
            .ToStateMap(trackGenerationContext)
            .MergeWith(trackStateMap)
            .MergeWith(CreatePatternChordNoteOffset(_trackDefinitions[trackNumber], trackStateMap, trackGenerationContext));
        StateTrace.Record(TracePoints.BarPattern, trackNumber, sectionId, barIndex, stateMap, phrase: scheme);

        var notes = GenerateNotes(stateMap, barStateTimelineMap, barIndex * Meter.BarDuration, trackNumber, sectionId, barIndex)
            .GeneratedTimeline;
        if (_trackDefinitions[trackNumber].Role == TrackRole.Melody)
            notes = MelodyPattern.EndPhrase(
                notes,
                barStateTimelineMap.GetEffectiveStateMapAt(barIndex * Meter.BarDuration).GetStateValue(CompositionStateKinds.MelodyPhraseEnd)
            );
        feels.Add(new BarFeel(trackNumber, barIndex, stateMap, notes.Count));
        return notes.ToEventStateTimelineMap(stateMap.OfScope(StateScope.Render));
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

        var melody = _trackDefinitions[trackNumber].Role == TrackRole.Melody ? new MelodyPattern(stateMap) : null;
        var dynamics = stateMap.GetStateValue(CompositionStateKinds.NoteDynamics);
        // a note's values, which a note of a repeated cycle takes from the note it repeats
        var noteValuesGenerator = (IGenerationContext innerContext, double position, KeptBeat beat) =>
        {
            var rank = beat.Rank;
            var builder = new StateMapBuilder("Note", perTrack: true)
                .Add(StateKinds.ArticulationOffset, articulationOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordRootNoteOffset, chordRootNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.ChordNoteOffset, chordNoteOffsetGenerator(innerContext, position))
                .Add(StateKinds.Velocity, BeatAccent.CreateVelocityGenerator(rank, rhythmPattern.MaxRank, dynamics).Then(x => x * VelocityLayers.Note))
                .AddNoteDurationLayer()
                .Add(CompositionStateKinds.BeatRank, rank);
            melody?.AddNoteState(builder, beat);
            return builder.ToStateMap(innerContext);
        };

        // and the chord at its own position
        return DyadicRankItemPattern<StateMap>.Create(
            _context,
            rhythmPattern,
            innerContext => (position, beat) => noteValuesGenerator(innerContext, position, beat),
            patternSeeds.NoteValues,
            (position, _, values) => values.MergeWith(GetChord(stateMap, barStateTimelineMap, patternStart, position, trackNumber, sectionId, barIndex))
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
                TracePoints.Chord,
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
        var period = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.Value) * Meter.BarDuration;
        var phase = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Value) * Meter.BarDuration;
        var maxRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.MaxRank);
        var seed = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Seed);
        var rankOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.RankOffset);
        var fullness = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Fullness);
        var variation = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Variation);

        return DyadicRankThresholdPattern.Create(
            _context,
            seed,
            WeightUtil.CreateGeometricRankWeightFunc(rankOffset, 0, 1.0, fullness),
            new DyadicTimelineDescriptor(Meter.BarDuration, period, phase, maxRank),
            variation
        );
    }

    /// <summary>
    ///     The rhythm settings of a bar pattern, which its layers added up, folded into their ranges and turned into a
    ///     period and a phase.
    /// </summary>
    private StateMap ResolveRhythm(StateMap stateMap)
    {
        var rhythm = ResolvedRhythm.Of(stateMap);
        return new StateMapBuilder("Resolved rhythm", perTrack: true)
            .Add(CompositionStateKinds.Rhythm.Fullness, rhythm.Fullness)
            .Add(CompositionStateKinds.Rhythm.Variation, rhythm.Variation)
            .Add(CompositionStateKinds.Rhythm.Period.Value, rhythm.PeriodValue)
            .Add(CompositionStateKinds.Rhythm.Phase.Value, rhythm.PhaseValue)
            .Add(CompositionStateKinds.Rhythm.MaxRank, rhythm.MaxRank)
            .Add(stateMap.GetState(CompositionStateKinds.Rhythm.RankOffset).Map(x => x.BounceInBounds(0, rhythm.MaxRank)))
            .Add(stateMap.GetState(CompositionStateKinds.Rhythm.Seed))
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

/// <summary>The bars of the tracks' patterns in a section, and the feel every track's bar plays in.</summary>
internal sealed record GeneratedBars(TrackEventStateTimelineMap<StateMap> Timeline, ImmutableArray<BarFeel> Feels);

/// <summary>The feel of a track's bar: the state its rhythm is resolved from, and how many notes it has.</summary>
/// <param name="Bar">The bar of the section's 4-bar pattern.</param>
internal readonly record struct BarFeel(int Track, int Bar, StateMap Rhythm, int NoteCount);

/// <summary>
///     A bar pattern's rhythm settings, which its layers added up, folded into their ranges; its period and phase in
///     bars.
/// </summary>
internal readonly record struct ResolvedRhythm(
    double PeriodValue,
    double PhaseValue,
    int MaxRank,
    int PrimeIndex,
    double Fullness,
    double Variation,
    int RankOffset = 0
)
{
    /// <summary>The finest rank a bar pattern plays down to.</summary>
    public const int MaxRankLimit = 2;

    /// <summary>A straight rhythm of a beat, down to 16ths, for a section whose drums play nothing.</summary>
    public static StateMap DefaultState { get; } = StateMap.FromStates(
        [
            CompositionStateKinds.Rhythm.Period.Power.CreateState(-2),
            CompositionStateKinds.Rhythm.MaxRank.CreateState(MaxRankLimit),
            CompositionStateKinds.Rhythm.Fullness.CreateState(RhythmSettings.Fullness),
            CompositionStateKinds.Rhythm.Variation.CreateState(RhythmSettings.Variation)
        ]
    );

    /// <summary>The period in beats.</summary>
    public double Period => PeriodValue * Meter.BarDuration;

    /// <summary>The phase in beats.</summary>
    public double Phase => PhaseValue * Meter.BarDuration;

    /// <param name="minNote">
    ///     The shortest note, in beats, which sets the finest rank the rhythm folds into, as a fill's does; none for a
    ///     bar pattern's, which folds into <see cref="MaxRankLimit" />.
    /// </param>
    public static ResolvedRhythm Of(StateMap stateMap, double? minNote = null)
    {
        var periodPower = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.Power)
            .BounceInBounds(-2, 1);
        var primeIndex = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Period.PrimeIndex)
            .BounceInBounds(-RhythmPeriod.MaxPrimeIndex, RhythmPeriod.MaxPrimeIndex);
        var periodValue = Math.Pow(2, periodPower) * primeIndex.ToRhythmPeriodValue();

        var maxRankLimit = minNote is { } note
            ? Math.Max(0, (int)Math.Floor(Math.Log2(periodValue * Meter.BarDuration / note) + 1e-9))
            : MaxRankLimit;
        var maxRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.MaxRank)
            .BounceInBounds(0, maxRankLimit);

        var phaseRank = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.Rank)
            .BounceInBounds(0, 2);
        var phaseRankedOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.Phase.RankedOffset)
            .BounceInBounds(-1, 1);
        var phaseValue = DyadicRankDistribution.GetHalfOffset(phaseRank, phaseRankedOffset) * periodValue;

        // fullness and variation keep to their ranges; the song sets where they start
        var fullness = Math.Clamp(stateMap.GetStateValue(CompositionStateKinds.Rhythm.Fullness), RhythmSettings.MinFullness, 1);
        var variation = Math.Clamp(stateMap.GetStateValue(CompositionStateKinds.Rhythm.Variation), 0, 1);
        var rankOffset = stateMap.GetStateValue(CompositionStateKinds.Rhythm.RankOffset).BounceInBounds(0, maxRank);
        return new ResolvedRhythm(periodValue, phaseValue, maxRank, primeIndex, fullness, variation, rankOffset);
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
