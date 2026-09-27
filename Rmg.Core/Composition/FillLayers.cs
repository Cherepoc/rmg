using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>The drums a fill plays, by what they do in the kit.</summary>
public enum DrumRole
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
public static class FillLayers
{
    /// <summary>How long a fill before a section change is, in beats before the line, and how likely each is; 0 is none.</summary>
    public static ImmutableArray<Weighted<double>> SectionSpans { get; } =
    [
        new(0.2, 0),
        new(0.07, 0.5),
        new(0.23, 1),
        new(0.28, 2),
        new(0.22, 4)
    ];

    /// <summary>How long a fill before the line in the middle of a section is, where the groove mostly runs on.</summary>
    public static ImmutableArray<Weighted<double>> PhraseSpans { get; } =
    [
        new(0.7, 0),
        new(0.1, 0.5),
        new(0.12, 1),
        new(0.06, 2),
        new(0.02, 4)
    ];

    /// <summary>What a fill does with the groove, and how likely each is; stopping's weight is multiplied by the chance scale.</summary>
    public static ImmutableArray<Weighted<GrooveTreatment>> Treatments { get; } =
    [
        new(0.78, GrooveTreatment.Played),
        new(0.1, GrooveTreatment.Stop),
        new(0.12, GrooveTreatment.Keep)
    ];

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
    ///     The chance, in a section of conventionality in the middle, that a fill starts a note near an 8th off the beat,
    ///     earlier or later, times the chance scale.
    /// </summary>
    public const double SpanShiftChance = 0.015;

    /// <summary>The chance, in a section of conventionality in the middle, that a run fades rather than swells, times the chance scale.</summary>
    public const double FadeChance = 0.01;

    /// <summary>The shortest note a fill plays, in seconds: a sextuplet at 125 beats a minute, a 32nd at 94.</summary>
    public const double MinNoteSeconds = 0.08;

    /// <summary>
    ///     The chance a run plays a role's drums: mostly the snare and the toms; the others' chances are multiplied by
    ///     the section's chance scale. A run that draws none plays the snare, or the first of the song's drums.
    /// </summary>
    public static ImmutableDictionary<DrumRole, double> RoleChances { get; } = new Dictionary<DrumRole, double>
    {
        [DrumRole.Snare] = 0.5,
        [DrumRole.Toms] = 0.65,
        [DrumRole.Kick] = 0.04,
        [DrumRole.HiHat] = 0.05,
        [DrumRole.Cymbal] = 0.04,
        [DrumRole.Percussion] = 0.08
    }.ToImmutableDictionary();

    /// <summary>The roles a run plays by convention, whose chances the chance scale leaves.</summary>
    public static ImmutableHashSet<DrumRole> ConventionalRoles { get; } = [DrumRole.Snare, DrumRole.Toms];

    /// <summary>How many of a role's sounds a run plays, such as how many toms, and how likely each is; at most its all.</summary>
    public static ImmutableArray<Weighted<int>> SoundCounts { get; } = [new(0.2, 1), new(0.3, 2), new(0.3, 3), new(0.2, 4)];

    /// <summary>
    ///     The chance, in a section of conventionality in the middle, that a run takes its toms in their order of pitch,
    ///     down or up; it is divided by the section's chance scale. Otherwise every sound's place is drawn.
    /// </summary>
    public const double PitchOrderChance = 0.7;

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
    ///     How much fuller than the groove a fill is: at a section change much fuller, at a phrase line about as sparse,
    ///     a few hits; the fill's layer spreads it, and the drummer moves it.
    /// </summary>
    public const double SectionFullness = 0.3;

    public const double PhraseFullness = 0.05;

    /// <summary>Whether a run changes speed halfway, and how likely each is; slowing down's weight is multiplied by the chance scale.</summary>
    public static ImmutableArray<Weighted<FillSpeed>> Speeds { get; } =
    [
        new(0.7, FillSpeed.Steady),
        new(0.27, FillSpeed.SpeedsUp),
        new(0.03, FillSpeed.SlowsDown)
    ];

    /// <summary>The chance the drums land on a role's sound after a section change: mostly a kick, often with a crash.</summary>
    public static ImmutableDictionary<DrumRole, double> SectionLandings { get; } = new Dictionary<DrumRole, double>
    {
        [DrumRole.Kick] = 0.85,
        [DrumRole.Cymbal] = 0.65
    }.ToImmutableDictionary();

    /// <summary>The chance the drums land on a role's sound after a phrase line, now and then.</summary>
    public static ImmutableDictionary<DrumRole, double> PhraseLandings { get; } = new Dictionary<DrumRole, double>
    {
        [DrumRole.Kick] = 0.05,
        [DrumRole.Cymbal] = 0.05
    }.ToImmutableDictionary();

    /// <summary>The chance the drums land on each of a line's landing sounds after they stopped, as they come back.</summary>
    public const double StopLandingChance = 0.97;

    /// <summary>The chance, in a section of conventionality in the middle, that a landing is pushed a note near an 8th early, times the chance scale.</summary>
    public const double EarlyLandingChance = 0.01;

    /// <summary>How loud a landing's hit is over the drum's state there, as a note's accent.</summary>
    public const double LandingVelocity = 0.8;

    /// <summary>How loud a run starts and ends, around the groove's loudness.</summary>
    public const double RunStartVelocity = -0.15;

    public const double RunEndVelocity = 0.5;

    /// <summary>How much a run's note is accented by its rank, as a groove's note is, which the swell is added to.</summary>
    public const double AccentWeight = 0.5;
}
