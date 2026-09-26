using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The state of a section that changes from bar to bar along its 4-bar pattern, the same for every track: the
///     progression's roots, the raised seventh of the cadence, the home and cadence chords, the pick from the chord
///     pool, the bar's loudness and note lengths, and how the chords and the bass move into the next bar.
/// </summary>
internal sealed class BarStateGenerator
{
    public const double PatternDuration = Progressions.BarCount * BarDuration;

    private const double BarDuration = 4;

    private static readonly Func<IGenerationContext, int> SeedGenerator = Generators.Int();

    private readonly IGenerationContext _context;
    private readonly Scale _scale;

    // the state that changes along the pattern besides the progression; every state has its own timeline, so each can
    // change at its own pace
    private readonly ImmutableArray<IStateTimelineGenerator> _timelineGenerators;

    public BarStateGenerator(IGenerationContext context, ProgressionSettings settings, Scale scale)
    {
        _context = context;
        _scale = scale;
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

    /// <param name="progression">The roots of the bars, in steps above the home.</param>
    /// <param name="home">The step of the section's home above the song's tonic.</param>
    /// <param name="bassLeading">How much the section's bass leads into the chords, from 0 to 1.</param>
    public StateTimelineMap Generate(
        ImmutableArray<int> progression,
        int home,
        HarmonicUnconventionality unconventionality,
        double bassLeading
    )
    {
        // each state draws from its own random sequence, so tuning one does not change the others
        var seed = SeedGenerator(_context);
        var progressionTimeline = StateTimeline.Create(
                PatternDuration,
                StateKinds.ChordRootNoteOffset,
                progression.Select((root, bar) =>
                    ImmutableArray.Create(Progressions.ToRootOffset(root)).ToTimelineItem(bar * BarDuration)
                )
            )
            .WithLayer("Progression");

        // the cadence bar may raise the seventh, for a major chord on the fifth; the bars before keep the scale
        var raisedStep = Progressions.GetCadenceRaisedStep(_scale.Offsets, home, progression[^1]);
        var raisedStepTimeline = StateTimeline.Create(
                PatternDuration,
                StateKinds.RaisedScaleSteps,
                raisedStep is { } step ? [ImmutableArray.Create(step).ToTimelineItem(CadenceBarPosition)] : []
            )
            .WithLayer("Progression");

        // the home bar plays a plain chord and the cadence bar one with pull; the bars between pick from the pool
        var roleChordTimeline = StateTimeline.Create(
                PatternDuration,
                CompositionStateKinds.RoleChord,
                [
                    ImmutableArray.Create(unconventionality.GenerateHomeChord(_context)).ToTimelineItem(0.0),
                    ImmutableArray<Chord>.Empty.ToTimelineItem(BarDuration),
                    ImmutableArray.Create(unconventionality.GenerateCadenceChord(_context)).ToTimelineItem(CadenceBarPosition)
                ]
            )
            .WithLayer("Progression");

        return StateTimelineMap.Create(
            PatternDuration,
            [
                .._timelineGenerators.Select((generator, index) =>
                    generator.Generate(_context.CreateContext(Seeds.Derive(seed, index)), PatternDuration)
                ),
                progressionTimeline,
                raisedStepTimeline,
                roleChordTimeline,
                GenerateResets(),
                ..GenerateBassLeading(bassLeading),
                GenerateMelodyContour()
            ]
        );
    }

    private static double CadenceBarPosition => (Progressions.BarCount - 1) * BarDuration;

    /// <summary>
    ///     The bars whose first chord starts afresh in its own register, now and then, most often the pattern's first;
    ///     each has its own number, so that bars in a row are told apart.
    /// </summary>
    private StateTimeline<int> GenerateResets()
    {
        return StateTimeline.Create(
                PatternDuration,
                StateKinds.ChordVoicingReset,
                Enumerable.Range(0, Progressions.BarCount)
                    .Select(bar =>
                        {
                            var chance = bar == 0 ? VoiceLeadingLayers.ResetAtPatternStart : VoiceLeadingLayers.ResetElsewhere;
                            return (_context.TestProbability(chance) ? bar + 1 : 0).ToTimelineItem(bar * BarDuration);
                        }
                    )
                    .ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>The shape the section's melody phrases take: the register the melody aims at in each bar.</summary>
    private StateTimeline<double> GenerateMelodyContour()
    {
        var contour = MelodyLayers.Contours[Generators.WeightedIndex(MelodyLayers.Contours)(_context)].Value;
        return StateTimeline.Create(
                PatternDuration,
                StateKinds.MelodyRegister,
                contour.Select((register, bar) => register.ToTimelineItem(bar * BarDuration)).ToArray()
            )
            .WithLayer("Bar");
    }

    /// <summary>How the bass leads out of every bar into the next chord, and what it lands on in every bar's new chord.</summary>
    private IEnumerable<IStateTimeline> GenerateBassLeading(double bassLeading)
    {
        var approachGenerator = Generators.WeightedIndex(BassLeadingLayers.Approaches);
        var approaches = StateTimeline.Create(
                PatternDuration,
                StateKinds.ChordApproach,
                Enumerable.Range(0, Progressions.BarCount)
                    .Select(bar =>
                        {
                            var approach = _context.TestProbability(bassLeading * BassLeadingLayers.MaxApproachChance)
                                ? BassLeadingLayers.Approaches[approachGenerator(_context)].Value
                                : ChordApproach.None;
                            return ((int)approach).ToTimelineItem(bar * BarDuration);
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
                PatternDuration,
                StateKinds.ChordArrival,
                Enumerable.Range(0, Progressions.BarCount)
                    .Select(bar => ((int)arrivalWeights[arrivalGenerator(_context)].Value).ToTimelineItem(bar * BarDuration))
                    .ToArray()
            )
            .WithLayer("Bar");

        return [approaches, arrivals];
    }
}
