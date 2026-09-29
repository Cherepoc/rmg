using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The state of a section that changes from bar to bar along its 4-bar pattern, the same for every track: its
///     scale, which holds for all of it, the progression's roots, the raised seventh of the cadence, the home and cadence chords, the pick from the chord
///     pool, the bar's loudness and note lengths, how the chords and the bass move into the next bar, and the shape of
///     the melody's phrase and where it ends.
/// </summary>
internal sealed class BarStateGenerator
{


    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    // the stream of the melody contour's period, apart from the timeline generators'
    private const int ContourPeriodStream = 100;

    // how the bass leads into its chords
    private const int BassLeadingStream = 101;

    // the state that changes along the pattern besides the progression; every state has its own timeline, so each can
    // change at its own pace
    private readonly ImmutableArray<IStateTimelineGenerator> _timelineGenerators;

    public BarStateGenerator(ProgressionSettings settings)
    {
        _timelineGenerators =
        [
            StateTimelineGenerator.Create(
                StateKinds.Velocity,
                settings.NoteStateStep,
                VelocityLayers.CreateGenerator(VelocityLayers.Bar),
                settings.PoolSize,
                "Bar"
            ),
            StateTimelineGenerator.Create(
                StateKinds.QuarterNoteDurationPower,
                settings.NoteStateStep,
                LayerStates.QuarterNoteDurationPower,
                settings.PoolSize,
                "Bar"
            ),
            StateTimelineGenerator.Create(
                StateKinds.NextNoteDurationFactor,
                settings.NoteStateStep,
                LayerStates.NextNoteDurationFactor,
                settings.PoolSize,
                "Bar"
            ),
            StateTimelineGenerator.Create(
                CompositionStateKinds.ChordPool.Index,
                settings.ChordShapeStep,
                LayerStates.ChordPoolIndex,
                settings.PoolSize,
                "Bar"
            )
        ];
    }

