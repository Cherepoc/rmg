using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drummer of a song, whose fills sound like one player's. How busy the drummer is, from 0 to 1, sets how often
///     a line gets a fill, how long the fills are and how full their runs: at 0 a drummer who mostly lets the groove run
///     on and plays short, gappy fills, at 1 one who fills most lines with long, full runs. A song's is spread widely
///     around the middle. The drummer also has a favourite fill, which it plays more often than the others.
/// </summary>
public sealed record Drummer(double Busyness, FillKind Favourite)
{
    /// <summary>How much more likely the favourite fill is than it would be.</summary>
    public const double FavouriteWeight = 3;

    /// <summary>How much fuller or gappier a run is, either way, at 1 and at 0.</summary>
    public const double RunFullnessRange = 0.1;

    // around 0 with a flat peak, from -2 to 2, so that songs spread over the whole range and more of them near the middle
    private static readonly Func<IGenerationContext, double> SpreadGenerator = Generators.SplineValue(0);

    public static Drummer Generate(IGenerationContext context)
    {
        var busyness = Math.Clamp(0.5 + SpreadGenerator(context) / 4, 0, 1);
        // any fill but none can be the favourite, the likelier ones more often
        ImmutableArray<Weighted<FillKind>> fills = [..FillLayers.SectionFills.Where(x => x.Value != FillKind.None)];
        return new Drummer(busyness, fills[Generators.WeightedIndex(fills)(context)].Value);
    }

    /// <summary>
    ///     The fills' weights as this drummer plays them: none less likely the busier the drummer, from half again as
    ///     likely at 0 to half as likely at 1, and the favourite more likely.
    /// </summary>
    public ImmutableArray<Weighted<FillKind>> Weigh(ImmutableArray<Weighted<FillKind>> fills)
    {
        return
        [
            ..fills.Select(x => x with
                {
                    Weight = x.Value switch
                    {
                        FillKind.None => x.Weight * (1.5 - Busyness),
                        _ when x.Value == Favourite => x.Weight * FavouriteWeight,
                        _ => x.Weight
                    }
                }
            )
        ];
    }

    /// <summary>
    ///     A fill's spans as this drummer plays them: the longer ones likelier the busier the drummer, a span twice as
    ///     long twice as likely at 1 and half as likely at 0.
    /// </summary>
    public ImmutableArray<Weighted<double>> WeighSpans(ImmutableArray<Weighted<double>> spans)
    {
        return [..spans.Select(x => x with { Weight = x.Weight * Math.Pow(x.Value, 2 * (Busyness - 0.5)) })];
    }

    /// <summary>How likely a run keeps each of its notes.</summary>
    public double RunFullness => FillLayers.RunFullness + (Busyness - 0.5) * 2 * RunFullnessRange;
}
