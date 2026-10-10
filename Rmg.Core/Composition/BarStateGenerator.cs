using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The state of a section that changes from bar to bar along its pattern, the same for every track: its
///     scale, which holds for all of it, the progression's roots, the raised seventh of the cadence, the home and cadence chords, the pick from the chord
///     pool, the bar's loudness and note lengths, how the chords and the bass move into the next bar, and the shape of
///     the melody's phrase and where it ends.
/// </summary>
internal sealed class BarStateGenerator
{
    // how many values each state that changes by bar picks from along the pattern, so that bars repeat theirs
    private const int PoolSize = 4;

    // the stream of the melody contour's period, apart from the timeline generators'
    private const int ContourPeriodStream = 100;

    // how the bass leads into its chords
    private const int BassLeadingStream = 101;

    // the home's and the cadence's chords, the voicing's resets, the melody's contour and its phrase end, each its own
    private const int HomeChordStream = 102, CadenceChordStream = 103, ResetsStream = 104, ContourStream = 105, PhraseEndStream = 106;

    // the state that changes along the pattern besides the progression; every state has its own timeline, so each can
    // change at its own pace
    private readonly ImmutableArray<IStateTimelineGenerator> _timelineGenerators;

    private readonly Meter _meter;

    // the genre whose bass leads and lands as it has them
    private readonly Genre _genre;

    /// <param name="meter">The meter the section's bars are in.</param>
    /// <param name="genre">The genre whose bass leads into its chords and lands on them as it has them.</param>
    public BarStateGenerator(Meter meter, Genre genre)
    {
        _meter = meter;
        _genre = genre;
        _timelineGenerators =
        [
            StateTimelineGenerator.Create(
                StateKinds.Velocity,
                meter.BarDuration,
                VelocityLayers.CreateGenerator(VelocityLayers.Bar * VelocityLayers.SpreadOf(genre)),
                PoolSize,
                "Bar"
            ),
            StateTimelineGenerator.Create(
                StateKinds.QuarterNoteDurationPower,
                meter.BarDuration,
                LayerStates.QuarterNoteDurationPower,
                PoolSize,
                "Bar"
            ),
            StateTimelineGenerator.Create(
                StateKinds.NextNoteDurationFactor,
                meter.BarDuration,
                LayerStates.NextNoteDurationFactor,
                PoolSize,
                "Bar"
            ),
            StateTimelineGenerator.Create(
                CompositionStateKinds.ChordPool.Index,
                meter.BarDuration,
                LayerStates.ChordPoolIndex,
                PoolSize,
                "Bar"
            )
        ];
    }