    /// <param name="scale">The section's scale, which every bar plays in.</param>
    /// <param name="progression">The roots of the chords, in steps above the home.</param>
    /// <param name="harmonicRhythm">Where the chords change.</param>
    /// <param name="home">The step of the section's home above the song's tonic.</param>
    /// <param name="bassLeading">How much the section's bass leads into the chords, from 0 to 1.</param>
    /// <param name="context">The section's random sequence.</param>
    /// <param name="rhythmTilt">How unconventional the section's rhythm is, which leans the melody's contour.</param>
    public StateTimelineMap Generate(
        IGenerationContext context,
        Scale scale,
        ImmutableArray<int> progression,
        HarmonicRhythm harmonicRhythm,
        int home,
        HarmonicUnconventionality unconventionality,
        double bassLeading,
        Tilt rhythmTilt
    )
    {
        // each state draws from its own random sequence, so tuning one does not change the others
        var seed = SeedGenerator(context);
        var changes = harmonicRhythm.Changes;
        var cadencePosition = changes[^1];
        var progressionTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordRoot,
                progression.Select((root, chord) => root.ToTimelineItem(changes[chord]))
            )
            .WithLayer("Progression");
        var changeTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordChange,
                changes.Select((position, chord) => (chord + 1).ToTimelineItem(position))
            )
            .WithLayer("Progression");

        // the cadence chord may raise the seventh, for a major chord on the fifth; the chords before keep the scale
        var raisedStep = Progressions.GetCadenceRaisedStep(scale.Offsets, home, progression[^1]);
        var raisedStepTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.RaisedScaleSteps,
                raisedStep is { } step ? [ImmutableArray.Create(step).ToTimelineItem(cadencePosition)] : []
            )
            .WithLayer("Progression");

        // the home chord plays a plain chord and the cadence one with pull; the chords between pick from the pool
        var homeChord = ImmutableArray.Create(unconventionality.GenerateHomeChord(context)).ToTimelineItem(0.0);
        var cadenceChord = ImmutableArray.Create(unconventionality.GenerateCadenceChord(context)).ToTimelineItem(cadencePosition);
        var roleChordTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                CompositionStateKinds.RoleChord,
                changes.Length > 2 ? [homeChord, ImmutableArray<Chord>.Empty.ToTimelineItem(changes[1]), cadenceChord] : [homeChord, cadenceChord]
            )
            .WithLayer("Progression");

        return StateTimelineMap.Create(
            Meter.PatternDuration,
            [
                .._timelineGenerators.Select((generator, index) =>
                    generator.Generate(context.CreateContext(Seeds.Derive(seed, index)), Meter.PatternDuration)
                ),
                StateTimeline.Create(Meter.PatternDuration, StateKinds.ScaleOffsets, [scale.Offsets.ToTimelineItem(0.0)]).WithLayer("Section"),
                progressionTimeline,
                changeTimeline,
                raisedStepTimeline,
                roleChordTimeline,
                GenerateResets(context),
                ..GenerateBassLeading(context.CreateContext(Seeds.Derive(seed, BassLeadingStream)), changes, bassLeading, rhythmTilt),
                GenerateMelodyContour(context, context.CreateContext(Seeds.Derive(seed, ContourPeriodStream)), rhythmTilt),
                GenerateMelodyPhraseEnd(context)
            ]
        );
    }

    private static double LastBarPosition => (Progressions.BarCount - 1) * Meter.BarDuration;

    /// <summary>
    ///     The bars whose first chord starts afresh in its own register, now and then, most often the pattern's first;
    ///     each has its own number, so that bars in a row are told apart.
    /// </summary>
    private StateTimeline<int> GenerateResets(IGenerationContext context)
    {
        return StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordVoicingReset,
                Enumerable.Range(0, Progressions.BarCount)
                    .Select(bar =>
                        {
                            var chance = bar == 0 ? VoiceLeadingLayers.ResetAtPatternStart : VoiceLeadingLayers.ResetElsewhere;
                            return (context.TestProbability(chance) ? bar + 1 : 0).ToTimelineItem(bar * Meter.BarDuration);
                        }
                    )
                    .ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>The shape the section's melody phrases take: the register the melody aims at in each bar.</summary>
    private StateTimeline<double> GenerateMelodyContour(IGenerationContext context, IGenerationContext periodContext, Tilt tilt)
    {
        var contour = MelodyLayers.GenerateContour(context, periodContext, tilt);
        return StateTimeline.Create(
                Meter.PatternDuration,
                CompositionStateKinds.LineRegister,
                contour.Select((register, bar) => register.ToTimelineItem(bar * Meter.BarDuration)).ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>Where the melody's phrase ends in the pattern's last bar, if it does.</summary>
    private StateTimeline<int> GenerateMelodyPhraseEnd(IGenerationContext context)
    {
        var end = context.Pick(MelodyLayers.PhraseEnds);
        return StateTimeline.Create(
                Meter.PatternDuration,
                CompositionStateKinds.MelodyPhraseEnd,
                [end.ToTimelineItem(LastBarPosition)]
            )
            .WithLayer("Bar");
    }

    /// <summary>
    ///     How the bass leads out of every chord into the next, by how much it leads, and what it lands on in every new
    ///     chord, the less conventional the section the less often the root.
    /// </summary>
    private IEnumerable<IStateTimeline> GenerateBassLeading(IGenerationContext context, ImmutableArray<double> changes, double bassLeading, Tilt rhythmTilt)
    {
        var approachGenerator = Generators.WeightedIndex(BassLeadingLayers.Approaches);
        var approaches = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordApproach,
                changes
                    .Select(position =>
                        {
                            // the way is drawn whether the chord leads or not, so that the draws are as many whatever the chance
                            var leads = context.TestProbability(bassLeading * BassLeadingLayers.MaxApproachChance);
                            var way = BassLeadingLayers.Approaches[approachGenerator(context)].Value;
                            var approach = leads ? way : ChordApproach.None;
                            return ((int)approach).ToTimelineItem(position);
                        }
                    )
                    .ToArray()
            )
            .WithLayer("Bar");

        var arrivalWeights = rhythmTilt.Weigh(BassLeadingLayers.Arrivals, x => x == ChordArrival.Root ? 0 : 1);
        var arrivalGenerator = Generators.WeightedIndex(arrivalWeights);
        var arrivals = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordArrival,
                changes
                    .Select(position => ((int)arrivalWeights[arrivalGenerator(context)].Value).ToTimelineItem(position))
                    .ToArray()
            )
            .WithLayer("Bar");

        return [approaches, arrivals];
    }
}
