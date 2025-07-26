using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

public static class WeightUtil
{
    public static Func<int, double> CreateGeometricRankWeightFunc(
        int rankOffset,
        double minWeight,
        double maxWeight,
        double rankMultiplier
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minWeight);
        ArgumentOutOfRangeException.ThrowIfNegative(maxWeight);
        ArgumentOutOfRangeException.ThrowIfNegative(rankMultiplier);

        var weightRange = maxWeight - minWeight;
        if (weightRange < 0)
            throw new ArgumentOutOfRangeException(nameof(maxWeight), "Max weight must be greater than min weight");

        return rank => Math.Pow(rankMultiplier, Math.Abs(rank - rankOffset)) * weightRange + minWeight;
    }

    public static ImmutableArray<Weighted<int>> WeightUsing(this IEnumerable<int> items, Func<int, double> weightFunc)
    {
        return items
            .Select(x => new Weighted<int>(weightFunc(x), x))
            .ToImmutableArray();
    }
}
