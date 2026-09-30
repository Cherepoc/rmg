using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     How far the rhythm of a song or a section strays from convention, from 0 to 1, as a facet of its
///     unconventionality has it, such as the groove's: the rhythm layers' moves never happen at 0, where a groove keeps
///     what its drums are given, happen as tuned at 0.5 and every time at 1 (<see cref="RhythmLayer.Lean" />); a choice
///     leans by its ends (<see cref="Ends" />, <see cref="WeightEnds" />), and a value by <see cref="Tilt" />, whose
///     odds run from a quarter at 0 to four times at 1. A song's is spread widely around the middle, and a section
///     moves it a little.
/// </summary>
public sealed record RhythmicUnconventionality(double Value)
{
    /// <summary>How little a section of the wildest rhythm follows its energy.</summary>
    public const double MaxDecoupling = 0.8;

    /// <summary>What the odds of an unconventional choice are multiplied by: from 1/4 at 0 through 1 at 0.5 to 4 at 1.</summary>
    public double ChanceScale => Tilt.Odds;

    /// <summary>
    ///     How the unconventionality leans a choice: an option's weight, or a chance's odds, times the chance scale to
    ///     the power of how unconventional the option is, such as 1 for stopping the groove and -1 for toms in order.
    /// </summary>
    public Tilt Tilt => Tilt.Of(16, Value - 0.5);

    /// <summary>How much of what a section's energy leans it to the rhythm follows: all at 0, a fifth at 1.</summary>
    public double Coupling => 1 - MaxDecoupling * Value;

    /// <summary>
    ///     A chance's ends, as it leaned by the unconventionality through <see cref="Tilt" />: an unconventional thing
    ///     (a lean above 0) never at the plain end and every time at the wild; a conventional one (below 0) as the plainest
    ///     tilt had it at the plain end and never at the wild; one of no lean as tuned at both.
    /// </summary>
    public static ByConvention Ends(double chance, double lean)
    {
        return lean > 0 ? new ByConvention(0, chance, 1)
            : lean < 0 ? new ByConvention(Plainest.Tilt.Chance(chance, lean), chance, 0)
            : new ByConvention(chance, chance, chance);
    }

    /// <summary>
    ///     An option's weight's ends, as it leaned by the unconventionality through <see cref="Tilt" />: a conventional or
    ///     a neutral option (a lean up to 0) allowed at the plain end, as the plainest tilt weighed it, and an unconventional
    ///     or a neutral one (from 0) at the wild, every such option as likely.
    /// </summary>
    public static ByConvention WeightEnds(double weight, double lean)
    {
        return new ByConvention(lean <= 0 ? Plainest.Tilt.Weigh(weight, lean) : 0, weight, lean >= 0 ? 1 : 0);
    }

    // the plainest, whose tilt the ends of what leaned conventional keep
    private static RhythmicUnconventionality Plainest { get; } = new(0);

    /// <summary>The layer by the unconventionality's ends (<see cref="RhythmLayer.Lean" />).</summary>
    public RhythmLayer Lean(RhythmLayer layer)
    {
        return layer.Lean(Value, Tilt);
    }
}
