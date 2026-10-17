using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

public static class WeightUtil
{
    /// <summary>A rank's weight, 1 at the offset and the multiplier's power of how far another is from it, weights being relative.</summary>
    public static Func<int, double> CreateGeometricRankWeightFunc(int rankOffset, double rankMultiplier)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rankMultiplier);

        return rank => Math.Pow(rankMultiplier, Math.Abs(rank - rankOffset));
    }

    public static ImmutableArray<Weighted<int>> WeightUsing(this IEnumerable<int> items, Func<int, double> weightFunc)
    {
        return items
            .Select(x => new Weighted<int>(weightFunc(x), x))
            .ToImmutableArray();
    }
}
