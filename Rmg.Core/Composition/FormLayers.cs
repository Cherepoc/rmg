using System.Collections.Immutable;
using Rmg.Core.Probabilities;

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
    Stop
}

/// <summary>How a song starts before its first section.</summary>
public enum IntroKind
{
    /// <summary>The whole band starts together.</summary>
    Cold,

    /// <summary>Bars of the first section's drums alone before it, the band coming in with a fill and a landing.</summary>
    DrumsFirst,

    /// <summary>A bar of the pedal hi-hat on the beats before the first section.</summary>
    CountIn,

    /// <summary>The first phrase with the chords alone, now and then with the bass, the band coming in after it.</summary>
    ChordsFirst,

    /// <summary>The first phrase building up: the chords, then the bass, then the drums, and the melody after it.</summary>
    Build
}

/// <summary>
///     How a song starts and ends around its sections. It starts with the whole band, with the drums or the chords
///     alone, with a count-in, or building up. It ends on the home chord of its last section, whose home is the
///     song's tonic, most of the time; the more its rhythm strays, the likelier an open or a stopped ending.
/// </summary>
public static class FormLayers
{
    public static ImmutableArray<Weighted<IntroKind>> Intros { get; } =
    [
        new(0.3, IntroKind.Cold),
        new(0.25, IntroKind.DrumsFirst),
        new(0.1, IntroKind.CountIn),
        new(0.2, IntroKind.ChordsFirst),
        new(0.15, IntroKind.Build)
    ];

    /// <summary>How many bars the drums play alone before the first section.</summary>
    public static ImmutableArray<Weighted<int>> DrumsFirstBars { get; } = [new(0.3, 1), new(0.5, 2), new(0.2, 4)];

    /// <summary>The chance that a count-in clicks only the last two beats, rather than all four.</summary>
    public const double HalfCountInChance = 0.3;

    /// <summary>How loud a count-in's clicks are, over the hi-hat's state.</summary>
    public const double CountInVelocity = -0.3;

    /// <summary>The chance that the bass joins the chords in an intro of the chords first.</summary>
    public const double ChordsFirstBassChance = 0.5;

    /// <summary>When each track comes in, in bars into the first phrase, as an intro builds up; the melody after the phrase.</summary>
    public const int BuildBassBar = 1;

    public const int BuildDrumsBar = 2;

    public static ImmutableArray<Weighted<EndingKind>> Endings { get; } =
    [
        new(0.35, EndingKind.Button),
        new(0.4, EndingKind.RingOut),
        new(0.1, EndingKind.Open),
        new(0.15, EndingKind.Stop)
    ];

    /// <summary>The endings whose weights are multiplied by how far the song's rhythm strays (its chance scale).</summary>
    public static ImmutableHashSet<EndingKind> AdventurousEndings { get; } = [EndingKind.Open, EndingKind.Stop];

    /// <summary>The endings' weights in a song whose rhythm strays as far as the given chance scale.</summary>
    public static ImmutableArray<Weighted<EndingKind>> WeighEndings(double chanceScale)
    {
        return [..Endings.Select(x => AdventurousEndings.Contains(x.Value) ? x with { Weight = x.Weight * chanceScale } : x)];
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
