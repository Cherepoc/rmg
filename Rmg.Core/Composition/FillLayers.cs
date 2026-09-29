using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     A drum's role in a fill, by the sounds a run or a landing plays, finer than its role in the groove
///     (<see cref="DrumRole" />): the toms and the cymbal both colour a groove, but a run plays them apart.
/// </summary>
public enum FillDrumRole
{
    Kick,

    /// <summary>The song's snares: the snare itself, its clap or its sidestick.</summary>
    Snare,

    Toms,

    /// <summary>The drums that keep time, such as the hi-hat or the ride.</summary>
    HiHat,

    Cymbal,

    /// <summary>The song's own percussion, such as a cowbell or congas, if it has any.</summary>
    Percussion
}

/// <summary>How a run walks the order of its sounds from note to note.</summary>
public enum FillPath
{
    /// <summary>From the first sound to the last, a share of the notes on each.</summary>
    OneWay,

    /// <summary>To the last sound and back.</summary>
    Turn,

    /// <summary>Through the sounds again and again, a note on each.</summary>
    Loop,

    /// <summary>At random, mostly to a neighbour.</summary>
    Random
}

/// <summary>Whether a run keeps its speed, or changes it by a rank halfway.</summary>
public enum FillSpeed
{
    Steady,

    /// <summary>A rank coarser for the first half.</summary>
    SpeedsUp,

    /// <summary>A rank coarser for the second half.</summary>
    SlowsDown
}

/// <summary>What a fill does with the groove in its span.</summary>
public enum GrooveTreatment
{
    /// <summary>The groove plays on under the fill, which plays over it: a few hits, or a lift into the line.</summary>
    Keep,

    /// <summary>The drums the fill plays leave the groove for it, and the others play on: a run.</summary>
    Played,

    /// <summary>
    ///     The drums stop, and the fill plays alone: a drum break where it runs, stop time where it keeps its strongest
    ///     notes, a break where it rests.
    /// </summary>
    Stop
}

/// <summary>
///     How the drums mark the lines between sections and phrases: the fill before a line, and what they land on after
///     it. Every fill is a run: how long it is, none for the groove running on, what it does with the groove, and the
///     sounds it walks, in the groove's rhythm (see <see cref="FillRhythm" />) with the fill's layer over it. A landing is
///     a note of a few sounds on the line. A section change is marked most, a phrase line inside a section now and then;
///     the stranger a section's rhythm, the likelier the unconventional choices.
/// </summary>
internal static class FillLayers
{
    /// <summary>
    ///     How much a line weighs, as a pull of the odds <see cref="SectionEnergy.HighOdds" /> give, which leans its fill
    ///     and landing (see <see cref="Spans" />, <see cref="Landings" /> and <see cref="Fullness" />): a section change
    ///     0, and the energy it leads into on top; the line in the middle of a section this, the odds of its longer fills
    ///     about a seventh, so that the groove mostly runs on there.
    /// </summary>
    public const double PhraseWeight = -0.55;

    /// <summary>How long a fill is, in beats before a line of no weight, and how likely each is; 0 is none.</summary>
    public static ImmutableArray<Weighted<double>> Spans { get; } =
    [
        new(0.2, 0),
        new(0.07, 0.5),
        new(0.23, 1),
        new(0.28, 2),
        new(0.22, 4)
    ];

    /// <summary>
    ///     How a span leans, for the line's weight: none light, the longer the heavier, a beat in the middle.
    /// </summary>
    public static double GetSpanLoudness(double span) => span <= 0 ? -1 : Math.Clamp(Math.Log2(span) / 2, -1, 1);

    /// <summary>What a fill does with the groove, and how likely each is; stopping's weight is multiplied by the chance scale.</summary>
    public static ImmutableArray<Weighted<GrooveTreatment>> Treatments { get; } =
    [
        new(0.78, GrooveTreatment.Played),
        new(0.1, GrooveTreatment.Stop),
        new(0.12, GrooveTreatment.Keep)
    ];

    /// <summary>How a treatment leans, for the energy of the section a line leads into: stopping, a stop or a break, quiet.</summary>
    public static ImmutableDictionary<GrooveTreatment, double> TreatmentLoudness { get; } = new Dictionary<GrooveTreatment, double>
    {
        [GrooveTreatment.Played] = 0,
        [GrooveTreatment.Keep] = 0,
        [GrooveTreatment.Stop] = -1
    }.ToImmutableDictionary();

    /// <summary>
    ///     How much fuller or sparser a fill is for what it does with the groove: one over the groove plays a few hits,
    ///     and one where the drums stop mostly keeps its strongest notes.
    /// </summary>
    public static ImmutableDictionary<GrooveTreatment, double> TreatmentFullness { get; } = new Dictionary<GrooveTreatment, double>
    {
        [GrooveTreatment.Played] = 0,
        [GrooveTreatment.Keep] = -0.25,
        [GrooveTreatment.Stop] = -0.35
    }.ToImmutableDictionary();

    /// <summary>The chance a fill where the drums stop rests, playing nothing: a break.</summary>
    public const double StopRestChance = 0.5;

