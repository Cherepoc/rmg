using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How far the rhythm of a song or a section strays from convention, from 0 to 1, apart from how far its harmony
///     does. It leans the chances with which the rhythm layers move their settings, by their odds: at 0.5 they are as
///     tuned, at 0 their odds are a quarter of that, or less as the layers lean (<see cref="RhythmLayers.ChanceLean" />),
///     for plain grooves that keep their accents and rarely leave the straight grid, and at 1 four times or more, for
///     grooves full of tuplets, displaced accents and syncopation. A song's is spread widely around the middle, and a
///     section moves it a little.
/// </summary>
public sealed record RhythmicUnconventionality(double Value)
{
    /// <summary>How far a section moves the song's value, either way.</summary>
    public const double SectionShift = 0.15;

    /// <summary>How little a section of the wildest rhythm follows its energy.</summary>
    public const double MaxDecoupling = 0.8;

    // around 0 with a flat peak, from -2 to 2, so that songs spread over the whole range and more of them near the middle
    private static readonly Func<IGenerationContext, double> SpreadGenerator = Generators.SplineValue(0);

    /// <summary>What the odds of an unconventional choice are multiplied by: from 1/4 at 0 through 1 at 0.5 to 4 at 1.</summary>
    public double ChanceScale => Tilt.Odds;

    /// <summary>
    ///     How the unconventionality leans a choice: an option's weight, or a chance's odds, times the chance scale to
    ///     the power of how unconventional the option is, such as 1 for stopping the groove and -1 for toms in order.
    /// </summary>
    public Tilt Tilt => Tilt.Of(16, Value - 0.5);

    /// <summary>How much of what a section's energy leans it to the rhythm follows: all at 0, a fifth at 1.</summary>
    public double Coupling => 1 - MaxDecoupling * Value;

    public static RhythmicUnconventionality Generate(IGenerationContext context)
    {
        return new RhythmicUnconventionality(Math.Clamp(0.5 + SpreadGenerator(context) / 4, 0, 1));
    }

    public RhythmicUnconventionality GenerateSection(IGenerationContext context)
    {
        return new RhythmicUnconventionality(Math.Clamp(Value + Generators.SplineValue()(context) * SectionShift, 0, 1));
    }

    /// <summary>The layer with its chances leaned by the unconventionality; how often its speed changes stays.</summary>
    public RhythmLayer Lean(RhythmLayer layer)
    {
        return layer.Lean(Tilt, RhythmLayers.ChanceLean);
    }
}
