using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How much a layer of the song moves a track's rhythm. Every layer adds a step of -1, 0 or 1 to each rhythm
///     setting, and the settings are the sums. A layer moves a setting with the chance it has for it, so that the
///     setting a drum is given, such as the snare's backbeat, stays the likeliest outcome while every layer still
///     varies it now and then.
/// </summary>
/// <param name="Groove">
///     The chance to move the settings that make the groove: how fast a track plays and where its strong beat falls.
///     They belong to the song and its sections, and change little inside them. A track playing twice as fast or as
///     slow keeps its groove, so its speed changes <see cref="SpeedShare" /> as often, which leaves a drum at the
///     speed it is given in about half of the bars.
/// </param>
/// <param name="Density">
///     The chance to move the settings that make how busy the rhythm is: how many subdivisions it has and which of
///     them play. They change freely, most of all from bar to bar.
/// </param>
/// <param name="Fullness">How far the layer moves a pattern's fullness either way, a value drawn around 0.</param>
/// <param name="Variation">How far the layer moves how often a pattern's cycles are drawn afresh, either way.</param>
/// <remarks>
///     A layer can be tilted (<see cref="Tilted" />), as a section by its energy, so that its fullness, density and speed
///     lean fuller, busier and faster or sparser and slower, how often they move kept.
/// </remarks>
public sealed record RhythmLayer(
    double Groove,
    double Density,
    double Fullness = 0,
    double Variation = 0
)
{
    /// <summary>The chance to move a track's speed, <see cref="SpeedShare" /> times the groove's.</summary>
    public double Speed { get; init; } = Math.Min(1, Groove * SpeedShare);

    /// <summary>
    ///     The layer by the groove facet of an unconventionality: its moves, of the groove, the speed and the density, as
    ///     unconventional things, never at the plain end, whose rhythm keeps what its drums are given, and every time at
    ///     the wild; its spreads times the odds of the tilt given.
    /// </summary>
    public RhythmLayer Lean(double unconventionality, Tilt tilt)
    {
        return this with
        {
            Groove = RhythmicUnconventionality.Ends(Groove, 1).At(unconventionality),
            Speed = RhythmicUnconventionality.Ends(Speed, 1).At(unconventionality),
            Density = RhythmicUnconventionality.Ends(Density, 1).At(unconventionality),
            Fullness = Fullness * tilt.Odds,
            Variation = Variation * tilt.Odds
        };
    }

    /// <summary>The layer with the chances it moves the groove, its speed and its phase, leaned by the tilt.</summary>
    public RhythmLayer LeanGroove(Tilt tilt, double lean)
    {
        return this with { Groove = tilt.Chance(Groove, lean), Speed = tilt.Chance(Speed, lean) };
    }

    /// <summary>How the layer's fullness, density and speed lean: fuller, busier and faster on the high side.</summary>
    public Tilt Tilt { get; init; } = Tilt.None;

    /// <summary>The layer with its fullness and density leaning by the tilt.</summary>
    public RhythmLayer Tilted(Tilt tilt)
    {
        return this with { Tilt = tilt };
    }

    public Func<IGenerationContext, double> CreateFullnessGenerator()
    {
        return Tilt.SplineValue().Then(x => x * Fullness);
    }

    public Func<IGenerationContext, double> CreateVariationGenerator()
    {
        return Generators.SplineValue().Then(x => x * Variation);
    }

    public const double SpeedShare = 3.25;

    public Func<IGenerationContext, int> CreateGrooveGenerator()
    {
        return CreateStepGenerator(Groove);
    }

    /// <summary>A step of the period's power, faster on the tilt's high side, as its period shortens: -1 is twice as fast.</summary>
    public Func<IGenerationContext, int> CreateSpeedGenerator()
    {
        return new Tilt(-Tilt.LogOdds).Step(Speed);
    }

    public Func<IGenerationContext, int> CreateDensityGenerator()
    {
        return Tilt.Step(Density);
    }

    /// <summary>A step of -1 or 1 with the chance given, each as likely, and 0 otherwise.</summary>
    internal static Func<IGenerationContext, int> CreateStepGenerator(double chance)
    {
        return Tilt.None.Step(chance);
    }
}

/// <summary>
///     The layers, from the song down to a bar. Their chances are small, so that together they leave most bars in the
///     groove the tracks are given: the snare keeps its backbeat in most bars, with a busier or shifted bar now and then.
///     The feel, straight or a tuplet, is not theirs to move, but the song's, a section's, a bar's or a fill's
///     (<see cref="Feels" />).
/// </summary>
internal static class RhythmLayers
{
    public static RhythmLayer Song { get; } = new(0.075, 0.1, 0.1, 0.2);

    public static RhythmLayer Section { get; } = new(0.05, 0.1, 0.1, 0.2);

    /// <summary>A track's own rhythm for the whole song.</summary>
    public static RhythmLayer Track { get; } = new(0.025, 0.05, 0.05, 0.1);

    /// <summary>How a track's rhythm differs in a section from the song.</summary>
    public static RhythmLayer SectionTrack { get; } = new(0.025, 0.05, 0.05, 0.1);

    /// <summary>The rhythm the drums share for the whole song.</summary>
    public static RhythmLayer DrumGroup { get; } = new(0.025, 0.05, 0.05, 0.1);

    /// <summary>How the drums' shared rhythm differs in a section.</summary>
    public static RhythmLayer SectionDrumGroup { get; } = new(0.025, 0.05, 0.05, 0.1);

    /// <summary>
    ///     A fill's layer over the groove, over and above the ranks finer it plays: a rank more or less, its weight moved
    ///     to weaker notes, and its fullness spread. Its cycle and phase stay the groove's.
    /// </summary>
    public static RhythmLayer Fill { get; } = new(0, 0.5, 0.2, 0);

    /// <summary>A track's bar pattern, where a busier or sparser bar sounds like a variation, not a new groove.</summary>
    public static RhythmLayer BarPattern { get; } = new(0.025, 0.15, 0.05, 0.1);
}
