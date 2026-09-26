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

    /// <summary>A few sparse hits of the snare or the toms over the groove, into the line.</summary>
    Pickup,

    /// <summary>A fast, full run down the toms, from the high tom to the floor tom, over the kick.</summary>
    TomRun,

    /// <summary>A snare roll that speeds up and swells, 8ths then 16ths, over the kick.</summary>
    SnareRoll,

    /// <summary>A fast run from the snare down the toms, over the kick.</summary>
    AroundTheKit,

    /// <summary>The drums stop, and come back at the line.</summary>
    Break,

    /// <summary>The drums hit together once, then stop until the line.</summary>
    StopTime,

    /// <summary>An open hi-hat or a crash on the last off-beat, lifting into the line.</summary>
    Lift
}

/// <summary>The drums a fill plays, by what they do in the kit; a song has one of each, or none.</summary>
public enum DrumRole
{
    Kick,

    /// <summary>The song's snare: the snare itself if it has one, else its clap or sidestick.</summary>
    Snare,

    Toms,
    HiHat,
    Cymbal
}

/// <summary>What a fill does with the groove in its span.</summary>
public enum GrooveTreatment
{
    /// <summary>The groove plays on under the fill.</summary>
    Keep,

    /// <summary>The hands leave the groove for the fill, and the kick plays on.</summary>
    KeepKick,

    /// <summary>The drums stop.</summary>
    Stop
}

/// <summary>The sounds a voice's notes play, one after another.</summary>
public enum FillWalk
{
    Snare,

    /// <summary>From the high tom down to the floor tom.</summary>
    TomsDown,

    /// <summary>The snare for the first notes, then the toms down.</summary>
    SnareThenTomsDown
}

/// <summary>How likely a voice keeps each of its notes.</summary>
public enum FillKeep
{
    /// <summary>Its first note, and nearly all the others, as many as the drummer does.</summary>
    Run,

    /// <summary>Less for every rank a note is weaker, so a few hits, mostly on the beats.</summary>
    Sparse
}

/// <summary>A drum's sound: the groove's, when it names none, one sound, or a choice among several.</summary>
public sealed record FillSound(DrumRole Role, ImmutableArray<Weighted<int>> Codes)
{
    public static FillSound Groove(DrumRole role) => new(role, []);

    public static FillSound Of(DrumRole role, int code) => new(role, [new Weighted<int>(1, code)]);
}

/// <summary>A hit at the start of a fill: the first of its sounds whose drum the song has.</summary>
public sealed record FillHit(ImmutableArray<FillSound> Choices, double Velocity);

/// <summary>
///     A voice of a fill: a dyadic pattern over the fill's span, whose notes play the sounds of its walk, louder or
///     quieter along it. One that speeds up plays 8ths for the first half and the song's finest grid for the second.
///     Another walk may take its place by a chance, if its drum is in the song.
/// </summary>
public sealed record FillVoice(
    FillWalk Walk,
    FillKeep Keep,
    double StartVelocity,
    double EndVelocity,
    bool SpeedsUp = false,
    FillWalk? Alternative = null,
    double AlternativeChance = 0
);

/// <summary>A fill: how long it may be, what it does with the groove, the hits it starts with and its voices.</summary>
/// <param name="ForcesLanding">Whether the next section always lands on a crash and a kick after it.</param>
public sealed record FillSpec(
    ImmutableArray<Weighted<double>> Spans,
    GrooveTreatment Groove,
    ImmutableArray<FillHit> Hits,
    ImmutableArray<FillVoice> Voices,
    bool ForcesLanding = false
);

/// <summary>
///     How the drums mark the lines between sections and phrases: the fill before a line, and what they land on after
///     it. A section change is marked most, a phrase line inside a section now and then. A fill's voices are dyadic
///     patterns like any other, so chance leaves notes out of them, as a drummer would.
/// </summary>
public static class FillLayers
{
    /// <summary>The fills before a section change, and how likely each is.</summary>
    public static ImmutableArray<Weighted<FillKind>> SectionFills { get; } =
    [
        new(0.2, FillKind.None),
        new(0.15, FillKind.Pickup),
        new(0.25, FillKind.TomRun),
        new(0.15, FillKind.SnareRoll),
        new(0.1, FillKind.AroundTheKit),
        new(0.08, FillKind.Break),
        new(0.05, FillKind.StopTime),
        new(0.07, FillKind.Lift)
    ];