    /// <param name="scale">The section's scale, which every bar plays in.</param>
    /// <param name="progression">The roots of the chords, in steps above the home.</param>
    /// <param name="harmonicRhythm">Where the chords change.</param>
    /// <param name="chordScales">Every chord's own scale, the section's with steps raised or lowered (<see cref="ChordScales" />).</param>
    /// <param name="shifts">Every chord moved by semitones, a chromatic passing chord's (<see cref="PassingChords" />), most none.</param>
    /// <param name="unconventionality">The chords facet, which the home's and the cadence's chord shapes are drawn by.</param>
    /// <param name="bassLeading">How much the section's bass leads into the chords, from 0 to 1.</param>
    /// <param name="context">The section's random sequence.</param>
    /// <param name="facets">How far the section strays from convention: its melody facet leans the melody's contour, its chords facet the bass's arrivals.</param>
    public StateTimelineMap Generate(
        IGenerationContext context,
        Scale scale,
        ImmutableArray<int> progression,
        HarmonicRhythm harmonicRhythm,
        ImmutableArray<ChordScale> chordScales,
        ImmutableArray<int> shifts,
        HarmonicUnconventionality unconventionality,
        double bassLeading,
        Unconventionality facets
    )
    {
        // each state draws from its own random sequence, so tuning one does not change the others
        var seed = context.GenerateSeed();
        var changes = harmonicRhythm.Changes;
        var cadencePosition = changes[^1];
        var progressionTimeline = StateTimeline.Create(
                _meter.PatternDuration,
                StateKinds.ChordRoot,
                progression.Select((root, chord) => root.ToTimelineItem(changes[chord]))
            )
            .WithLayer(Layers.Progression);
        var changeTimeline = StateTimeline.Create(
                _meter.PatternDuration,
                StateKinds.ChordChange,
                changes.Select((position, chord) => (chord + 1).ToTimelineItem(position))
            )
            .WithLayer(Layers.Progression);

        // a chord moved by semitones, set where any is, so that a section with none has no such state
        IStateTimeline[] shiftTimeline = shifts.Any(x => x != 0)
            ? [StateTimeline.Create(_meter.PatternDuration, StateKinds.ChordShift, shifts.Select((x, chord) => x.ToTimelineItem(changes[chord]))).WithLayer(Layers.Progression)]
            : [];

        // every chord in its own scale, set by the section where it differs from the chord's before, the first's from the
        // section's, none
        var scaleTimeline = StateTimeline.Create(
                _meter.PatternDuration,
                StateKinds.ScaleAlterations,
                chordScales.Select((x, chord) => (x.Steps, Position: changes[chord]))
                    .Where((x, chord) => x.Steps != (chord == 0 ? StepAlterations.None : chordScales[chord - 1].Steps))
                    .Select(x => new LayerValue<StepAlterations>(StateDepths.Section, x.Steps).ToTimelineItem(x.Position))
            )
            .WithLayer(Layers.Progression);

        // the home chord plays a plain chord and the cadence one with pull; the chords between pick from the pool
        var homeChord = ImmutableArray.Create(unconventionality.GenerateHomeChord(context.CreateContext(Seeds.Derive(seed, HomeChordStream)))).ToTimelineItem(0.0);
        var cadenceChord = ImmutableArray.Create(unconventionality.GenerateCadenceChord(context.CreateContext(Seeds.Derive(seed, CadenceChordStream)))).ToTimelineItem(cadencePosition);
        var roleChordTimeline = StateTimeline.Create(
                _meter.PatternDuration,
                CompositionStateKinds.RoleChord,
                changes.Length > 2 ? [homeChord, ImmutableArray<Chord>.Empty.ToTimelineItem(changes[1]), cadenceChord] : [homeChord, cadenceChord]
            )
            .WithLayer(Layers.Progression);

        return StateTimelineMap.Create(
            _meter.PatternDuration,
            [
                .._timelineGenerators.Select((generator, index) =>
                    generator.Generate(context.CreateContext(Seeds.Derive(seed, index)), _meter.PatternDuration)
                ),
                StateTimeline.Create(_meter.PatternDuration, StateKinds.ScaleOffsets, [scale.Offsets.ToTimelineItem(0.0)]).WithLayer(Layers.Section),
                progressionTimeline,
                changeTimeline,
                scaleTimeline,
                ..shiftTimeline,
                roleChordTimeline,
                GenerateResets(context.CreateContext(Seeds.Derive(seed, ResetsStream)), facets[Facet.Chords]),
                ..GenerateBassLeading(context.CreateContext(Seeds.Derive(seed, BassLeadingStream)), changes, bassLeading, facets[Facet.Chords]),
                GenerateMelodyContour(context.CreateContext(Seeds.Derive(seed, ContourStream)), context.CreateContext(Seeds.Derive(seed, ContourPeriodStream)), facets[Facet.Melody]),
                GenerateMelodyPhraseEnd(context.CreateContext(Seeds.Derive(seed, PhraseEndStream)))
            ]
        );
    }

    private double LastBarPosition => (_meter.PatternBarCount - 1) * _meter.BarDuration;

