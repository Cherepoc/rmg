using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How a song ends after its last section.</summary>
public enum EndingKind
{
    /// <summary>It stops on the last section's cadence, unresolved.</summary>
    Open,

    /// <summary>A bar more: everyone hits the home chord on its downbeat, short, then silence.</summary>
    Button,

    /// <summary>The home chord held for a bar or two, the last bar before it slowing down now and then.</summary>
    RingOut,

    /// <summary>The whole band stops a beat or two before the line, then hits the home chord together.</summary>
    Stop,

    /// <summary>The last section plays once more, as it would again, and the band fades out over it.</summary>
    Fade
}

/// <summary>How a song starts before its first section.</summary>
public enum IntroKind
{
    /// <summary>The whole band starts together.</summary>
    Cold,

    /// <summary>A bar of the pedal hi-hat on the beats before the first section.</summary>
    CountIn,

    /// <summary>
    ///     The band coming in part by part, in an order drawn, over bars of the first section's before it or over its
    ///     first phrase, and the parts left coming in together at its end, with a fill and a landing.
    /// </summary>
    Entries
}

/// <summary>
///     A part of the band that comes in as one in an intro: a pitched track by its role, or the drums of a role in the
///     first section.
/// </summary>
/// <param name="DrumRole">The drums' role, for the drums; none for a pitched track.</param>
public readonly record struct IntroPart(TrackRole Role, DrumRole? DrumRole = null)
{
    public override string ToString() => DrumRole?.ToString() ?? Role.ToString();
}

/// <summary>Where an intro's parts come in: over bars of their own before the first section, or over its first phrase.</summary>
/// <param name="Bars">How many bars the parts come in over.</param>
/// <param name="IsBefore">Whether they are bars of their own before the first section, rather than its first phrase.</param>
public readonly record struct IntroWindow(int Bars, bool IsBefore);

/// <summary>
///     How a song starts and ends around its sections. It starts with the whole band, with a count-in, or with the band
///     coming in part by part. It ends on the home chord of its last section, whose home is the song's tonic, most of
///     the time, or fades out; the more its rhythm strays, the likelier an open or a stopped ending.
/// </summary>
public static class FormLayers
{
    public static ImmutableArray<Weighted<IntroKind>> Intros { get; } =
    [
        new(0.3, IntroKind.Cold),
        new(0.1, IntroKind.CountIn),
        new(0.6, IntroKind.Entries)
    ];

    /// <summary>Where an intro's parts come in: bars of their own before the first section, or its first phrase.</summary>
    public static ImmutableArray<Weighted<IntroWindow>> IntroWindows { get; } =
    [
        new(0.12, new IntroWindow(1, true)),
        new(0.2, new IntroWindow(2, true)),
        new(0.1, new IntroWindow(4, true)),
        new(0.58, new IntroWindow(Progressions.BarCount, false))
    ];

    /// <summary>
    ///     How likely each part is to come in next, of those left: most often one that keeps the time or the chords, and
    ///     the melody hardly ever before the band, as it is sung over it; those with a lean of 1 lean unconventional,
    ///     likelier the further the song's rhythm strays.
    /// </summary>
    public static ImmutableArray<(IntroPart Part, double Weight, double Lean)> IntroParts { get; } =
    [
        (new IntroPart(TrackRole.Drum, DrumRole.Time), 2, 0),
        (new IntroPart(TrackRole.Chords), 2, 0),
        (new IntroPart(TrackRole.Drum, DrumRole.Ground), 1.5, 0),
        (new IntroPart(TrackRole.Bass), 1, 0),
        (new IntroPart(TrackRole.Drum, DrumRole.Backbeat), 0.7, 1),
        (new IntroPart(TrackRole.Drum, DrumRole.Colour), 0.5, 1),
        (new IntroPart(TrackRole.Melody), 0.05, 1)
    ];

    /// <summary>The chance that a count-in clicks only the last two beats, rather than all four.</summary>
    public const double HalfCountInChance = 0.3;

    /// <summary>How loud a count-in's clicks are, over the state of the drum it clicks on.</summary>
    public const double CountInVelocity = -0.3;

    /// <summary>
    ///     The sounds a count-in clicks on, the first of them the song has: the hi-hat's pedal, as a drummer counts in, or
    ///     a dry sound of the percussion, or the snare's, which every song has.
    /// </summary>
    public static ImmutableArray<(PercussionInstrumentDefinition Drum, int Sound)> CountInSounds { get; } =
    [
        (DrumDefinitions.HiHat, DrumSounds.PedalHiHat),
        (DrumDefinitions.Claves, 75),
        (DrumDefinitions.WoodBlock, 76),
        (DrumDefinitions.Cowbell, 56),
        (DrumDefinitions.AcousticSnare, 37),
        (DrumDefinitions.ElectricSnare, 37),
        (DrumDefinitions.Clap, 39)
    ];

    public static ImmutableArray<Weighted<EndingKind>> Endings { get; } =
    [
        new(0.3, EndingKind.Button),
        new(0.34, EndingKind.RingOut),
        new(0.08, EndingKind.Open),
        new(0.13, EndingKind.Stop),
        new(0.15, EndingKind.Fade)
    ];

    /// <summary>Whether an ending lands on a final chord, which it holds, after the last section.</summary>
    public static bool HasFinalChord(EndingKind ending) => ending is EndingKind.Button or EndingKind.RingOut or EndingKind.Stop;

    /// <summary>How often a fade steps down, in beats.</summary>
    public const double FadeStep = 0.25;

    /// <summary>The endings that lean unconventional, likelier the further the song's rhythm strays.</summary>
    public static ImmutableHashSet<EndingKind> AdventurousEndings { get; } = [EndingKind.Open, EndingKind.Stop];

    /// <summary>The endings' weights in a song whose rhythm leans as the tilt given.</summary>
    public static ImmutableArray<Weighted<EndingKind>> WeighEndings(Tilt tilt)
    {
        return tilt.Weigh(Endings, x => AdventurousEndings.Contains(x) ? 1 : 0);
    }

    /// <summary>How long a button's hit is, in beats, in the bar it has.</summary>
    public const double ButtonLength = 1;

    /// <summary>How long a ringing chord is held, in beats: a bar or two.</summary>
    public static ImmutableArray<Weighted<double>> RingOutLengths { get; } = [new(0.5, 4), new(0.5, 8)];

    /// <summary>How long the band is silent before a stopped ending's hit, in beats.</summary>
    public static ImmutableArray<Weighted<double>> StopLengths { get; } = [new(0.6, 1), new(0.4, 2)];

    /// <summary>The chance that the bar before a ringing ending slows down.</summary>
    public const double RitardandoChance = 0.5;

    /// <summary>How the tempo slows over the bar before the ending, beat by beat, and stays for the ending.</summary>
    public static ImmutableArray<double> Ritardando { get; } = [0.96, 0.92, 0.88, 0.85];
}
