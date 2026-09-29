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

    // the key an answer's bar derives its fresh rhythm's seed from its bar pattern's by
    private const int AnswerRhythmKey = 1;

    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    private static readonly Func<IGenerationContext, double> ArticulationOffset = Generators.SplineValue();

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
    /// <param name="barDrums">How the drums play the bars of a letter: the bars a track sits out, keeping its state there with no notes, and the strokes it changes to.</param>
    /// <param name="barStateTimelineMap">The state that changes by bar, such as the chord, along the 4-bar pattern.</param>
    /// <param name="answer">
    ///     For a track that answers its 4-bar pattern, as the melody does, how its answer plays: the pattern's bars again,
    ///     of the same bar patterns, a bar as the question's did or, drawing its rhythm afresh, of the same settings on
    ///     another rhythm, and the phrase ending where the answer has it; none for no answer.
    /// </param>
    /// <param name="context">The section's random sequence.</param>
    public GeneratedBars GenerateBars(
        IGenerationContext context,
        int sectionId,
        ImmutableDictionary<int, StateMap> trackStateMaps,
        BarDrums barDrums,
        ImmutableDictionary<int, Doubling> doubles,
        ImmutableDictionary<int, int> feelLeads,
        StateTimelineMap barStateTimelineMap,
        SectionRhythm sectionRhythm,
        MelodyAnswer? answer
    )
    {
        var seeds = DrawSeeds(context, trackStateMaps.Keys, sectionRhythm.Scheme);
        return BuildBars(seeds, sectionId, trackStateMaps, barDrums, doubles, feelLeads, barStateTimelineMap, sectionRhythm, answer, []);
    }

    /// <summary>The seeds of the tracks' bar patterns, a set for every letter of the scheme, one seed per track.</summary>
    public static ImmutableArray<ImmutableDictionary<int, int>> DrawSeeds(IGenerationContext context, IEnumerable<int> trackNumbers, PhraseScheme scheme)
    {
        var tracks = trackNumbers.ToArray();
        var trackSeedMapGenerator = (IGenerationContext innerContext) => tracks.ToImmutableDictionary(x => x, _ => SeedGenerator(innerContext));
        return Generators.Sequence(trackSeedMapGenerator, scheme.PatternCount)(context);
    }

    /// <summary>
    ///     The bars of <see cref="GenerateBars" />, of the seeds given (<see cref="DrawSeeds" />), a bar with a rhythm
    ///     key drawing its rhythm afresh by it, as an answer's does by its own.
    /// </summary>
    /// <param name="rhythmKeys">Every bar's key to draw its rhythm afresh by, 0 for none; none for no bar.</param>
    /// <param name="feelLeads">The tracks that play on another's feel, its tuplet, and the tracks whose feel they play on.</param>
    public GeneratedBars BuildBars(
        ImmutableArray<ImmutableDictionary<int, int>> trackSeedMaps,
        int sectionId,
        ImmutableDictionary<int, StateMap> trackStateMaps,
        BarDrums barDrums,
        ImmutableDictionary<int, Doubling> doubles,
        ImmutableDictionary<int, int> feelLeads,
        StateTimelineMap barStateTimelineMap,
        SectionRhythm sectionRhythm,
        MelodyAnswer? answer,
        ImmutableArray<int> rhythmKeys
    )
    {
        // a bar pattern's own layer, drawn for every track and bar; no chord root offset here: every track plays the
        // progression's chord, and a track leaves it only by moving its root from note to note
        var barPatternLayerGenerator = new StateMapBuilder("Bar pattern", perTrack: true)
            .AddRhythmLayer(sectionRhythm.Unconventionality.Lean(RhythmLayers.BarPattern))
            .AddNoteWalkLayer()
            .Add(StateKinds.Velocity, VelocityLayers.CreateGenerator(VelocityLayers.BarPattern))
            .AddNoteDurationLayer()
            .ToStateMapGenerator();

        var scheme = sectionRhythm.Scheme;
        var feels = new List<BarFeel>();
        // the question's letters, and the answer's, the same again
        var letters = answer is null ? scheme.Letters : [..scheme.Letters, ..scheme.Letters];
        var timeline = letters
            .Select((letter, barIndex) =>
                {
                    var patternBar = barIndex % scheme.Letters.Length;
                    var inAnswer = barIndex >= scheme.Letters.Length;
                    var redrawsRhythm = inAnswer && answer!.RedrawsRhythm[patternBar];
                    var rhythmKey = rhythmKeys.IsDefaultOrEmpty ? 0 : rhythmKeys[barIndex];
                    var phraseEnd = inAnswer ? answer!.PhraseEnd : null;
                    // a drum that doubles or accents a lead plays its lead's bar patterns, and one that plays a figure of its
                    // own on the lead's feel its own; a track that plays on another's feel makes its bar after that one's
                    var seeds = trackSeedMaps[letter];
                    var barFeels = new List<BarFeel>();
                    var bars = new Dictionary<int, EventStateTimelineMap<StateMap>>();
                    foreach (var x in seeds.OrderBy(x => feelLeads.ContainsKey(x.Key)))
                        bars[x.Key] = GenerateBar(
                                x.Key,
                                doubles.TryGetValue(x.Key, out var doubling) && doubling.Binding != DrumBinding.Figure ? seeds[doubling.Lead] : x.Value,
                                trackStateMaps[x.Key],
                                barStateTimelineMap,
                                sectionId,
                                barIndex,
                                barPatternLayerGenerator,
                                scheme.IsVaried[patternBar],
                                redrawsRhythm,
                                rhythmKey,
                                phraseEnd,
                                barDrums.Resting.Contains((x.Key, letter)),
                                barDrums.Strokes.TryGetValue((x.Key, letter), out var stroke) ? stroke : null,
                                scheme.ToString(),
                                sectionRhythm.Energy,
                                doubles.GetValueOrDefault(x.Key),
                                feelLeads.TryGetValue(x.Key, out var feelLead) ? barFeels.Single(feel => feel.Track == feelLead).Rhythm : null,
                                barFeels
                            );
                    feels.AddRange(seeds.Select(x => barFeels.Single(feel => feel.Track == x.Key)));
                    var trackNotePatterns = seeds.Select(x => new KeyValuePair<int, EventStateTimelineMap<StateMap>>(x.Key, bars[x.Key]));
                    return TrackEventStateTimelineMap.Create(Meter.BarDuration, trackNotePatterns, StateTimelineMap.Create(Meter.BarDuration));
                }
            )
            .Unroll();
        return new GeneratedBars(timeline, [..feels]);
    }

    /// <summary>Whether a drum that accents its lead plays the lead's beat at the place given, as the bar pattern's seed has it.</summary>
    private static bool IsAccented(int seed, int trackNumber, double position, double share)
    {
        var key = Seeds.Derive(Seeds.Derive(seed, trackNumber), (int)Math.Round(position * AccentPlacesPerBeat));
        return new GenerationContext(key).TestProbability(share);
    }

    // the state that makes a track's feel, the tuplet its cycles play in, which its grouping follows
    private static readonly IStateKind FeelKind = CompositionStateKinds.Rhythm.Period.PrimeIndex;

    // how finely the places of a bar are told apart, where a drum accents its lead: a 48th of a beat, which holds the
    // 16ths, their triplets and the 32nds
    private const int AccentPlacesPerBeat = 48;

    /// <summary>The seed of a bar's rhythm: its bar pattern's, drawn afresh for an answer, and again by its own key.</summary>
    private static int GetRhythmSeed(int seed, bool redrawsRhythm, int rhythmKey)
    {
        var answered = redrawsRhythm ? Seeds.Derive(seed, AnswerRhythmKey) : seed;
        return rhythmKey == 0 ? answered : Seeds.Derive(answered, rhythmKey);
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
        bool redrawsRhythm,
        int rhythmKey,
        int? phraseEnd,
        bool isResting,
        int? stroke,
        string scheme,
        Tilt energy,
        Doubling? doubling,
        StateMap? leadFeel,
        List<BarFeel> feels
    )
    {
        var patternSeeds = CreatePatternSeeds(seed);
        var trackGenerationContext = _context.CreateContext(patternSeeds.TrackState);
        // an answer's bar that draws its rhythm afresh plays its bar pattern's settings on another rhythm
        var builder = new StateMapBuilder("Bar pattern", perTrack: true)
            .Add(barPatternLayerGenerator)
            .Add(CompositionStateKinds.Rhythm.Seed, GetRhythmSeed(patternSeeds.Rhythm, redrawsRhythm, rhythmKey))
            .Add(CompositionStateKinds.ValueSeed, seed);
        // a varied repeat plays its bar pattern with its cycles drawn afresh more often: it starts as the first did
        if (isVaried)
            builder.Add(CompositionStateKinds.Rhythm.Variation, PhraseSchemes.VariedRepeatVariation);
        // a drum's stroke, where the bar changes it from its section's
        if (stroke is { } barStroke)
            builder.Add(DrumStrokes.At(StateDepths.BarPattern, barStroke));
        var stateMap = builder
            .ToStateMap(trackGenerationContext)
            .MergeWith(trackStateMap);
        // a track on another's feel plays its tuplet, all its layers' steps of it in place of its own
        if (leadFeel is not null)
            stateMap = stateMap.Except([FeelKind]).MergeWith(leadFeel.Subset([FeelKind]));
        // an answer's bar is recorded at its place in the 4-bar pattern, as its question's is
        StateTrace.Record(TracePoints.BarPattern, trackNumber, sectionId, barIndex % Progressions.BarCount, stateMap, phrase: scheme);

        // the bar's place in the 4-bar pattern, whose state an answer's bar plays over as its question's does
        var patternBar = barIndex % Progressions.BarCount;
        var notes = isResting
            ? EventTimeline.Create<StateMap>(Meter.BarDuration)
            : GenerateNotes(stateMap, barStateTimelineMap, patternBar * Meter.BarDuration, trackNumber, sectionId, patternBar, energy).GeneratedTimeline;
        // a drum bound to a lead plays its strong beats, and one that accents it a share of them, the same ones wherever
        // its bar pattern plays, by a draw keyed by the drum and the beat's place
        if (doubling is not null)
            notes = EventTimeline.Create(
                notes.Duration,
                notes.Where(x => x.Value.GetStateValue(CompositionStateKinds.BeatRank) <= doubling.MaxRank
                                 && (doubling.Binding != DrumBinding.Accent || IsAccented(seed, trackNumber, x.Position, doubling.Share)))
            );
        // a bass bar that leads into the next chord plays a note in its last beat, for its approach to play on
        if (_trackDefinitions[trackNumber].Role == TrackRole.Bass)
            notes = LeadIn(notes, stateMap, barStateTimelineMap, barIndex, _rhythmPatternGenerator(stateMap).MaxRank);
        // a melody's phrase ends where its bar has it, or where its answer does
        var barEnd = barStateTimelineMap.GetEffectiveStateMapAt(patternBar * Meter.BarDuration).GetStateValue(CompositionStateKinds.MelodyPhraseEnd);
        if (_trackDefinitions[trackNumber].Role == TrackRole.Melody)
            notes = LinePattern.EndPhrase(notes, patternBar == Progressions.BarCount - 1 && phraseEnd is { } answerEnd ? answerEnd : barEnd);
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
        int barIndex,
        Tilt energy
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

        // a line's notes draw where they mean to go, by its profile
        var line = _trackDefinitions[trackNumber].Role switch
        {
            TrackRole.Melody => new LinePattern(stateMap, MelodyLayers.Line),
            TrackRole.Bass => new LinePattern(stateMap, BassLeadingLayers.Line),
            _ => null
        };
        // a drum that strikes may accent a note with another of its sounds
        var accentSounds = _trackDefinitions[trackNumber] is PercussionInstrumentTrack drum && drum.Sounds.Any(x => x.Accent > 0) ? drum.Sounds : [];
        var dynamics = stateMap.GetStateValue(CompositionStateKinds.NoteDynamics);
        // a note's values, which a note of a repeated cycle takes from the note it repeats
        var noteValuesGenerator = (IGenerationContext innerContext, double position, KeptBeat beat) =>
        {
            var rank = beat.Rank;
            var builder = new StateMapBuilder("Note", perTrack: true)
                .Add(StateKinds.ArticulationOffset, articulationOffsetGenerator(innerContext, position))
                .Add(StateKinds.Velocity, BeatAccent.CreateVelocityGenerator(rank, rhythmPattern.MaxRank, dynamics).Then(x => x * VelocityLayers.Note))
                .AddNoteDurationLayer()
                .Add(CompositionStateKinds.BeatRank, rank);
            line?.AddNoteState(builder, beat);
            if (!accentSounds.IsEmpty)
                builder.Add(context => DrumAccents.Draw(context, accentSounds, rank, energy));
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
    ///     A bass bar's notes with one in the beat before every change of chord in it or at its end, where the chord
    ///     before leads into the next (<see cref="StateKinds.ChordApproach" />) and none starts there: the note sounding
    ///     there played again, over the chord at its place and on the rhythm's weakest beat, as lightly as a note there,
    ///     so that the approach has a note to play on; marked as a pickup, which stays only where the chord does change,
    ///     as the song put together knows, the next section's chord too (<see cref="LinePattern.Place" />).
    /// </summary>
    /// <param name="maxRank">The weakest rank of the bar's rhythm.</param>
    internal static EventTimeline<StateMap> LeadIn(
        EventTimeline<StateMap> notes,
        StateMap stateMap,
        StateTimelineMap barStateTimelineMap,
        int barIndex,
        int maxRank
    )
    {
        var patternBar = barIndex % Progressions.BarCount;
        var start = patternBar * Meter.BarDuration;
        var end = start + Meter.BarDuration;
        // the changes of chord in the bar, and at its end where the next bar starts a chord or the pattern starts again
        var patternChanges = barStateTimelineMap.GetStateTimeline(StateKinds.ChordChange).Select(x => x.Position).ToArray();
        var changes = patternChanges
            .Where(x => x > start + 1e-9 && x < end - 1e-9)
            .Append(end)
            .Where(x => x >= Meter.PatternDuration - 1e-9 || patternChanges.Any(c => Math.Abs(c - x) < 1e-9))
            .Select(x => x - start);

        var led = notes.ToList();
        foreach (var change in changes)
        {
            var pickup = change - 1;
            var before = led.Where(x => x.Position < pickup - 1e-9).ToArray();
            if (barStateTimelineMap.GetEffectiveStateMapAt(start + pickup).GetStateValue(StateKinds.ChordApproach) == 0
                || before.Length == 0
                || led.Any(x => x.Position >= pickup - 1e-9 && x.Position < change - 1e-9))
                continue;

            var chord = PickChord(stateMap, barStateTimelineMap.GetEffectiveStateMapAt(start + pickup));
            var sounding = before[^1].Value;
            var rank = sounding.GetStateValue(CompositionStateKinds.BeatRank);
            var velocity = sounding.GetStateValue(StateKinds.Velocity)
                + VelocityLayers.Note * BeatAccent.GetShift(rank, maxRank, maxRank, stateMap.GetStateValue(CompositionStateKinds.NoteDynamics));
            var note = sounding
                .Except([StateKinds.ChordNotePitchOffsets, StateKinds.ChordVoicingFixed])
                .MergeWith(chord)
                .With(StateKinds.Velocity, velocity)
                .With(CompositionStateKinds.BeatRank, maxRank)
                .With(CompositionStateKinds.LinePickup, 1);
            led.Add(note.ToTimelineItem(pickup));
            led.Sort((x, y) => x.Position.CompareTo(y.Position));
        }

        return led.Count == notes.Count ? notes : EventTimeline.Create(notes.Duration, led);
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
        // the chord at the note: its shape from the pool and index, and its root from the section's home and the
        // progression
        if (StateTrace.IsRunning)
            StateTrace.Record(
                TracePoints.Chord,
                trackNumber,
                sectionId,
                barIndex,
                stateMap
                    .Subset([CompositionStateKinds.ChordPool.Collection, CompositionStateKinds.ChordPool.Index, StateKinds.ChordRoot])
                    .MergeWith(
                        barStateMap.Subset([CompositionStateKinds.ChordPool.Index, StateKinds.ChordRoot, CompositionStateKinds.RoleChord])
                    ),
                position
            );

        return PickChord(stateMap, barStateMap);
    }

    /// <summary>The chord at a position, from the bar's state there, as <see cref="GetChord" /> has it, unrecorded.</summary>
    private static StateMap PickChord(StateMap stateMap, StateMap barStateMap)
    {
        var chordStateMap = stateMap.MergeWith(barStateMap.Subset([CompositionStateKinds.ChordPool.Index]));
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
                    [..CompositionStateKinds.IncrementalArticulationOffset.GetAll()]
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
            new DyadicTimelineDescriptor(Meter.BarDuration, period, phase, maxRank, ResolvedRhythm.RestartOf(period)),
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

    /// <summary>The grid a bar pattern's notes fall on, in beats: 16ths, or a tuplet's where the period divides the beat by one.</summary>
    public const double Grid = 0.25;

    /// <summary>
    ///     Whether a cycle groups the grid's steps by a number that is no power of two, such as the dotted 8th's three
    ///     16ths: its notes fall on the grid, but its halves would not, and it fits the bar only by being cut off.
    /// </summary>
    /// <param name="period">The cycle, in beats.</param>
    public static bool IsGrouped(double period)
    {
        var steps = period / Grid;
        var whole = Math.Round(steps);
        return Math.Abs(steps - whole) < 1e-9 && whole > 0 && Math.Abs(Math.Log2(whole) - Math.Round(Math.Log2(whole))) > 1e-9;
    }

    /// <summary>
    ///     How often a cycle starts again, in beats: a grouped one every smallest power-of-two span that holds two of it,
    ///     so that a dotted 8th's plays 3+3+2 every half bar, as a tresillo does, where cut off at the bar it would crowd
    ///     its last note onto the next bar's first; any other the bar.
    /// </summary>
    /// <param name="period">The cycle, in beats.</param>
    public static double RestartOf(double period)
    {
        return IsGrouped(period) ? Math.Min(Meter.BarDuration, Math.Pow(2, Math.Ceiling(Math.Log2(2 * period) - 1e-9))) : Meter.BarDuration;
    }

    /// <summary>
    ///     The finest rank a cycle plays on the grid: a grouped one only as far as its halves are whole steps of it, as a
    ///     dotted 8th's are none, where they would fall between the 16ths; any other down to <see cref="MaxRankLimit" />.
    /// </summary>
    /// <param name="period">The cycle, in beats.</param>
    public static int GridRankLimit(double period)
    {
        if (!IsGrouped(period))
            return MaxRankLimit;

        var rank = 0;
        while (rank < MaxRankLimit && IsWhole(period / Math.Pow(2, rank + 1) / Grid))
            rank++;
        return rank;

        static bool IsWhole(double steps) => Math.Abs(steps - Math.Round(steps)) < 1e-9;
    }

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

        // a bar pattern's to its limit, and a fill's as fine as its shortest note; a grouped cycle's no finer than the grid
        var maxRankLimit = minNote is { } note
            ? Math.Max(0, (int)Math.Floor(Math.Log2(periodValue * Meter.BarDuration / note) + 1e-9))
            : MaxRankLimit;
        if (IsGrouped(periodValue * Meter.BarDuration))
            maxRankLimit = Math.Min(maxRankLimit, GridRankLimit(periodValue * Meter.BarDuration));
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

/// <summary>
///     A drum that doubles a lead: the lead's track, the weakest rank of its beats it plays, how it is bound to the lead,
///     and the share of those beats it plays, for a drum that accents it.
/// </summary>
public sealed record Doubling(int Lead, int MaxRank, DrumBinding Binding, double Share);
