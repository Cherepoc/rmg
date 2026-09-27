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
    /// <param name="progression">The roots of the bars, in steps above the home.</param>
    /// <param name="home">The step of the section's home above the song's tonic.</param>
    /// <param name="bassLeading">How much the section's bass leads into the chords, from 0 to 1.</param>
    /// <param name="context">The section's random sequence.</param>
    public StateTimelineMap Generate(
        IGenerationContext context,
        Scale scale,
        ImmutableArray<int> progression,
        int home,
        HarmonicUnconventionality unconventionality,
        double bassLeading
    )
    {
        // each state draws from its own random sequence, so tuning one does not change the others
        var seed = SeedGenerator(context);
        var progressionTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordRootNoteOffset,
                progression.Select((root, bar) =>
                    ImmutableArray.Create(Progressions.ToRootOffset(root)).ToTimelineItem(bar * Meter.BarDuration)
                )
            )
            .WithLayer("Progression");

        // the cadence bar may raise the seventh, for a major chord on the fifth; the bars before keep the scale
        var raisedStep = Progressions.GetCadenceRaisedStep(scale.Offsets, home, progression[^1]);
        var raisedStepTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.RaisedScaleSteps,
                raisedStep is { } step ? [ImmutableArray.Create(step).ToTimelineItem(CadenceBarPosition)] : []
            )
            .WithLayer("Progression");

        // the home bar plays a plain chord and the cadence bar one with pull; the bars between pick from the pool
        var roleChordTimeline = StateTimeline.Create(
                Meter.PatternDuration,
                CompositionStateKinds.RoleChord,
                [
                    ImmutableArray.Create(unconventionality.GenerateHomeChord(context)).ToTimelineItem(0.0),
                    ImmutableArray<Chord>.Empty.ToTimelineItem(Meter.BarDuration),
                    ImmutableArray.Create(unconventionality.GenerateCadenceChord(context)).ToTimelineItem(CadenceBarPosition)
                ]
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
                raisedStepTimeline,
                roleChordTimeline,
                GenerateResets(context),
                ..GenerateBassLeading(context, bassLeading),
                GenerateMelodyContour(context),
                GenerateMelodyPhraseEnd(context)
            ]
        );
    }

    private static double CadenceBarPosition => (Progressions.BarCount - 1) * Meter.BarDuration;

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
    private StateTimeline<double> GenerateMelodyContour(IGenerationContext context)
    {
        var contour = MelodyLayers.GenerateContour(context);
        return StateTimeline.Create(
                Meter.PatternDuration,
                CompositionStateKinds.MelodyRegister,
                contour.Select((register, bar) => register.ToTimelineItem(bar * Meter.BarDuration)).ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>Where the melody's phrase ends in the pattern's last bar, if it does.</summary>
    private StateTimeline<int> GenerateMelodyPhraseEnd(IGenerationContext context)
    {
        var end = MelodyLayers.PhraseEnds[Generators.WeightedIndex(MelodyLayers.PhraseEnds)(context)].Value;
        return StateTimeline.Create(
                Meter.PatternDuration,
                CompositionStateKinds.MelodyPhraseEnd,
                [end.ToTimelineItem(CadenceBarPosition)]
            )
            .WithLayer("Bar");
    }

    /// <summary>How the bass leads out of every bar into the next chord, and what it lands on in every bar's new chord.</summary>
    private IEnumerable<IStateTimeline> GenerateBassLeading(IGenerationContext context, double bassLeading)
    {
        var approachGenerator = Generators.WeightedIndex(BassLeadingLayers.Approaches);
        var approaches = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordApproach,
                Enumerable.Range(0, Progressions.BarCount)
                    .Select(bar =>
                        {
                            var approach = context.TestProbability(bassLeading * BassLeadingLayers.MaxApproachChance)
                                ? BassLeadingLayers.Approaches[approachGenerator(context)].Value
                                : ChordApproach.None;
                            return ((int)approach).ToTimelineItem(bar * Meter.BarDuration);
                        }
                    )
                    .ToArray()
            )
            .WithLayer("Bar");

        var rootArrivalChance = BassLeadingLayers.MinRootArrivalChance + bassLeading * BassLeadingLayers.RootArrivalChanceRange;
        ImmutableArray<Weighted<ChordArrival>> arrivalWeights =
        [
            new(rootArrivalChance, ChordArrival.Root),
            new(BassLeadingLayers.InversionArrivalChance, ChordArrival.Third),
            new(BassLeadingLayers.InversionArrivalChance, ChordArrival.Fifth),
            new(Math.Max(0, 1 - rootArrivalChance - 2 * BassLeadingLayers.InversionArrivalChance), ChordArrival.Free)
        ];
        var arrivalGenerator = Generators.WeightedIndex(arrivalWeights);
        var arrivals = StateTimeline.Create(
                Meter.PatternDuration,
                StateKinds.ChordArrival,
                Enumerable.Range(0, Progressions.BarCount)
                    .Select(bar => ((int)arrivalWeights[arrivalGenerator(context)].Value).ToTimelineItem(bar * Meter.BarDuration))
                    .ToArray()
            )
            .WithLayer("Bar");

        return [approaches, arrivals];
    }
}
