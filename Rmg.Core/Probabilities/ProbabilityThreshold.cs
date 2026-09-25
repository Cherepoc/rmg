using System.Collections.Immutable;

namespace Rmg.Core.Probabilities;

public readonly struct ProbabilityThreshold<T>
{
    public T Value { get; }
    public double Threshold { get; }

    public ProbabilityThreshold(double threshold, T value)
    {
        ProbabilityThreshold.ValidateProbabilityThreshold(threshold);

        Threshold = threshold;
        Value = value;
    }
}

public static class ProbabilityThreshold
{
    public static void ValidateProbabilityThreshold(double value)
    {
        if (value is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Value must be greater than 0 and lesser or equal to 1."
            );
    }

    /// <summary>
    ///     A probability to look up, as a random double gives it: from 0, which it can give, up to 1. Unlike a
    ///     threshold it can be 0, which falls to the first threshold.
    /// </summary>
    private static void ValidateProbability(double probability)
    {
        if (probability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(
                nameof(probability),
                probability,
                "Probability must be greater than or equal to 0 and lesser or equal to 1."
            );
    }

    /// <returns>The index of the first threshold not below <paramref name="probability" />, or -1 if there is none.</returns>
    private static int FindFirstThresholdNotBelow<T>(
        this ImmutableArray<ProbabilityThreshold<T>> probabilityThresholds,
        double probability
    )
    {
        // the lowest such index, so a threshold repeated by a zero weight is never picked
        var low = 0;
        var high = probabilityThresholds.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (probabilityThresholds[middle].Threshold < probability)
                low = middle + 1;
            else
                high = middle;
        }

        return low < probabilityThresholds.Length ? low : -1;
    }

    public static int FindIndexByProbability<T>(
        this ImmutableArray<ProbabilityThreshold<T>> probabilityThresholds,
        double probability
    )
    {
        ValidateProbability(probability);

        if (probabilityThresholds.Length == 0)
            throw new ArgumentException("Probability thresholds must not be empty", nameof(probabilityThresholds));

        return probabilityThresholds.FindFirstThresholdNotBelow(probability);
    }

    public static ImmutableArray<ProbabilityThreshold<T>> ToProbabilityThresholds<T>(this ImmutableArray<Weighted<T>> weights)
    {
        if (weights.Length == 0)
            return [];

        var result = new ProbabilityThreshold<T>[weights.Length];
        var weightSum = weights.Sum(x => x.Weight);

        double lastThreshold = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            var weightItem = weights[i];
            lastThreshold = (lastThreshold + weightItem.Weight / weightSum).RoundByEpsilon(1);
            result[i] = new ProbabilityThreshold<T>(lastThreshold, weightItem.Value);
        }

        return [..result];
    }
}
