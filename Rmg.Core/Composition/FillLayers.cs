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
    Percussion,

    /// <summary>The song's call, a triangle, a cuica or a whistle, if it has one, now and then in a run or a landing.</summary>
    Calls
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

/// <summary>A fill's rarer choice, which a drummer's signature makes more often (<see cref="FillLayers.Chances" />).</summary>
public enum FillRarity
{
    /// <summary>A fill starting a note near an 8th off the beat, earlier or later.</summary>
    OffBeat,

    /// <summary>A run fading rather than swelling.</summary>
    Fades,

    /// <summary>A landing pushed a note near an 8th early.</summary>
    EarlyLanding
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

    /// <summary>
    ///     How much a line between the phrases of a longer pattern weighs, which ends a phrase but not the pattern's cycle:
    ///     lighter than a pattern's, so that a twelve-bar's fills at its fourth and eighth bars are rarer and shorter than
    ///     its turnaround's.
    /// </summary>
    public const double InnerPhraseWeight = 2 * PhraseWeight;

    /// <summary>
    ///     How far a genre's fills lean to longer ones and to none at their most and least (<see cref="GenreChoices.Fills" />),
    ///     as the odds of a span's loudness: a beat's few fills, a rock drummer's many.
    /// </summary>
    public const double GenreAmountOdds = 6;

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

    /// <summary>What a fill does with the groove, how likely each is in a middling section, and how it leans; stopping the unconventional one, never in the plainest section and as likely as the others in the wildest (<see cref="RhythmicUnconventionality.WeightEnds" />).</summary>
    public static ImmutableArray<(GrooveTreatment Value, double Weight, double Lean)> Treatments { get; } =
    [
        (GrooveTreatment.Played, 0.78, 0),
        (GrooveTreatment.Stop, 0.1, 1),
        (GrooveTreatment.Keep, 0.12, 0)
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
    ///     beat, that a run fades, and that its landing comes early, each leaning by the section's fills facet to its ends
    ///     and by a drummer's signature (<see cref="FillGenerator.GetChance" />).
    /// </summary>
    public static ImmutableArray<(FillRarity Choice, double Chance)> Chances { get; } =
    [
        (FillRarity.OffBeat, 0.015),
        (FillRarity.Fades, 0.01),
        (FillRarity.EarlyLanding, 0.01)
    ];

    /// <summary>The chance a drummer has a signature, in a song of conventionality in the middle: never in the plainest, every time in the wildest.</summary>
    public const double SignatureChance = 0.1;

    /// <summary>How much more likely a drummer's signature choice is than it would be.</summary>
    public const double SignatureWeight = 5;

    /// <summary>The shortest note a fill plays, in seconds: a sextuplet at 125 beats a minute, a 32nd at 94.</summary>
    public const double MinNoteSeconds = 0.08;

    /// <summary>How many of a role's sounds a run plays, such as how many toms, and how likely each is; at most its all.</summary>
    public static ImmutableArray<Weighted<int>> SoundCounts { get; } = [new(0.2, 1), new(0.3, 2), new(0.3, 3), new(0.2, 4)];

    /// <summary>
    ///     The chance, in a section of conventionality in the middle, that a run takes its toms in their order of pitch,
    ///     down or up, a conventional thing (<see cref="PitchOrderLean" />): every run in the plainest section, none in the
    ///     wildest. Otherwise every sound's place is drawn.
    /// </summary>
    public const double PitchOrderChance = 0.7;

    public const double PitchOrderLean = -2;

    /// <summary>How a run walks its sounds, how likely each is in a middling song, and how it leans; the random walk the unconventional one, never in the plainest.</summary>
    public static ImmutableArray<(FillPath Value, double Weight, double Lean)> Paths { get; } =
    [
        (FillPath.OneWay, 0.55, 0),
        (FillPath.Turn, 0.15, 0),
        (FillPath.Loop, 0.2, 0),
        (FillPath.Random, 0.1, 1)
    ];

    /// <summary>The chance a random walk steps to a neighbour rather than to any sound.</summary>
    public const double NeighbourStepChance = 0.8;

    /// <summary>
    ///     How many sounds of the walk each note plays together, how likely each is in a middling section, and how it leans;
    ///     more than one the unconventional, never in the plainest section.
    /// </summary>
    public static ImmutableArray<(int Value, double Weight, double Lean)> Widths { get; } = [(1, 0.85, 0), (2, 0.12, 1), (3, 0.03, 1)];

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

    /// <summary>Whether a run changes speed halfway, how likely each is in a middling section, and how it leans; slowing down the unconventional one, never in the plainest.</summary>
    public static ImmutableArray<(FillSpeed Value, double Weight, double Lean)> Speeds { get; } =
    [
        (FillSpeed.Steady, 0.7, 0),
        (FillSpeed.SpeedsUp, 0.27, 0),
        (FillSpeed.SlowsDown, 0.03, 1)
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
        [FillDrumRole.Percussion] = 0.85,
        [FillDrumRole.Calls] = 0.2
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