    /// <summary>
    ///     The chances of a fill's rarer choices, in a section of conventionality in the middle: that it starts off the
    ///     beat, that a run fades, and that its landing comes early. The section's chance scale and a drummer's
    ///     signature multiply their odds (<see cref="FillGenerator.GetChance" />).
    /// </summary>
    public static ImmutableArray<(StateKind<double> Kind, double Chance)> Chances { get; } =
    [
        (CompositionStateKinds.Fill.OffBeatChance, 0.015),
        (CompositionStateKinds.Fill.FadeChance, 0.01),
        (CompositionStateKinds.Fill.EarlyLandingChance, 0.01)
    ];

    /// <summary>The chance a drummer has a signature, in a song of conventionality in the middle, times its chance scale.</summary>
    public const double SignatureChance = 0.1;

    /// <summary>How much more likely a drummer's signature choice is than it would be.</summary>
    public const double SignatureWeight = 5;

    /// <summary>The shortest note a fill plays, in seconds: a sextuplet at 125 beats a minute, a 32nd at 94.</summary>
    public const double MinNoteSeconds = 0.08;

    /// <summary>How many of a role's sounds a run plays, such as how many toms, and how likely each is; at most its all.</summary>
    public static ImmutableArray<Weighted<int>> SoundCounts { get; } = [new(0.2, 1), new(0.3, 2), new(0.3, 3), new(0.2, 4)];

    /// <summary>
    ///     The chance, in a section of conventionality in the middle, that a run takes its toms in their order of pitch,
    ///     down or up, which leans conventional twice as much as the rarer choices lean unconventional
    ///     (<see cref="PitchOrderLean" />): 97% in the plainest section, 13% in the wildest. Otherwise every sound's place
    ///     is drawn.
    /// </summary>
    public const double PitchOrderChance = 0.7;

    public const double PitchOrderLean = -2;

    /// <summary>How a run walks its sounds, and how likely each is; the random walk's weight is multiplied by the chance scale.</summary>
    public static ImmutableArray<Weighted<FillPath>> Paths { get; } =
    [
        new(0.55, FillPath.OneWay),
        new(0.15, FillPath.Turn),
        new(0.2, FillPath.Loop),
        new(0.1, FillPath.Random)
    ];

    /// <summary>The chance a random walk steps to a neighbour rather than to any sound.</summary>
    public const double NeighbourStepChance = 0.8;

    /// <summary>
    ///     How many sounds of the walk each note plays together, and how likely each is; the weights of more than one
    ///     are multiplied by the chance scale.
    /// </summary>
    public static ImmutableArray<Weighted<int>> Widths { get; } = [new(0.85, 1), new(0.12, 2), new(0.03, 3)];

    /// <summary>
    ///     How many ranks finer than the groove a fill plays: the snare's backbeat plays its cycle's strongest notes
    ///     alone, so two ranks make 8ths of it, and a groove already fine folds back into range.
    /// </summary>
    public const int FinerRanks = 2;

    /// <summary>
    ///     How much fuller than the groove a fill is before a line of no weight, much fuller; the fill's layer spreads it,
    ///     and the drummer moves it.
    /// </summary>
    public const double Fullness = 0.3;

    /// <summary>
    ///     How much fuller a fill is for every step of its line's weight, so that at a phrase line it is about as sparse
    ///     as the groove, a few hits.
    /// </summary>
    public const double FullnessPerWeight = 0.45;

    /// <summary>Whether a run changes speed halfway, and how likely each is; slowing down's weight is multiplied by the chance scale.</summary>
    public static ImmutableArray<Weighted<FillSpeed>> Speeds { get; } =
    [
        new(0.7, FillSpeed.Steady),
        new(0.27, FillSpeed.SpeedsUp),
        new(0.03, FillSpeed.SlowsDown)
    ];

    /// <summary>
    ///     The chance the drums land on a role's sound after a line of no weight, mostly a kick, often with a crash, and
    ///     into a section of percussion only on the percussion, as on the kick; a landing leans on the line's weight twice
    ///     as much as a span (<see cref="LandingLean" />), so that after a phrase line it comes now and then.
    /// </summary>
    public static ImmutableDictionary<FillDrumRole, double> Landings { get; } = new Dictionary<FillDrumRole, double>
    {
        [FillDrumRole.Kick] = 0.85,
        [FillDrumRole.Cymbal] = 0.65,
        [FillDrumRole.Percussion] = 0.85
    }.ToImmutableDictionary();

    /// <summary>
    ///     The chance a run in a section of the drum kit plays the song's percussion alone, in a song that has any, with
    ///     no lean; the less conventional the section's rhythm, the likelier. A run in a section of percussion only plays
    ///     nothing else.
    /// </summary>
    public const double PercussionRunChance = 0.05;

    public const double LandingLean = 2;

    /// <summary>The chance the drums land on each of a line's landing sounds after they stopped, as they come back.</summary>
    public const double StopLandingChance = 0.97;

    /// <summary>How loud a landing's hit is over the drum's state there, as a note's accent.</summary>
    public const double LandingVelocity = 0.8;

    /// <summary>How loud a run starts and ends, around the groove's loudness.</summary>
    public const double RunStartVelocity = -0.15;

    public const double RunEndVelocity = 0.5;

    /// <summary>How much a run's note is accented by its rank, as a groove's note is, which the swell is added to.</summary>
    public const double AccentWeight = 0.5;
}
