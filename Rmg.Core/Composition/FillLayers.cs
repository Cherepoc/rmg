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

    /// <summary>The share of the drums' hits in the bar before a line off the 16th grid from which the bar is in a tuplet feel.</summary>
    public const double TupletFeelShare = 0.25;

    /// <summary>How long each fill is, in beats before the line, and how likely each length is.</summary>
    public static ImmutableDictionary<FillKind, ImmutableArray<Weighted<double>>> Spans { get; } =
        new Dictionary<FillKind, ImmutableArray<Weighted<double>>>
        {
            [FillKind.Pickup] = [new(0.7, 1), new(0.3, 2)],
            [FillKind.TomRun] = [new(0.4, 1), new(0.45, 2), new(0.15, 4)],
            [FillKind.SnareRoll] = [new(0.3, 1), new(0.4, 2), new(0.3, 4)],
            [FillKind.AroundTheKit] = [new(0.6, 2), new(0.4, 4)],
            [FillKind.Break] = [new(0.3, 1), new(0.4, 2), new(0.3, 4)],
            [FillKind.StopTime] = [new(0.5, 2), new(0.5, 4)],
            [FillKind.Lift] = [new(1, 0.5)]
        }.ToImmutableDictionary();

    /// <summary>What the drums land on at a section change, and how likely each is.</summary>
    public static ImmutableArray<Weighted<FillLanding>> SectionLandings { get; } =
    [
        new(0.65, FillLanding.CrashAndKick),
        new(0.2, FillLanding.Kick),
        new(0.15, FillLanding.None)
    ];

    /// <summary>The chance that a phrase line with a fill before it lands on a crash and a kick.</summary>
    public const double PhraseLandingChance = 0.15;

    /// <summary>The crashes of the cymbal, by their sound's number, from 1: the first crash, then the second.</summary>
    public static ImmutableArray<Weighted<int>> Crashes { get; } =
    [
        new(0.7, 1),
        new(0.3, 4)
    ];

    /// <summary>The toms' sounds, by their number, from 1: the low floor tom up to the high tom.</summary>
    public const int TomCount = 6;

    /// <summary>The hi-hat's open sound, by its number, from 1.</summary>
    public const int OpenHiHat = 3;

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
}