    /// <summary>
    ///     The bars whose first chord starts afresh in its own register, by the chords facet: never in the plainest
    ///     songs, whose chords are all led from the one before; now and then at the middle, most often the pattern's
    ///     first; every bar at the wild end. Each has its own number, so that bars in a row are told apart.
    /// </summary>
    private StateTimeline<int> GenerateResets(IGenerationContext context, double unconventionality)
    {
        return StateTimeline.Create(
                _meter.PatternDuration,
                StateKinds.ChordVoicingReset,
                Enumerable.Range(0, _meter.PatternBarCount)
                    .Select(bar =>
                        {
                            var chance = bar == 0 ? VoiceLeadingLayers.ResetAtPatternStart : VoiceLeadingLayers.ResetElsewhere;
                            return (context.TestProbability(chance.At(unconventionality)) ? bar + 1 : 0).ToTimelineItem(bar * _meter.BarDuration);
                        }
                    )
                    .ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>The shape the section's melody phrases take: the register the melody aims at in each bar, every phrase of the pattern arcing alike.</summary>
    private StateTimeline<double> GenerateMelodyContour(IGenerationContext context, IGenerationContext periodContext, double unconventionality)
    {
        var contour = MelodyLayers.GenerateContour(context, periodContext, unconventionality, _meter.PhraseBars);
        return StateTimeline.Create(
                _meter.PatternDuration,
                CompositionStateKinds.LineRegister,
                Enumerable.Range(0, _meter.PatternBarCount).Select(bar => contour[bar % contour.Length].ToTimelineItem(bar * _meter.BarDuration)).ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>Where the melody's phrase ends in the pattern's last bar, if it does.</summary>
    private StateTimeline<int> GenerateMelodyPhraseEnd(IGenerationContext context)
    {
        var end = context.Pick(MelodyLayers.PhraseEnds);
        return StateTimeline.Create(
                _meter.PatternDuration,
                CompositionStateKinds.MelodyPhraseEnd,
                [end.ToTimelineItem(LastBarPosition)]
            )
            .WithLayer("Bar");
    }

    /// <summary>
    ///     How the bass leads out of every chord into the next, by how much it leads, and what it lands on in every new
    ///     chord, the less conventional the section the less often the root.
    /// </summary>
    private IEnumerable<IStateTimeline> GenerateBassLeading(IGenerationContext context, ImmutableArray<double> changes, double bassLeading, double unconventionality)
    {
        var approachGenerator = Generators.WeightedIndex(BassLeadingLayers.Approaches);
        // a genre's bass leading more or less, through the chance's odds
        var leaning = Tilt.Of(BassLeadingLayers.GenreLeadOdds, _genre.Shift(GenreChoices.Bass, "leading"));
        var approaches = StateTimeline.Create(
                _meter.PatternDuration,
                StateKinds.ChordApproach,
                changes
                    .Select(position =>
                        {
                            // the way is drawn whether the chord leads or not, so that the draws are as many whatever the chance
                            var leads = context.TestProbability(leaning.Chance(bassLeading * BassLeadingLayers.MaxApproachChance, 1));
                            var way = BassLeadingLayers.Approaches[approachGenerator(context)].Value;
                            var approach = leads ? way : ChordApproach.None;
                            return ((int)approach).ToTimelineItem(position);
                        }
                    )
                    .ToArray()
            )
            .WithLayer("Bar");

        var arrivalWeights = ByConvention.Weigh(BassLeadingLayers.ArrivalWeights, unconventionality, _genre.Pool<ChordArrival>(GenreChoices.BassLanding, x => GenreChoices.Name(x.ToString())));
        var arrivalGenerator = Generators.WeightedIndex(arrivalWeights);
        var arrivals = StateTimeline.Create(
                _meter.PatternDuration,
                StateKinds.ChordArrival,
                changes
                    .Select(position => ((int)arrivalWeights[arrivalGenerator(context)].Value).ToTimelineItem(position))
                    .ToArray()
            )
            .WithLayer("Bar");

        return [approaches, arrivals];
    }
}
