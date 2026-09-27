using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>What the drums land on at the downbeat after a line.</summary>
public enum FillLanding
{
    None,
    Kick,
    CrashAndKick
}

/// <summary>The kinds of fill a drummer plays before a line.</summary>
public enum FillKind
{
    /// <summary>The groove runs straight on.</summary>
    None,

    /// <summary>
    ///     A run in the groove's rhythm over a few of the song's drums, mostly the snare and the toms, walking from one
    ///     to the next: a roll where it plays the snare alone, a few hits where it is sparse.
    /// </summary>
    Run,

    /// <summary>The drums stop, and come back at the line.</summary>
    Break,

    /// <summary>The drums hit together once, then stop until the line.</summary>
    StopTime,

    /// <summary>An open hi-hat or a crash on the last off-beat, lifting into the line.</summary>
    Lift
}

/// <summary>The drums a fill plays, by what they do in the kit.</summary>
public enum DrumRole
{
    Kick,

    /// <summary>The song's snare: the snare itself if it has one, else its clap or sidestick.</summary>
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

/// <summary>
///     A change a fill may take from convention, any fill and several at once; the stranger a section's rhythm, the
///     likelier each is. One that does not concern a fill, such as a slow-down for a break, leaves it as it is.
/// </summary>
[Flags]
public enum FillTwist
{
    None = 0,

    /// <summary>The run plays a tuplet, such as triplets or quintuplets, where the groove plays none.</summary>
    Tuplet = 1,

    /// <summary>The fill starts off the beat, a note near an 8th earlier or later.</summary>
    OddSpan = 2,

    /// <summary>The run slows down, the finest notes then a rank coarser.</summary>
    SlowDown = 4,

    /// <summary>The run fades out rather than swells.</summary>
    Fading = 8,

    /// <summary>The next section lands early, pushed a note near an 8th ahead of the line.</summary>
    EarlyLanding = 16,

    /// <summary>The next section lands on nothing, even after a break.</summary>
    NoLanding = 32
}

/// <summary>What a fill does with the groove in its span.</summary>
public enum GrooveTreatment
{
    /// <summary>The groove plays on under the fill.</summary>
    Keep,

    /// <summary>The drums the fill plays leave the groove for it, and the others play on.</summary>
    Played,

    /// <summary>The drums stop.</summary>
    Stop
}

/// <summary>A drum's sound: the groove's, when it names none, one sound, or a choice among several.</summary>
public sealed record FillSound(DrumRole Role, ImmutableArray<Weighted<int>> Codes)
{
    public static FillSound Groove(DrumRole role) => new(role, []);

    public static FillSound Of(DrumRole role, int code) => new(role, [new Weighted<int>(1, code)]);
}

/// <summary>A hit at the start of a fill: the first of its sounds whose drum the song has.</summary>
public sealed record FillHit(ImmutableArray<FillSound> Choices, double Velocity);

/// <summary>A fill: how long it may be, what it does with the groove, the hits it starts with, and whether it runs.</summary>
/// <param name="Runs">Whether it plays a run over its span.</param>
/// <param name="ForcesLanding">Whether the next section always lands on a crash and a kick after it.</param>
public sealed record FillSpec(
    ImmutableArray<Weighted<double>> Spans,
    GrooveTreatment Groove,
    ImmutableArray<FillHit> Hits,
    bool Runs = false,
    bool ForcesLanding = false
);

/// <summary>
///     How the drums mark the lines between sections and phrases: the fill before a line, and what they land on after
///     it. A section change is marked most, a phrase line inside a section now and then. A run's rhythm is the
///     groove's (see <see cref="FillRhythm" />), so chance leaves notes out of it, as a drummer would.
/// </summary>
public static class FillLayers
{
    /// <summary>The fills before a section change, and how likely each is.</summary>
    public static ImmutableArray<Weighted<FillKind>> SectionFills { get; } =
    [
        new(0.2, FillKind.None),
        new(0.65, FillKind.Run),
        new(0.08, FillKind.Break),
        new(0.05, FillKind.StopTime),
        new(0.07, FillKind.Lift)
    ];

    /// <summary>The fills before the line in the middle of a section, where the groove mostly runs on.</summary>
    public static ImmutableArray<Weighted<FillKind>> PhraseFills { get; } =
    [
        new(0.7, FillKind.None),
        new(0.28, FillKind.Run),
        new(0.02, FillKind.Break),
        new(0.1, FillKind.Lift)
    ];

    /// <summary>
    ///     The fills that stray from convention, whose weights are multiplied by how far a section's rhythm strays (its
    ///     chance scale), as the adventurous phrase schemes are.
    /// </summary>
    public static ImmutableHashSet<FillKind> AdventurousFills { get; } = [FillKind.Break, FillKind.StopTime];