    /// <summary>The fills before the line in the middle of a section, where the groove mostly runs on.</summary>
    public static ImmutableArray<Weighted<FillKind>> PhraseFills { get; } =
    [
        new(0.7, FillKind.None),
        new(0.2, FillKind.Pickup),
        new(0.05, FillKind.TomRun),
        new(0.03, FillKind.SnareRoll),
        new(0.02, FillKind.Break),
        new(0.1, FillKind.Lift)
    ];

    /// <summary>
    ///     The fills a section in a tuplet feel may play, since a run in straight 16ths would fight it: none, a break,
    ///     stop-time or a lift, each as likely relative to the others as it is otherwise.
    /// </summary>
    public static ImmutableHashSet<FillKind> TupletFills { get; } = [FillKind.None, FillKind.Break, FillKind.StopTime, FillKind.Lift];

    /// <summary>The share of the drums' notes in a bar that a tuplet needs to set the bar's feel.</summary>
    public const double TupletFeelShare = 0.25;

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

    /// <summary>How loud a run starts and ends, and a roll, which swells more.</summary>
    public const double RunStartVelocity = 0.2;

    public const double RunEndVelocity = 0.6;
    public const double RollStartVelocity = -0.4;
    public const double RollEndVelocity = 0.8;

    /// <summary>How likely a run keeps each of its notes: nearly all, as chance leaves one out now and then.</summary>
    public const double RunFullness = 0.85;

    /// <summary>How likely a pickup keeps a note, less for every rank it is weaker.</summary>
    public const double PickupFullness = 0.5;

    /// <summary>The chance that a pickup plays the toms rather than the snare.</summary>
    public const double PickupTomChance = 0.3;

    /// <summary>The share of the notes of a run around the kit that the snare plays, before the toms.</summary>
    public const double AroundTheKitSnareShare = 0.25;

    /// <summary>The fastest tempo, in beats a minute, at which a run plays 16ths; a faster one plays 8ths.</summary>
    public const double MaxSixteenthTempo = 150;

    /// <summary>Every fill but none: how long it may be, in beats before the line, and what it plays.</summary>
    public static ImmutableDictionary<FillKind, FillSpec> Specs { get; } = new Dictionary<FillKind, FillSpec>
    {
        [FillKind.Pickup] = new(
            [new(0.7, 1), new(0.3, 2)],
            GrooveTreatment.Keep,
            [],
            [new FillVoice(FillWalk.Snare, FillKeep.Sparse, RunStartVelocity, RunEndVelocity, Alternative: FillWalk.TomsDown, AlternativeChance: PickupTomChance)]
        ),
        [FillKind.TomRun] = new(
            [new(0.4, 1), new(0.45, 2), new(0.15, 4)],
            GrooveTreatment.KeepKick,
            [],
            [new FillVoice(FillWalk.TomsDown, FillKeep.Run, RunStartVelocity, RunEndVelocity)]
        ),
        [FillKind.SnareRoll] = new(
            [new(0.3, 1), new(0.4, 2), new(0.3, 4)],
            GrooveTreatment.KeepKick,
            [],
            [new FillVoice(FillWalk.Snare, FillKeep.Run, RollStartVelocity, RollEndVelocity, SpeedsUp: true)]
        ),
        [FillKind.AroundTheKit] = new(
            [new(0.6, 2), new(0.4, 4)],
            GrooveTreatment.KeepKick,
            [],
            [new FillVoice(FillWalk.SnareThenTomsDown, FillKeep.Run, RunStartVelocity, RunEndVelocity)]
        ),
        [FillKind.Break] = new([new(0.3, 1), new(0.4, 2), new(0.3, 4)], GrooveTreatment.Stop, [], [], ForcesLanding: true),
        [FillKind.StopTime] = new(
            [new(0.5, 2), new(0.5, 4)],
            GrooveTreatment.Stop,
            [
                new FillHit([FillSound.Groove(DrumRole.Kick)], LandingVelocity),
                new FillHit([FillSound.Groove(DrumRole.Snare)], LandingVelocity),
                new FillHit([Crash], LandingVelocity)
            ],
            [],
            ForcesLanding: true
        ),
        [FillKind.Lift] = new(
            [new(1, 0.5)],
            GrooveTreatment.Keep,
            [new FillHit([FillSound.Of(DrumRole.HiHat, DrumSounds.OpenHiHat), Crash], RunEndVelocity)],
            []
        )
    }.ToImmutableDictionary();
}
