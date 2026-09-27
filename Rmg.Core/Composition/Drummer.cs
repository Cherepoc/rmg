using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Composition;

/// <summary>
///     The drummer of a song, whose fills sound like one player's. How busy the drummer is, from 0 to 1, sets how often
///     a line gets a fill, how long the fills are and how full their runs: at 0 a drummer who mostly lets the groove run
///     on and plays short, sparse runs, at 1 one who fills most lines with long, full runs. A song's is spread widely
///     around the middle. The drummer also has a favourite way of walking a run's drums, which it takes more often
///     than the others, and the more a song's rhythm strays, the likelier it has a signature twist, which it gives its
///     fills more often.
/// </summary>
public sealed record Drummer(double Busyness, FillPath Favourite, FillTwist Signature = FillTwist.None)
{
    /// <summary>The chance of a signature twist, in a song of conventionality in the middle, times its chance scale.</summary>
    public const double SignatureChance = 0.1;

    /// <summary>How much more likely the signature twist is than it would be.</summary>
    public const double SignatureWeight = 5;

    /// <summary>How much more likely the favourite walk is than it would be.</summary>
    public const double FavouriteWeight = 3;

    /// <summary>How much fuller or gappier a run is, either way, at 1 and at 0.</summary>
    public const double RunFullnessRange = 0.1;

    // around 0 with a flat peak, from -2 to 2, so that songs spread over the whole range and more of them near the middle
    private static readonly Func<IGenerationContext, double> SpreadGenerator = Generators.SplineValue(0);

    /// <param name="rhythm">How far the song's rhythm strays, which makes a signature twist likelier.</param>
    public static Drummer Generate(IGenerationContext context, RhythmicUnconventionality rhythm)
    {
        var busyness = Math.Clamp(0.5 + SpreadGenerator(context) / 4, 0, 1);
        // any walk can be the favourite, the likelier ones more often
        var favourite = FillLayers.Paths[Generators.WeightedIndex(FillLayers.Paths)(context)].Value;
        // and any twist the signature, the likelier ones more often
        var signature = context.TestProbability(Math.Min(1, SignatureChance * rhythm.ChanceScale))
            ? FillLayers.Twists[Generators.WeightedIndex(FillLayers.Twists)(context)].Value
            : FillTwist.None;
        return new Drummer(busyness, favourite, signature);
    }

    /// <summary>The chance of a twist in a fill of a section with the given chance scale, as this drummer plays it.</summary>
    public double GetTwistChance(Weighted<FillTwist> twist, double chanceScale)
    {
        return Math.Min(1, twist.Weight * chanceScale * (twist.Value == Signature ? SignatureWeight : 1));
    }

    /// <summary>
    ///     The fills' weights as this drummer plays them: none less likely the busier the drummer, from half again as
    ///     likely at 0 to half as likely at 1, and the adventurous ones by the chance scale.
    /// </summary>
    /// <param name="chanceScale">The section's chance scale, which the adventurous fills' weights are multiplied by.</param>
    public ImmutableArray<Weighted<FillKind>> Weigh(ImmutableArray<Weighted<FillKind>> fills, double chanceScale = 1)
    {
        return
        [
            ..fills.Select(x => x with
                {
                    Weight = x.Value switch
                    {
                        FillKind.None => x.Weight * (1.5 - Busyness),
                        _ => x.Weight * (FillLayers.AdventurousFills.Contains(x.Value) ? chanceScale : 1)
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

    /// <summary>The walks' weights as this drummer takes them: the favourite more likely, and the random walk by the chance scale.</summary>
    public ImmutableArray<Weighted<FillPath>> WeighPaths(ImmutableArray<Weighted<FillPath>> paths, double chanceScale = 1)
    {
        return
        [
            ..paths.Select(x => x with
                {
                    Weight = x.Weight * (x.Value == Favourite ? FavouriteWeight : 1) * (x.Value == FillPath.Random ? chanceScale : 1)
                }
            )
        ];
    }

    /// <summary>How much fuller or sparser than the line's the drummer's runs are.</summary>
    public double FullnessOffset => (Busyness - 0.5) * 2 * RunFullnessRange;
}
