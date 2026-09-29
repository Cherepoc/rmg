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
/// <param name="Layer">The drummer's layer over the fills' chances, such as its signature's.</param>
internal sealed record Drummer(double Busyness, FillPath Favourite, StateMap? Layer = null)
{
    /// <summary>How much more likely the favourite walk is than it would be.</summary>
    public const double FavouriteWeight = 3;

    /// <summary>How much fuller or sparser a run is, either way, at 1 and at 0.</summary>
    public const double RunFullnessRange = 0.1;

    // around 0 with a flat peak, from -2 to 2, so that songs spread over the whole range and more of them near the middle
    private static readonly Func<IGenerationContext, double> SpreadGenerator = Generators.SplineValue(0);

    /// <param name="rhythm">How far the song's rhythm strays, which makes a signature likelier.</param>
    public static Drummer Generate(IGenerationContext context, RhythmicUnconventionality rhythm)
    {
        var busyness = Math.Clamp(0.5 + SpreadGenerator(context) / 4, 0, 1);
        // any walk can be the favourite, the likelier ones more often
        var favourite = context.Pick(FillLayers.Paths);
        // and any of the rarer choices the signature, the likelier ones more often
        var builder = new StateMapBuilder("Drummer", perTrack: true);
        if (context.TestProbability(rhythm.Tilt.Chance(FillLayers.SignatureChance, 1)))
        {
            ImmutableArray<Weighted<StateKind<double>>> kinds = [..FillLayers.Chances.Select(x => new Weighted<StateKind<double>>(x.Chance, x.Kind))];
            builder.Add(context.Pick(kinds), FillLayers.SignatureWeight);
        }

        return new Drummer(busyness, favourite, builder.ToStateMap(context));
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

    /// <summary>The walks' weights as this drummer takes them: the favourite more likely, and the random walk leaning by the tilt.</summary>
    public ImmutableArray<Weighted<FillPath>> WeighPaths(ImmutableArray<Weighted<FillPath>> paths, Tilt tilt = default)
    {
        return tilt.Weigh(
            paths.Select(x => x.Value == Favourite ? x with { Weight = x.Weight * FavouriteWeight } : x),
            x => x == FillPath.Random ? 1 : 0
        );
    }

    /// <summary>How much fuller or sparser than the line's the drummer's runs are.</summary>
    public double FullnessOffset => (Busyness - 0.5) * 2 * RunFullnessRange;
}