    /// <summary>
    ///     How likely each twist is, in a section of conventionality in the middle; its chance is multiplied by the
    ///     section's chance scale, from a quarter in a plain one to four times in a wild one.
    /// </summary>
    public static ImmutableArray<Weighted<FillTwist>> Twists { get; } =
    [
        new(0.02, FillTwist.Tuplet),
        new(0.015, FillTwist.OddSpan),
        new(0.01, FillTwist.SlowDown),
        new(0.01, FillTwist.Fading),
        new(0.01, FillTwist.EarlyLanding),
        new(0.005, FillTwist.NoLanding)
    ];

    /// <summary>The tuplets a fill's twist may play, where the groove plays none of its own.</summary>
    public static ImmutableArray<Weighted<int>> TwistTuplets { get; } = [new(0.75, 3), new(0.25, 5)];

    /// <summary>The shortest note a fill plays, in seconds: a sextuplet at 125 beats a minute, a 32nd at 94.</summary>
    public const double MinNoteSeconds = 0.08;

    /// <summary>How many ranks finer than the groove a run plays, and how likely each is.</summary>
    public static ImmutableArray<Weighted<int>> ExtraRanks { get; } = [new(0.3, 0), new(0.5, 1), new(0.2, 2)];

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
    ///     The finest rank a role's sounds play in a run, such as a crash on the strongest notes only; a role not named
    ///     plays any. A wild section lifts the limit by a rank now and then.
    /// </summary>
    public static ImmutableDictionary<DrumRole, int> RankLimits { get; } = new Dictionary<DrumRole, int>
    {
        [DrumRole.Cymbal] = 1
    }.ToImmutableDictionary();

    /// <summary>The chance a rank limit is a rank finer, in a section of conventionality in the middle, times its chance scale.</summary>
    public const double RankLimitLiftChance = 0.1;

    /// <summary>
    ///     How likely a run keeps a note, less for every rank it is weaker: at a section change mostly full, at a
    ///     phrase line mostly sparse, a few hits; each is spread around, and the drummer moves it.
    /// </summary>
    public const double SectionRunFullness = 0.8;

    public const double PhraseRunFullness = 0.55;
    public const double RunFullnessSpread = 0.2;

    /// <summary>The fewest and the most a run keeps of a note a rank weaker.</summary>
    public const double MinRunFullness = 0.3;

    public const double MaxRunFullness = 0.95;

    /// <summary>The chance a run speeds up, a rank coarser for its first half.</summary>
    public const double SpeedUpChance = 0.3;

    /// <summary>What the drums land on at a section change, and how likely each is.</summary>
    public static ImmutableArray<Weighted<FillLanding>> SectionLandings { get; } =
    [
        new(0.65, FillLanding.CrashAndKick),
        new(0.2, FillLanding.Kick),
        new(0.15, FillLanding.None)
    ];

    /// <summary>The chance that a phrase line with a fill before it lands on a crash and a kick.</summary>
    public const double PhraseLandingChance = 0.15;

    /// <summary>The crashes, the first more often.</summary>
    public static ImmutableArray<Weighted<int>> Crashes { get; } =
    [
        new(0.7, DrumSounds.CrashCymbal1),
        new(0.3, DrumSounds.CrashCymbal2)
    ];

    public static FillSound Crash { get; } = new(DrumRole.Cymbal, Crashes);

    /// <summary>How loud a landing's hit is over the drum's state there, as a note's accent.</summary>
    public const double LandingVelocity = 0.8;

    /// <summary>How loud a run starts and ends, around the groove's loudness.</summary>
    public const double RunStartVelocity = -0.15;

    public const double RunEndVelocity = 0.5;

    /// <summary>How much a run's note is accented by its rank, as a groove's note is, which the swell is added to.</summary>
    public const double AccentWeight = 0.5;

    /// <summary>How loud a lift's hit is.</summary>
    public const double LiftVelocity = 0.6;

    /// <summary>Every fill but none: how long it may be, in beats before the line, and what it plays.</summary>
    public static ImmutableDictionary<FillKind, FillSpec> Specs { get; } = new Dictionary<FillKind, FillSpec>
    {
        [FillKind.Run] = new([new(0.35, 1), new(0.4, 2), new(0.25, 4)], GrooveTreatment.Played, [], Runs: true),
        [FillKind.Break] = new([new(0.3, 1), new(0.4, 2), new(0.3, 4)], GrooveTreatment.Stop, [], ForcesLanding: true),
        [FillKind.StopTime] = new(
            [new(0.5, 2), new(0.5, 4)],
            GrooveTreatment.Stop,
            [
                new FillHit([FillSound.Groove(DrumRole.Kick)], LandingVelocity),
                new FillHit([FillSound.Groove(DrumRole.Snare)], LandingVelocity),
                new FillHit([Crash], LandingVelocity)
            ],
            ForcesLanding: true
        ),
        [FillKind.Lift] = new(
            [new(1, 0.5)],
            GrooveTreatment.Keep,
            [new FillHit([FillSound.Of(DrumRole.HiHat, DrumSounds.OpenHiHat), Crash], LiftVelocity)]
        )
    }.ToImmutableDictionary();
}
