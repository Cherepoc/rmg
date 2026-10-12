using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drummer of a song, whose fills sound like one player's. How busy the drummer is, from 0 to 1, sets how often
///     a line gets a fill, how long the fills are and how full their runs: at 0 a drummer who mostly lets the groove run
///     on and plays short, sparse runs, at 1 one who fills most lines with long, full runs. A song's is spread widely
///     around the middle. The drummer also has a favourite way of walking a run's drums, which it takes more often
///     than the others, and the more a song's rhythm strays, the likelier a signature: one of the fills' rarer choices,
///     such as starting off the beat, that it makes more often, as a layer of the song over the fills' chances.
/// </summary>
/// <param name="Signature">The fills' rarer choice the drummer makes more often; none for a drummer without one.</param>
internal sealed record Drummer(double Busyness, FillPath Favourite, FillRarity? Signature = null)
{
    /// <summary>How much more likely the favourite walk is than it would be.</summary>
    public const double FavouriteWeight = 3;

    /// <summary>How much fuller or sparser a run is, either way, at 1 and at 0.</summary>
    public const double RunFullnessRange = 0.1;

    // around 0 with a flat peak, from -2 to 2, so that songs spread over the whole range and more of them near the middle
    private static readonly Func<IGenerationContext, double> SpreadGenerator = Generators.SplineValue(0);

    /// <param name="unconventionality">The fills facet of the song's unconventionality: no signature at the plain end, always one at the wild.</param>
    public static Drummer Generate(IGenerationContext context, double unconventionality)
    {
        var busyness = Math.Clamp(0.5 + SpreadGenerator(context) / 4, 0, 1);
        // any walk can be the favourite, the likelier ones more often
        var favourite = context.Pick([..FillLayers.Paths.Select(x => new Weighted<FillPath>(x.Weight, x.Value))]);
        // and any of the rarer choices the signature, the likelier ones more often, its kind drawn whether it has one, so
        // that the draws after it are as many
        var signs = context.TestProbability(RhythmicUnconventionality.Ends(FillLayers.SignatureChance, 1).At(unconventionality));
        var kind = context.Pick([..FillLayers.Chances.Select(x => new Weighted<FillRarity>(x.Chance, x.Choice))]);
        return new Drummer(busyness, favourite, signs ? kind : (FillRarity?)null);
    }

    /// <summary>
    ///     A fill's spans as this drummer plays them: none less likely the busier the drummer, from half again as likely
    ///     at 0 to half as likely at 1, and the longer ones likelier, a span twice as long twice as likely at 1 and half
    ///     as likely at 0.
    /// </summary>
    public ImmutableArray<Weighted<double>> WeighSpans(ImmutableArray<Weighted<double>> spans)
    {
        return
        [
            ..spans.Select(x => x with
                {
                    Weight = x.Value <= 0 ? x.Weight * (1.5 - Busyness) : x.Weight * Math.Pow(x.Value, 2 * (Busyness - 0.5))
                }
            )
        ];
    }

    /// <summary>
    ///     The walks' weights as this drummer takes them, by the fills facet: the favourite more likely, the random walk
    ///     never at the plain end, every walk as likely at the wild.
    /// </summary>
    public ImmutableArray<Weighted<FillPath>> WeighPaths(ImmutableArray<(FillPath Value, double Weight, double Lean)> paths, double unconventionality)
    {
        return RhythmicUnconventionality.Weigh(paths.Select(x => x.Value == Favourite ? x with { Weight = x.Weight * FavouriteWeight } : x), unconventionality);
    }

    /// <summary>How much fuller or sparser than the line's the drummer's runs are.</summary>
    public double FullnessOffset => (Busyness - 0.5) * 2 * RunFullnessRange;
}
